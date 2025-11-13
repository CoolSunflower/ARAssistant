using System;
using UnityEngine;
using TMPro;

// This bridge wraps yasirkula's SpeechToText plugin and reproduces the same
// static events + IsRecording property your code expects.
// Minimal and safe: it forwards partials/final results and busy state.
public class STTBridge : MonoBehaviour, ISpeechToTextListener
{
    [Header("UI (optional)")]
    public TMP_Text partialText;
    public TMP_Text finalText;

    [Header("Plugin Options")]
    public string languageTag = "en-US";     // set or leave blank for device default
    public bool preferOffline = false;      // if plugin/device supports offline

    // Public events used by your AssistantStreamController & other code
    public static Action<string> OnFinalUtterance;   // final text
    public static Action<bool> OnSttBusyChanged;     // true = listening/busy

    // Expose same property name used previously
    public bool IsRecording => SpeechToText.IsBusy();

    // ------------- Mono --------------
    void Awake()
    {
        // Ensure plugin initialised (optional)
        try { SpeechToText.Initialize(languageTag); }
        catch (Exception e) { Debug.LogWarning("[STTBridge] Init failed: " + e.Message); }
        if(partialText) partialText.text = "";
        if(finalText) finalText.text = "";
    }

    void OnEnable()
    {
        // nothing else; plugin callbacks happen via ISpeechToTextListener methods below
    }

    void OnDisable()
    {
        // ensure stopped
        if (SpeechToText.IsBusy()) SpeechToText.ForceStop();
    }

    // ---------- Button-facing API ----------
    public void OnToggleRecord()
    {
        if (SpeechToText.IsBusy()) ForceStop();
        else StartListening();
    }

    public void StartListening()
    {
        // Request permission and start via plugin
        SpeechToText.RequestPermissionAsync(result =>
        {
            if (result == SpeechToText.Permission.Granted)
            {
                bool ok = SpeechToText.Start(this, useFreeFormLanguageModel: true, preferOfflineRecognition: preferOffline);
                if (!ok)
                {
                    Debug.LogWarning("[STTBridge] SpeechToText.Start returned false.");
                    OnSttBusyChanged?.Invoke(false);
                }
                else
                {
                    OnSttBusyChanged?.Invoke(true);
                    if (partialText) partialText.text = "(listening...)";
                    if (finalText) finalText.text = "";
                }
            }
            else
            {
                Debug.LogWarning("[STTBridge] Microphone permission denied.");
                OnSttBusyChanged?.Invoke(false);
            }
        });
    }

    public void StopListening()
    {
        if (SpeechToText.IsBusy())
        {
            // ForceStop processes speech input so far and triggers the result callback.
            SpeechToText.ForceStop();
            // plugin will call OnResultReceived soon; OnSttBusyChanged will be fired there.
        }
    }


    public void ForceStop()
    {
        if (SpeechToText.IsBusy())
        {
            SpeechToText.ForceStop();
            OnSttBusyChanged?.Invoke(false);
        }
    }

    // ------------- ISpeechToTextListener callbacks -------------
    // Implement these methods exactly (plugin will call them on Unity main thread)
    public void OnReadyForSpeech() { /*noop*/ }
    public void OnBeginningOfSpeech() { /*noop*/ }

    public void OnVoiceLevelChanged(float normalizedVoiceLevel)
    {
        // optional: could map to UI VU meter
    }

    public void OnPartialResultReceived(string spokenText)
    {
        if (partialText) partialText.text = spokenText;
    }

    // Final result; plugin may pass error as second param in some versions, but
    // this method signature matches the plugin's interface.
    public void OnResultReceived(string spokenText, int? errorCode)
    {
        // plugin uses null/0 for success typically; treat any string as final
        if (partialText) partialText.text = "";
        if (finalText) finalText.text = string.IsNullOrEmpty(spokenText) ? "(no text)" : spokenText;

        OnFinalUtterance?.Invoke(spokenText ?? "");
        OnSttBusyChanged?.Invoke(false);
    }
}
