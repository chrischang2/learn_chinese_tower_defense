using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Creates and manages all HUD / UI elements programmatically:
/// - Main menu (start game, learned characters)
/// - Wave select screen
/// - Learned characters grid (5×5, paginated)
/// - In-game HUD (score, lives, wave)
/// - Wave intro screen
/// - Game Over screen
/// </summary>
public class HUDManager : MonoBehaviour
{
    // ── In-game HUD ──────────────────────────────────────────────────
    private Text scoreText;
    private Text livesText;
    private Text waveText;
    private GameObject hudBar; // parent for score/wave/lives

    // ── Panels ───────────────────────────────────────────────────────
    private GameObject mainMenuPanel;
    private GameObject waveSelectPanel;
    private GameObject learnedCharsPanel;
    private GameObject waveIntroPanel;
    private GameObject gameOverPanel;
    private GameObject quitConfirmPanel;
    private GameObject wordListPanel;
    private RoleplayUI roleplayUI;
    private GameObject learnedWordsPanel;
    private Text finalScoreText;
    private Text pointsBalanceText;
    private Text typedInputText;
    private GameObject typedInputBox;
    private TypingManager typingManager;


    // ── Wave progress bar ─────────────────────────────────────────
    private Image progressBarFill;

    // ── Hint system ──────────────────────────────────────────────────
    private GameObject hintBox;
    private WordList.HanziEntry? activeHint;
    private bool hintPurchasedThisWave;

    // ── Learned‐characters pagination ────────────────────────────────
    private int learnedPage = 0;
    private int learnedWordsPage = 0;
    private const int GridSize = 25; // 5×5
    private const int WordsPerPage = 15;

    void Start()
    {
        Canvas canvas = GetComponent<Canvas>();

        // Configure CanvasScaler for consistent sizing
        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
        }

        // ── Top bar: Score | Wave | Lives (hidden until game starts) ─
        hudBar = new GameObject("HUDBar");
        hudBar.transform.SetParent(transform, false);
        RectTransform hudBarRect = hudBar.AddComponent<RectTransform>();
        hudBarRect.anchorMin = Vector2.zero;
        hudBarRect.anchorMax = Vector2.one;
        hudBarRect.offsetMin = Vector2.zero;
        hudBarRect.offsetMax = Vector2.zero;

        scoreText = CreateText(hudBar.transform, "ScoreText", "SCORE: 0",
            TextAnchor.UpperLeft, new Vector2(30, -15), new Vector2(400, 50));

        waveText = CreateText(hudBar.transform, "WaveText", "WAVE: 1",
            TextAnchor.UpperCenter, new Vector2(0, -15), new Vector2(400, 50));
        RectTransform wrt = waveText.GetComponent<RectTransform>();
        wrt.anchorMin = new Vector2(0.5f, 1f);
        wrt.anchorMax = new Vector2(0.5f, 1f);
        wrt.pivot = new Vector2(0.5f, 1f);

        livesText = CreateText(hudBar.transform, "LivesText", "LIVES: 3",
            TextAnchor.UpperRight, new Vector2(-30, -15), new Vector2(400, 50));
        RectTransform lrt = livesText.GetComponent<RectTransform>();
        lrt.anchorMin = new Vector2(1f, 1f);
        lrt.anchorMax = new Vector2(1f, 1f);
        lrt.pivot = new Vector2(1f, 1f);

        // ── Quit button (top-right, in-game only) ───────────────────
        CreateMenuButton(hudBar.transform, "QUIT", new Vector2(-30, -15), () =>
        {
            ShowQuitConfirm();
        }, 120, 40);
        // Reposition quit button to top-right
        Transform quitBtn = hudBar.transform.Find("Btn_QUIT");
        if (quitBtn != null)
        {
            RectTransform qrt = quitBtn.GetComponent<RectTransform>();
            qrt.anchorMin = new Vector2(1f, 1f);
            qrt.anchorMax = new Vector2(1f, 1f);
            qrt.pivot = new Vector2(1f, 1f);
            qrt.anchoredPosition = new Vector2(-30, -60);
        }

        // ── Wave progress bar (below top bar) ─────────────────────
        GameObject progressBg = new GameObject("ProgressBarBg");
        progressBg.transform.SetParent(hudBar.transform, false);
        RectTransform pbgRect = progressBg.AddComponent<RectTransform>();
        pbgRect.anchorMin = new Vector2(0f, 1f);
        pbgRect.anchorMax = new Vector2(1f, 1f);
        pbgRect.pivot = new Vector2(0.5f, 1f);
        pbgRect.anchoredPosition = new Vector2(0, -55);
        pbgRect.sizeDelta = new Vector2(-60, 8); // 30px margin each side
        Image pbgImg = progressBg.AddComponent<Image>();
        pbgImg.color = new Color(0.15f, 0.15f, 0.15f, 0.8f);
        pbgImg.raycastTarget = false;

        GameObject progressFill = new GameObject("ProgressBarFill");
        progressFill.transform.SetParent(progressBg.transform, false);
        RectTransform pfRect = progressFill.AddComponent<RectTransform>();
        pfRect.anchorMin = new Vector2(0f, 0f);
        pfRect.anchorMax = new Vector2(0f, 1f);
        pfRect.pivot = new Vector2(0f, 0.5f);
        pfRect.anchoredPosition = Vector2.zero;
        pfRect.sizeDelta = new Vector2(0, 0);
        progressBarFill = progressFill.AddComponent<Image>();
        progressBarFill.color = new Color(0.3f, 0.8f, 0.3f, 0.9f);
        progressBarFill.raycastTarget = false;

