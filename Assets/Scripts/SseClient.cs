using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Text;

public class SseClient : MonoBehaviour
{
    public event Action<string> OnDelta; // already-decoded text delta (your worker sends JSON-encoded strings)
    public event Action OnDone;
    UnityWebRequest req;

    public void StartStream(string url)
    {
        StopStream();
        Debug.Log("[SSE] Opening: " + url); // <— add this

        req = new UnityWebRequest(url, "GET");
        req.downloadHandler = new SseDownloadHandler(line =>
        {
            Debug.Log("SSE Line: " + line); // <— you already added this
            if (line == "[DONE]") { OnDone?.Invoke(); StopStream(); return; }
            if (line.Length >= 2 && line[0] == '"' && line[^1] == '"')
                line = line.Substring(1, line.Length - 2).Replace("\\n", "\n").Replace("\\t", "\t").Replace("\\\"", "\"");
            if (!string.IsNullOrEmpty(line)) OnDelta?.Invoke(line);
        });
        req.SetRequestHeader("Accept", "text/event-stream");
        req.SendWebRequest();

        // Watchdog to surface any errors
        StartCoroutine(Watch(req));
    }

    private System.Collections.IEnumerator Watch(UnityWebRequest r)
    {
        // Wait a bit for data; SSE connections remain open.
        float t = 0f;
        while (!r.isDone && t < 5f)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        if (r.result != UnityWebRequest.Result.Success && r.result != UnityWebRequest.Result.InProgress)
        {
            Debug.LogError("[SSE] Error: " + r.error);
        }
        else
        {
            Debug.Log("[SSE] Connected (result=" + r.result + ")");
        }
    }

    public void StopStream()
    {
        if (req != null) { try { req.Abort(); } catch { } req = null; }
    }

    class SseDownloadHandler : DownloadHandlerScript
    {
        private StringBuilder sb = new StringBuilder();
        private Action<string> onLine;

        public SseDownloadHandler(Action<string> onLine, byte[] buffer = null) : base(buffer) { this.onLine = onLine; }

        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            if (data == null || dataLength == 0) return true;
            var chunk = Encoding.UTF8.GetString(data, 0, dataLength);
            sb.Append(chunk);

            // split on double-newline event separators
            var text = sb.ToString();
            int idx;
            while ((idx = text.IndexOf("\n\n", StringComparison.Ordinal)) >= 0)
            {
                var frame = text.Substring(0, idx);
                text = text.Substring(idx + 2);

                foreach (var ln in frame.Split('\n'))
                {
                    var s = ln.Trim();
                    if (s.StartsWith("data:"))
                    {
                        var payload = s.Substring(5).Trim();
                        onLine?.Invoke(payload);
                    }
                }
            }
            sb.Length = 0;
            sb.Append(text);
            return true;
        }

        protected override void CompleteContent() { /* no-op */ }

        [System.Obsolete]
        protected override void ReceiveContentLength(int contentLength) { /* optional */ }
    }
}
