using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Sends prompts to the local llama-server to generate themed Chinese word lists.
/// Returns results as List&lt;WordList.HanziEntry&gt; compatible with the existing system.
/// 
/// Usage:
///   var gen = LLMWordGenerator.Instance;
///   gen.GenerateThemedWords("animals", 10, entries => {
///       // entries is a List&lt;WordList.HanziEntry&gt;
///   });
/// </summary>
public class LLMWordGenerator : MonoBehaviour
{
    public static LLMWordGenerator Instance { get; private set; }

    [Header("Generation Settings")]
    [Tooltip("Max tokens for the LLM response")]
    public int maxTokens = 512;

    [Tooltip("Temperature (0 = deterministic, 1 = creative)")]
    [Range(0f, 1.5f)]
    public float temperature = 0.7f;

    /// <summary>True while a generation request is in flight.</summary>
    public bool IsBusy { get; private set; }

    /// <summary>Pre-defined themes to cycle through.</summary>
    private static readonly string[] Themes = new string[]
    {
        "food and drinks",
        "animals",
        "weather and nature",
        "family and people",
        "body parts",
        "colours and shapes",
        "numbers and time",
        "clothing",
        "school and study",
        "travel and transport",
        "emotions and feelings",
        "house and furniture",
        "sports and hobbies",
        "work and jobs",
        "technology"
    };

    private int themeIndex = 0;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── Public API ───────────────────────────────────────────────────

    /// <summary>
    /// Generate a themed word list. Calls onComplete with the results
    /// (may be empty if the server is unavailable).
    /// </summary>
    /// <param name="theme">Theme description, e.g. "animals"</param>
    /// <param name="count">How many words to request</param>
    /// <param name="onComplete">Callback with the generated entries</param>
    public void GenerateThemedWords(string theme, int count,
        System.Action<List<WordList.HanziEntry>> onComplete)
    {
        if (IsBusy)
        {
            Debug.LogWarning("[LLMWordGen] Already busy, skipping request.");
            onComplete?.Invoke(new List<WordList.HanziEntry>());
            return;
        }

        StartCoroutine(GenerateCoroutine(theme, count, onComplete));
    }

    /// <summary>
    /// Generate words using the next theme in the rotation.
    /// </summary>
    public void GenerateNextTheme(int count,
        System.Action<List<WordList.HanziEntry>> onComplete)
    {
        string theme = Themes[themeIndex % Themes.Length];
        themeIndex++;
        GenerateThemedWords(theme, count, onComplete);
    }

    /// <summary>
    /// Check if the LLM server is available and ready.
    /// </summary>
    public bool IsServerReady()
    {
        return LlamaServerManager.Instance != null &&
               LlamaServerManager.Instance.IsReady;
    }

    // ── Core generation ──────────────────────────────────────────────

    private IEnumerator GenerateCoroutine(string theme, int count,
        System.Action<List<WordList.HanziEntry>> onComplete)
    {
        IsBusy = true;
        List<WordList.HanziEntry> results = new List<WordList.HanziEntry>();

        if (!IsServerReady())
        {
            Debug.LogWarning("[LLMWordGen] Server not ready, returning empty list.");
            IsBusy = false;
            onComplete?.Invoke(results);
            yield break;
        }

        string prompt = BuildPrompt(theme, count);
        string url = LlamaServerManager.Instance.BaseUrl + "/v1/chat/completions";

        // Build the OpenAI-compatible request body
        string requestBody = BuildRequestJson(prompt);

        Debug.Log($"[LLMWordGen] Requesting {count} words, theme: \"{theme}\"");

        using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(requestBody);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 120; // CPU inference can be slow

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[LLMWordGen] Request failed: {req.error}");
                IsBusy = false;
                onComplete?.Invoke(results);
                yield break;
            }

