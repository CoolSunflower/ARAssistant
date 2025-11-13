using System.Collections.Generic;

[System.Serializable]
public class Turn { public string user; public string assistant; }

public class ConversationMemory
{
    readonly int cap;
    readonly List<Turn> turns = new List<Turn>();
    public ConversationMemory(int capacity = 10) { cap = capacity; }
    public void Add(string user, string assistant)
    {
        turns.Add(new Turn { user = user, assistant = assistant });
        if (turns.Count > cap) turns.RemoveAt(0);
    }
    public List<Turn> Snapshot() => new List<Turn>(turns);
    public void Clear() => turns.Clear();
}
