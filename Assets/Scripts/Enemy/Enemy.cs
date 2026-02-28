using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Enemy behaviour: moves toward the center tower, displays a hanzi character
/// overlaid on the enemy body, tracks letter matching progress, and explodes on death.
/// </summary>
public class Enemy : MonoBehaviour
{
    // ── Global tracking ──────────────────────────────────────────────
    public static readonly List<Enemy> ActiveEnemies = new List<Enemy>();
    /// <summary>Kill count for the current wave (reset by EnemySpawner).</summary>
    public static int WaveKillCount = 0;

    // ── Per-enemy state ──────────────────────────────────────────────
    /// <summary>The ASCII pinyin the player types to kill this enemy.</summary>
    public string Word { get; private set; }
    /// <summary>The Chinese character displayed on this enemy.</summary>
    public string Hanzi { get; private set; }
    /// <summary>Toned pinyin for display, e.g. "nǐ".</summary>
    public string PinyinDisplay { get; private set; }
    /// <summary>Short English definition.</summary>
    public string Definition { get; private set; }
    public int MatchedLetters { get; set; }
    public bool IsTargeted { get; set; }
    /// <summary>True when this is the nearest candidate for the current input.</summary>
    public bool IsPrimaryTarget { get; set; }

    private float speed;
    private Color enemyColor;
    private SpriteRenderer spriteRenderer;
    private TextMesh hanziTextMesh;
    private MeshRenderer hanziRenderer;

    // ── Initialisation ───────────────────────────────────────────────

    /// <summary>
    /// Set up this enemy with a HanziEntry, speed, spawn position, and colour.
    /// Call once immediately after instantiation.
    /// </summary>
    public void Initialize(WordList.HanziEntry entry, float speed, Vector3 spawnPos, Color color)
    {
        Word          = entry.PinyinTypeable;
        Hanzi         = entry.Character;
        PinyinDisplay = entry.PinyinDisplay;
        Definition    = entry.Definition;
        this.speed    = speed;
        transform.position = spawnPos;
        enemyColor    = color;
        MatchedLetters = 0;
        IsTargeted     = false;

        // ── Visual: enemy body (coloured square, 20% larger) ────────
        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = GameSetup.WhiteSquareSprite;
        spriteRenderer.color = enemyColor;
        spriteRenderer.sortingOrder = 1;

        float bodySize = 0.799f;  // was 0.726, +10%
        transform.localScale = new Vector3(bodySize, bodySize, 1f);

        // Use a CJK-capable font (try several common options)
        Font cjkFont = Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 72);
        if (cjkFont == null)
            cjkFont = Font.CreateDynamicFontFromOSFont("SimHei", 72);
        if (cjkFont == null)
            cjkFont = Font.CreateDynamicFontFromOSFont("Arial Unicode MS", 72);

        // ── Visual: hanzi character overlaid on the enemy body ───────
        GameObject hanziObj = new GameObject("HanziLabel");
        hanziObj.transform.SetParent(transform);
        // Centre on the body (local 0,0)
        hanziObj.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        // Un-scale so text isn't squished by body scale
        hanziObj.transform.localScale = new Vector3(
            1f / bodySize, 1f / bodySize, 1f);

        hanziTextMesh = hanziObj.AddComponent<TextMesh>();
        hanziTextMesh.text = Hanzi;
        hanziTextMesh.characterSize = 0.0945f;  // was 0.105, -10%
        hanziTextMesh.fontSize = 72;
        hanziTextMesh.anchor = TextAnchor.MiddleCenter;
        hanziTextMesh.alignment = TextAlignment.Center;
        hanziTextMesh.richText = false;
        hanziTextMesh.color = Color.white;

        if (cjkFont != null)
            hanziTextMesh.font = cjkFont;

        hanziRenderer = hanziObj.GetComponent<MeshRenderer>();
        hanziRenderer.sortingOrder = 2;  // just above the body sprite
        if (hanziTextMesh.font != null)
            hanziRenderer.material = hanziTextMesh.font.material;

