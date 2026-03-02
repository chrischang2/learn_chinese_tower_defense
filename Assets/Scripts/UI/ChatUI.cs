using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// Chat interface for roleplay conversations with the LLM.
/// Full-screen panel with scrollable message history, text input field,
/// and send button. Built programmatically.
/// </summary>
public class ChatUI : MonoBehaviour
{
    // ── Panel References ─────────────────────────────────────────────
    private GameObject chatPanel;
    private ScrollRect scrollRect;
    private RectTransform contentRect;
    private InputField inputField;
    private Button sendButton;
    private Text sendButtonText;
    private Text scenarioTitle;
    private Text statusText;

    // ── State ────────────────────────────────────────────────────────
    private RoleplayScenario activeScenario;
    private List<RoleplayManager.ChatMessage> chatHistory = new List<RoleplayManager.ChatMessage>();
    private bool isWaitingForResponse;

    // ── Layout Constants ─────────────────────────────────────────────
    private Font cjkFont;

    // ══════════════════════════════════════════════════════════════════
    //  PUBLIC API
    // ══════════════════════════════════════════════════════════════════

    /// <summary>Open the chat for the given scenario.</summary>
    public void OpenChat(RoleplayScenario scenario)
    {
        activeScenario = scenario;
        chatHistory.Clear();
        isWaitingForResponse = false;

        cjkFont = Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 48);
        if (cjkFont == null)
            cjkFont = Font.CreateDynamicFontFromOSFont("SimHei", 48);

        BuildChatPanel();

        // Add an opening system message describing the scenario
        string userRoleStr = !string.IsNullOrEmpty(scenario.userRole) ? scenario.userRole : "learner";
        string llmRoleStr = !string.IsNullOrEmpty(scenario.llmRole) ? scenario.llmRole : "tutor";

        AddMessageBubble("assistant",
            $"Scenario: {scenario.title}\n{scenario.description}\n" +
            $"You are: {userRoleStr}  |  Partner: {llmRoleStr}\n\n" +
            $"Type in Chinese to practice! The conversation will end naturally " +
            $"once all vocabulary words have been used.");

