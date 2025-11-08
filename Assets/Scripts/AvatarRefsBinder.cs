// Assets/Scripts/AvatarRefsBinder.cs
using UnityEngine;

public class AvatarRefsBinder : MonoBehaviour
{
    public ARReticleAndPlace placer;                 // assign in Inspector
    public MicToggle micToggle;                 // assign in Inspector

    void Update()
    {
        if (micToggle.mic != null) return;

        var avatar = placer.GetPlacedAvatar();
        if (avatar != null)
        {
            micToggle.mic  = avatar.GetComponentInChildren<uLipSync.uLipSyncMicrophone>(true);
            micToggle.ulip = avatar.GetComponentInChildren<uLipSync.uLipSync>(true);

            if (micToggle.mic == null)
                Debug.LogError("uLipSyncMicrophone not found in avatar children.");
            if (micToggle.ulip == null)
                Debug.LogError("uLipSync (analyzer) not found in avatar children.");
        }
    }
}
