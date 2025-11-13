using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class ClaimingPollingClient : MonoBehaviour
{
    public string baseUrl = "https://YOUR_NGROK_URL.ngrok.io";
    public string apiKey = "mysecret";
    public float pollInterval = 1.0f;
    public string clientId; // set to GUID once on Start

    // events
    public event Action<string, string> OnMessageClaimed; // (id, text)

    void Start()
    {
        if (string.IsNullOrEmpty(clientId)) clientId = System.Guid.NewGuid().ToString();
        StartCoroutine(PollLoop());
    }

    IEnumerator PollLoop()
    {
        while (true)
        {
            var url = $"{baseUrl}/latest?clientId={UnityWebRequest.EscapeURL(clientId)}";
            using (var req = UnityWebRequest.Get(url))
            {
                req.timeout = 5;
                req.SetRequestHeader("x-api-key", apiKey);
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var json = req.downloadHandler.text;
                        var obj = JsonUtility.FromJson<LatestResp>(json);
                        if (!string.IsNullOrEmpty(obj.id))
                        {
                            Debug.Log("[Polling] Claimed: " + obj.id + " -> " + obj.text);
                            OnMessageClaimed?.Invoke(obj.id, obj.text);
                        }
                    }
                    catch (Exception e) { Debug.LogWarning("[Polling] parse: " + e); }
                }
            }
            yield return new WaitForSeconds(pollInterval);
        }
    }

    // call after successful playback to ack
    public IEnumerator AckCoroutine(string id)
    {
        var url = $"{baseUrl}/ack";
        var body = JsonUtility.ToJson(new AckReq { id = id, clientId = clientId });
        using (var req = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(body);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("x-api-key", apiKey);
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
                Debug.Log("[Polling] Ack success for " + id);
            else
                Debug.LogWarning("[Polling] Ack failed: " + req.error + " - " + req.downloadHandler.text);
        }
    }

    [Serializable] class LatestResp { public string id; public string text; public string ts; public long claimExpiry; }
    [Serializable] class AckReq { public string id; public string clientId; }
}
