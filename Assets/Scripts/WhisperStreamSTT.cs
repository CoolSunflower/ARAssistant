using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using TMPro;
using Whisper; 
using System.Reflection;
using System.Linq;

public class WhisperStreamSTT : MonoBehaviour
{
    [Header("Whisper")]
    public WhisperManager whisper;              // assign your scene's WhisperManager (set model/language in its Inspector)

    [Header("UI")]
    public TMP_Text partialText;                // live partial text while recording
    public TMP_Text finalText;                  // final text after stop

    [Header("Mic")]
    public int sampleRate = 16000;              // whisper expects 16 kHz mono
    public int loopLengthSec = 30;              // ring buffer size (seconds)
    public float chunkSec = 2.0f;               // partial window length
    public float hopSec = 1.0f;                 // stride between partials (overlap = chunkSec - hopSec)

    [Header("Events")]
    public static System.Action<string> OnFinalUtterance; 
    public static System.Action<bool> OnSttBusyChanged; // true = busy, false = idle
    public bool IsRecording => isRecording;

    private string micDevice;
    private AudioClip ringClip;                 // looping microphone capture
    private bool isRecording = false;
    private Coroutine streamCo;

    private int loopSamples;                    // loopLengthSec * sampleRate
    private int startSample;                    // mic position at start
    private int lastProcessedSample;            // last position we chunked
    private const int channels = 1;             // mic mono

    // private readonly List<float> fullCapture = new List<float>(); // aggregate for final pass

    void Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
#endif
        if (Microphone.devices.Length == 0)
            Debug.LogWarning("No microphone devices found.");

