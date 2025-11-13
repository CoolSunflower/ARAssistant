using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using UnityEngine.UI;
using System.Collections;

public class AssistantStreamController : MonoBehaviour
{
    [Header("Scene refs")]
    public UnityEngine.UI.Button speakButton; // assign your mic/speak button in Inspector
    bool sttBusy;                             // updated by Whisper events
    bool llmBusy;  // <— NEW: disable button during /chat network call
    public UnityEngine.UI.Image speakButtonImage;

    public STTBridge whisper;
    public ARReticleAndPlace placer;
    public TMP_Text finalTextUI;

    public string speakingBoolParam = "isSpeaking";    // Animator bool name


    [Header("Net / TTS / Audio")]
    public ChatOnceClient chatOnce;              // assign a GO with ChatOnceClient
    public LocalAndroidTTSStreamer ttsStreamer;  // assign LocalTTS GO
    public StreamingAudioPlayer player;          // assign AudioStream GO
    ClaimingPollingClient poller;

    [Header("Chunking")]
    [Range(1, 24)] public int minWordsPerChunk = 8;  // for /chat reply re-chunking

    GameObject cachedAvatar;
    bool wired;
    ConversationMemory memory = new ConversationMemory(10);
    Animator avatarAnimator;

    void Start()
    {
        poller = FindFirstObjectByType<ClaimingPollingClient>();
        if (poller != null) poller.OnMessageClaimed += OnExternalClaimed;
    }
    void OnDestroy()
    {
        if (poller != null) poller.OnMessageClaimed -= OnExternalClaimed;
    }


    // called by the poller when it has claimed a message
    void OnExternalClaimed(string id, string text)
    {
        Debug.Log("[Assistant] External claimed: " + id + " -> " + text);
        StartCoroutine(HandleExternalDetected(id, text));
    }

    IEnumerator HandleExternalDetected(string id, string text)
    {
        // 1) Immediately treat as a final user utterance (same as OnUserFinalText)
        // Show the user's detected input in the UI
        if (finalTextUI) finalTextUI.text = text;

        // Ensure avatar audio binding (so playback will be bound later)
        EnsureAvatarAudioBinding();
        if (!wired)
        {
            Debug.LogWarning("[Assistant] Avatar not placed; external input aborted.");
            // We still might want to ack so it doesn't reappear — but better to not ack so demo can retry.
            yield break;
        }

        // 2) Start the normal LLM flow (this sets llmBusy inside)
        // Reuse your existing function so memory, etc. are used.
        StartChatOnce(text);

        // 3) Wait for assistant reply to arrive (FinalText will be changed by StartChatOnce)
        // We know finalTextUI was set to the user text; wait until it changes or a timeout occurs.
        float waitForReplyTimeout = 20f; // max wait for LLM reply (adjust as needed)
        float t0 = Time.realtimeSinceStartup;
        bool replyArrived = false;
        while (Time.realtimeSinceStartup - t0 < waitForReplyTimeout)
        {
            // If finalText is not equal to the user text, LLM reply arrived and StartChatOnce already updated the UI.
            if (finalTextUI && finalTextUI.text != text)
            {
                replyArrived = true;
                break;
            }
            // Also if TTS started (player got enqueued), proceed to waiting for playback
            if (ttsStreamer != null && !ttsStreamer.IsIdle()) { replyArrived = true; break; }
            yield return null;
        }

        if (!replyArrived)
        {
            Debug.LogWarning("[Assistant] No assistant reply within timeout; acking to avoid re-delivery.");
            // ack anyway (or you may prefer to not ack so server retries). We'll ack to avoid duplicate stuck messages.
            if (poller != null) yield return StartCoroutine(poller.AckCoroutine(id));
            yield break;
        }

        // 4) Wait for TTS playback to finish:
        // Wait until both the TTS engine is idle AND the audio player has drained its buffer.
        // If no TTS was produced (assistant reply might be empty or no TTS), this loop exits immediately.
        while (true)
        {
            bool ttsBusy = (ttsStreamer != null && !ttsStreamer.IsIdle());
            bool audioBusy = (player != null && player.IsPlayingOrBuffered());
            if (!ttsBusy && !audioBusy) break;
            yield return null;
        }

        // 5) ACK the server so message is consumed
        if (poller != null)
        {
            yield return StartCoroutine(poller.AckCoroutine(id));
        }

        Debug.Log("[Assistant] External message processed and acked: " + id);
    }

    void OnEnable()
    {
        STTBridge.OnFinalUtterance += OnUserFinalText;
        STTBridge.OnSttBusyChanged += OnSttBusyChanged;
    }