        // Send an initial prompt to the LLM to kick off the roleplay
        SendToLLM($"请开始角色扮演。你是{llmRoleStr}，我是{userRoleStr}。请用中文打招呼并开始场景。");
    }

    /// <summary>Close the chat and return to roleplay list.</summary>
    public void CloseChat()
    {
        if (chatPanel != null)
        {
            Destroy(chatPanel);
            chatPanel = null;
        }

        // Return to roleplay list
        RoleplayUI rpUI = GetComponentInParent<Canvas>()
            .GetComponentInChildren<RoleplayUI>(true);
        if (rpUI != null)
            rpUI.ShowScenarioList();
    }

    // ══════════════════════════════════════════════════════════════════
    //  BUILD UI
    // ══════════════════════════════════════════════════════════════════

    private void BuildChatPanel()
    {
        if (chatPanel != null) Destroy(chatPanel);

        Transform canvasTransform = GetComponentInParent<Canvas>().transform;

        chatPanel = new GameObject("ChatPanel");
        chatPanel.transform.SetParent(canvasTransform, false);
        RectTransform panelRect = chatPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image panelBg = chatPanel.AddComponent<Image>();
        panelBg.color = new Color(0.05f, 0.05f, 0.08f, 0.98f);

        // ── Top bar ──────────────────────────────────────────────────
        GameObject topBar = new GameObject("TopBar");
        topBar.transform.SetParent(chatPanel.transform, false);
        RectTransform topRect = topBar.AddComponent<RectTransform>();
        topRect.anchorMin = new Vector2(0, 1);
        topRect.anchorMax = new Vector2(1, 1);
        topRect.pivot = new Vector2(0.5f, 1);
        topRect.anchoredPosition = Vector2.zero;
        topRect.sizeDelta = new Vector2(0, 70);

        Image topBg = topBar.AddComponent<Image>();
        topBg.color = new Color(0.1f, 0.1f, 0.15f, 1f);

        // Title
        scenarioTitle = CreateText(topBar.transform,
            activeScenario.title, 24, TextAnchor.MiddleCenter);
        RectTransform stRect = scenarioTitle.GetComponent<RectTransform>();
        stRect.anchorMin = new Vector2(0.15f, 0);
        stRect.anchorMax = new Vector2(0.85f, 1);
        stRect.offsetMin = Vector2.zero;
        stRect.offsetMax = Vector2.zero;
        scenarioTitle.fontStyle = FontStyle.Bold;

        // Back button
        CreateButton(topBar.transform, "< BACK", new Vector2(0, 0), () => CloseChat(),
            100, 40, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(60, 0));

        // ── Bottom bar: input field + send ───────────────────────────
        GameObject bottomBar = new GameObject("BottomBar");
        bottomBar.transform.SetParent(chatPanel.transform, false);
        RectTransform botRect = bottomBar.AddComponent<RectTransform>();
        botRect.anchorMin = new Vector2(0, 0);
        botRect.anchorMax = new Vector2(1, 0);
        botRect.pivot = new Vector2(0.5f, 0);
        botRect.anchoredPosition = Vector2.zero;
        botRect.sizeDelta = new Vector2(0, 70);

        Image botBg = bottomBar.AddComponent<Image>();
        botBg.color = new Color(0.1f, 0.1f, 0.15f, 1f);

        // Input field
        GameObject inputObj = new GameObject("InputField");
        inputObj.transform.SetParent(bottomBar.transform, false);
        RectTransform inputRect = inputObj.AddComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0, 0);
        inputRect.anchorMax = new Vector2(1, 1);
        inputRect.offsetMin = new Vector2(15, 10);
        inputRect.offsetMax = new Vector2(-115, -10);

        Image inputBg = inputObj.AddComponent<Image>();
        inputBg.color = new Color(0.18f, 0.18f, 0.22f, 1f);

        inputField = inputObj.AddComponent<InputField>();

        // Input text
        GameObject inputTextObj = new GameObject("InputText");
        inputTextObj.transform.SetParent(inputObj.transform, false);
        RectTransform itRect = inputTextObj.AddComponent<RectTransform>();
        itRect.anchorMin = Vector2.zero;
        itRect.anchorMax = Vector2.one;
        itRect.offsetMin = new Vector2(10, 2);
        itRect.offsetMax = new Vector2(-10, -2);

        Text inputText = inputTextObj.AddComponent<Text>();
        inputText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        inputText.fontSize = 22;
        inputText.color = Color.white;
        inputText.alignment = TextAnchor.MiddleLeft;
        inputText.supportRichText = false;

        inputField.textComponent = inputText;

        // Placeholder
        GameObject placeholderObj = new GameObject("Placeholder");
        placeholderObj.transform.SetParent(inputObj.transform, false);
        RectTransform phRect = placeholderObj.AddComponent<RectTransform>();
        phRect.anchorMin = Vector2.zero;
        phRect.anchorMax = Vector2.one;
        phRect.offsetMin = new Vector2(10, 2);
        phRect.offsetMax = new Vector2(-10, -2);

        Text placeholder = placeholderObj.AddComponent<Text>();
        placeholder.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        placeholder.fontSize = 22;
        placeholder.color = new Color(0.5f, 0.5f, 0.5f);
        placeholder.alignment = TextAnchor.MiddleLeft;
        placeholder.fontStyle = FontStyle.Italic;
        placeholder.text = "Type your message...";

        inputField.placeholder = placeholder;

        // Send button
        GameObject sendObj = new GameObject("SendButton");
        sendObj.transform.SetParent(bottomBar.transform, false);
        RectTransform sendRect = sendObj.AddComponent<RectTransform>();
        sendRect.anchorMin = new Vector2(1, 0);
        sendRect.anchorMax = new Vector2(1, 1);
        sendRect.pivot = new Vector2(1, 0.5f);
        sendRect.anchoredPosition = new Vector2(-10, 0);
        sendRect.sizeDelta = new Vector2(90, -20);

        Image sendBg = sendObj.AddComponent<Image>();
        sendBg.color = new Color(0.2f, 0.4f, 0.2f, 1f);

        sendButton = sendObj.AddComponent<Button>();
        ColorBlock scb = sendButton.colors;
        scb.highlightedColor = new Color(0.3f, 0.5f, 0.3f, 1f);
        scb.pressedColor = new Color(0.15f, 0.3f, 0.15f, 1f);
        sendButton.colors = scb;
        sendButton.onClick.AddListener(OnSendClicked);

        sendButtonText = CreateText(sendObj.transform,
            "SEND", 20, TextAnchor.MiddleCenter);
        RectTransform sbtRect = sendButtonText.GetComponent<RectTransform>();
        sbtRect.anchorMin = Vector2.zero;
        sbtRect.anchorMax = Vector2.one;
        sbtRect.offsetMin = Vector2.zero;
        sbtRect.offsetMax = Vector2.zero;

        // Handle Enter key
        inputField.onEndEdit.AddListener(text =>
        {
            if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
                OnSendClicked();
        });

        // ── Scroll area (between top and bottom bars) ────────────────
        GameObject scrollObj = new GameObject("ScrollArea");
        scrollObj.transform.SetParent(chatPanel.transform, false);
        RectTransform scrollObjRect = scrollObj.AddComponent<RectTransform>();
        scrollObjRect.anchorMin = new Vector2(0, 0);
        scrollObjRect.anchorMax = new Vector2(1, 1);
        scrollObjRect.offsetMin = new Vector2(0, 70);   // above bottom bar
        scrollObjRect.offsetMax = new Vector2(0, -70);   // below top bar

        // No Image/Mask here — the Viewport child handles clipping
        scrollRect = scrollObj.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 30f;

        // Viewport
        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollObj.transform, false);
        RectTransform vpRect = viewport.AddComponent<RectTransform>();
        vpRect.anchorMin = Vector2.zero;
        vpRect.anchorMax = Vector2.one;
        vpRect.offsetMin = Vector2.zero;
        vpRect.offsetMax = Vector2.zero;

        Image vpImg = viewport.AddComponent<Image>();
        vpImg.color = Color.white;
        viewport.AddComponent<Mask>().showMaskGraphic = false;

        scrollRect.viewport = vpRect;

        // Content container
        GameObject content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        contentRect = content.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0, 0);

        VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.spacing = 10;
        vlg.padding = new RectOffset(20, 20, 10, 10);
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.content = contentRect;

        // Status text (typing indicator)
        statusText = CreateText(chatPanel.transform, "", 16, TextAnchor.MiddleCenter);
        statusText.color = new Color(0.5f, 0.5f, 0.7f);
        RectTransform statusRect = statusText.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0, 0);
        statusRect.anchorMax = new Vector2(1, 0);
        statusRect.pivot = new Vector2(0.5f, 0);
        statusRect.anchoredPosition = new Vector2(0, 72);
        statusRect.sizeDelta = new Vector2(0, 20);
    }

    // ══════════════════════════════════════════════════════════════════
    //  MESSAGE HANDLING
    // ══════════════════════════════════════════════════════════════════

    private void OnSendClicked()
    {
        if (isWaitingForResponse) return;
        if (inputField == null) return;

        string message = inputField.text.Trim();
        if (string.IsNullOrEmpty(message)) return;

        inputField.text = "";

        // Add user message
        AddMessageBubble("user", message);
        chatHistory.Add(new RoleplayManager.ChatMessage
        {
            role = "user",
            content = message
        });

        // Send to LLM
        SendToLLM(message);

        // Re-focus input
        if (inputField != null)
            inputField.ActivateInputField();
    }

    private void SendToLLM(string userMessage)
    {
        if (RoleplayManager.Instance == null) return;

        isWaitingForResponse = true;
        UpdateSendButton();
        if (statusText != null) statusText.text = "Thinking...";

        RoleplayManager.Instance.SendChatMessage(
            activeScenario, chatHistory, userMessage, response =>
        {
            isWaitingForResponse = false;
            UpdateSendButton();
            if (statusText != null) statusText.text = "";

            if (!string.IsNullOrEmpty(response))
            {
                // Check if the LLM signals conversation complete
                bool conversationDone = response.Contains("[CONVERSATION COMPLETE]");
                string displayResponse = response.Replace("[CONVERSATION COMPLETE]", "").Trim();

                if (!string.IsNullOrEmpty(displayResponse))
                {
                    AddMessageBubble("assistant", displayResponse);
                    chatHistory.Add(new RoleplayManager.ChatMessage
                    {
                        role = "assistant",
                        content = displayResponse
                    });
                }

                if (conversationDone)
                {
                    AddMessageBubble("system",
                        "Conversation complete! All vocabulary words have been practiced. " +
                        "Great work!");

                    // Disable input
                    if (inputField != null) inputField.interactable = false;
                    if (sendButton != null) sendButton.interactable = false;
                }
            }
        });
    }

    private void UpdateSendButton()
    {
        if (sendButton != null)
            sendButton.interactable = !isWaitingForResponse;
        if (sendButtonText != null)
            sendButtonText.text = isWaitingForResponse ? "..." : "SEND";
    }

    private void AddMessageBubble(string role, string message)
    {
        if (contentRect == null) return;

        bool isUser = role == "user";
        bool isSystem = role == "system";

        // Bubble container
        GameObject bubble = new GameObject("Msg_" + role);
        bubble.transform.SetParent(contentRect, false);

        RectTransform bubbleRect = bubble.AddComponent<RectTransform>();
        // Let VerticalLayoutGroup handle positioning

        // Layout element to control sizing
        LayoutElement le = bubble.AddComponent<LayoutElement>();
        le.minHeight = 40;

        // Inner alignment container
        GameObject inner = new GameObject("Inner");
        inner.transform.SetParent(bubble.transform, false);
        RectTransform innerRect = inner.AddComponent<RectTransform>();

        if (isSystem)
        {
            innerRect.anchorMin = new Vector2(0.1f, 0);
            innerRect.anchorMax = new Vector2(0.9f, 1);
        }
        else if (isUser)
        {
            innerRect.anchorMin = new Vector2(0.3f, 0);
            innerRect.anchorMax = new Vector2(1, 1);
        }
        else
        {
            innerRect.anchorMin = new Vector2(0, 0);
            innerRect.anchorMax = new Vector2(0.7f, 1);
        }
        innerRect.offsetMin = Vector2.zero;
        innerRect.offsetMax = Vector2.zero;

        Image bubbleBg = inner.AddComponent<Image>();
        if (isSystem)
            bubbleBg.color = new Color(0.2f, 0.3f, 0.15f, 1f);   // green for system
        else if (isUser)
            bubbleBg.color = new Color(0.15f, 0.25f, 0.4f, 1f);   // blue for user
        else
            bubbleBg.color = new Color(0.18f, 0.18f, 0.22f, 1f);  // grey for assistant
        bubbleBg.raycastTarget = false;

        // Role label
        string displayRole;
        if (isSystem)
            displayRole = "System";
        else if (isUser)
            displayRole = !string.IsNullOrEmpty(activeScenario?.userRole) ? activeScenario.userRole : "You";
        else
            displayRole = !string.IsNullOrEmpty(activeScenario?.llmRole) ? activeScenario.llmRole : "Tutor";

        Text roleLabel = CreateText(inner.transform,
            displayRole, 14, isSystem ? TextAnchor.UpperCenter : (isUser ? TextAnchor.UpperRight : TextAnchor.UpperLeft));
        if (isSystem)
            roleLabel.color = new Color(0.7f, 1f, 0.5f);
        else if (isUser)
            roleLabel.color = new Color(0.6f, 0.8f, 1f);
        else
            roleLabel.color = new Color(0.6f, 1f, 0.6f);
        roleLabel.fontStyle = FontStyle.Bold;
        RectTransform rlRect = roleLabel.GetComponent<RectTransform>();
        rlRect.anchorMin = new Vector2(0, 1);
        rlRect.anchorMax = new Vector2(1, 1);
        rlRect.pivot = new Vector2(0.5f, 1);
        rlRect.offsetMin = new Vector2(10, -22);
        rlRect.offsetMax = new Vector2(-10, -4);

        // Message text
        Text msgText = CreateText(inner.transform, message, 20,
            isUser ? TextAnchor.UpperRight : TextAnchor.UpperLeft);
        msgText.horizontalOverflow = HorizontalWrapMode.Wrap;
        msgText.verticalOverflow = VerticalWrapMode.Overflow;
        if (cjkFont != null) msgText.font = cjkFont;
        RectTransform mtRect = msgText.GetComponent<RectTransform>();
        mtRect.anchorMin = new Vector2(0, 0);
        mtRect.anchorMax = new Vector2(1, 1);
        mtRect.offsetMin = new Vector2(10, 8);
        mtRect.offsetMax = new Vector2(-10, -26);

        // Calculate height needed
        Canvas.ForceUpdateCanvases();
        float textWidth = innerRect.rect.width - 20;
        if (textWidth <= 0) textWidth = 400; // fallback
        TextGenerationSettings settings = msgText.GetGenerationSettings(new Vector2(textWidth, 0));
        settings.generateOutOfBounds = true;
        TextGenerator gen = msgText.cachedTextGenerator;
        float preferredHeight = gen.GetPreferredHeight(message, settings);

        float totalHeight = Mathf.Max(50, preferredHeight + 40); // +40 for padding and role label
        le.preferredHeight = totalHeight;

        // Scroll to bottom
        StartCoroutine(ScrollToBottom());
    }

    private System.Collections.IEnumerator ScrollToBottom()
    {
        yield return null; // wait one frame for layout
        yield return null;
        if (scrollRect != null)
            scrollRect.normalizedPosition = Vector2.zero;
    }

    // ══════════════════════════════════════════════════════════════════
    //  UI HELPERS
    // ══════════════════════════════════════════════════════════════════

    private Text CreateText(Transform parent, string content, int fontSize, TextAnchor alignment)
    {
        GameObject obj = new GameObject("Text");
        obj.transform.SetParent(parent, false);

        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(600, fontSize + 10);

        Text txt = obj.AddComponent<Text>();
        txt.text = content;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = fontSize;
        txt.color = Color.white;
        txt.alignment = alignment;
        txt.raycastTarget = false;

        return txt;
    }

    private void CreateButton(Transform parent, string label, Vector2 pos,
        UnityEngine.Events.UnityAction onClick, float width, float height,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos)
    {
        GameObject btnObj = new GameObject("Btn_" + label);
        btnObj.transform.SetParent(parent, false);

        RectTransform btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.anchorMin = anchorMin;
        btnRect.anchorMax = anchorMax;
        btnRect.pivot = new Vector2(0.5f, 0.5f);
        btnRect.anchoredPosition = anchoredPos;
        btnRect.sizeDelta = new Vector2(width, height);

        Image btnBg = btnObj.AddComponent<Image>();
        btnBg.color = new Color(0.2f, 0.2f, 0.2f, 1f);

        Button btn = btnObj.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.highlightedColor = new Color(0.4f, 0.4f, 0.4f, 1f);
        cb.pressedColor = new Color(0.15f, 0.15f, 0.15f, 1f);
        btn.colors = cb;
        btn.onClick.AddListener(onClick);

        Text btnText = CreateText(btnObj.transform, label, 18, TextAnchor.MiddleCenter);
        RectTransform btRect = btnText.GetComponent<RectTransform>();
        btRect.anchorMin = Vector2.zero;
        btRect.anchorMax = Vector2.one;
        btRect.offsetMin = Vector2.zero;
        btRect.offsetMax = Vector2.zero;
    }
}
