using UnityEngine;

public class MicToggle : MonoBehaviour
{
    [HideInInspector] public uLipSync.uLipSyncMicrophone mic;
    [HideInInspector] public uLipSync.uLipSync ulip;

    public void StartMic()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
        {
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
            Debug.Log("Requested mic permission. Try again after granting.");
            return;
        }
#endif
        if (mic == null)
        {
            Debug.LogWarning("Mic not ready yet (avatar not placed?).");
            return;
        }

        if (!mic.isReady)
            mic.UpdateMicInfo();

        if (!mic.isRecording)
        {
            mic.StartRecord();
            Debug.Log("Mic started.");
        }
        else
        {
            Debug.Log("Mic already recording.");
        }
    }

    public void StopMic()
    {
        if (mic == null)
        {
            Debug.LogWarning("Mic not ready yet.");
            return;
        }

        if (mic.isRecording)
        {
            mic.StopRecord(); // or mic.StopRecordAndCreateAudioClip() to freeze the last recorded clip
            Debug.Log("Mic stopped.");
        }
        else
        {
            Debug.Log("Mic not recording.");
        }
    }
}
