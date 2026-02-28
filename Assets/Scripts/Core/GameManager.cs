using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// Central game state manager. Singleton that tracks score, lives, wave, and game state.
/// Persists highest wave reached via PlayerPrefs.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum State { MainMenu, Playing, WaveIntro, GameOver }

    public State GameState { get; private set; } = State.MainMenu;
    public int Score { get; private set; }
    public int Lives { get; private set; } = 3;
    public int Wave { get; private set; } = 1;

    /// <summary>The wave the player chose to start from.</summary>
    public int StartingWave { get; private set; } = 1;

    /// <summary>The new characters being introduced this wave (set during WaveIntro).</summary>
    public List<WordList.HanziEntry> IntroEntries { get; private set; }

    /// <summary>True when a boss wave is active.</summary>
    public bool IsBossWave { get; private set; }

    // ── Persistence ──────────────────────────────────────────────────

    private const string HighestWaveKey = "HighestWave";
    private const string PointsKey = "GlobalPoints";

    private const string PendingBossKey = "PendingBossWave";
    private const string HighestBossClearedKey = "HighestBossCleared";

    /// <summary>Highest wave the player has ever reached (persisted).</summary>
    public static int HighestWaveReached
    {
        get { return PlayerPrefs.GetInt(HighestWaveKey, 1); }
        set
        {
            if (value > PlayerPrefs.GetInt(HighestWaveKey, 1))
            {
                PlayerPrefs.SetInt(HighestWaveKey, value);
                PlayerPrefs.Save();
            }
        }
    }

    /// <summary>Global points balance (persistent currency).</summary>
    public static int GlobalPoints
    {
        get { return PlayerPrefs.GetInt(PointsKey, 0); }
        set
        {
            PlayerPrefs.SetInt(PointsKey, Mathf.Max(0, value));
            PlayerPrefs.Save();
        }
    }


    /// <summary>Boss wave that was reached but not yet cleared (0 = none).</summary>
    public static int PendingBossWave
    {
        get { return PlayerPrefs.GetInt(PendingBossKey, 0); }
        set
        {
            PlayerPrefs.SetInt(PendingBossKey, Mathf.Max(0, value));
            PlayerPrefs.Save();
        }
    }

    /// <summary>Highest boss wave cleared (e.g. 5 = boss after wave 5 cleared).</summary>
    public static int HighestBossCleared
    {
        get { return PlayerPrefs.GetInt(HighestBossClearedKey, 0); }
        set
        {
            if (value > PlayerPrefs.GetInt(HighestBossClearedKey, 0))
            {
                PlayerPrefs.SetInt(HighestBossClearedKey, value);
                PlayerPrefs.Save();
            }
        }
    }

    // Events for UI updates
    public event System.Action OnScoreChanged;
    public event System.Action OnLivesChanged;
    public event System.Action OnWaveChanged;
    public event System.Action OnGameOver;
    public event System.Action<List<WordList.HanziEntry>> OnWaveIntro;
    public event System.Action OnWaveStart;
    public event System.Action OnMainMenu;
    public event System.Action<int> OnGameStart; // passes starting wave
    public event System.Action OnPointsChanged;
    /// <summary>Fired when wave progress changes. Args: (killed, total).</summary>
    public event System.Action<int, int> OnWaveProgress;
    /// <summary>Fired when starting a boss wave from the menu. Args: boss wave number.</summary>
    public event System.Action<int> OnBossStart;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Enemy.ActiveEnemies.Clear();
    }

    void Start()
    {
        // Delay one frame so all other scripts' Start() methods have subscribed
        StartCoroutine(DelayedMainMenu());
    }

    private System.Collections.IEnumerator DelayedMainMenu()
    {
        yield return null;
        OnMainMenu?.Invoke();
    }

    /// <summary>
    /// Called from the menu — begin the game at the specified wave.
    /// </summary>
    public void StartGame(int fromWave)
    {
        StartingWave = Mathf.Max(1, fromWave);
        Wave = StartingWave;
        Score = 0;
        Lives = 3;
        IsBossWave = false;
        GameState = State.WaveIntro; // EnemySpawner will begin intro
        OnScoreChanged?.Invoke();
        OnLivesChanged?.Invoke();
        OnWaveChanged?.Invoke();
        OnGameStart?.Invoke(StartingWave);
    }

    /// <summary>Start a boss wave directly from wave select.</summary>
    public void StartBossWave(int bossWave)
    {
        StartingWave = bossWave;
        Wave = bossWave;
        Score = 0;
        Lives = 3;
        IsBossWave = true;
        GameState = State.WaveIntro;
        OnScoreChanged?.Invoke();
        OnLivesChanged?.Invoke();
        OnWaveChanged?.Invoke();
        OnBossStart?.Invoke(bossWave);
    }

    public void AddScore(int points)
    {
        if (GameState != State.Playing) return;
        Score += points;
        OnScoreChanged?.Invoke();
    }

    /// <summary>Bank current score into global points (called on game over / quit).</summary>
    public void BankScore()
    {
        if (Score > 0)
        {
            GlobalPoints += Score;
            Score = 0;
            OnPointsChanged?.Invoke();
        }
    }

    public void LoseLife()
    {
        if (GameState != State.Playing) return;

        Lives--;
        OnLivesChanged?.Invoke();

        if (Lives <= 0)
        {
            GameState = State.GameOver;
            BankScore();
            OnGameOver?.Invoke();
        }
    }

    public void SetWave(int wave)
    {
        Wave = wave;
        HighestWaveReached = wave; // persist if new record
        OnWaveChanged?.Invoke();
    }

    /// <summary>Update wave progress (called by EnemySpawner).</summary>
    public void UpdateWaveProgress(int killed, int total)
    {
        OnWaveProgress?.Invoke(killed, total);
    }

    /// <summary>
    /// Called by EnemySpawner to show the wave intro screen.
    /// </summary>
    public void BeginWaveIntro(List<WordList.HanziEntry> newEntries)
    {
        if (GameState == State.GameOver) return;
        Lives = 3;
        OnLivesChanged?.Invoke();
        GameState = State.WaveIntro;
        IntroEntries = newEntries;
        OnWaveIntro?.Invoke(newEntries);
    }

    /// <summary>Show boss wave intro screen.</summary>
    public void BeginBossWaveIntro(List<WordList.HanziEntry> bossEntries)
    {
        if (GameState == State.GameOver) return;
        GameState = State.WaveIntro;
        IsBossWave = true;
        IntroEntries = bossEntries;
        OnWaveIntro?.Invoke(bossEntries);
    }

    /// <summary>Called when a boss wave is cleared.</summary>
    public void BossCleared(int bossWave)
    {
        IsBossWave = false;
        HighestBossCleared = bossWave;
        PendingBossWave = 0;
        HighestWaveReached = bossWave + 1;
        OnWaveChanged?.Invoke();
    }

    /// <summary>
    /// Called by HUDManager when the player dismisses the intro screen.
    /// </summary>
    public void EndWaveIntro()
    {
        if (GameState != State.WaveIntro) return;
        GameState = State.Playing;
        IntroEntries = null;
        OnWaveStart?.Invoke();
    }

    /// <summary>Quit current game, bank score, return to menu.</summary>
    public void QuitToMenu()
    {
        BankScore();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void ReturnToMenu()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
