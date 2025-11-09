using UnityEngine;
using UnityEngine.Networking; // REQUIRED for EscapeURL
using TMPro;

public class AssistantStreamController : MonoBehaviour
{
    [Header("Scene refs")]
    public ARReticleAndPlace placer;
    public TMP_Text finalTextUI;              // your FinalText label

    [Header("Net / TTS / Audio")]
    public SseClient sse;
    public LocalAndroidTTSStreamer ttsStreamer;
    public StreamingAudioPlayer player;

    [Header("Backend")]
    public string sseUrlBase = "https://ar-ai-worker.iamadarshgupta8.workers.dev/chat-sse?q=";
    
    [Header("/chat endpoint")]
    public ChatOnceClient chatOnce;     // assign the ChatOnceClient component
    public bool useSse = false;         // turn OFF to use /chat path

    TextToChunks chunker = new TextToChunks();
    GameObject cachedAvatar;
    bool wired;

    void OnEnable()
    {
        // Auto-trigger when Whisper posts a final result
        WhisperStreamSTT.OnFinalUtterance += OnUserFinalText;

        sse.OnDelta += OnDelta;
        sse.OnDone += OnDone;
    }

    void OnDisable()
    {
        WhisperStreamSTT.OnFinalUtterance -= OnUserFinalText;

        sse.OnDelta -= OnDelta;
        sse.OnDone  -= OnDone;
    }

    // Called automatically by WhisperStreamSTT after final transcription
    public void OnUserFinalText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        if (finalTextUI) finalTextUI.text = text; // keep UI in sync

        EnsureAvatarAudioBinding();    // bind to Lipsync/AudioSource of placed avatar (or warn if not placed)
        if (!wired) { Debug.LogWarning("Avatar not placed/bound yet, skipping SSE."); return; }

        // StartSse(text);
        if (useSse) StartSse(text);
        else        StartChatOnce(text);
    }

    void EnsureAvatarAudioBinding()
    {
        var avatar = placer != null ? placer.GetPlacedAvatar() : null;
        if (!avatar) { Debug.LogWarning("Avatar not placed yet"); return; }
        if (wired && avatar == cachedAvatar) return;

        // Your prefab: AvatarFinal has child "Lipsync" with AudioSource + uLipSync
        Transform lip = avatar.transform.Find("Lipsync");
        AudioSource a = lip ? lip.GetComponent<AudioSource>() : avatar.GetComponentInChildren<AudioSource>();
        if (!a) { Debug.LogError("No AudioSource found on avatar Lipsync"); return; }

        player.target = a;                // stream into this AudioSource
        player.ResetStream();             // clear any previous
        cachedAvatar = avatar; wired = true;

        Debug.Log("Bound StreamingAudioPlayer to avatar Lipsync AudioSource");
    }

    void StartSse(string query)
    {
        if (!sse) { Debug.LogError("SseClient ref missing on AssistantStreamController"); return; }
        chunker.Reset();
        ttsStreamer.ResetStream();
        Debug.Log("[Assistant] Starting SSE for query: " + query);
        string url = sseUrlBase + UnityWebRequest.EscapeURL(query);
        Debug.Log("[Assistant] Start SSE: " + url);
        sse.StartStream(url);
    }

    void OnDelta(string delta)
    {
        foreach (var chunk in chunker.PushDelta(delta))
            ttsStreamer.SpeakChunk(chunk);
    }

    void OnDone()
    {
        foreach (var rest in chunker.FlushRemainder())
            ttsStreamer.SpeakChunk(rest);
        Debug.Log("SSE done.");
    }

    void StartChatOnce(string query)
    {
        if (!chatOnce) { Debug.LogError("ChatOnceClient missing"); return; }
        chunker.Reset();
        ttsStreamer.ResetStream();

        StartCoroutine(chatOnce.Ask(query, full =>
        {
            if (string.IsNullOrWhiteSpace(full))
            {
                Debug.LogError("[Assistant] Empty reply from /chat");
                return;
            }

            // Option 1: speak whole reply in one go
            // ttsStreamer.SpeakChunk(full);

            // Option 2 (better): chunk by sentence so it starts speaking faster
            foreach (var part in SplitSentences(full))
                ttsStreamer.SpeakChunk(part);
        }));
    }

    static System.Collections.Generic.IEnumerable<string> SplitSentences(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) yield break;
        int last = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '.' || c == '!' || c == '?' || c == '।')
            {
                var s = text.Substring(last, i - last + 1).Trim();
                if (s.Length > 0) yield return s;
                last = i + 1;
            }
        }
        // tail
        var tail = text.Substring(last).Trim();
        if (tail.Length > 0) yield return tail;
    }

}
