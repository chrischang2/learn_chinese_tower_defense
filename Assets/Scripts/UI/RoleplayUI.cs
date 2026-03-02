using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Full-screen UI for the Roleplay Scenarios feature.
/// Shows scenario list, generate button, scenario detail popup,
/// and handles navigation back to the main menu.
/// 
/// Built entirely in code (no prefabs), matching HUDManager style.
/// Attach to the HUD Canvas.
/// </summary>
public class RoleplayUI : MonoBehaviour
{
    // ── Panel references ─────────────────────────────────────────────
    private GameObject listPanel;       // scenario list screen
    private GameObject detailPopup;     // scenario detail popup
    private GameObject generatingPopup; // "generating…" overlay
    private Transform canvasTransform;

    // ── State ────────────────────────────────────────────────────────
    private RoleplayScenario selectedScenario;

    private Transform EnsureCanvas()
    {
        if (canvasTransform == null)
        {
            Canvas c = GetComponentInParent<Canvas>();
            if (c != null) canvasTransform = c.transform;
            else canvasTransform = transform; // fallback
        }
        return canvasTransform;
    }

    // ══════════════════════════════════════════════════════════════════
    //  PUBLIC — called by HUDManager
    // ══════════════════════════════════════════════════════════════════

    /// <summary>Show the roleplay scenarios screen.</summary>
    public void ShowScenarioList()
    {
        EnsureCanvas();
        CloseAll();
        BuildListPanel();
    }

    /// <summary>Close everything and return to main menu.</summary>
    public void Close()
    {
        CloseAll();
    }

    // ══════════════════════════════════════════════════════════════════
    //  SCENARIO LIST SCREEN
    // ══════════════════════════════════════════════════════════════════

    private void BuildListPanel()
    {
        if (listPanel != null) Destroy(listPanel);

        listPanel = CreateFullscreenPanel("RoleplayListPanel",
            new Color(0.06f, 0.06f, 0.1f, 0.97f));

        // Title
        Text title = CreatePanelText(listPanel.transform,
            "ROLEPLAY SCENARIOS", 48, new Vector2(0, 340));
        title.fontStyle = FontStyle.Bold;

        // Subtitle — server status
        bool serverReady = LLMWordGenerator.Instance != null &&
                           LLMWordGenerator.Instance.IsServerReady();
        bool serverLaunched = LlamaServerManager.Instance != null &&
                              LlamaServerManager.Instance.IsLaunched;
        string statusStr;
        Color statusColor;
        if (serverReady)
        {
            statusStr = "LLM Server: Ready";
            statusColor = new Color(0.4f, 1f, 0.4f);
        }
        else if (serverLaunched)
        {
            statusStr = "LLM Server: Loading model... please wait";
            statusColor = new Color(1f, 0.85f, 0.3f);
            // Auto-refresh when server becomes ready
            StartCoroutine(RefreshWhenReady());
        }
        else
        {
            statusStr = "LLM Server: Not running";
            statusColor = new Color(1f, 0.5f, 0.3f);
        }
        Text status = CreatePanelText(listPanel.transform, statusStr, 20, new Vector2(0, 290));
        status.color = statusColor;

        // ── Input fields ─────────────────────────────────────────────
        InputField scenarioField = CreateLabeledInput(listPanel.transform,
            "Scenario:", "e.g. ordering food at a restaurant, shopping at a market...",
            new Vector2(0, 258), 500, 35);

        InputField userRoleField = CreateLabeledInput(listPanel.transform,
            "Your role:", "e.g. tourist, customer, patient...",
            new Vector2(0, 216), 500, 35);

        InputField llmRoleField = CreateLabeledInput(listPanel.transform,
            "LLM role:", "e.g. waiter, shopkeeper, doctor...",
            new Vector2(0, 174), 500, 35);

        // Generate button
        bool canGenerate = serverReady &&
            (RoleplayManager.Instance == null || !RoleplayManager.Instance.IsGenerating);
        string genLabel = canGenerate ? "GENERATE NEW SCENARIO" : "GENERATING...";
        CreateMenuButton(listPanel.transform, genLabel, new Vector2(0, 130), () =>
        {
            if (RoleplayManager.Instance != null && canGenerate)
            {
                string scenario = scenarioField != null ? scenarioField.text.Trim() : "";
                string userRole = userRoleField != null ? userRoleField.text.Trim() : "";
                string llmRole = llmRoleField != null ? llmRoleField.text.Trim() : "";
                if (string.IsNullOrEmpty(scenario))
                {
                    Debug.LogWarning("[RoleplayUI] Please enter a scenario.");
                    return;
                }
                ShowGeneratingOverlay();
                RoleplayManager.Instance.GenerateScenario(scenario, userRole, llmRole, result =>
                {
                    HideGeneratingOverlay();
                    if (result != null)
                        BuildListPanel(); // refresh
                    else
                        Debug.LogWarning("[RoleplayUI] Scenario generation failed.");
                });
            }
        }, 400, 60);

        // Grey out if can't generate
        if (!canGenerate)
        {
            Transform btn = listPanel.transform.Find("Btn_" + genLabel);
            if (btn != null)
            {
                Image img = btn.GetComponent<Image>();
                if (img != null) img.color = new Color(0.15f, 0.15f, 0.15f, 0.6f);
            }
        }

        // ── Scrollable scenario list ─────────────────────────────────
        List<RoleplayScenario> scenarios = RoleplayManager.Instance != null
            ? RoleplayManager.Instance.Scenarios
            : new List<RoleplayScenario>();

        Font cjkFont = Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 48);
        if (cjkFont == null)
            cjkFont = Font.CreateDynamicFontFromOSFont("SimHei", 48);