    void Update()
    {
        // Button control
        if (speakButton)
        {
            bool recording = whisper && whisper.IsRecording;              // user must be able to stop
            bool ttsBusy = (ttsStreamer != null && !ttsStreamer.IsIdle());
            bool audioBusy = (player != null && player.IsPlayingOrBuffered());
            bool sttLock = sttBusy && !recording;                       // lock during finalizing (not during recording)
            bool overallBusy = sttLock || llmBusy || ttsBusy || audioBusy;
            speakButton.interactable = recording || !overallBusy;

            if (speakButtonImage)
            {
                bool userSpeaking = whisper && whisper.IsRecording;
                speakButtonImage.color = userSpeaking ? Color.red : Color.white;
            }
        }

        // Animator “isSpeaking” toggle (Speaking when TTS working or audio buffered/playing)
        if (avatarAnimator)
        {
            bool ttsBusy   = (ttsStreamer != null && !ttsStreamer.IsIdle());
            bool audioBusy = (player != null && player.IsPlayingOrBuffered());
            bool isSpeaking = ttsBusy || audioBusy;
            avatarAnimator.SetBool(speakingBoolParam, isSpeaking);
        }
    }


    void OnDisable()
    {
        STTBridge.OnFinalUtterance -= OnUserFinalText;
        STTBridge.OnSttBusyChanged -= OnSttBusyChanged;
    }

    void OnSttBusyChanged(bool busy) { sttBusy = busy; }

    public void OnUserFinalText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (finalTextUI) finalTextUI.text = text;

        EnsureAvatarAudioBinding();
        if (!wired) { Debug.LogWarning("[Assistant] Avatar not placed; aborting."); return; }
        if (speakButton) speakButton.interactable = false;   // disable during request+speech
        StartChatOnce(text);
    }

    public void OnTapSpeak()
    {
        if (whisper == null) return;
        whisper.OnToggleRecord();  // start/stop native Android STT
    }


    void EnsureAvatarAudioBinding()
    {
        var avatar = placer != null ? placer.GetPlacedAvatar() : null;
        if (!avatar) { Debug.LogWarning("[Assistant] Avatar not placed yet"); return; }
        if (wired && avatar == cachedAvatar) return;

        // Your prefab has child "Lipsync" with AudioSource
        var lip = avatar.transform.Find("Lipsync");
        var a = lip ? lip.GetComponent<AudioSource>() : avatar.GetComponentInChildren<AudioSource>();
        if (!a) { Debug.LogError("[Assistant] No AudioSource on avatar"); return; }

        player.target = a;     // bind ring buffer to avatar mouth audio
        player.ResetStream();
        cachedAvatar = avatar; wired = true;

        avatarAnimator = avatar.GetComponentInChildren<Animator>();

        Debug.Log("Bound StreamingAudioPlayer to avatar Lipsync AudioSource");
    }

    void StartChatOnce(string query)
    {
        if (!chatOnce) { Debug.LogError("[Assistant] ChatOnceClient missing"); return; }
        ttsStreamer.ResetStream();

        llmBusy = true;  // mark busy during /chat

        StartCoroutine(chatOnce.Ask(query, memory.Snapshot(), full =>
        {
            llmBusy = false;  // re-enable logic will be handled by Update()

            if (string.IsNullOrWhiteSpace(full))
            {
                Debug.LogError("[Assistant] Empty reply from /chat");
                if (finalTextUI) finalTextUI.text = "(no reply)";
                return;
            }

            // 1) Show assistant reply in FinalText
            if (finalTextUI) finalTextUI.text = full;

            // 2) Add to memory (user -> assistant)
            memory.Add(query, full);

            // 3) Speak in chunks
            foreach (var part in SplitIntoChunks(full, minWordsPerChunk))
                ttsStreamer.SpeakChunk(part);
        }));
    }

    static System.Collections.Generic.IEnumerable<string> SplitIntoChunks(string s, int minWords)
    {
        if (string.IsNullOrWhiteSpace(s)) yield break;
        s = s.Replace("\r", "");
        var buf = "";
        int wordCount = 0;

        foreach (var token in s.Split(' '))
        {
            if (buf.Length > 0) buf += " ";
            buf += token;
            wordCount++;

            bool sentenceEnd = buf.EndsWith(".") || buf.EndsWith("!") || buf.EndsWith("?") || buf.EndsWith("।");
            if (sentenceEnd && wordCount >= minWords)
            {
                yield return buf.Trim();
                buf = "";
                wordCount = 0;
            }
        }

        if (buf.Trim().Length > 0) yield return buf.Trim();
    }
}
