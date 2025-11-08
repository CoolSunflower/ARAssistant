using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;

public class ARReticleAndPlace : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject avatarPrefab;     // your VRoid prefab (cube icon!)
    public GameObject reticlePrefab;    // tiny circle/quad

    [Header("Refs")]
    public ARRaycastManager raycaster;  // drag XR Origin's ARRaycastManager here

    GameObject placedAvatar;
    GameObject reticle;
    static List<ARRaycastHit> hits = new();

    void Awake()
    {
        if (!raycaster) raycaster = GetComponent<ARRaycastManager>();
        if (reticlePrefab) reticle = Instantiate(reticlePrefab);
        if (reticle) reticle.SetActive(false);
    }

    public GameObject GetPlacedAvatar() => placedAvatar;

    void Update()
    {
        // draw reticle at hit point every frame until avatar is placed
        bool gotHit = (!placedAvatar) && raycaster && raycaster.Raycast(
            new Vector2(Screen.width/2f, Screen.height/2f), // center of screen
            hits, TrackableType.Planes);

        if (gotHit)
        {
            var pose = hits[0].pose;
            if (reticle)
            {
                reticle.SetActive(true);
                reticle.transform.SetPositionAndRotation(pose.position, pose.rotation);
            }
        }
        else if (reticle) reticle.SetActive(false);

        // if avatar is placed, make it orient toward camera
        if (placedAvatar)
        {
            var camPos = Camera.main.transform.position;
            var lookPos = new Vector3(camPos.x, placedAvatar.transform.position.y, camPos.z);
            placedAvatar.transform.LookAt(lookPos);
        }

        // tap to place
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            // confirm we’re receiving taps with haptic feedback
            Handheld.Vibrate();

            // raycast at finger
            var touch = Input.GetTouch(0);
            if (touch.position.y < Screen.height * 0.2f || touch.position.y > Screen.height * 0.8f) return; // touch should be within y level screen height of 0.2 to 0.8

            bool tapHit = raycaster && raycaster.Raycast(touch.position, hits, TrackableType.Planes);
            if (!tapHit) return;

            var pose = hits[0].pose;

            if (!avatarPrefab) return;

            if (!placedAvatar)
            {
                placedAvatar = Instantiate(avatarPrefab, pose.position, pose.rotation);
                placedAvatar.transform.localScale = Vector3.one * 0.1f;
                placedAvatar.transform.Rotate(0f, 180f, 0f, Space.Self);
            }
            else
            {
                placedAvatar.transform.SetPositionAndRotation(pose.position, pose.rotation);
                placedAvatar.transform.localScale = Vector3.one * 0.1f;
                placedAvatar.transform.Rotate(0f, 180f, 0f, Space.Self);
            }
        }
    }
}