        // Scroll view container (between generate button and back button)
        GameObject scrollGO = new GameObject("ScenarioScroll");
        scrollGO.transform.SetParent(listPanel.transform, false);
        RectTransform scrollObjRect = scrollGO.AddComponent<RectTransform>();
        scrollObjRect.anchorMin = new Vector2(0.5f, 0.5f);
        scrollObjRect.anchorMax = new Vector2(0.5f, 0.5f);
        scrollObjRect.pivot = new Vector2(0.5f, 0.5f);
        scrollObjRect.anchoredPosition = new Vector2(0, -80f);
        scrollObjRect.sizeDelta = new Vector2(720, 340);

        Image scrollBg = scrollGO.AddComponent<Image>();
        scrollBg.color = new Color(0.04f, 0.04f, 0.07f, 0.8f);
        scrollGO.AddComponent<Mask>().showMaskGraphic = true;

        ScrollRect scrollView = scrollGO.AddComponent<ScrollRect>();
        scrollView.horizontal = false;
        scrollView.movementType = ScrollRect.MovementType.Clamped;
        scrollView.scrollSensitivity = 30f;

        // Content container inside scroll
        GameObject contentGO = new GameObject("Content");
        contentGO.transform.SetParent(scrollGO.transform, false);
        RectTransform contentRect = contentGO.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.anchoredPosition = Vector2.zero;

        float cardHeight = 70f;
        float cardGap = 8f;
        float totalHeight = scenarios.Count * (cardHeight + cardGap) + 10f;
        contentRect.sizeDelta = new Vector2(0, Mathf.Max(totalHeight, 10f));

        scrollView.content = contentRect;

        for (int i = 0; i < scenarios.Count; i++)
        {
            float y = -5f - i * (cardHeight + cardGap);
            RoleplayScenario sc = scenarios[i];

            // Card background
            GameObject card = new GameObject("ScenarioCard_" + i);
            card.transform.SetParent(contentGO.transform, false);
            RectTransform cardRect = card.AddComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0, 1);
            cardRect.anchorMax = new Vector2(1, 1);
            cardRect.pivot = new Vector2(0.5f, 1);
            cardRect.anchoredPosition = new Vector2(0, y);
            cardRect.sizeDelta = new Vector2(-20, cardHeight);

