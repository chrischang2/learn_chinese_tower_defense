using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages roleplay scenarios: persistence, generation, and the active scenario
/// for word-drill / conversation modes.
/// </summary>
public class RoleplayManager : MonoBehaviour
{
    public static RoleplayManager Instance { get; private set; }

    private const string ScenariosKey = "RoleplayScenarios";

    /// <summary>All saved scenarios.</summary>
    public List<RoleplayScenario> Scenarios { get; private set; } = new List<RoleplayScenario>();

    /// <summary>The scenario currently being played (word drill or conversation).</summary>
    public RoleplayScenario ActiveScenario { get; set; }

    /// <summary>True while the LLM is generating a new scenario.</summary>
    public bool IsGenerating { get; private set; }

    /// <summary>Fired when scenarios list changes (generated, deleted).</summary>
    public event System.Action OnScenariosChanged;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadScenarios();
    }

    // ── Persistence ──────────────────────────────────────────────────

    private void LoadScenarios()
    {
        Scenarios.Clear();
        string json = PlayerPrefs.GetString(ScenariosKey, "");
        if (!string.IsNullOrEmpty(json))
        {
            ScenarioListWrapper wrapper = JsonUtility.FromJson<ScenarioListWrapper>(json);
            if (wrapper != null && wrapper.scenarios != null)
                Scenarios = wrapper.scenarios;
        }
        Debug.Log($"[RoleplayManager] Loaded {Scenarios.Count} saved scenarios.");
    }

    private void SaveScenarios()
    {
        ScenarioListWrapper wrapper = new ScenarioListWrapper();
        wrapper.scenarios = Scenarios;
        string json = JsonUtility.ToJson(wrapper);
        PlayerPrefs.SetString(ScenariosKey, json);
        PlayerPrefs.Save();
    }

    [System.Serializable]
    private class ScenarioListWrapper
    {
        public List<RoleplayScenario> scenarios = new List<RoleplayScenario>();
    }

    // ── Public API ───────────────────────────────────────────────────

    /// <summary>
    /// Generate a new scenario using the LLM. Calls onComplete when done.
    /// The LLM generates a scenario title, description, and word list.
    /// </summary>
    public void GenerateScenario(string userTopic, string userRole, string llmRole,
        System.Action<RoleplayScenario> onComplete)
    {
        if (IsGenerating)
        {
            Debug.LogWarning("[RoleplayManager] Already generating a scenario.");
            onComplete?.Invoke(null);
            return;
        }

        if (LLMWordGenerator.Instance == null || !LLMWordGenerator.Instance.IsServerReady())
        {
            Debug.LogWarning("[RoleplayManager] LLM server not ready.");
            onComplete?.Invoke(null);
            return;
        }

        IsGenerating = true;
        StartCoroutine(GenerateScenarioCoroutine(userTopic, userRole, llmRole, onComplete));
    }

    private System.Collections.IEnumerator GenerateScenarioCoroutine(
        string userTopic, string userRole, string llmRole,
        System.Action<RoleplayScenario> onComplete)
    {
        // Step 1: Ask LLM to generate a scenario
        string prompt = BuildScenarioPrompt(userTopic, userRole, llmRole);
        string url = LlamaServerManager.Instance.BaseUrl + "/v1/chat/completions";

        string requestBody = BuildChatRequestJson(
            "You are a Chinese language teaching assistant. Generate creative roleplay scenarios for language learners.",
            prompt, 1024);

        Debug.Log("[RoleplayManager] Generating new scenario...");

        string responseContent = null;

        using (var req = new UnityEngine.Networking.UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(requestBody);
            req.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 180;

            yield return req.SendWebRequest();

            if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[RoleplayManager] Scenario generation failed: {req.error}");
                IsGenerating = false;
                onComplete?.Invoke(null);
                yield break;
            }

            responseContent = ExtractContent(req.downloadHandler.text);
        }

        if (string.IsNullOrEmpty(responseContent))
        {
            Debug.LogError("[RoleplayManager] Empty response from LLM.");
            IsGenerating = false;
            onComplete?.Invoke(null);
            yield break;
        }

        // Parse the scenario — dump raw response for debugging
        Debug.LogWarning($"[RoleplayManager] Raw LLM response:\n{responseContent}");

        // Also write to a file so we can inspect easily
        try
        {
            string debugPath = System.IO.Path.Combine(Application.dataPath, "..", "llm_debug_response.txt");
            System.IO.File.WriteAllText(debugPath, responseContent);
            Debug.LogWarning($"[RoleplayManager] Raw response saved to: {debugPath}");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[RoleplayManager] Could not write debug file: {ex.Message}");
        }

        RoleplayScenario scenario = ParseScenarioResponse(responseContent);
        if (scenario == null)
        {
            Debug.LogError("[RoleplayManager] Failed to parse scenario response.");
            IsGenerating = false;
            onComplete?.Invoke(null);
            yield break;
        }

        // Store the roles the user specified
        scenario.userRole = string.IsNullOrEmpty(userRole) ? "learner" : userRole;
        scenario.llmRole = string.IsNullOrEmpty(llmRole) ? "tutor" : llmRole;

        // Add to list and save
        Scenarios.Insert(0, scenario);
        SaveScenarios();
        OnScenariosChanged?.Invoke();

        IsGenerating = false;
        Debug.Log($"[RoleplayManager] Generated scenario: \"{scenario.title}\" with {scenario.words.Count} words.");
        onComplete?.Invoke(scenario);
    }

    public void DeleteScenario(RoleplayScenario scenario)
    {
        Scenarios.Remove(scenario);
        SaveScenarios();
        OnScenariosChanged?.Invoke();
    }

    public void MarkWordsLearned(RoleplayScenario scenario)
    {
        scenario.wordsLearned = true;
        SaveScenarios();
        OnScenariosChanged?.Invoke();
    }

    // ── Prompt building ──────────────────────────────────────────────

    private string BuildScenarioPrompt(string userTopic, string userRole, string llmRole)
    {
        string roleContext = "";
        if (!string.IsNullOrEmpty(userRole))
            roleContext += $"\nThe student is playing the role of: {userRole}";
        if (!string.IsNullOrEmpty(llmRole))
            roleContext += $"\nThe conversation partner is playing the role of: {llmRole}";

        return $@"Create a Chinese vocabulary list for this scenario: {userTopic}{roleContext}

Use EXACTLY this format:

TITLE: {userTopic}
DESCRIPTION: A short description of the scenario.
WORDS:
你|nǐ|you
好|hǎo|good
吃|chī|eat
饭|fàn|rice/meal
水|shuǐ|water
茶|chá|tea
买|mǎi|buy
卖|mài|sell
大|dà|big
小|xiǎo|small
多|duō|many
少|shǎo|few
要|yào|want
给|gěi|give
谢|xiè|thank

Now replace the example words above with 15 real Chinese words relevant to: {userTopic}
Keep the exact same pipe format: character|pinyin|english
Each word must be 1-2 Chinese characters, not the example words.";
    }

    private RoleplayScenario ParseScenarioResponse(string content)
    {
        RoleplayScenario scenario = new RoleplayScenario();
        scenario.id = System.DateTime.Now.Ticks.ToString();
        scenario.createdAt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        scenario.wordsLearned = false;

        // Clean up markdown formatting the LLM might add
        content = content.Replace("**", "").Replace("##", "").Replace("# ", "");

        string[] lines = content.Split('\n');
        bool inWords = false;

        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            // Strip leading list markers like "1. ", "- ", "* "
            string stripped = System.Text.RegularExpressions.Regex.Replace(line, @"^\d+[\.\)]\s*", "");
            stripped = System.Text.RegularExpressions.Regex.Replace(stripped, @"^[-\*]\s+", "");

            // Try to find title (flexible matching)
            string upperLine = line.ToUpper().TrimStart('-', '*', ' ', '#');
            if (string.IsNullOrEmpty(scenario.title))
            {
                if (upperLine.StartsWith("TITLE:") || upperLine.StartsWith("TITLE :")
                    || upperLine.StartsWith("SCENARIO:") || upperLine.StartsWith("SCENARIO :"))
                {
                    int colonIdx = line.IndexOf(':');
                    if (colonIdx >= 0 && colonIdx < line.Length - 1)
                        scenario.title = line.Substring(colonIdx + 1).Trim().Trim('"', '*', '#');
                    continue;
                }
            }

            // Try to find description
            if (string.IsNullOrEmpty(scenario.description) && !string.IsNullOrEmpty(scenario.title))
            {
                if (upperLine.StartsWith("DESCRIPTION:") || upperLine.StartsWith("DESCRIPTION :")
                    || upperLine.StartsWith("SCENE:") || upperLine.StartsWith("SITUATION:"))
                {
                    int colonIdx = line.IndexOf(':');
                    if (colonIdx >= 0 && colonIdx < line.Length - 1)
                        scenario.description = line.Substring(colonIdx + 1).Trim().Trim('"', '*');
                    continue;
                }
            }

            // Detect start of words section
            if (upperLine.StartsWith("WORDS:") || upperLine.StartsWith("WORDS")
                || upperLine.StartsWith("VOCABULARY:") || upperLine.StartsWith("VOCABULARY")
                || upperLine.StartsWith("WORD LIST:") || upperLine.StartsWith("WORD LIST"))
            {
                inWords = true;
                // Check if there's a word on the same line after the colon
                int colonIdx = line.IndexOf(':');
                if (colonIdx >= 0)
                {
                    string rest = line.Substring(colonIdx + 1).Trim();
                    if (rest.Contains("|"))
                        TryParseWord(rest, scenario);
                }
                continue;
            }

            // If we haven't found words section header but see pipe-delimited
            // lines with Chinese characters, start parsing anyway
            if (line.Contains("|"))
            {
                if (!inWords)
                {
                    // Check if this looks like a word entry (contains CJK characters)
                    string firstPart = line.Split('|')[0].Trim();
                    if (ContainsCJK(firstPart))
                        inWords = true;
                }

                if (inWords)
                    TryParseWord(stripped, scenario);
                continue;
            }

            // If no title yet, and this is the first non-empty non-label line,
            // treat it as the title
            if (string.IsNullOrEmpty(scenario.title) && line.Length > 3 && line.Length < 80
                && !line.Contains("|"))
            {
                scenario.title = line.Trim('"', '*', '#', ' ');
                continue;
            }

            // If we have a title but no description, and this is a sentence,
            // treat it as description
            if (!string.IsNullOrEmpty(scenario.title) && string.IsNullOrEmpty(scenario.description)
                && line.Length > 10 && !line.Contains("|") && !inWords)
            {
                scenario.description = line.Trim('"', '*');
                continue;
            }
        }

        // Cap at 20 words
        if (scenario.words.Count > 20)
            scenario.words.RemoveRange(20, scenario.words.Count - 20);

        Debug.Log($"[RoleplayManager] Parsed: title=\"{scenario.title}\", " +
                  $"desc=\"{scenario.description}\", words={scenario.words.Count}");

        // Validate — be lenient: just need a title and at least 1 word
        if (string.IsNullOrEmpty(scenario.title) || scenario.words.Count < 1)
            return null;

        if (string.IsNullOrEmpty(scenario.description))
            scenario.description = "Practice Chinese in this roleplay scenario.";

        return scenario;
    }

    /// <summary>Try to parse a single word line like "猫|māo|cat".</summary>
    private void TryParseWord(string line, RoleplayScenario scenario)
    {
        // Strip leading list markers
        line = System.Text.RegularExpressions.Regex.Replace(line, @"^\d+[\.\)]\s*", "");
        line = System.Text.RegularExpressions.Regex.Replace(line, @"^[-\*]\s+", "");

        string[] parts = line.Split('|');
        if (parts.Length < 3) return;

        string character = parts[0].Trim().Trim('*', ' ');
        string pinyin = parts[1].Trim().Trim('*', ' ');
        string definition = parts[2].Trim().Trim('*', ' ');

        // Strip any extra parts (some LLMs add a 4th column)
        if (character.Length < 1 || character.Length > 4) return;
        if (string.IsNullOrEmpty(pinyin)) return;
        if (!ContainsCJK(character)) return;

        string typeable = WordList.StripTonesPublic(pinyin);
        if (typeable.Length < 1) return; // be lenient — allow single-letter pinyin

        RoleplayScenario.RoleplayWord word = new RoleplayScenario.RoleplayWord();
        word.character = character;
        word.pinyinDisplay = pinyin;
        word.pinyinTypeable = typeable;
        word.definition = definition;
        scenario.words.Add(word);
    }

    /// <summary>Check if a string contains any CJK Unified Ideograph characters.</summary>
    private bool ContainsCJK(string s)
    {
        foreach (char c in s)
        {
            if (c >= 0x4E00 && c <= 0x9FFF) return true;  // CJK Unified
            if (c >= 0x3400 && c <= 0x4DBF) return true;  // CJK Extension A
        }
        return false;
    }

    // ── Chat support ─────────────────────────────────────────────────

    /// <summary>
    /// Send a chat message in the context of the active scenario.
    /// Returns the LLM's response via callback.
    /// </summary>
    public void SendChatMessage(RoleplayScenario scenario,
        List<ChatMessage> history, string userMessage,
        System.Action<string> onResponse)
    {
        StartCoroutine(ChatCoroutine(scenario, history, userMessage, onResponse));
    }

    [System.Serializable]
    public class ChatMessage
    {
        public string role; // "user" or "assistant"
        public string content;
    }

    private System.Collections.IEnumerator ChatCoroutine(
        RoleplayScenario scenario, List<ChatMessage> history,
        string userMessage, System.Action<string> onResponse)
    {
        if (LlamaServerManager.Instance == null || !LlamaServerManager.Instance.IsReady)
        {
            onResponse?.Invoke("[LLM server not ready]");
            yield break;
        }

        string url = LlamaServerManager.Instance.BaseUrl + "/v1/chat/completions";

        // Build the word list context, distinguishing learned vs not-yet-learned
        System.Text.StringBuilder wordContext = new System.Text.StringBuilder();
        foreach (var w in scenario.words)
        {
            bool learned = LearnedWordsManager.Instance != null &&
                           LearnedWordsManager.Instance.IsLearned(w.character);
            if (learned)
                wordContext.AppendLine($"  {w.character} (learned)");
            else
                wordContext.AppendLine($"  {w.character} ({w.pinyinDisplay}) = {w.definition} [NOT YET LEARNED]");
        }

        string userRoleStr = !string.IsNullOrEmpty(scenario.userRole) ? scenario.userRole : "learner";
        string llmRoleStr = !string.IsNullOrEmpty(scenario.llmRole) ? scenario.llmRole : "tutor";

        string systemPrompt = $@"You are roleplaying as a {llmRoleStr} in a Chinese language practice conversation.
The student is playing the role of a {userRoleStr}.

Scenario: {scenario.title}
{scenario.description}

Vocabulary for this scenario:
{wordContext}

Rules:
- Stay in character as the {llmRoleStr} at all times
- Type ONLY in Chinese characters. Do NOT use English at all.
- For words marked [NOT YET LEARNED], include pinyin and English translation in parentheses after the word, e.g. 菜单(càidān, menu)
- For words marked (learned), use them naturally without any translation help
- Keep responses short and natural (1-3 sentences)
- Naturally steer the conversation to use all the vocabulary words from the list
- When you have naturally used all vocabulary words in the conversation, wrap up the roleplay with a natural farewell
- After your farewell message, add on a new line: [CONVERSATION COMPLETE]
- Correct mistakes gently by rephrasing correctly in your response";

        // Build messages array JSON
        System.Text.StringBuilder msgs = new System.Text.StringBuilder();
        msgs.Append($"{{\"role\":\"system\",\"content\":\"{EscapeJson(systemPrompt)}\"}}");

        foreach (var msg in history)
        {
            msgs.Append($",{{\"role\":\"{msg.role}\",\"content\":\"{EscapeJson(msg.content)}\"}}");
        }
        msgs.Append($",{{\"role\":\"user\",\"content\":\"{EscapeJson(userMessage)}\"}}");

        string requestBody = $@"{{
  ""model"": ""local"",
  ""messages"": [{msgs}],
  ""max_tokens"": 256,
  ""temperature"": 0.8,
  ""stream"": false,
  ""stop"": [""<|im_end|>"", ""<|im_start|>""]
}}";

        using (var req = new UnityEngine.Networking.UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(requestBody);
            req.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 120;

            yield return req.SendWebRequest();

            if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[RoleplayManager] Chat request failed: {req.error}");
                onResponse?.Invoke("[Error: could not reach LLM server]");
                yield break;
            }

            string rawJson = req.downloadHandler.text;
            Debug.Log($"[RoleplayManager] Chat raw response: {rawJson.Substring(0, Mathf.Min(rawJson.Length, 500))}");
            string content = ExtractContent(rawJson);
            Debug.Log($"[RoleplayManager] Chat extracted content: {content}");
            onResponse?.Invoke(content ?? "[Empty response]");
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private string BuildChatRequestJson(string systemMsg, string userMsg, int maxTokens)
    {
        return $@"{{
  ""model"": ""local"",
  ""messages"": [
    {{""role"": ""system"", ""content"": ""{EscapeJson(systemMsg)}""}},
    {{""role"": ""user"", ""content"": ""{EscapeJson(userMsg)}""}}
  ],
  ""max_tokens"": {maxTokens},
  ""temperature"": 0.7,
  ""stream"": false,
  ""stop"": [""<|im_end|>"", ""<|im_start|>""]
}}";
    }

    private string EscapeJson(string s)
    {
        return s.Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
    }

    /// <summary>Extract "content" from OpenAI-format JSON response.</summary>
    private string ExtractContent(string json)
    {
        string marker = "\"content\":";
        int idx = json.LastIndexOf(marker);
        if (idx < 0) return null;

        idx += marker.Length;
        while (idx < json.Length && (json[idx] == ' ' || json[idx] == '\t'))
            idx++;

        if (idx >= json.Length || json[idx] != '"') return null;
        idx++;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        while (idx < json.Length)
        {
            char c = json[idx];
            if (c == '"') break;
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

        return CleanChatResponse(sb.ToString());
    }

    /// <summary>Strip chatml tokens and fake continuation turns from LLM output.</summary>
    private string CleanChatResponse(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw;

        // Truncate at the first chatml token — the model sometimes generates these
        string[] stopTokens = { "<|im_end|>", "<|im_start|>", "<|endoftext|>" };
        foreach (string token in stopTokens)
        {
            int idx = raw.IndexOf(token);
            if (idx >= 0)
                raw = raw.Substring(0, idx);
        }

        return raw.Trim();
    }
}