        // Register
        ActiveEnemies.Add(this);
        UpdateWordDisplay();
    }

    // ── Lifecycle ────────────────────────────────────────────────────

    void Update()
    {
        if (GameManager.Instance != null &&
            GameManager.Instance.GameState != GameManager.State.Playing)
            return;

        // Move toward centre
        Vector3 dir = (Vector3.zero - transform.position).normalized;
        transform.position += dir * speed * Time.deltaTime;

        // Check arrival at tower
        if (Vector3.Distance(transform.position, Vector3.zero) < 0.55f)
        {
            ReachedTower();
        }
    }

    void OnDestroy()
    {
        ActiveEnemies.Remove(this);
    }

    // ── Word matching ────────────────────────────────────────────────

    /// <summary>
    /// Try to match the next expected letter of the pinyin.
    /// Returns true on match.
    /// </summary>
    public bool TryMatchLetter(char c)
    {
        if (MatchedLetters >= Word.Length) return false;

        if (char.ToLower(Word[MatchedLetters]) == char.ToLower(c))
        {
            MatchedLetters++;
            UpdateWordDisplay();
            return true;
        }
        return false;
    }

    public bool IsFullyMatched => MatchedLetters >= Word.Length;

    /// <summary>
    /// Reset matching progress (e.g. when the player switches target).
    /// </summary>
    public void ResetMatch()
    {
        MatchedLetters = 0;
        IsTargeted = false;
        IsPrimaryTarget = false;
        UpdateWordDisplay();
    }

    /// <summary>
    /// Set highlight state from the free-input system.
    /// matchCount = how many letters of the pinyin are matched by current input.
    /// isPrimary = true if this is the nearest candidate that will be killed.
    /// </summary>
    public void SetHighlight(int matchCount, bool isPrimary)
    {
        MatchedLetters = matchCount;
        IsTargeted = matchCount > 0;
        IsPrimaryTarget = isPrimary;
        UpdateWordDisplay();
    }

    // ── Display ──────────────────────────────────────────────────────

    /// <summary>
    /// Update the hanzi character colour to show targeting / match state:
    ///   untargeted       → white
    ///   targeted         → yellow
    ///   partially matched → light green
    /// </summary>
    public void UpdateWordDisplay()
    {
        // Tint the hanzi character to show targeting / match progress
        if (hanziTextMesh != null)
        {
            if (IsPrimaryTarget && MatchedLetters > 0)
                hanziTextMesh.color = new Color(0.5f, 1f, 0.5f); // light green — primary target
            else if (MatchedLetters > 0)
                hanziTextMesh.color = Color.yellow; // candidate match
            else
                hanziTextMesh.color = Color.white;
        }
    }

    // ── Death ────────────────────────────────────────────────────────

    /// <summary>
    /// Kill the enemy: award score, play explosion, destroy GameObject.
    /// </summary>
    public void Die()
    {
        WaveKillCount++;

        // Flat 30 points per kill
        if (GameManager.Instance != null)
            GameManager.Instance.AddScore(30);

        // Laser beam from tower to enemy
        LaserEffect.Spawn(transform.position, Color.cyan);

        // Pixel explosion
        PixelExplosion.Spawn(transform.position, enemyColor, Random.Range(15, 26));

        Destroy(gameObject);
    }

    /// <summary>
    /// Enemy reached the tower — player loses a life, enemy respawns at a new edge.
    /// </summary>
    private void ReachedTower()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.LoseLife();

        // Small red explosion to indicate damage
        PixelExplosion.Spawn(transform.position, Color.red, 8);

        // Recycle: move to a new random edge position
        transform.position = GetRandomEdgePosition();

        // Reset any partial typing match
        MatchedLetters = 0;
        IsTargeted = false;
        IsPrimaryTarget = false;
        if (spriteRenderer != null)
            spriteRenderer.color = enemyColor;
        UpdateWordDisplay();
    }

    /// <summary>
    /// Pick a random point just outside the visible screen edge.
    /// </summary>
    private static Vector3 GetRandomEdgePosition()
    {
        Camera cam = Camera.main;
        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        float margin = 1.0f;

        int edge = Random.Range(0, 4);
        float x, y;

        switch (edge)
        {
            case 0: x = Random.Range(-halfW, halfW); y = halfH + margin; break;
            case 1: x = Random.Range(-halfW, halfW); y = -(halfH + margin); break;
            case 2: x = -(halfW + margin); y = Random.Range(-halfH, halfH); break;
            default: x = halfW + margin; y = Random.Range(-halfH, halfH); break;
        }
        return new Vector3(x, y, 0f);
    }
}