            Image cardBg = card.AddComponent<Image>();
            cardBg.color = sc.wordsLearned
                ? new Color(0.1f, 0.25f, 0.1f, 0.9f)
                : new Color(0.15f, 0.15f, 0.2f, 0.9f);

            // Make clickable
            Button cardBtn = card.AddComponent<Button>();
            ColorBlock cb = cardBtn.colors;
            cb.highlightedColor = new Color(0.25f, 0.25f, 0.35f, 1f);
            cb.pressedColor = new Color(0.1f, 0.1f, 0.15f, 1f);
            cardBtn.colors = cb;
            cardBtn.targetGraphic = cardBg;

            RoleplayScenario captured = sc;
            cardBtn.onClick.AddListener(() => ShowDetailPopup(captured));

            // Title text
            Text titleText = CreatePanelText(card.transform,
                sc.title, 22, new Vector2(-50, 8));
            titleText.alignment = TextAnchor.MiddleLeft;
            titleText.raycastTarget = false;
            RectTransform ttRect = titleText.GetComponent<RectTransform>();
            ttRect.anchorMin = new Vector2(0, 0);
            ttRect.anchorMax = new Vector2(1, 1);
            ttRect.offsetMin = new Vector2(15, 25);
            ttRect.offsetMax = new Vector2(-15, 0);

