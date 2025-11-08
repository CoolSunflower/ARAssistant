using UnityEngine;

public class LipSyncTest : MonoBehaviour
{
    [Header("Refs")]
    public ARTapToPlace placer; // assign in Inspector

    private uLipSync.uLipSyncMicrophone mic;

    void Update()
    {
        // Grab the mic component once the avatar is placed.
        if (mic == null)
        {
            var avatar = placer != null ? placer.GetPlacedAvatar() : null;
            if (avatar != null)
            {
                mic = avatar.GetComponent<uLipSync.uLipSyncMicrophone>();
                if (mic == null)
                {
                    Debug.LogError("uLipSyncMicrophone not found on the placed avatar. "+
                                   "Add uLipSyncMicrophone + AudioSource on the avatar prefab (root).");
                }
            }
        }
    }

    public void StartMic()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // Request permission if needed
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
        {
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
            Debug.Log("Requested microphone permission. Try again after granting.");
            return;
        }
#endif
        if (mic == null) { Debug.LogWarning("Mic not ready yet."); return; }

        // Ensure device info is populated
        if (!mic.isReady) mic.UpdateMicInfo();

        // Start recording if not already
        if (!mic.isRecording)
        {
            mic.StartRecord();
            Debug.Log("Mic started (uLipSyncMicrophone.StartRecord).");
        }
        else
        {
            Debug.Log("Mic already recording.");
        }
    }

    public void StopMic()
    {
        if (mic == null) { Debug.LogWarning("Mic not ready yet."); return; }

        if (mic.isRecording)
        {
            mic.StopRecord(); // or mic.StopRecordAndCreateAudioClip() if you want to keep the last audio looped
            Debug.Log("Mic stopped (uLipSyncMicrophone.StopRecord).");
        }
        else
        {
            Debug.Log("Mic not recording.");
        }
    }
}
