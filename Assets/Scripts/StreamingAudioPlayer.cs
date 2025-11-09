using System.Collections.Generic;
using UnityEngine;

public class StreamingAudioPlayer : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("AudioSource on your AvatarFinal/Lipsync object")]
    public AudioSource target;

    [Header("Buffering")]
    [Range(0f, 2f)] public float minStartBufferSec = 0.6f;   // wait before first Play()
    [Range(0f, 2f)] public float reStartBufferSec  = 0.3f;   // if stopped, wait this much then Play()

    int sampleRate = 22050;
    int channels = 1;
    Queue<float> q = new Queue<float>(44100 * 8);
    readonly object lockObj = new object();
    AudioClip clip;
    bool started;
    int clipRate, clipCh;

    void Update()
    {
        // If target got stopped by Android/OS but we still have buffered audio, re-start
        if (started && target && !target.isPlaying)
        {
            float secBuffered;
            lock (lockObj)
            {
                secBuffered = (float)q.Count / (sampleRate * channels);
            }
            if (secBuffered >= reStartBufferSec)
            {
                Debug.Log("[Player] Auto-restart AudioSource");
                target.Play();
            }
        }
    }

    public void Configure(int sr, int ch)
    {
        sr = Mathf.Clamp(sr, 8000, 96000);
        ch = Mathf.Clamp(ch, 1, 2);

        // Recreate clip if format changed
        if (clip == null || clipRate != sr || clipCh != ch)
        {
            clipRate = sr;
            clipCh = ch;
            CreateClip(sr, ch);
        }
        sampleRate = sr;
        channels = ch;
    }

    void CreateClip(int sr, int ch)
    {
        if (!target)
        {
            Debug.LogError("[Player] No AudioSource assigned.");
            return;
        }
        // Dispose old clip if any
        if (clip != null)
        {
            if (target.isPlaying) target.Stop();
            Destroy(clip);
            clip = null;
            started = false;
        }

        // 12 seconds ring clip; Unity will call PCMReaderCallback continuously
        clip = AudioClip.Create("tts-stream", sr * 12, ch, sr, true, OnAudioRead, OnSetPosition);
        target.clip = clip;
        target.loop = true; // keep callback firing
        Debug.Log($"[Player] Created streaming clip sr={sr} ch={ch}");
    }

    public void EnqueuePcm16(byte[] bytes)
    {
        if (bytes == null || bytes.Length < 2) return;

        bool shouldStart = false;

        lock (lockObj)
        {
            for (int i = 0; i < bytes.Length; i += 2)
            {
                short s = (short)(bytes[i] | (bytes[i + 1] << 8));
                q.Enqueue(s / 32768f);
            }

            if (!started && target && target.clip)
            {
                float sec = (float)q.Count / (sampleRate * channels);
                if (sec >= minStartBufferSec)
                {
                    started = true;
                    shouldStart = true;
                }
            }
        }

        if (shouldStart)
        {
            Debug.Log($"[Player] Start play (buffered ≥ {minStartBufferSec:0.00}s)");
            target.Play();
        }
    }

    void OnAudioRead(float[] data)
    {
        lock (lockObj)
        {
            for (int i = 0; i < data.Length; i++)
                data[i] = q.Count > 0 ? q.Dequeue() : 0f; // smooth underflow = silence
        }
    }

    void OnSetPosition(int pos) { /* not used */ }

    public void ResetStream()
    {
        lock (lockObj) q.Clear();
        started = false;
        if (target && target.isPlaying) target.Stop();
        Debug.Log("[Player] Reset stream");
    }
}