            // Subtitle: word count + date
            string sub = $"{sc.words.Count} words | {sc.createdAt}";
            if (sc.wordsLearned) sub += " | \u2713 Words Learned";
            Text subText = CreatePanelText(card.transform, sub, 16, new Vector2(-50, -15));
            subText.alignment = TextAnchor.MiddleLeft;
            subText.color = new Color(0.6f, 0.6f, 0.6f);
            subText.raycastTarget = false;
            RectTransform stRect = subText.GetComponent<RectTransform>();
            stRect.anchorMin = new Vector2(0, 0);
            stRect.anchorMax = new Vector2(1, 0.5f);
            stRect.offsetMin = new Vector2(15, 5);
            stRect.offsetMax = new Vector2(-15, -2);
        }

        if (scenarios.Count == 0)
        {
            Text empty = CreatePanelText(scrollGO.transform,
                "No scenarios yet.\nGenerate one to get started!", 24, new Vector2(0, 0));
            empty.color = new Color(0.5f, 0.5f, 0.5f);
        }

        // Back button (below the scroll area)
        CreateMenuButton(listPanel.transform, "BACK TO MENU", new Vector2(0, -290), () =>
        {
            CloseAll();
            // Tell HUDManager to show main menu
            if (GameManager.Instance != null)
                GameManager.Instance.ReturnToMainMenu();
        }, 300, 60);
    }

    // ══════════════════════════════════════════════════════════════════
    //  SCENARIO DETAIL POPUP
    // ══════════════════════════════════════════════════════════════════

    private void ShowDetailPopup(RoleplayScenario scenario)
    {
        selectedScenario = scenario;

        if (detailPopup != null) Destroy(detailPopup);

        detailPopup = CreateFullscreenPanel("ScenarioDetailPopup",
            new Color(0f, 0f, 0f, 0.8f));

        // Inner card
        GameObject inner = new GameObject("InnerCard");
        inner.transform.SetParent(detailPopup.transform, false);
        RectTransform innerRect = inner.AddComponent<RectTransform>();
        innerRect.anchorMin = new Vector2(0.5f, 0.5f);
        innerRect.anchorMax = new Vector2(0.5f, 0.5f);
        innerRect.pivot = new Vector2(0.5f, 0.5f);
        innerRect.anchoredPosition = Vector2.zero;
        innerRect.sizeDelta = new Vector2(600, 500);

        Image innerBg = inner.AddComponent<Image>();
        innerBg.color = new Color(0.1f, 0.1f, 0.15f, 1f);

        // Title
        Text title = CreatePanelText(inner.transform,
            scenario.title, 32, new Vector2(0, 190));
        title.fontStyle = FontStyle.Bold;

        // Description
        Text desc = CreatePanelText(inner.transform,
            scenario.description, 20, new Vector2(0, 130));
        desc.color = new Color(0.8f, 0.8f, 0.8f);
        RectTransform descRect = desc.GetComponent<RectTransform>();
        descRect.sizeDelta = new Vector2(540, 80);
        desc.horizontalOverflow = HorizontalWrapMode.Wrap;
        desc.verticalOverflow = VerticalWrapMode.Overflow;

        // Roles
        string rolesStr = "";
        if (!string.IsNullOrEmpty(scenario.userRole))
            rolesStr += $"You: {scenario.userRole}";
        if (!string.IsNullOrEmpty(scenario.llmRole))
            rolesStr += (rolesStr.Length > 0 ? "   |   " : "") + $"Partner: {scenario.llmRole}";
        if (!string.IsNullOrEmpty(rolesStr))
        {
            Text roles = CreatePanelText(inner.transform, rolesStr, 18, new Vector2(0, 72));
            roles.color = new Color(0.6f, 0.7f, 0.9f);
        }

        // Word count summary
        Text wordCount = CreatePanelText(inner.transform,
            $"{scenario.words.Count} words to learn", 20, new Vector2(0, 45));
        wordCount.color = new Color(0.6f, 0.8f, 0.6f);

        // ── Buttons ──────────────────────────────────────────────────
        float btnY = -20f;

        // View All Words button
        CreateMenuButton(inner.transform, "VIEW ALL WORDS", new Vector2(0, btnY), () =>
        {
            ShowWordListPopup(scenario);
        }, 360, 55);
        btnY -= 70f;

        // Learn Words button
        string learnLabel = scenario.wordsLearned ? "PRACTICE WORDS AGAIN" : "LEARN WORDS";
        CreateMenuButton(inner.transform, learnLabel, new Vector2(0, btnY), () =>
        {
            StartWordDrill(scenario);
        }, 360, 55);
        btnY -= 70f;

        // Conversation button
        CreateMenuButton(inner.transform, "CONVERSATION", new Vector2(0, btnY), () =>
        {
            StartConversation(scenario);
        }, 360, 55);
        btnY -= 70f;

        // Delete button (smaller, red)
        CreateMenuButton(inner.transform, "DELETE", new Vector2(210, 190), () =>
        {
            if (RoleplayManager.Instance != null)
            {
                RoleplayManager.Instance.DeleteScenario(scenario);
                Destroy(detailPopup);
                detailPopup = null;
                BuildListPanel(); // refresh
            }
        }, 100, 35);
        Transform delBtn = inner.transform.Find("Btn_DELETE");
        if (delBtn != null)
        {
            Image img = delBtn.GetComponent<Image>();
            if (img != null) img.color = new Color(0.4f, 0.1f, 0.1f, 1f);
        }

        // Close popup button
        CreateMenuButton(inner.transform, "CLOSE", new Vector2(0, btnY), () =>
        {
            Destroy(detailPopup);
            detailPopup = null;
        }, 200, 50);
    }

    // ════════════════════════════════════════════════════════════════
    //  WORD LIST POPUP
    // ════════════════════════════════════════════════════════════════

    private GameObject wordListPopup;

    private void ShowWordListPopup(RoleplayScenario scenario)
    {
        if (wordListPopup != null) Destroy(wordListPopup);

        wordListPopup = CreateFullscreenPanel("WordListPopup",
            new Color(0f, 0f, 0f, 0.9f));

        // Inner card
        GameObject inner = new GameObject("WordListInner");
        inner.transform.SetParent(wordListPopup.transform, false);
        RectTransform innerRect = inner.AddComponent<RectTransform>();
        innerRect.anchorMin = new Vector2(0.5f, 0.5f);
        innerRect.anchorMax = new Vector2(0.5f, 0.5f);
        innerRect.pivot = new Vector2(0.5f, 0.5f);
        innerRect.anchoredPosition = Vector2.zero;
        innerRect.sizeDelta = new Vector2(620, 550);

        Image innerBg = inner.AddComponent<Image>();
        innerBg.color = new Color(0.08f, 0.08f, 0.12f, 1f);

        // Title
        Text title = CreatePanelText(inner.transform,
            scenario.title + " — Word List", 28, new Vector2(0, 240));
        title.fontStyle = FontStyle.Bold;

        // CJK font
        Font cjkFont = Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 48);
        if (cjkFont == null) cjkFont = Font.CreateDynamicFontFromOSFont("SimHei", 48);

        // Scroll view
        GameObject scrollGO = new GameObject("Scroll");
        scrollGO.transform.SetParent(inner.transform, false);
        RectTransform scrollRect = scrollGO.AddComponent<RectTransform>();
        scrollRect.anchorMin = new Vector2(0.5f, 0.5f);
        scrollRect.anchorMax = new Vector2(0.5f, 0.5f);
        scrollRect.pivot = new Vector2(0.5f, 0.5f);
        scrollRect.anchoredPosition = new Vector2(0, -10);
        scrollRect.sizeDelta = new Vector2(580, 400);

        ScrollRect scroll = scrollGO.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        Image scrollBg = scrollGO.AddComponent<Image>();
        scrollBg.color = new Color(0.06f, 0.06f, 0.09f, 1f);

        // Mask
        scrollGO.AddComponent<Mask>().showMaskGraphic = true;

        // Content
        GameObject contentGO = new GameObject("Content");
        contentGO.transform.SetParent(scrollGO.transform, false);
        RectTransform contentRect = contentGO.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.anchoredPosition = Vector2.zero;

        float lineHeight = 32f;
        float totalHeight = scenario.words.Count * lineHeight + 20f;
        contentRect.sizeDelta = new Vector2(0, totalHeight);

        scroll.content = contentRect;

        // Add each word
        for (int i = 0; i < scenario.words.Count; i++)
        {
            var w = scenario.words[i];
            float y = -10f - i * lineHeight;
            string wordLine = $"{i + 1}.  {w.character}    ({w.pinyinDisplay})    —  {w.definition}";

            GameObject lineGO = new GameObject("Word_" + i);
            lineGO.transform.SetParent(contentGO.transform, false);
            RectTransform lineRect = lineGO.AddComponent<RectTransform>();
            lineRect.anchorMin = new Vector2(0, 1);
            lineRect.anchorMax = new Vector2(1, 1);
            lineRect.pivot = new Vector2(0, 1);
            lineRect.anchoredPosition = new Vector2(20, y);
            lineRect.sizeDelta = new Vector2(-40, lineHeight);

            Text wt = lineGO.AddComponent<Text>();
            wt.text = wordLine;
            wt.font = cjkFont != null ? cjkFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            wt.fontSize = 20;
            wt.color = new Color(0.75f, 0.9f, 0.75f);
            wt.alignment = TextAnchor.MiddleLeft;
            wt.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        // Close button
        CreateMenuButton(inner.transform, "CLOSE", new Vector2(0, -245), () =>
        {
            Destroy(wordListPopup);
            wordListPopup = null;
        }, 200, 45);
    }

    // ══════════════════════════════════════════════════════════════════
    //  ACTIONS
    // ══════════════════════════════════════════════════════════════════

    private void StartWordDrill(RoleplayScenario scenario)
    {
        if (RoleplayManager.Instance != null)
            RoleplayManager.Instance.ActiveScenario = scenario;

        CloseAll();

        // Start the game in roleplay word-drill mode
        if (GameManager.Instance != null)
            GameManager.Instance.StartRoleplayDrill(scenario);
    }

    private void StartConversation(RoleplayScenario scenario)
    {
        if (RoleplayManager.Instance != null)
            RoleplayManager.Instance.ActiveScenario = scenario;

        CloseAll();

        // Show the chat UI
        ChatUI chatUI = canvasTransform.GetComponentInChildren<ChatUI>(true);
        if (chatUI == null)
        {
            GameObject chatObj = new GameObject("ChatUI");
            chatObj.transform.SetParent(canvasTransform, false);
            chatUI = chatObj.AddComponent<ChatUI>();
        }
        chatUI.OpenChat(scenario);
    }

    // ══════════════════════════════════════════════════════════════════
    //  GENERATING OVERLAY
    // ══════════════════════════════════════════════════════════════════

    private void ShowGeneratingOverlay()
    {
        if (generatingPopup != null) return;

        generatingPopup = CreateFullscreenPanel("GeneratingPopup",
            new Color(0f, 0f, 0f, 0.7f));

        Text t = CreatePanelText(generatingPopup.transform,
            "Generating scenario...\n\nThis may take a minute on CPU.", 28, Vector2.zero);
        t.color = new Color(0.8f, 0.8f, 1f);

        // Animated dots via coroutine
        StartCoroutine(AnimateDots(t));
    }

    private void HideGeneratingOverlay()
    {
        if (generatingPopup != null)
        {
            Destroy(generatingPopup);
            generatingPopup = null;
        }
    }

    private System.Collections.IEnumerator AnimateDots(Text text)
    {
        string baseText = "Generating scenario";
        int dots = 0;
        while (generatingPopup != null && text != null)
        {
            dots = (dots + 1) % 4;
            text.text = baseText + new string('.', dots) +
                "\n\nThis may take a minute on CPU.";
            yield return new WaitForSeconds(0.5f);
        }
    }

    /// <summary>Auto-refresh the scenario list once the LLM server becomes ready.</summary>
    private System.Collections.IEnumerator RefreshWhenReady()
    {
        float timeout = 120f;
        float elapsed = 0f;
        while (elapsed < timeout)
        {
            yield return new WaitForSeconds(3f);
            elapsed += 3f;

            if (LLMWordGenerator.Instance != null && LLMWordGenerator.Instance.IsServerReady())
            {
                // Only refresh if we're still showing the list panel
                if (listPanel != null)
                    BuildListPanel();
                yield break;
            }

            // If user navigated away, stop polling
            if (listPanel == null)
                yield break;
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  UI HELPERS (matching HUDManager style)
    // ══════════════════════════════════════════════════════════════════

    private void CloseAll()
    {
        if (listPanel != null) { Destroy(listPanel); listPanel = null; }
        if (detailPopup != null) { Destroy(detailPopup); detailPopup = null; }
        if (wordListPopup != null) { Destroy(wordListPopup); wordListPopup = null; }
        HideGeneratingOverlay();
    }

    private GameObject CreateFullscreenPanel(string name, Color bgColor)
    {
        EnsureCanvas();

        GameObject panel = new GameObject(name);
        panel.transform.SetParent(canvasTransform, false);

        RectTransform rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image bg = panel.AddComponent<Image>();
        bg.color = bgColor;

        return panel;
    }

    private void CreateMenuButton(Transform parent, string label, Vector2 pos,
        UnityEngine.Events.UnityAction onClick, float width = 320f, float height = 60f)
    {
        GameObject btnObj = new GameObject("Btn_" + label);
        btnObj.transform.SetParent(parent, false);

        RectTransform btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 0.5f);
        btnRect.anchorMax = new Vector2(0.5f, 0.5f);
        btnRect.pivot = new Vector2(0.5f, 0.5f);
        btnRect.anchoredPosition = pos;
        btnRect.sizeDelta = new Vector2(width, height);

        Image btnBg = btnObj.AddComponent<Image>();
        btnBg.color = new Color(0.2f, 0.2f, 0.2f, 1f);

        Button btn = btnObj.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.highlightedColor = new Color(0.4f, 0.4f, 0.4f, 1f);
        cb.pressedColor = new Color(0.15f, 0.15f, 0.15f, 1f);
        btn.colors = cb;
        btn.onClick.AddListener(onClick);

        CreatePanelText(btnObj.transform, label, 22, Vector2.zero);
    }

    private Text CreatePanelText(Transform parent, string content,
        int fontSize, Vector2 offset)
    {
        GameObject obj = new GameObject("Text_" + content);
        obj.transform.SetParent(parent, false);

        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = offset;
        rt.sizeDelta = new Vector2(800, fontSize + 20);

        Text txt = obj.AddComponent<Text>();
        txt.text = content;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = fontSize;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.raycastTarget = false;

        return txt;
    }

    /// <summary>Creates a labeled input field: "Label: [___________]" in a single row.</summary>
    private InputField CreateLabeledInput(Transform parent, string label, string placeholder,
        Vector2 position, float totalWidth, float height)
    {
        float labelWidth = 100f;
        float fieldWidth = totalWidth - labelWidth - 10f;

        // Label
        Text lbl = CreatePanelText(parent, label, 18, new Vector2(
            position.x - (totalWidth / 2f) + (labelWidth / 2f), position.y));
        lbl.alignment = TextAnchor.MiddleRight;
        lbl.color = new Color(0.75f, 0.75f, 0.85f);
        RectTransform lblRect = lbl.GetComponent<RectTransform>();
        lblRect.sizeDelta = new Vector2(labelWidth, height);

        // Input field container
        float fieldX = position.x - (totalWidth / 2f) + labelWidth + 10f + (fieldWidth / 2f);

        GameObject inputGO = new GameObject("Input_" + label);
        inputGO.transform.SetParent(parent, false);
        RectTransform inputRect = inputGO.AddComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0.5f, 0.5f);
        inputRect.anchorMax = new Vector2(0.5f, 0.5f);
        inputRect.pivot = new Vector2(0.5f, 0.5f);
        inputRect.anchoredPosition = new Vector2(fieldX, position.y);
        inputRect.sizeDelta = new Vector2(fieldWidth, height);

        Image inputBg = inputGO.AddComponent<Image>();
        inputBg.color = new Color(0.12f, 0.12f, 0.18f, 1f);

        // Placeholder
        GameObject phGO = new GameObject("Placeholder");
        phGO.transform.SetParent(inputGO.transform, false);
        RectTransform phRect = phGO.AddComponent<RectTransform>();
        phRect.anchorMin = Vector2.zero;
        phRect.anchorMax = Vector2.one;
        phRect.offsetMin = new Vector2(8, 2);
        phRect.offsetMax = new Vector2(-8, -2);
        Text phText = phGO.AddComponent<Text>();
        phText.text = placeholder;
        phText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        phText.fontSize = 16;
        phText.color = new Color(0.4f, 0.4f, 0.5f);
        phText.alignment = TextAnchor.MiddleLeft;
        phText.fontStyle = FontStyle.Italic;

        // Input text
        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(inputGO.transform, false);
        RectTransform textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8, 2);
        textRect.offsetMax = new Vector2(-8, -2);
        Text inputText = textGO.AddComponent<Text>();
        inputText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        inputText.fontSize = 16;
        inputText.color = Color.white;
        inputText.alignment = TextAnchor.MiddleLeft;
        inputText.supportRichText = false;

        InputField field = inputGO.AddComponent<InputField>();
        field.textComponent = inputText;
        field.placeholder = phText;
        field.characterLimit = 100;

        return field;
    }
}