        partialText.text = "";
        finalText.text = "";
    }

    // Hook this to your Canvas/Image/Button OnClick()
    public void OnToggleRecord()
    {
        if (!isRecording) StartRecording();
        else StopRecording();
    }

    void StartRecording()
    {
        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("No microphone found");
            return;
        }

        micDevice = Microphone.devices[0];
        loopSamples = Mathf.Max(1, loopLengthSec * sampleRate);

        ringClip = Microphone.Start(micDevice, true, loopLengthSec, sampleRate);
        while (Microphone.GetPosition(micDevice) <= 0) { } // wait for mic to start

        isRecording = true;
        partialText.text = "(listening...)";
        finalText.text = "";
        OnSttBusyChanged?.Invoke(true);

        startSample = Microphone.GetPosition(micDevice);
        lastProcessedSample = startSample;
        // fullCapture.Clear();

        streamCo = StartCoroutine(StreamLoop());
    }

    void StopRecording()
    {
        if (!isRecording) return;
        isRecording = false;
        OnSttBusyChanged?.Invoke(true); // still busy while we build final transcript

        if (streamCo != null) StopCoroutine(streamCo);

        int endPos = Microphone.GetPosition(micDevice);

        // Build the WHOLE utterance once, directly from the ring buffer:
        var all = ExtractSegmentFromRing(ringClip, startSample, endPos, loopSamples);

        Microphone.End(micDevice);

        partialText.text = "(processing final...)";
        _ = FinalTranscribeAsync(all.ToArray());  // pass only this once-assembled buffer
    }


    IEnumerator StreamLoop()
    {
        float elapsed = 0f;

        while (isRecording)
        {
            // Instead of WaitForSeconds, check in smaller increments to exit quickly
            yield return null;
            elapsed += Time.deltaTime;

            if (elapsed < hopSec)
                continue; // wait until hop interval

            elapsed = 0f; // reset for next hop

            // Exit early if recording stopped during the wait
            if (!isRecording)
                break;

            int micPos = Microphone.GetPosition(micDevice);
            int newSamples = DeltaSamples(lastProcessedSample, micPos, loopSamples);
            if (newSamples < Mathf.RoundToInt(sampleRate * hopSec * 0.5f))
                continue; // not enough fresh audio yet

            // last chunkSec seconds ending at micPos
            int chunkSamples = Mathf.Min(loopSamples - 1, Mathf.RoundToInt(chunkSec * sampleRate));
            var chunk = ExtractSegmentFromRing(ringClip, micPos - chunkSamples, micPos, loopSamples);

            // // append to full capture (for final pass) (already being done in StopRecording)
            // fullCapture.AddRange(chunk);

            // PARTIAL pass on this small window (fire-and-forget)
            _ = PartialTranscribeAsync(chunk.ToArray());

            lastProcessedSample = micPos;
        }
    }

    // ---- async Whisper calls (your build uses GetTextAsync(samples, sr, ch)) ----
    async System.Threading.Tasks.Task PartialTranscribeAsync(float[] samples)
    {
        var result = await whisper.GetTextAsync(samples, sampleRate, channels);
        var text = ExtractWhisperText(result);
        if (!string.IsNullOrEmpty(text) && isRecording) partialText.text = text;
    }

    async System.Threading.Tasks.Task FinalTranscribeAsync(float[] samples)
    {
        var result = await whisper.GetTextAsync(samples, sampleRate, channels);
        var text = ExtractWhisperText(result);
        partialText.text = "";
        finalText.text = string.IsNullOrEmpty(text) ? "(no text)" : text;
        OnFinalUtterance?.Invoke(finalText.text);
        OnSttBusyChanged?.Invoke(false); // STT fully done
    }

    // ---- helpers ----

    int DeltaSamples(int a, int b, int mod)
    {
        int d = b - a;
        if (d < 0) d += mod;
        return d;
    }

    // Extract [start, end) from a circular AudioClip (ring length = ringSamples).
    // start/end can be negative or >= ringSamples; we modulo them.
    List<float> ExtractSegmentFromRing(AudioClip clip, int start, int end, int ringSamples)
    {
        while (start < 0) start += ringSamples;
        while (end < 0) end += ringSamples;
        start %= ringSamples; end %= ringSamples;

        var outList = new List<float>();

        if (end >= start)
        {
            int length = end - start;
            if (length <= 0) return outList;
            var temp = new float[length * channels];
            clip.GetData(temp, start);
            outList.AddRange(temp);
        }
        else
        {
            int len1 = ringSamples - start;
            int len2 = end;
            var temp1 = new float[len1 * channels];
            var temp2 = new float[len2 * channels];
            clip.GetData(temp1, start);
            clip.GetData(temp2, 0);
            outList.AddRange(temp1);
            outList.AddRange(temp2);
        }
        return outList;
    }

    static string ExtractWhisperText(object result)
    {
        if (result == null) return "";

        // Case 1: already a string
        if (result is string s) return s;

        var t = result.GetType();

        // Case 2: common single-text properties
        var propNames = new[] { "Text", "text", "Result", "result", "Transcription", "transcription" };
        foreach (var name in propNames)
        {
            var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (p != null && p.PropertyType == typeof(string))
            {
                var val = (string)p.GetValue(result);
                if (!string.IsNullOrEmpty(val)) return val;
            }
            var f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (f != null && f.FieldType == typeof(string))
            {
                var val = (string)f.GetValue(result);
                if (!string.IsNullOrEmpty(val)) return val;
            }
        }

        // Case 3: segments collection with per-segment text
        var segProp = t.GetProperty("Segments", BindingFlags.Public | BindingFlags.Instance)
                    ?? t.GetProperty("segments", BindingFlags.Public | BindingFlags.Instance);
        if (segProp != null)
        {
            var segs = segProp.GetValue(result) as System.Collections.IEnumerable;
            if (segs != null)
            {
                var pieces = new System.Collections.Generic.List<string>();
                foreach (var seg in segs)
                {
                    var st = seg.GetType();
                    var sp = st.GetProperty("Text", BindingFlags.Public | BindingFlags.Instance)
                            ?? st.GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
                    if (sp != null && sp.PropertyType == typeof(string))
                    {
                        var piece = (string)sp.GetValue(seg);
                        if (!string.IsNullOrEmpty(piece)) pieces.Add(piece);
                    }
                }
                if (pieces.Count > 0) return string.Join(" ", pieces);
            }
        }

        // Fallback: dump anything useful-looking
        return "";
    }
}

