using UnityEngine;
using UnityEngine.Networking;
using TMPro;

public class AssistantStreamController : MonoBehaviour
{
    [Header("Scene refs")]
    public ARReticleAndPlace placer;
    public TMP_Text finalTextUI;

    [Header("Net / TTS / Audio")]
    public ChatOnceClient chatOnce;              // assign a GO with ChatOnceClient
    public LocalAndroidTTSStreamer ttsStreamer;  // assign LocalTTS GO
    public StreamingAudioPlayer player;          // assign AudioStream GO

    [Header("Chunking")]
    [Range(1, 24)] public int minWordsPerChunk = 8;  // for /chat reply re-chunking

    GameObject cachedAvatar;
    bool wired;

    void OnEnable()
    {
        WhisperStreamSTT.OnFinalUtterance += OnUserFinalText;
    }

    void OnDisable()
    {
        WhisperStreamSTT.OnFinalUtterance -= OnUserFinalText;
    }

    public void OnUserFinalText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (finalTextUI) finalTextUI.text = text;

        EnsureAvatarAudioBinding();
        if (!wired) { Debug.LogWarning("[Assistant] Avatar not placed; aborting."); return; }

        StartChatOnce(text);
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

        Debug.Log("Bound StreamingAudioPlayer to avatar Lipsync AudioSource");
    }

    void StartChatOnce(string query)
    {
        if (!chatOnce) { Debug.LogError("[Assistant] ChatOnceClient missing"); return; }
        ttsStreamer.ResetStream();

        StartCoroutine(chatOnce.Ask(query, full =>
        {
            if (string.IsNullOrWhiteSpace(full))
            {
                Debug.LogError("[Assistant] Empty reply from /chat");
                return;
            }

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
