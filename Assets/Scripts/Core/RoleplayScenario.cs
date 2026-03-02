using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Data model for a roleplay scenario, including its generated word list.
/// Serialised to/from JSON for PlayerPrefs persistence.
/// </summary>
[System.Serializable]
public class RoleplayScenario
{
    /// <summary>Unique ID (timestamp-based).</summary>
    public string id;

    /// <summary>Short title, e.g. "Ordering Food at a Restaurant".</summary>
    public string title;

    /// <summary>Scene description the LLM will use for roleplay.</summary>
    public string description;

    /// <summary>The role the user plays in this scenario (e.g. "tourist", "customer").</summary>
    public string userRole;

    /// <summary>The role the LLM plays in this scenario (e.g. "waiter", "shopkeeper").</summary>
    public string llmRole;

    /// <summary>Vocabulary words associated with this scenario.</summary>
    public List<RoleplayWord> words = new List<RoleplayWord>();

    /// <summary>Whether the player has completed the word drill for this scenario.</summary>
    public bool wordsLearned;

    /// <summary>When this scenario was generated.</summary>
    public string createdAt;

    [System.Serializable]
    public class RoleplayWord
    {
        public string character;
        public string pinyinDisplay;
        public string pinyinTypeable;
        public string definition;
    }

    /// <summary>Convert a HanziEntry to a RoleplayWord for serialisation.</summary>
    public static RoleplayWord FromHanziEntry(WordList.HanziEntry entry)
    {
        RoleplayWord w = new RoleplayWord();
        w.character = entry.Character;
        w.pinyinDisplay = entry.PinyinDisplay;
        w.pinyinTypeable = entry.PinyinTypeable;
        w.definition = entry.Definition;
        return w;
    }

    /// <summary>Convert a RoleplayWord back to a HanziEntry for gameplay.</summary>
    public static WordList.HanziEntry ToHanziEntry(RoleplayWord word)
    {
        WordList.HanziEntry entry;
        entry.Character = word.character;
        entry.PinyinDisplay = word.pinyinDisplay;
        entry.PinyinTypeable = word.pinyinTypeable;
        entry.Definition = word.definition;
        entry.HskLevel = 0;
        entry.FrequencyRank = 0;
        return entry;
    }

    /// <summary>Get all words as HanziEntry list for the spawner.</summary>
    public List<WordList.HanziEntry> ToHanziEntries()
    {
        List<WordList.HanziEntry> entries = new List<WordList.HanziEntry>();
        foreach (RoleplayWord w in words)
            entries.Add(ToHanziEntry(w));
        return entries;
    }
}
