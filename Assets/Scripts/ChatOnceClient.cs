using UnityEngine;
using UnityEngine.Networking;
using System.Text;
using System.Collections;

public class ChatOnceClient : MonoBehaviour
{
    [Tooltip("Base URL WITHOUT trailing slash, e.g. https://ar-ai-worker.iamadarshgupta8.workers.dev")]
    public string baseUrl = "https://ar-ai-worker.iamadarshgupta8.workers.dev";

    [Tooltip("Endpoint path, must start with /")]
    public string chatPath = "/chat";

    public IEnumerator Ask(string userText, System.Collections.Generic.List<Turn> history, System.Action<string> onReply)
    {
        var url = baseUrl + chatPath;

        var payload = new Payload { text = userText, history = history };
        var json = JsonUtility.ToJson(payload);
        // var payload = "{\"text\":\"" + Escape(userText) + "\"}";
        // var body = Encoding.UTF8.GetBytes(payload);

        using var req = new UnityWebRequest(url, "POST");
        // req.uploadHandler = new UploadHandlerRaw(body);
        // req.downloadHandler = new DownloadHandlerBuffer();
        // req.SetRequestHeader("Content-Type", "application/json");
        // req.timeout = 20; // seconds
        req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.timeout = 20;

        Debug.Log("[CHAT] POST " + url + " body=" + json);
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            var reply = req.downloadHandler.text;
            Debug.Log("[CHAT] 200 OK: " + reply);
            onReply?.Invoke(reply);
        }
        else
        {
            Debug.LogError("[CHAT] Error (" + req.result + "): " + req.error + " - " + req.downloadHandler.text);
            onReply?.Invoke("");
        }
    }

    [System.Serializable] class Payload { public string text; public System.Collections.Generic.List<Turn> history; }

    static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
