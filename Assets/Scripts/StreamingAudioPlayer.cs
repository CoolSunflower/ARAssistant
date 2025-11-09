using System.Collections.Generic;
using UnityEngine;

public class StreamingAudioPlayer : MonoBehaviour
{
    public AudioSource target; // your Lipsync/AudioSource on the avatar

    int sampleRate = 22050;
    int channels = 1;
    Queue<float> q = new Queue<float>(44100 * 4);
    object lockObj = new object();
    AudioClip clip;
    bool started;

    public void Configure(int sr, int ch)
    {
        sampleRate = Mathf.Max(8000, sr);
        channels = Mathf.Max(1, ch);
        CreateClipIfNeeded();
    }

    void CreateClipIfNeeded()
    {
        if (clip != null) return;
        // 10 seconds virtual length; streaming = true; Unity will repeatedly call PCMReaderCallback
        clip = AudioClip.Create("tts-stream", sampleRate * 10, channels, sampleRate, true, OnAudioRead, OnSetPosition);
        target.clip = clip;
    }

    public void EnqueuePcm16(byte[] bytes)
    {
        // convert interleaved PCM16 -> float
        lock (lockObj)
        {
            for (int i = 0; i < bytes.Length; i += 2)
            {
                short s = (short)(bytes[i] | (bytes[i + 1] << 8));
                q.Enqueue(s / 32768f);
            }
        }
        if (!started && target.clip != null)
        {
            started = true;
            target.Play();
        }
    }

    void OnAudioRead(float[] data)
    {
        lock (lockObj)
        {
            for (int i = 0; i < data.Length; i++)
                data[i] = q.Count > 0 ? q.Dequeue() : 0f; // if underflow, fill silence
        }
    }

    void OnSetPosition(int pos) { /* not used */ }

    public void ResetStream()
    {
        lock (lockObj) q.Clear();
        started = false;
        if (target.isPlaying) target.Stop();
    }
}
