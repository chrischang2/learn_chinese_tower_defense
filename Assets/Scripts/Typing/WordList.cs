using UnityEngine;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>
/// Loads the hanziDB.json character database from Resources and serves
/// entries sorted by frequency rank (most common first).
/// Each entry holds the hanzi character, toned pinyin (display), and
/// ASCII pinyin (what the player types).
/// </summary>
public static class WordList
{
    // ── Public data type ─────────────────────────────────────────────

    public struct HanziEntry
    {
        public string Character;      // e.g. "你"
        public string PinyinDisplay;  // e.g. "nǐ"  (toned, for display)
        public string PinyinTypeable; // e.g. "ni"  (ASCII, for matching)
        public string Definition;     // e.g. "you, second person pronoun"
        public int    HskLevel;       // 1-6, or 0 if unlabelled
        public int    FrequencyRank;  // 1 = most frequent
    }

    // ── Internals ────────────────────────────────────────────────────

    private static bool loaded;
    // Sorted by frequency rank (index 0 = most frequent)
    private static readonly List<HanziEntry> allEntries = new List<HanziEntry>();

    // ── Public API ───────────────────────────────────────────────────

    /// <summary>
    /// Total number of available entries.
    /// </summary>
    public static int Count
    {
        get { EnsureLoaded(); return allEntries.Count; }
    }

    /// <summary>
    /// Get the entry at the given index (0-based, sorted by frequency).
    /// Index 0 = most frequent character, index 1 = second most, etc.
    /// </summary>
    public static HanziEntry GetEntryByIndex(int index)
    {
        EnsureLoaded();
        if (index < 0 || index >= allEntries.Count)
        {
            Debug.LogError($"WordList: index {index} out of range (count={allEntries.Count})");
            return allEntries[0];
        }
        return allEntries[index];
    }

    /// <summary>
    /// Get a random entry from the given pool whose typeable
    /// pinyin is not already in use on screen.
    /// </summary>
    public static HanziEntry GetRandomFrom(List<HanziEntry> pool, HashSet<string> usedPinyin)
    {
        EnsureLoaded();
        if (pool.Count == 0) return allEntries[0];

        int attempts = 0;
        HanziEntry entry;
        do
        {
            entry = pool[Random.Range(0, pool.Count)];
            attempts++;
        }
        while (usedPinyin.Contains(entry.PinyinTypeable) && attempts < 200);

        return entry;
    }

    // ── Loading ──────────────────────────────────────────────────────

    private static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;

        TextAsset asset = Resources.Load<TextAsset>("hanziDB");
        if (asset == null)
        {
            Debug.LogError("WordList: Could not load hanziDB.json from Resources!");
            return;
        }

        string[] lines = asset.text.Split('\n');

        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            JsonEntry je = JsonUtility.FromJson<JsonEntry>(line);
            if (string.IsNullOrEmpty(je.pinyin)) continue;
            if (string.IsNullOrEmpty(je.charcter)) continue;

            HanziEntry entry;
            entry.Character      = je.charcter;
            entry.PinyinDisplay  = je.pinyin;
            entry.PinyinTypeable = StripTones(je.pinyin);
            entry.Definition     = je.definition ?? "";
            entry.FrequencyRank  = ParseInt(je.frequency_rank, 9999);

            int hsk = ParseInt(je.hsk_levl, 0);
            entry.HskLevel = (hsk >= 1 && hsk <= 6) ? hsk : 0;

            // Skip entries whose typeable pinyin is empty or only 1 letter
            if (entry.PinyinTypeable.Length < 2) continue;

            allEntries.Add(entry);
        }

        // Sort by frequency rank (ascending = most frequent first)
        allEntries.Sort((a, b) => a.FrequencyRank.CompareTo(b.FrequencyRank));

        // Deduplicate by character (keep first occurrence = lowest frequency rank)
        HashSet<string> seen = new HashSet<string>();
        int before = allEntries.Count;
        allEntries.RemoveAll(e =>
        {
            if (seen.Contains(e.Character)) return true;
            seen.Add(e.Character);
            return false;
        });

        Debug.Log($"WordList: loaded {allEntries.Count} hanzi entries (removed {before - allEntries.Count} duplicates), sorted by frequency.");
    }

    // ── Helpers ──────────────────────────────────────────────────────

    /// <summary>    /// Public wrapper for StripTones — used by LLMWordGenerator to convert
    /// toned pinyin into typeable ASCII pinyin.
    /// </summary>
    public static string StripTonesPublic(string pinyin)
    {
        return StripTones(pinyin);
    }

    /// <summary>    /// Strip Unicode diacritics / tone marks to produce plain ASCII pinyin.
    /// e.g. "zhōng" → "zhong", "nǐ" → "ni", "lǜ" → "lv"
    /// </summary>
    private static string StripTones(string pinyin)
    {
        // Handle ü/ǖ/ǘ/ǚ/ǜ before decomposition
        pinyin = pinyin.Replace("ü", "v");

        // Normalise to decomposed form so diacritics become separate chars
        string normalised = pinyin.Normalize(NormalizationForm.FormD);

        StringBuilder sb = new StringBuilder(normalised.Length);
        foreach (char c in normalised)
        {
            UnicodeCategory cat = CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat == UnicodeCategory.NonSpacingMark)
                continue; // skip combining diacritical marks

            sb.Append(c);
        }

        string result = sb.ToString().Normalize(NormalizationForm.FormC).ToLower();

        return result;
    }

    private static int ParseInt(string s, int fallback)
    {
        if (string.IsNullOrEmpty(s)) return fallback;
        int val;
        if (int.TryParse(s, out val)) return val;
        return fallback;
    }

    // ── JSON mapping (used by JsonUtility) ───────────────────────────

    [System.Serializable]
    private class JsonEntry
    {
        public string frequency_rank;
        public string charcter;
        public string pinyin;
        public string definition;
        public string radical;
        public string radical_code;
        public string stroke_count;
        public string hsk_levl;
        public string general_standard_num;
    }
}
