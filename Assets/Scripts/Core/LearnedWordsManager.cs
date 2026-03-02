using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Persistent log of all words learned through roleplay scenario drills.
/// Words are added when a scenario's word drill is completed.
/// Persisted via PlayerPrefs JSON.
/// </summary>
public class LearnedWordsManager : MonoBehaviour
{
    public static LearnedWordsManager Instance { get; private set; }

    private const string PrefsKey = "LearnedWordsList";

    /// <summary>All learned words, newest first.</summary>
    public List<LearnedWord> Words { get; private set; } = new List<LearnedWord>();

    /// <summary>Fast lookup by character.</summary>
    private HashSet<string> knownCharacters = new HashSet<string>();

    /// <summary>Fired when the word list changes.</summary>
    public event System.Action OnWordsChanged;

    [System.Serializable]
    public class LearnedWord
    {
        public string character;
        public string pinyinDisplay;
        public string pinyinTypeable;
        public string definition;
        public string scenarioTitle;   // which scenario it came from
        public string learnedDate;
    }

    [System.Serializable]
    private class WordListWrapper
    {
        public List<LearnedWord> words = new List<LearnedWord>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Load();
    }

    // ── Persistence ──────────────────────────────────────────────────

    private void Load()
    {
        Words.Clear();
        knownCharacters.Clear();
        string json = PlayerPrefs.GetString(PrefsKey, "");
        if (!string.IsNullOrEmpty(json))
        {
            WordListWrapper wrapper = JsonUtility.FromJson<WordListWrapper>(json);
            if (wrapper != null && wrapper.words != null)
            {
                Words = wrapper.words;
                foreach (var w in Words)
                    knownCharacters.Add(w.character);
            }
        }
        Debug.Log($"[LearnedWordsManager] Loaded {Words.Count} learned words.");
    }

    private void Save()
    {
        WordListWrapper wrapper = new WordListWrapper();
        wrapper.words = Words;
        string json = JsonUtility.ToJson(wrapper);
        PlayerPrefs.SetString(PrefsKey, json);
        PlayerPrefs.Save();
    }

    // ── Public API ───────────────────────────────────────────────────

    /// <summary>Is this character already in the learned words log?</summary>
    public bool IsLearned(string character)
    {
        return knownCharacters.Contains(character);
    }

    /// <summary>
    /// Add all words from a completed scenario drill.
    /// Skips duplicates (same character).
    /// </summary>
    public void AddWordsFromScenario(RoleplayScenario scenario)
    {
        if (scenario == null || scenario.words == null) return;

        string date = System.DateTime.Now.ToString("yyyy-MM-dd");
        int added = 0;

        foreach (var w in scenario.words)
        {
            if (knownCharacters.Contains(w.character))
                continue;

            LearnedWord lw = new LearnedWord();
            lw.character = w.character;
            lw.pinyinDisplay = w.pinyinDisplay;
            lw.pinyinTypeable = w.pinyinTypeable;
            lw.definition = w.definition;
            lw.scenarioTitle = scenario.title;
            lw.learnedDate = date;

            Words.Insert(0, lw); // newest first
            knownCharacters.Add(w.character);
            added++;
        }

        if (added > 0)
        {
            Save();
            OnWordsChanged?.Invoke();
            Debug.Log($"[LearnedWordsManager] Added {added} new words from \"{scenario.title}\". Total: {Words.Count}");
        }
    }

    /// <summary>Total number of unique learned words.</summary>
    public int Count => Words.Count;
}