        hudBar.SetActive(false);

        // ── Typed input display (bottom-centre, in-game only) ───────
        typedInputBox = new GameObject("TypedInputBox");
        typedInputBox.transform.SetParent(hudBar.transform, false);
        RectTransform tib = typedInputBox.AddComponent<RectTransform>();
        tib.anchorMin = new Vector2(0.5f, 0f);
        tib.anchorMax = new Vector2(0.5f, 0f);
        tib.pivot = new Vector2(0.5f, 0f);
        tib.anchoredPosition = new Vector2(0, 20);
        tib.sizeDelta = new Vector2(400, 50);

        Image tibBg = typedInputBox.AddComponent<Image>();
        tibBg.color = new Color(0f, 0f, 0f, 0.5f);
        tibBg.raycastTarget = false;

        typedInputText = CreatePanelText(typedInputBox.transform, "", 36, Vector2.zero);
        typedInputText.alignment = TextAnchor.MiddleCenter;
        typedInputText.color = new Color(0.6f, 1f, 0.6f);
        typedInputText.GetComponent<RectTransform>().sizeDelta = new Vector2(380, 50);

        // ── Find TypingManager and subscribe ───────────────────────
        typingManager = FindAnyObjectByType<TypingManager>();
        if (typingManager != null)
            typingManager.OnTypedTextChanged += RefreshTypedInput;

        // ── Build static panels ──────────────────────────────────────
        BuildGameOverPanel();