            string responseText = req.downloadHandler.text;
            results = ParseResponse(responseText);
            Debug.Log($"[LLMWordGen] Generated {results.Count} entries.");
        }

        IsBusy = false;
        onComplete?.Invoke(results);
    }

    // ── Prompt building ──────────────────────────────────────────────

    private string BuildPrompt(string theme, int count)
    {
        return $@"Generate exactly {count} Chinese vocabulary words related to the theme ""{theme}"".

For each word, output ONE line in this exact format (no extra text, no numbering):
character|pinyin_with_tones|english_definition

Rules:
- character: a single Chinese character (汉字), NOT a multi-character word
- pinyin_with_tones: pinyin with tone marks (e.g. nǐ, zhōng, lǜ)
- english_definition: brief English meaning (2-5 words)
- Use only common, well-known characters
- Do NOT include any other text, headers, or explanations
- Output exactly {count} lines

Example output for theme ""animals"":
猫|māo|cat
狗|gǒu|dog
鸟|niǎo|bird";
    }

    private string BuildRequestJson(string prompt)
    {
        // Escape the prompt for JSON
        string escapedPrompt = prompt
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");

        return $@"{{
  ""model"": ""local"",
  ""messages"": [
    {{
      ""role"": ""system"",
      ""content"": ""You are a Chinese language teaching assistant. You output structured data only, no explanations.""
    }},
    {{
      ""role"": ""user"",
      ""content"": ""{escapedPrompt}""
    }}
  ],
  ""max_tokens"": {maxTokens},
  ""temperature"": {temperature.ToString(System.Globalization.CultureInfo.InvariantCulture)},
  ""stream"": false
}}";
    }

    // ── Response parsing ─────────────────────────────────────────────

    private List<WordList.HanziEntry> ParseResponse(string json)
    {
        List<WordList.HanziEntry> entries = new List<WordList.HanziEntry>();

        // Extract the "content" field from the OpenAI-style response
        // Response format: { "choices": [{ "message": { "content": "..." } }] }
        string content = ExtractContent(json);
        if (string.IsNullOrEmpty(content))
        {
            Debug.LogWarning("[LLMWordGen] Could not extract content from response.");
            return entries;
        }

        string[] lines = content.Split('\n');
        int rank = 1;

        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            // Expected format: character|pinyin|definition
            string[] parts = line.Split('|');
            if (parts.Length < 3) continue;

            string character = parts[0].Trim();
            string pinyin = parts[1].Trim();
            string definition = parts[2].Trim();

            // Validate: character should be 1-2 chars, pinyin should be non-empty
            if (character.Length < 1 || character.Length > 2) continue;
            if (string.IsNullOrEmpty(pinyin)) continue;

            string typeable = WordList.StripTonesPublic(pinyin);
            if (typeable.Length < 2) continue;

            WordList.HanziEntry entry;
            entry.Character = character;
            entry.PinyinDisplay = pinyin;
            entry.PinyinTypeable = typeable;
            entry.Definition = definition;
            entry.HskLevel = 0;
            entry.FrequencyRank = rank++;

            entries.Add(entry);
        }

        return entries;
    }

    /// <summary>
    /// Extract the assistant message content from an OpenAI-format JSON response.
    /// Uses simple string parsing to avoid needing a JSON library.
    /// </summary>
    private string ExtractContent(string json)
    {
        // Find "content": "..." in the response
        string marker = "\"content\":";
        int idx = json.LastIndexOf(marker);
        if (idx < 0) return null;

        idx += marker.Length;

        // Skip whitespace
        while (idx < json.Length && (json[idx] == ' ' || json[idx] == '\t'))
            idx++;

        if (idx >= json.Length || json[idx] != '"') return null;
        idx++; // skip opening quote

        StringBuilder sb = new StringBuilder();
        while (idx < json.Length)
        {
            char c = json[idx];
            if (c == '"') break; // end of string
            if (c == '\\' && idx + 1 < json.Length)
            {
                idx++;
                char next = json[idx];
                switch (next)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    default: sb.Append(next); break;
                }
            }
            else
            {
                sb.Append(c);
            }
            idx++;
        }

        return sb.ToString();
    }
}
