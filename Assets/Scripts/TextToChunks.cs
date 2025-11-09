using System.Text;
using System.Collections.Generic;

public class TextToChunks
{
    StringBuilder sb = new StringBuilder();
    List<string> ready = new List<string>();
    int wordLimit = 8;

    public void Reset() { sb.Clear(); ready.Clear(); }

    public IEnumerable<string> PushDelta(string delta)
    {
        if (string.IsNullOrEmpty(delta)) yield break;
        sb.Append(delta);

        // Split on sentence-ish punctuation first
        string text = sb.ToString();
        int cut = FindSentenceBoundary(text);
        while (cut > 0)
        {
            var chunk = text.Substring(0, cut).Trim();
            if (chunk.Length > 0) yield return chunk;
            text = text.Substring(cut);
            cut = FindSentenceBoundary(text);
        }

        // If sentence boundary not found, emit on word threshold
        var words = text.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);
        if (words.Length >= wordLimit)
        {
            // emit first wordLimit words as a chunk
            var chunk = string.Join(" ", words, 0, wordLimit);
            yield return chunk;
            sb.Clear();
            sb.Append(text.Substring(chunk.Length));
        }
        else
        {
            sb.Clear();
            sb.Append(text);
        }
    }

    public IEnumerable<string> FlushRemainder()
    {
        var rest = sb.ToString().Trim();
        sb.Clear();
        if (rest.Length > 0) yield return rest;
    }

    static int FindSentenceBoundary(string s)
    {
        int idx = -1;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '.' || c == '!' || c == '?' || c == '。' || c == '！' || c == '？')
                idx = i + 1;
        }
        return idx;
    }
}