        // ── Subscribe to events ──────────────────────────────────────
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnScoreChanged += RefreshScore;
            GameManager.Instance.OnLivesChanged += RefreshLives;
            GameManager.Instance.OnWaveChanged  += RefreshWave;
            GameManager.Instance.OnGameOver     += ShowGameOver;
            GameManager.Instance.OnWaveIntro    += ShowWaveIntro;
            GameManager.Instance.OnWaveStart    += HideWaveIntro;
            GameManager.Instance.OnMainMenu     += ShowMainMenu;
            GameManager.Instance.OnGameStart    += OnGameStart;
            GameManager.Instance.OnBossStart    += OnBossStartFromMenu;
            GameManager.Instance.OnWaveProgress += RefreshWaveProgress;
            GameManager.Instance.OnRoleplayDrillStart += OnRoleplayDrillStart;
        }
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnScoreChanged -= RefreshScore;
            GameManager.Instance.OnLivesChanged -= RefreshLives;
            GameManager.Instance.OnWaveChanged  -= RefreshWave;
            GameManager.Instance.OnGameOver     -= ShowGameOver;
            GameManager.Instance.OnWaveIntro    -= ShowWaveIntro;
            GameManager.Instance.OnWaveStart    -= HideWaveIntro;
            GameManager.Instance.OnMainMenu     -= ShowMainMenu;
            GameManager.Instance.OnGameStart    -= OnGameStart;
            GameManager.Instance.OnBossStart    -= OnBossStartFromMenu;
            GameManager.Instance.OnWaveProgress -= RefreshWaveProgress;
            GameManager.Instance.OnRoleplayDrillStart -= OnRoleplayDrillStart;
        }
        if (typingManager != null)
            typingManager.OnTypedTextChanged -= RefreshTypedInput;
    }

    // ── Refresh callbacks ────────────────────────────────────────────

    private void RefreshScore()
    {
        if (scoreText != null)
            scoreText.text = "SCORE: " + GameManager.Instance.Score;
    }

    private void RefreshLives()
    {
        if (livesText != null)
            livesText.text = "LIVES: " + GameManager.Instance.Lives;
    }

    private void RefreshWave()
    {
        if (waveText != null)
        {
            if (GameManager.Instance.IsBossWave)
                waveText.text = "BOSS WAVE";
            else
                waveText.text = "WAVE: " + GameManager.Instance.Wave;
        }
    }

    private void RefreshTypedInput(string text)
    {
        if (typedInputText != null)
            typedInputText.text = text;
    }

    private void RefreshWaveProgress(int killed, int total)
    {
        if (progressBarFill == null) return;
        float frac = total > 0 ? (float)killed / total : 0f;
        RectTransform fill = progressBarFill.GetComponent<RectTransform>();
        fill.anchorMax = new Vector2(frac, 1f);
    }

    private void OnGameStart(int startingWave)
    {
        HideAllMenus();
        hudBar.SetActive(true);
        ClearHintBox();
        if (Camera.main != null)
            Camera.main.backgroundColor = Color.black;
    }

    private void OnBossStartFromMenu(int bossWave)
    {
        HideAllMenus();
        hudBar.SetActive(true);
        ClearHintBox();
        if (Camera.main != null)
            Camera.main.backgroundColor = new Color(0.12f, 0f, 0f);
    }

    private void OnRoleplayDrillStart(RoleplayScenario scenario)
    {
        HideAllMenus();
        if (roleplayUI != null)
            roleplayUI.Close();
        hudBar.SetActive(true);
        ClearHintBox();
        if (Camera.main != null)
            Camera.main.backgroundColor = new Color(0f, 0.05f, 0.1f); // dark blue tint for roleplay
    }

    // ══════════════════════════════════════════════════════════════════
    //  MAIN MENU
    // ══════════════════════════════════════════════════════════════════

    private void ShowMainMenu()
    {
        HideAllMenus();
        hudBar.SetActive(false);
        if (Camera.main != null)
            Camera.main.backgroundColor = Color.black;

        mainMenuPanel = CreateFullscreenPanel("MainMenuPanel", new Color(0.08f, 0.08f, 0.12f, 0.95f));

        // Title
        Text title = CreatePanelText(mainMenuPanel.transform,
            "TYPING TOWER DEFENSE", 56, new Vector2(0, 200));
        title.fontStyle = FontStyle.Bold;

        // Subtitle
        Text subtitle = CreatePanelText(mainMenuPanel.transform,
            "Learn Chinese Characters", 28, new Vector2(0, 130));
        subtitle.color = new Color(0.7f, 0.7f, 0.7f);

        // Points balance
        pointsBalanceText = CreatePanelText(mainMenuPanel.transform,
            "POINTS: " + GameManager.GlobalPoints, 32, new Vector2(0, 70));
        pointsBalanceText.color = new Color(1f, 0.85f, 0.3f);

        // Start Game button
        CreateMenuButton(mainMenuPanel.transform, "START GAME", new Vector2(0, -10), () =>
        {
            ShowWaveSelect();
        });

        // Learned Characters button
        CreateMenuButton(mainMenuPanel.transform, "LEARNED CHARACTERS", new Vector2(0, -100), () =>
        {
            ShowLearnedCharacters();
        });

        // Roleplay Scenarios button
        CreateMenuButton(mainMenuPanel.transform, "ROLEPLAY SCENARIOS", new Vector2(0, -190), () =>
        {
            HideAllMenus();
            if (roleplayUI == null)
            {
                GameObject rpObj = new GameObject("RoleplayUI");
                rpObj.transform.SetParent(transform, false);
                roleplayUI = rpObj.AddComponent<RoleplayUI>();
            }
            roleplayUI.ShowScenarioList();
        });

        // Learned Words button
        CreateMenuButton(mainMenuPanel.transform, "LEARNED WORDS", new Vector2(0, -280), () =>
        {
            ShowLearnedWords();
        });

    }

    // ══════════════════════════════════════════════════════════════════
    //  WAVE SELECT
    // ══════════════════════════════════════════════════════════════════

    private void ShowWaveSelect()
    {
        HideAllMenus();

        waveSelectPanel = CreateFullscreenPanel("WaveSelectPanel", new Color(0.08f, 0.08f, 0.12f, 0.95f));

        // Title
        CreatePanelText(waveSelectPanel.transform,
            "SELECT STARTING WAVE", 48, new Vector2(0, 280));

        int highestBoss = GameManager.HighestBossCleared;
        int pending = GameManager.PendingBossWave;

        // Build list of available wave buttons
        // Wave 1 always, then post-boss starting points (6, 11, 16, ...)
        List<System.Action> buttonCreators = new List<System.Action>();

        // Layout settings
        int cols = 3;
        float btnWidth = 240f;
        float btnHeight = 60f;
        float gapX = 30f;
        float gapY = 20f;
        float totalWidth = cols * btnWidth + (cols - 1) * gapX;
        float startX = -totalWidth / 2f + btnWidth / 2f;
        float startY = 140f;
        int btnIndex = 0;

        // Wave 1 button (always)
        {
            int idx = btnIndex++;
            int row = idx / cols;
            int col = idx % cols;
            float x = startX + col * (btnWidth + gapX);
            float y = startY - row * (btnHeight + gapY);

            CreateMenuButton(waveSelectPanel.transform, "WAVE 1",
                new Vector2(x, y), () =>
                {
                    HideAllMenus();
                    if (GameManager.Instance != null)
                        GameManager.Instance.StartGame(1);
                }, btnWidth, btnHeight);
        }

        // For each boss milestone, show cleared start points + next boss always available
        int nextBoss = highestBoss + 5;
        for (int m = 5; m <= nextBoss; m += 5)
        {
            if (highestBoss >= m)
            {
                // Boss cleared — show "WAVE M+1" button
                int waveStart = m + 1;
                int idx = btnIndex++;
                int row = idx / cols;
                int col = idx % cols;
                float x = startX + col * (btnWidth + gapX);
                float y = startY - row * (btnHeight + gapY);

                CreateMenuButton(waveSelectPanel.transform, "WAVE " + waveStart,
                    new Vector2(x, y), () =>
                    {
                        HideAllMenus();
                        if (GameManager.Instance != null)
                            GameManager.Instance.StartGame(waveStart);
                    }, btnWidth, btnHeight);
            }
            else
            {
                // Boss not yet cleared — always show boss button
                int bossWave = m;
                int idx = btnIndex++;
                int row = idx / cols;
                int col = idx % cols;
                float x = startX + col * (btnWidth + gapX);
                float y = startY - row * (btnHeight + gapY);

                CreateMenuButton(waveSelectPanel.transform, "BOSS WAVE " + bossWave,
                    new Vector2(x, y), () =>
                    {
                        HideAllMenus();
                        if (GameManager.Instance != null)
                            GameManager.Instance.StartBossWave(bossWave);
                    }, btnWidth, btnHeight);

                // Style the boss button differently (red tint)
                Transform bossBtn = waveSelectPanel.transform.Find("Btn_BOSS WAVE " + bossWave);
                if (bossBtn != null)
                {
                    Image img = bossBtn.GetComponent<Image>();
                    if (img != null) img.color = new Color(0.4f, 0.1f, 0.1f, 1f);
                }
            }
        }

        // Back button
        CreateMenuButton(waveSelectPanel.transform, "BACK", new Vector2(0, -280), () =>
        {
            ShowMainMenu();
        });
    }


    // ══════════════════════════════════════════════════════════════════
    //  LEARNED CHARACTERS
    // ══════════════════════════════════════════════════════════════════

    private void ShowLearnedCharacters()
    {
        HideAllMenus();
        learnedPage = 0;
        BuildLearnedCharsPage();
    }

    private void BuildLearnedCharsPage()
    {
        if (learnedCharsPanel != null)
            Destroy(learnedCharsPanel);

        learnedCharsPanel = CreateFullscreenPanel("LearnedCharsPanel", new Color(0.08f, 0.08f, 0.12f, 0.95f));

        // CJK font for character display
        Font cjkFont = Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 48);
        if (cjkFont == null)
            cjkFont = Font.CreateDynamicFontFromOSFont("SimHei", 48);
        if (cjkFont == null)
            cjkFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        int totalChars = WordList.Count;
        int totalPages = Mathf.CeilToInt((float)totalChars / GridSize);
        if (totalPages < 1) totalPages = 1;
        learnedPage = Mathf.Clamp(learnedPage, 0, totalPages - 1);

        // How many characters the player has learned (based on highest wave × 2)
        int learnedCount = GameManager.HighestWaveReached * 2;

        // Title
        CreatePanelText(learnedCharsPanel.transform,
            "LEARNED CHARACTERS", 42, new Vector2(0, 340));

        // Page indicator
        CreatePanelText(learnedCharsPanel.transform,
            $"Page {learnedPage + 1} / {totalPages}", 24, new Vector2(0, 290));

        // 5×5 grid
        float cellSize = 90f;
        float gap = 10f;
        float gridWidth = 5 * cellSize + 4 * gap;
        float gridStartX = -gridWidth / 2f + cellSize / 2f;
        float gridStartY = 200f;

        int pageStart = learnedPage * GridSize;

        for (int i = 0; i < GridSize; i++)
        {
            int charIndex = pageStart + i;
            int row = i / 5;
            int col = i % 5;
            float x = gridStartX + col * (cellSize + gap);
            float y = gridStartY - row * (cellSize + gap);

            // Cell background
            GameObject cellObj = new GameObject("Cell_" + i);
            cellObj.transform.SetParent(learnedCharsPanel.transform, false);
            RectTransform cellRect = cellObj.AddComponent<RectTransform>();
            cellRect.anchorMin = new Vector2(0.5f, 0.5f);
            cellRect.anchorMax = new Vector2(0.5f, 0.5f);
            cellRect.pivot = new Vector2(0.5f, 0.5f);
            cellRect.anchoredPosition = new Vector2(x, y);
            cellRect.sizeDelta = new Vector2(cellSize, cellSize);

            Image cellBg = cellObj.AddComponent<Image>();

            if (charIndex >= totalChars)
            {
                // Empty cell past end of wordlist
                cellBg.color = new Color(0.1f, 0.1f, 0.1f, 0.3f);
                continue;
            }

            bool isLearned = charIndex < learnedCount;
            WordList.HanziEntry entry = WordList.GetEntryByIndex(charIndex);

            if (isLearned)
            {
                cellBg.color = new Color(0.15f, 0.3f, 0.15f, 1f); // dark green

                // Character text
                Text charText = CreatePanelText(cellObj.transform,
                    entry.Character, 40, new Vector2(0, 8));
                if (cjkFont != null) charText.font = cjkFont;

                // Pinyin below
                Text pinyinText = CreatePanelText(cellObj.transform,
                    entry.PinyinDisplay, 14, new Vector2(0, -30));
                pinyinText.color = new Color(0.7f, 0.9f, 0.7f);
            }
            else
            {
                cellBg.color = new Color(0.15f, 0.15f, 0.15f, 0.8f); // greyed out

                // Question mark
                Text qText = CreatePanelText(cellObj.transform,
                    "?", 36, Vector2.zero);
                qText.color = new Color(0.3f, 0.3f, 0.3f);
            }
        }

        // ── Pagination buttons ───────────────────────────────────────
        float navY = -320f;

        if (learnedPage > 0)
        {
            CreateMenuButton(learnedCharsPanel.transform, "< PREV",
                new Vector2(-160, navY), () =>
                {
                    learnedPage--;
                    BuildLearnedCharsPage();
                }, 180, 50);
        }

        if (learnedPage < totalPages - 1)
        {
            CreateMenuButton(learnedCharsPanel.transform, "NEXT >",
                new Vector2(160, navY), () =>
                {
                    learnedPage++;
                    BuildLearnedCharsPage();
                }, 180, 50);
        }

        // Back button
        CreateMenuButton(learnedCharsPanel.transform, "BACK",
            new Vector2(0, navY - 70), () =>
            {
                ShowMainMenu();
            });
    }

    // ══════════════════════════════════════════════════════════════════
    //  LEARNED WORDS (from roleplay scenarios)
    // ══════════════════════════════════════════════════════════════════

    private void ShowLearnedWords()
    {
        HideAllMenus();
        learnedWordsPage = 0;
        BuildLearnedWordsPage();
    }

    private void BuildLearnedWordsPage()
    {
        if (learnedWordsPanel != null)
            Destroy(learnedWordsPanel);

        learnedWordsPanel = CreateFullscreenPanel("LearnedWordsPanel",
            new Color(0.06f, 0.06f, 0.1f, 0.97f));

        var words = LearnedWordsManager.Instance != null
            ? LearnedWordsManager.Instance.Words
            : new List<LearnedWordsManager.LearnedWord>();

        int totalPages = Mathf.Max(1, Mathf.CeilToInt((float)words.Count / WordsPerPage));
        learnedWordsPage = Mathf.Clamp(learnedWordsPage, 0, totalPages - 1);

        // CJK font
        Font cjkFont = Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 48);
        if (cjkFont == null)
            cjkFont = Font.CreateDynamicFontFromOSFont("SimHei", 48);
        if (cjkFont == null)
            cjkFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Title
        Text title = CreatePanelText(learnedWordsPanel.transform,
            "LEARNED WORDS", 42, new Vector2(0, 340));
        title.fontStyle = FontStyle.Bold;

        // Stats
        string stats = $"{words.Count} words learned from roleplay scenarios";
        Text statsText = CreatePanelText(learnedWordsPanel.transform,
            stats, 20, new Vector2(0, 295));
        statsText.color = new Color(0.6f, 0.8f, 0.6f);

        // Page info
        CreatePanelText(learnedWordsPanel.transform,
            $"Page {learnedWordsPage + 1} / {totalPages}", 20, new Vector2(0, 265));

        if (words.Count == 0)
        {
            Text empty = CreatePanelText(learnedWordsPanel.transform,
                "No words learned yet.\nComplete a roleplay scenario word drill to add words!",
                24, new Vector2(0, 50));
            empty.color = new Color(0.5f, 0.5f, 0.5f);
            RectTransform emptyRect = empty.GetComponent<RectTransform>();
            emptyRect.sizeDelta = new Vector2(600, 80);
            empty.horizontalOverflow = HorizontalWrapMode.Wrap;
        }
        else
        {
            int startIdx = learnedWordsPage * WordsPerPage;
            int endIdx = Mathf.Min(startIdx + WordsPerPage, words.Count);
            float startY = 220f;
            float lineHeight = 38f;

            for (int i = startIdx; i < endIdx; i++)
            {
                int row = i - startIdx;
                float y = startY - row * lineHeight;
                var w = words[i];

                // Character (large, left)
                Text charText = CreatePanelText(learnedWordsPanel.transform,
                    w.character, 28, new Vector2(-280, y));
                if (cjkFont != null) charText.font = cjkFont;
                charText.alignment = TextAnchor.MiddleLeft;
                RectTransform ctRect = charText.GetComponent<RectTransform>();
                ctRect.sizeDelta = new Vector2(80, lineHeight);

                // Pinyin
                Text pinyinText = CreatePanelText(learnedWordsPanel.transform,
                    w.pinyinDisplay, 20, new Vector2(-170, y));
                pinyinText.alignment = TextAnchor.MiddleLeft;
                pinyinText.color = new Color(0.7f, 0.9f, 0.7f);
                RectTransform ptRect = pinyinText.GetComponent<RectTransform>();
                ptRect.sizeDelta = new Vector2(120, lineHeight);

                // Definition
                string def = w.definition;
                if (def.Length > 25) def = def.Substring(0, 22) + "...";
                Text defText = CreatePanelText(learnedWordsPanel.transform,
                    def, 18, new Vector2(-20, y));
                defText.alignment = TextAnchor.MiddleLeft;
                defText.color = new Color(0.8f, 0.8f, 0.8f);
                RectTransform dtRect = defText.GetComponent<RectTransform>();
                dtRect.sizeDelta = new Vector2(220, lineHeight);

                // Scenario source
                string src = w.scenarioTitle ?? "";
                if (src.Length > 18) src = src.Substring(0, 15) + "...";
                Text srcText = CreatePanelText(learnedWordsPanel.transform,
                    src, 14, new Vector2(220, y));
                srcText.alignment = TextAnchor.MiddleLeft;
                srcText.color = new Color(0.5f, 0.5f, 0.6f);
                srcText.fontStyle = FontStyle.Italic;
                RectTransform srRect = srcText.GetComponent<RectTransform>();
                srRect.sizeDelta = new Vector2(180, lineHeight);
            }
        }

        // Pagination
        float navY = -350f;
        if (learnedWordsPage > 0)
        {
            CreateMenuButton(learnedWordsPanel.transform, "< PREV",
                new Vector2(-160, navY), () =>
                {
                    learnedWordsPage--;
                    BuildLearnedWordsPage();
                }, 180, 50);
        }
        if (learnedWordsPage < totalPages - 1)
        {
            CreateMenuButton(learnedWordsPanel.transform, "NEXT >",
                new Vector2(160, navY), () =>
                {
                    learnedWordsPage++;
                    BuildLearnedWordsPage();
                }, 180, 50);
        }

        // Back button
        CreateMenuButton(learnedWordsPanel.transform, "BACK",
            new Vector2(0, navY - 70), () =>
            {
                ShowMainMenu();
            });
    }

    // ══════════════════════════════════════════════════════════════════
    //  WAVE INTRO
    // ══════════════════════════════════════════════════════════════════

    private void ShowWaveIntro(List<WordList.HanziEntry> entries)
    {
        // Clear hint from previous wave
        ClearHintBox();

        // Destroy old intro panel if it exists
        if (waveIntroPanel != null)
            Destroy(waveIntroPanel);

        bool isBoss = GameManager.Instance != null && GameManager.Instance.IsBossWave;
        int wave = GameManager.Instance != null ? GameManager.Instance.Wave : 1;

        Color bgColor = isBoss
            ? new Color(0.15f, 0f, 0f, 0.9f)  // dark red for boss
            : new Color(0f, 0f, 0f, 0.85f);
        waveIntroPanel = CreateFullscreenPanel("WaveIntroPanel", bgColor);

        // Try to get a CJK font for the characters
        Font cjkFont = Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 96);
        if (cjkFont == null)
            cjkFont = Font.CreateDynamicFontFromOSFont("SimHei", 96);
        if (cjkFont == null)
            cjkFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        if (isBoss)
        {
            // ── Boss wave intro (characters hidden) ──────────────────
            Text bossTitle = CreatePanelText(waveIntroPanel.transform,
                "BOSS WAVE", 64, new Vector2(0, 80));
            bossTitle.color = new Color(1f, 0.3f, 0.3f);
            bossTitle.fontStyle = FontStyle.Bold;

            Text bossSubtitle = CreatePanelText(waveIntroPanel.transform,
                "Defeat all " + (entries.Count * 2) + " enemies!", 32, new Vector2(0, 0));
            bossSubtitle.color = new Color(1f, 0.6f, 0.6f);

            // Warning text
            Text warn = CreatePanelText(waveIntroPanel.transform,
                "ENEMIES ARE FASTER!", 32, new Vector2(0, -80));
            warn.color = new Color(1f, 0.4f, 0.4f);
            warn.fontStyle = FontStyle.Bold;

            // Prompt
            Text prompt = CreatePanelText(waveIntroPanel.transform,
                "PRESS ANY KEY TO START", 28, new Vector2(0, -200));
            prompt.color = new Color(0.7f, 0.5f, 0.5f);
        }
        else
        {
            // ── Normal wave intro ────────────────────────────────────
            bool isRoleplay = GameManager.Instance != null && GameManager.Instance.IsRoleplayDrill;
            string introTitle = isRoleplay
                ? "WAVE " + wave + " — NEW WORDS"
                : "WAVE " + wave + " — NEW CHARACTERS";
            CreatePanelText(waveIntroPanel.transform,
                introTitle, 42, new Vector2(0, 200));

            float startY = 80f;
            float offsetX = entries.Count > 1 ? -220f : 0f;

            for (int i = 0; i < entries.Count; i++)
            {
                float x = offsetX + i * 440f;
                WordList.HanziEntry entry = entries[i];

                // Large hanzi character
                Text charText = CreatePanelText(waveIntroPanel.transform,
                    entry.Character, 96, new Vector2(x, startY));
                if (cjkFont != null)
                    charText.font = cjkFont;

                // Pinyin below
                CreatePanelText(waveIntroPanel.transform,
                    entry.PinyinDisplay, 36, new Vector2(x, startY - 80f));

                // Definition below pinyin
                string def = entry.Definition;
                if (def.Length > 40)
                    def = def.Substring(0, 37) + "...";
                Text defText = CreatePanelText(waveIntroPanel.transform,
                    def, 24, new Vector2(x, startY - 130f));
                defText.color = new Color(0.8f, 0.8f, 0.8f);
                defText.GetComponent<RectTransform>().sizeDelta = new Vector2(400, 40);
            }

            // ── Word List button (centered) ─────────────────────
            hintPurchasedThisWave = false;
            int pts = GameManager.GlobalPoints;

            string wordListLabel = pts >= 900 ? "WORD LIST (900 PTS)" : "WORD LIST (NEED 900 PTS)";
            CreateMenuButton(waveIntroPanel.transform, wordListLabel,
                new Vector2(0, -200), () =>
                {
                    if (GameManager.GlobalPoints >= 900)
                    {
                        GameManager.GlobalPoints -= 900;
                        ShowWordListForWave();
                    }
                }, 380, 50);
            if (pts < 900)
            {
                Transform wlBtn = waveIntroPanel.transform.Find("Btn_" + wordListLabel);
                if (wlBtn != null)
                {
                    Image btnImg = wlBtn.GetComponent<Image>();
                    if (btnImg != null) btnImg.color = new Color(0.15f, 0.15f, 0.15f, 0.6f);
                }
            }

            // Prompt
            Text prompt = CreatePanelText(waveIntroPanel.transform,
                "PRESS ANY KEY TO START", 28, new Vector2(0, -270));
            prompt.color = new Color(0.7f, 0.7f, 0.7f);
        }

        // Start listening for key press to dismiss
        StartCoroutine(WaitForDismiss());
    }

    private System.Collections.IEnumerator WaitForDismiss()
    {
        // Wait a couple frames so a lingering key press doesn't count
        yield return null;
        yield return null;

        while (true)
        {
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.anyKey.wasPressedThisFrame)
            {
                break;
            }
            yield return null;
        }

        if (GameManager.Instance != null)
            GameManager.Instance.EndWaveIntro();
    }

    private void HideWaveIntro()
    {
        if (wordListPanel != null)
        {
            Destroy(wordListPanel);
            wordListPanel = null;
        }
        if (waveIntroPanel != null)
        {
            Destroy(waveIntroPanel);
            waveIntroPanel = null;
        }

        // Set camera background based on boss wave
        if (Camera.main != null)
        {
            bool isBoss = GameManager.Instance != null && GameManager.Instance.IsBossWave;
            Camera.main.backgroundColor = isBoss
                ? new Color(0.12f, 0f, 0f)
                : Color.black;
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  HINT BOX
    // ══════════════════════════════════════════════════════════════════

    private void ShowHintBox(WordList.HanziEntry entry)
    {
        ClearHintBox();

        Font cjkFont = Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 48);
        if (cjkFont == null)
            cjkFont = Font.CreateDynamicFontFromOSFont("SimHei", 48);
        if (cjkFont == null)
            cjkFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        hintBox = new GameObject("HintBox");
        hintBox.transform.SetParent(hudBar.transform, false);
        RectTransform hbRect = hintBox.AddComponent<RectTransform>();
        hbRect.anchorMin = new Vector2(0f, 0.5f);
        hbRect.anchorMax = new Vector2(0f, 0.5f);
        hbRect.pivot = new Vector2(0f, 0.5f);
        hbRect.anchoredPosition = new Vector2(15, 0);
        hbRect.sizeDelta = new Vector2(120, 80);

        Image hbBg = hintBox.AddComponent<Image>();
        hbBg.color = new Color(0.1f, 0.15f, 0.3f, 0.85f);
        hbBg.raycastTarget = false;

        // Character
        Text charText = CreatePanelText(hintBox.transform, entry.Character, 40, new Vector2(0, 8));
        if (cjkFont != null) charText.font = cjkFont;
        charText.GetComponent<RectTransform>().sizeDelta = new Vector2(110, 50);

        // Pinyin below
        Text pinText = CreatePanelText(hintBox.transform, entry.PinyinDisplay, 16, new Vector2(0, -28));
        pinText.color = new Color(0.6f, 0.8f, 1f);
        pinText.GetComponent<RectTransform>().sizeDelta = new Vector2(110, 24);
    }

    private void ClearHintBox()
    {
        activeHint = null;
        if (hintBox != null)
        {
            Destroy(hintBox);
            hintBox = null;
        }
    }

    /// <summary>
    /// Show all introduced characters so far as a reference grid.
    /// Player pays 900 points to see this.
    /// Cells are clickable to purchase a hint (900 pts).
    /// </summary>
    private void ShowWordListForWave()
    {
        if (wordListPanel != null)
            Destroy(wordListPanel);

        wordListPanel = CreateFullscreenPanel("WordListPanel", new Color(0.05f, 0.05f, 0.1f, 0.95f));

        Font cjkFont = Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 48);
        if (cjkFont == null)
            cjkFont = Font.CreateDynamicFontFromOSFont("SimHei", 48);
        if (cjkFont == null)
            cjkFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        int wave = GameManager.Instance != null ? GameManager.Instance.Wave : 1;
        int introducedCount = wave * 2;
        introducedCount = Mathf.Min(introducedCount, WordList.Count);

        CreatePanelText(wordListPanel.transform,
            "WORD LIST \u2014 " + introducedCount + " CHARACTERS", 36, new Vector2(0, 340));

        // Subtitle: hint instruction
        string subtitle = hintPurchasedThisWave
            ? "HINT SELECTED"
            : "CLICK A CHARACTER TO SET AS HINT (900 PTS)";
        Color subtitleColor = hintPurchasedThisWave
            ? new Color(0.4f, 1f, 0.4f)
            : new Color(0.8f, 0.8f, 0.5f);
        Text subText = CreatePanelText(wordListPanel.transform, subtitle, 22, new Vector2(0, 298));
        subText.color = subtitleColor;

        // Grid: 5 columns
        float cellW = 160f;
        float cellH = 60f;
        float gap = 8f;
        int cols = 5;
        float totalW = cols * cellW + (cols - 1) * gap;
        float gridStartX = -totalW / 2f + cellW / 2f;
        float gridStartY = 260f;

        for (int i = 0; i < introducedCount; i++)
        {
            int row = i / cols;
            int col = i % cols;
            float x = gridStartX + col * (cellW + gap);
            float y = gridStartY - row * (cellH + gap);

            WordList.HanziEntry entry = WordList.GetEntryByIndex(i);

            GameObject cellObj = new GameObject("WLCell_" + i);
            cellObj.transform.SetParent(wordListPanel.transform, false);
            RectTransform cellRect = cellObj.AddComponent<RectTransform>();
            cellRect.anchorMin = new Vector2(0.5f, 0.5f);
            cellRect.anchorMax = new Vector2(0.5f, 0.5f);
            cellRect.pivot = new Vector2(0.5f, 0.5f);
            cellRect.anchoredPosition = new Vector2(x, y);
            cellRect.sizeDelta = new Vector2(cellW, cellH);

            Image cellBg = cellObj.AddComponent<Image>();
            // Highlight selected hint cell in blue
            bool isSelected = activeHint.HasValue &&
                activeHint.Value.Character == entry.Character;
            cellBg.color = isSelected
                ? new Color(0.1f, 0.15f, 0.4f, 0.95f)
                : new Color(0.12f, 0.22f, 0.12f, 0.9f);

            // Make cell clickable
            Button cellBtn = cellObj.AddComponent<Button>();
            cellBtn.targetGraphic = cellBg;
            ColorBlock cb = cellBtn.colors;
            cb.highlightedColor = new Color(0.2f, 0.35f, 0.2f, 1f);
            cb.pressedColor = new Color(0.15f, 0.25f, 0.15f, 1f);
            cellBtn.colors = cb;

            WordList.HanziEntry capturedEntry = entry;
            cellBtn.onClick.AddListener(() =>
            {
                if (hintPurchasedThisWave) return;
                if (GameManager.GlobalPoints < 900) return;
                GameManager.GlobalPoints -= 900;
                hintPurchasedThisWave = true;
                activeHint = capturedEntry;
                ShowHintBox(capturedEntry);
                // Rebuild word list to reflect selection
                ShowWordListForWave();
            });

            // Character + pinyin side by side
            Text charText = CreatePanelText(cellObj.transform,
                entry.Character, 32, new Vector2(-30, 0));
            if (cjkFont != null) charText.font = cjkFont;
            charText.GetComponent<RectTransform>().sizeDelta = new Vector2(50, cellH);
            charText.raycastTarget = false;

            Text pinText = CreatePanelText(cellObj.transform,
                entry.PinyinDisplay, 18, new Vector2(25, 0));
            pinText.color = new Color(0.7f, 0.9f, 0.7f);
            pinText.GetComponent<RectTransform>().sizeDelta = new Vector2(90, cellH);
            pinText.raycastTarget = false;
        }

        // Close button
        CreateMenuButton(wordListPanel.transform, "CLOSE", new Vector2(0, -360), () =>
        {
            if (wordListPanel != null)
            {
                Destroy(wordListPanel);
                wordListPanel = null;
            }
        });
    }

    // ══════════════════════════════════════════════════════════════════
    //  GAME OVER
    // ══════════════════════════════════════════════════════════════════

    private void ShowGameOver()
    {
        HideWaveIntro();
        HideQuitConfirm();
        if (Camera.main != null)
            Camera.main.backgroundColor = Color.black;
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            if (finalScoreText != null)
                finalScoreText.text = "FINAL SCORE: " + GameManager.Instance.Score;
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  QUIT CONFIRM
    // ══════════════════════════════════════════════════════════════════

    private void ShowQuitConfirm()
    {
        if (quitConfirmPanel != null) return; // already showing

        // Pause the game while confirming
        Time.timeScale = 0f;

        quitConfirmPanel = CreateFullscreenPanel("QuitConfirmPanel", new Color(0f, 0f, 0f, 0.7f));

        CreatePanelText(quitConfirmPanel.transform,
            "QUIT TO MENU?", 48, new Vector2(0, 60));

        Text subtext = CreatePanelText(quitConfirmPanel.transform,
            "Your score will be banked as points.", 24, new Vector2(0, 5));
        subtext.color = new Color(0.7f, 0.7f, 0.7f);

        CreateMenuButton(quitConfirmPanel.transform, "YES, QUIT", new Vector2(-180, -80), () =>
        {
            Time.timeScale = 1f;
            if (GameManager.Instance != null)
                GameManager.Instance.QuitToMenu();
        }, 260, 60);

        CreateMenuButton(quitConfirmPanel.transform, "CANCEL", new Vector2(180, -80), () =>
        {
            HideQuitConfirm();
        }, 260, 60);
    }

    private void HideQuitConfirm()
    {
        Time.timeScale = 1f;
        if (quitConfirmPanel != null)
        {
            Destroy(quitConfirmPanel);
            quitConfirmPanel = null;
        }
    }

    private void BuildGameOverPanel()
    {
        gameOverPanel = CreateFullscreenPanel("GameOverPanel", new Color(0f, 0f, 0f, 0.75f));

        // ── "GAME OVER" title ────────────────────────────────────────
        CreatePanelText(gameOverPanel.transform, "GAME OVER", 72, new Vector2(0, 80));

        // ── Final score ──────────────────────────────────────────────
        finalScoreText = CreatePanelText(gameOverPanel.transform,
            "FINAL SCORE: 0", 40, new Vector2(0, -10));

        // ── Restart button ───────────────────────────────────────────
        CreateMenuButton(gameOverPanel.transform, "RESTART", new Vector2(0, -90), () =>
        {
            if (GameManager.Instance != null)
                GameManager.Instance.RestartGame();
        });

        // ── Main Menu button ─────────────────────────────────────────
        CreateMenuButton(gameOverPanel.transform, "MAIN MENU", new Vector2(0, -170), () =>
        {
            if (GameManager.Instance != null)
                GameManager.Instance.ReturnToMenu();
        });

        gameOverPanel.SetActive(false);
    }

    // ══════════════════════════════════════════════════════════════════
    //  HELPERS
    // ══════════════════════════════════════════════════════════════════

    private void HideAllMenus()
    {
        if (mainMenuPanel != null) { Destroy(mainMenuPanel); mainMenuPanel = null; }
        if (waveSelectPanel != null) { Destroy(waveSelectPanel); waveSelectPanel = null; }
        if (learnedCharsPanel != null) { Destroy(learnedCharsPanel); learnedCharsPanel = null; }
        if (learnedWordsPanel != null) { Destroy(learnedWordsPanel); learnedWordsPanel = null; }
        HideQuitConfirm();
    }

    /// <summary>Create a full-screen overlay panel.</summary>
    private GameObject CreateFullscreenPanel(string name, Color bgColor)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(transform, false);

        RectTransform rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image bg = panel.AddComponent<Image>();
        bg.color = bgColor;

        return panel;
    }

    /// <summary>Create a styled menu button.</summary>
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

        CreatePanelText(btnObj.transform, label, 28, Vector2.zero);
    }

    /// <summary>
    /// Create a simple white UI Text element anchored to the top-left.
    /// </summary>
    private Text CreateText(Transform parent, string name, string content,
        TextAnchor anchor, Vector2 position, Vector2 size)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;

        Text txt = obj.AddComponent<Text>();
        txt.text = content;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 32;
        txt.color = Color.white;
        txt.alignment = anchor;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow = VerticalWrapMode.Overflow;

        return txt;
    }

    /// <summary>Utility: centred text inside a panel.</summary>
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
}
