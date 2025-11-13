using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using UnityEngine.UI;

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

    [Header("Chunking")]
    [Range(1, 24)] public int minWordsPerChunk = 8;  // for /chat reply re-chunking

    GameObject cachedAvatar;
    bool wired;
    ConversationMemory memory = new ConversationMemory(10);
    Animator avatarAnimator;


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
