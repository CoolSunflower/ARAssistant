using UnityEngine;
using System;
using System.IO;
using System.Collections;

public class LocalAndroidTTSStreamer : MonoBehaviour
{
#if UNITY_ANDROID && !UNITY_EDITOR
    AndroidJavaObject activity, tts;
#endif

    [Header("Output")]
    public StreamingAudioPlayer player;   // assign your AudioStream GO here

    [Header("Engine (optional)")]
    // You can still pick engine in Android settings. Leave blank here for compatibility.
    public string enginePackage = "";

    // Fallback WAV parsing
    struct WavInfo { public int sampleRate; public int channels; public int dataOffset; public int dataSize; }

    void Awake()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

        var onInit = new OnInitListener(() => Debug.Log("[TTS] init (fallback)"));
        // 2-arg ctor is safest across OEMs
        tts = new AndroidJavaObject("android.speech.tts.TextToSpeech", activity, onInit);
#endif
    }

    public void ResetStream()
    {
        if (player) player.ResetStream();
    }

    // Speak small chunk -> synthesize to WAV file -> enqueue PCM16 to ring buffer
    public void SpeakChunk(string text)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (string.IsNullOrWhiteSpace(text) || player == null) return;

        try
        {
            string cacheDir = activity.Call<AndroidJavaObject>("getCacheDir").Call<string>("getAbsolutePath");
            string id = Guid.NewGuid().ToString("N");
            string path = Path.Combine(cacheDir, $"tts_{id}.wav");

            var fileObj = new AndroidJavaObject("java.io.File", path);
            var bundle  = new AndroidJavaObject("android.os.Bundle");

            // API 21+: synthesizeToFile(CharSequence, Bundle, File, String)
            int code = tts.Call<int>("synthesizeToFile", text, bundle, fileObj, id);
            if (code < 0) { Debug.LogWarning("[TTS] synthesizeToFile returned " + code); return; }

            StartCoroutine(WaitAndPlay(path));
        }
        catch (Exception e)
        {
            Debug.LogError("[TTS] synthesizeToFile error: " + e.Message);
        }
#else
        Debug.Log("Editor SpeakChunk: " + text);
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    IEnumerator WaitAndPlay(string wavPath)
    {
        // Wait for file creation
        float timeout = 10f, t = 0f;
        FileInfo fi = new FileInfo(wavPath);
        while (!fi.Exists && t < timeout)
        {
            yield return new WaitForSeconds(0.05f);
            fi.Refresh(); t += 0.05f;
        }
        if (!fi.Exists) { Debug.LogError("[TTS] file not created: " + wavPath); yield break; }

        // Wait until size stabilizes (finished writing)
        long last = -1;
        int stable = 0;
        while (t < timeout)
        {
            fi.Refresh();
            if (fi.Length > 44 && fi.Length == last) { stable++; if (stable >= 3) break; } // ~150ms stable
            else { stable = 0; }
            last = fi.Length;
            yield return new WaitForSeconds(0.05f);
            t += 0.05f;
        }

        byte[] bytes;
        try { bytes = File.ReadAllBytes(wavPath); }
        catch (Exception e) { Debug.LogError("[TTS] read failed: " + e.Message); yield break; }
        finally { try { File.Delete(wavPath); } catch { } }

        if (bytes.Length < 44) { Debug.LogError("[TTS] bad wav size: " + bytes.Length); yield break; }

        if (!TryParseWavHeader(bytes, out var info))
        {
            Debug.LogError("[TTS] invalid WAV header");
            yield break;
        }

        // Configure player on first chunk
        player.Configure(info.sampleRate, info.channels);

        // Enqueue PCM16 (strip header)
        int dataLen = Mathf.Min(info.dataSize, bytes.Length - info.dataOffset);
        var pcm = new byte[dataLen];
        Buffer.BlockCopy(bytes, info.dataOffset, pcm, 0, dataLen);
        player.EnqueuePcm16(pcm);
    }

    static bool TryParseWavHeader(byte[] b, out WavInfo i)
    {
        i = default;
        // Minimal RIFF/WAVE/fmt/data parser
        if (b.Length < 44) return false;
        int p = 0;
        if (ReadTag(b, ref p) != "RIFF") return false;
        p += 4; // file size
        if (ReadTag(b, ref p) != "WAVE") return false;

        // find "fmt " chunk
        while (p + 8 <= b.Length)
        {
            string tag = ReadTag(b, ref p);
            int size = ReadInt(b, ref p);
            if (tag == "fmt ")
            {
                int audioFormat = ReadShort(b, ref p);
                i.channels      = ReadShort(b, ref p);
                i.sampleRate    = ReadInt(b, ref p);
                p += 6; // byte rate(4) + block align(2)
                int bitsPerSample = ReadShort(b, ref p);
                int extra = size - 16;
                if (extra > 0) p += extra;
                if (audioFormat != 1 || bitsPerSample != 16) return false; // PCM16 only
            }
            else if (tag == "data")
            {
                i.dataSize   = size;
                i.dataOffset = p;
                return i.sampleRate > 0 && i.channels > 0 && i.dataSize > 0;
            }
            else
            {
                p += size;
            }
        }
        return false;
    }

    static string ReadTag(byte[] b, ref int p)
    {
        var s = System.Text.Encoding.ASCII.GetString(b, p, 4); p += 4; return s;
    }
    static int ReadInt(byte[] b, ref int p)
    {
        int v = b[p] | (b[p+1] << 8) | (b[p+2] << 16) | (b[p+3] << 24); p += 4; return v;
    }
    static short ReadShort(byte[] b, ref int p)
    {
        short v = (short)(b[p] | (b[p+1] << 8)); p += 2; return v;
    }

    class OnInitListener : AndroidJavaProxy
    {
        readonly Action cb;
        public OnInitListener(Action cb) : base("android.speech.tts.TextToSpeech$OnInitListener") { this.cb = cb; }
        public void onInit(int status) { cb?.Invoke(); }
    }
#endif
}
