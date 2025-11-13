using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class LocalAndroidTTSStreamer : MonoBehaviour
{
#if UNITY_ANDROID && !UNITY_EDITOR
    AndroidJavaObject activity, tts;
#endif

    [Header("Output")]
    public StreamingAudioPlayer player;

    [Header("Engine (leave blank; pick engine in device settings)")]
    public string enginePackage = "";

    Queue<string> pending = new Queue<string>();
    bool running;

    void Awake()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

        var onInit = new OnInitListener(() => Debug.Log("[TTS] init (fallback file mode)"));

        // Use safest 2-arg ctor; engine selection via device settings avoids OEM quirks
        tts = new AndroidJavaObject("android.speech.tts.TextToSpeech", activity, onInit);
#endif
    }

    public bool IsIdle() => !running;

    public void ResetStream()
    {
        pending.Clear();
        running = false;
        if (player) player.ResetStream();
    }

    /// <summary>Queue one text chunk; processed strictly in-order.</summary>
    public void SpeakChunk(string text)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (string.IsNullOrWhiteSpace(text))
            return;

        pending.Enqueue(text);
        if (!running)
        {
            running = true;
            StartCoroutine(RunQueue());
        }
#else
        Debug.Log("[TTS] (Editor) would speak: " + text);
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    IEnumerator RunQueue()
    {
        while (pending.Count > 0)
        {
            var text = pending.Dequeue();
            yield return SynthesizeAndEnqueue(text);
        }
        running = false;
    }

    IEnumerator SynthesizeAndEnqueue(string text)
    {
        if (player == null)
        {
            Debug.LogError("[TTS] No StreamingAudioPlayer assigned");
            yield break;
        }

        string cacheDir = activity.Call<AndroidJavaObject>("getCacheDir").Call<string>("getAbsolutePath");
        string id = Guid.NewGuid().ToString("N");
        string wavPath = Path.Combine(cacheDir, $"tts_{id}.wav");

        var fileObj = new AndroidJavaObject("java.io.File", wavPath);
        var bundle  = new AndroidJavaObject("android.os.Bundle");

        // synthesizeToFile(CharSequence, Bundle, File, String)  (API 21+)
        int code = -999;
        try
        {
            code = tts.Call<int>("synthesizeToFile", text, bundle, fileObj, id);
        }
        catch (Exception e)
        {
            Debug.LogError("[TTS] synthesizeToFile JNI error: " + e.Message);
            yield break;
        }

        if (code < 0)
        {
            // -2 often means "busy"; we can wait a bit and retry once
            Debug.LogWarning("[TTS] synthesizeToFile returned " + code + " (text=\"" + Trunc(text) + "\")");
            if (code == -2)
            {
                yield return new WaitForSeconds(0.2f);
                try { code = tts.Call<int>("synthesizeToFile", text, bundle, fileObj, id); }
                catch (Exception e) { Debug.LogError("[TTS] retry failed: " + e.Message); yield break; }
                if (code < 0) yield break;
            }
        }

        // Wait for the file to appear
        var fi = new FileInfo(wavPath);
        float timeout = 15f, t = 0f;
        while (!fi.Exists && t < timeout)
        {
            yield return new WaitForSeconds(0.05f);
            fi.Refresh(); t += 0.05f;
        }
        if (!fi.Exists)
        {
            Debug.LogError("[TTS] file not created: " + wavPath);
            yield break;
        }

        // Wait until size stabilizes (writing complete)
        long last = -1; int stable = 0;
        while (t < timeout)
        {
            fi.Refresh();
            if (fi.Length > 44 && fi.Length == last) { stable++; if (stable >= 3) break; }
            else { stable = 0; }
            last = fi.Length;
            yield return new WaitForSeconds(0.05f);
            t += 0.05f;
        }

        byte[] bytes;
        try { bytes = File.ReadAllBytes(wavPath); }
        catch (Exception e) { Debug.LogError("[TTS] read failed: " + e.Message); yield break; }
        finally { try { File.Delete(wavPath); } catch { } }

        if (!TryParseWavPcm16(bytes, out int sr, out int ch, out int dataOffset, out int dataSize))
        {
            Debug.LogError("[TTS] invalid WAV (not PCM16): " + bytes.Length + " bytes");
            yield break;
        }

        // Configure on first chunk / on format change
        player.Configure(sr, ch);

        int len = Mathf.Min(dataSize, bytes.Length - dataOffset);
        var pcm = new byte[len];
        Buffer.BlockCopy(bytes, dataOffset, pcm, 0, len);
        player.EnqueuePcm16(pcm);

        Debug.Log($"[TTS] Enqueued {len} bytes (sr={sr}, ch={ch}) text=\"{Trunc(text)}\"");
    }

    static string Trunc(string s) => s.Length > 48 ? s.Substring(0, 48) + "..." : s;

    // -------- WAV PCM16 parser (complete) --------
    static bool TryParseWavPcm16(byte[] b, out int sampleRate, out int channels, out int dataOffset, out int dataSize)
    {
        sampleRate = channels = dataOffset = dataSize = 0;
        if (b == null || b.Length < 44) return false;

        int p = 0;
        if (ReadTag(b, ref p) != "RIFF") return false;
        p += 4; // file size
        if (ReadTag(b, ref p) != "WAVE") return false;

        int fmtFound = 0;
        while (p + 8 <= b.Length)
        {
            string tag = ReadTag(b, ref p);
            int size = ReadLE32(b, ref p);
            if (tag == "fmt ")
            {
                if (p + size > b.Length) return false;
                int audioFormat = ReadLE16(b, ref p);   // 1 = PCM
                channels = ReadLE16(b, ref p);
                sampleRate = ReadLE32(b, ref p);
                int byteRate = ReadLE32(b, ref p);
                int blockAlign = ReadLE16(b, ref p);
                int bitsPerSample = ReadLE16(b, ref p);

                int extra = size - 16;
                if (extra > 0) p += extra;

                if (audioFormat != 1 || (bitsPerSample != 16)) return false;
                fmtFound = 1;
            }
            else if (tag == "data")
            {
                if (p + size > b.Length) size = b.Length - p;
                dataOffset = p;
                dataSize = size;
                return (fmtFound == 1) && sampleRate > 0 && channels > 0 && dataSize > 0;
            }
            else
            {
                // skip unknown chunk
                p += size;
            }
        }
        return false;
    }

    static string ReadTag(byte[] b, ref int p) { var s = System.Text.Encoding.ASCII.GetString(b, p, 4); p += 4; return s; }
    static int ReadLE32(byte[] b, ref int p) { int v = b[p] | (b[p+1] << 8) | (b[p+2] << 16) | (b[p+3] << 24); p += 4; return v; }
    static int ReadLE16(byte[] b, ref int p) { int v = b[p] | (b[p+1] << 8); p += 2; return v; }

    // ---------- Android proxies ----------
    class OnInitListener : AndroidJavaProxy
    {
        readonly Action cb;
        public OnInitListener(Action cb) : base("android.speech.tts.TextToSpeech$OnInitListener") { this.cb = cb; }
        public void onInit(int status) { cb?.Invoke(); }
    }
#endif
}
