using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;

public class ARTapToPlace : MonoBehaviour
{
    public GameObject avatarPrefab;
    private GameObject placedAvatar;
    private ARRaycastManager raycaster;
    static List<ARRaycastHit> hits = new();

    void Awake() => raycaster = GetComponent<ARRaycastManager>();

    void Update()
    {
        if (Input.touchCount == 0) return;
        var t = Input.GetTouch(0);
        if (t.phase != TouchPhase.Began) return;

        if (raycaster.Raycast(t.position, hits, TrackableType.Planes))
        {
            var pose = hits[0].pose;
            if (placedAvatar == null)
                placedAvatar = Instantiate(avatarPrefab, pose.position, pose.rotation);
            else
                placedAvatar.transform.SetPositionAndRotation(pose.position, pose.rotation);
        }
    }

    public GameObject GetPlacedAvatar() => placedAvatar;
}
