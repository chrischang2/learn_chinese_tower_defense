using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Handles wave logic, enemy spawning from random screen edges,
/// and progressive difficulty scaling.
/// 
/// Each wave introduces 2 new characters (by frequency order).
/// New characters appear 3 times per wave.
/// Previously introduced characters appear randomly, max 2 times each per wave.
/// 
/// Waits for GameManager.OnGameStart before beginning — supports starting from
/// any unlocked wave (e.g. wave 5, 10, …).
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    private int currentWave = 0;

    /// <summary>
    /// How many characters from the frequency list have been introduced so far.
    /// When starting from wave N, this is set to (N-1) * 2 so the spawner
    /// resumes with the correct next characters.
    /// </summary>
    private int introducedCount = 0;

    private bool gameStarted = false;
    private int waveEnemiesTotal = 0;
    private int waveEnemiesKilled = 0;

    void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStart += OnGameStart;
            GameManager.Instance.OnBossStart += OnBossStartFromMenu;
        }
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStart -= OnGameStart;
            GameManager.Instance.OnBossStart -= OnBossStartFromMenu;
        }
    }

    private void OnGameStart(int startingWave)
    {
        if (gameStarted) return;
        gameStarted = true;

        currentWave = startingWave - 1; // will be incremented at top of loop
        introducedCount = Mathf.Max(0, (startingWave - 1) * 2);
        introducedCount = Mathf.Min(introducedCount, WordList.Count);

        StartCoroutine(NormalStartRoutine());
    }

    private IEnumerator NormalStartRoutine()
    {
        yield return new WaitForSeconds(1.0f);
        yield return WaveLoop();
    }

    /// <summary>Start a boss wave directly from the menu.</summary>
    private void OnBossStartFromMenu(int bossWave)
    {
        if (gameStarted) return;
        gameStarted = true;

        currentWave = bossWave;
        introducedCount = Mathf.Min(bossWave * 2, WordList.Count);

        StartCoroutine(BossFromMenuRoutine(bossWave));
    }

    private IEnumerator BossFromMenuRoutine(int bossWave)
    {
        yield return new WaitForSeconds(1.0f);
        yield return RunBossWave(bossWave);
        if (IsGameOver()) yield break;
        // Boss cleared — continue with normal waves from bossWave+1
        yield return new WaitForSeconds(1.5f);
        yield return WaveLoop();
    }

    // ── Wave loop ────────────────────────────────────────────────────

    private bool IsGameOver()
    {
        return GameManager.Instance != null &&
               GameManager.Instance.GameState == GameManager.State.GameOver;
    }

    private IEnumerator WaveLoop()
    {
        while (true)
        {
            if (IsGameOver()) yield break;

            currentWave++;
            if (GameManager.Instance != null)
                GameManager.Instance.SetWave(currentWave);

            // ── Determine the 2 new characters for this wave ─────────
            int newStartIndex = introducedCount;
            int newCount = Mathf.Min(2, WordList.Count - introducedCount);
            List<WordList.HanziEntry> newEntries = new List<WordList.HanziEntry>();
            for (int i = 0; i < newCount; i++)
            {
                newEntries.Add(WordList.GetEntryByIndex(newStartIndex + i));
            }

            // ── Show wave intro screen ───────────────────────────────
            if (GameManager.Instance != null)
                GameManager.Instance.BeginWaveIntro(newEntries);

            // Wait for intro to finish
            yield return new WaitUntil(() =>
                GameManager.Instance == null ||
                GameManager.Instance.GameState != GameManager.State.WaveIntro);

            if (IsGameOver()) yield break;

            // Update introduced count
            introducedCount += newCount;

            // ── Build the spawn queue ────────────────────────────────
            List<WordList.HanziEntry> spawnQueue = new List<WordList.HanziEntry>();

            // Add new characters 3 times each
            for (int i = 0; i < newCount; i++)
            {
                for (int j = 0; j < 3; j++)
                    spawnQueue.Add(newEntries[i]);
            }

            // Guaranteed appearances for recent characters:
            // 1 wave ago: 2× each, 2-5 waves ago: 1× each
            for (int i = 0; i < newStartIndex; i++)
            {
                int introWave = (i / 2) + 1;
                int wavesAgo = currentWave - introWave;
                int guaranteed = 0;
                if (wavesAgo == 1) guaranteed = 2;
                else if (wavesAgo >= 2 && wavesAgo <= 5) guaranteed = 1;

                for (int j = 0; j < guaranteed; j++)
                    spawnQueue.Add(WordList.GetEntryByIndex(i));
            }

            // Cap enemies at 30 after wave 10
            int totalEnemies = currentWave > 10 ? 30 : 4 + currentWave * 2;
            int extraSlots = Mathf.Max(0, totalEnemies - spawnQueue.Count);

            // Fill remaining slots randomly from all learned characters
            if (newStartIndex > 0 && extraSlots > 0)
            {
                Dictionary<string, int> extraCounts = new Dictionary<string, int>();
                // Pre-count guaranteed appearances
                for (int i = 0; i < newStartIndex; i++)
                {
                    WordList.HanziEntry e = WordList.GetEntryByIndex(i);
                    int introWave = (i / 2) + 1;
                    int wavesAgo = currentWave - introWave;
                    int guaranteed = 0;
                    if (wavesAgo == 1) guaranteed = 2;
                    else if (wavesAgo >= 2 && wavesAgo <= 5) guaranteed = 1;
                    if (guaranteed > 0)
                        extraCounts[e.Character] = guaranteed;
                }

                int filled = 0;
                int safetyLimit = extraSlots * 10;
                int tries = 0;

                while (filled < extraSlots && tries < safetyLimit)
                {
                    tries++;
                    WordList.HanziEntry pick = WordList.GetEntryByIndex(Random.Range(0, newStartIndex));
                    int count;
                    extraCounts.TryGetValue(pick.Character, out count);

                    if (count < 2)
                    {
                        extraCounts[pick.Character] = count + 1;
                        spawnQueue.Add(pick);
                        filled++;
                    }
                }
            }

            // Shuffle the spawn queue
            ShuffleQueue(spawnQueue);

            // ── Spawn enemies one by one ─────────────────────────────
            yield return SpawnAndWaitForWave(spawnQueue, GetSpawnInterval(), false);
            if (IsGameOver()) yield break;

            // ── Boss wave after every 5th wave ───────────────────────
            if (currentWave % 5 == 0)
            {
                yield return new WaitForSeconds(1.5f);
                yield return RunBossWave(currentWave);
                if (IsGameOver()) yield break;
            }

            // Brief pause between waves
            yield return new WaitForSeconds(1.5f);
        }
    }

    // ── Boss wave ────────────────────────────────────────────────────

    /// <summary>
    /// Run a boss wave. Uses characters from the previous 5 waves,
    /// each appearing twice, at faster speed.
    /// </summary>
    private IEnumerator RunBossWave(int bossWave)
    {
        // Characters from waves (bossWave-4) to bossWave
        int bossStartIndex = Mathf.Max(0, (bossWave - 5) * 2);
        int bossEndIndex = Mathf.Min(bossWave * 2, WordList.Count);

        List<WordList.HanziEntry> bossEntries = new List<WordList.HanziEntry>();
        for (int i = bossStartIndex; i < bossEndIndex; i++)
        {
            bossEntries.Add(WordList.GetEntryByIndex(i));
        }

        // Mark pending boss (only if not already cleared)
        if (GameManager.HighestBossCleared < bossWave)
            GameManager.PendingBossWave = bossWave;

        // Show boss intro
        if (GameManager.Instance != null)
            GameManager.Instance.BeginBossWaveIntro(bossEntries);

        yield return new WaitUntil(() =>
            GameManager.Instance == null ||
            GameManager.Instance.GameState != GameManager.State.WaveIntro);

        if (IsGameOver()) yield break;

        // Build spawn queue: each character twice
        List<WordList.HanziEntry> spawnQueue = new List<WordList.HanziEntry>();
        foreach (WordList.HanziEntry entry in bossEntries)
        {
            spawnQueue.Add(entry);
            spawnQueue.Add(entry);
        }

        ShuffleQueue(spawnQueue);

        // Faster spawn interval for boss
        float spawnInterval = Mathf.Max(0.4f, 1.5f - bossWave * 0.05f);

        yield return SpawnAndWaitForWave(spawnQueue, spawnInterval, true);
        if (IsGameOver()) yield break;

        // Boss cleared!
        if (GameManager.Instance != null)
            GameManager.Instance.BossCleared(bossWave);
    }

    // ── Spawn helpers ────────────────────────────────────────────────

    /// <summary>Spawn all enemies in the queue and wait for them to be killed.</summary>
    private IEnumerator SpawnAndWaitForWave(List<WordList.HanziEntry> spawnQueue,
        float spawnInterval, bool isBoss)
    {
        waveEnemiesTotal = spawnQueue.Count;
        waveEnemiesKilled = 0;
        Enemy.WaveKillCount = 0;
        int spawnedSoFar = 0;
        if (GameManager.Instance != null)
            GameManager.Instance.UpdateWaveProgress(0, waveEnemiesTotal);

        foreach (WordList.HanziEntry entry in spawnQueue)
        {
            if (IsGameOver()) yield break;
            SpawnEnemy(entry, isBoss);
            spawnedSoFar++;

            // Track kills (only typed kills, not enemies that hit the player)
            if (Enemy.WaveKillCount > waveEnemiesKilled)
            {
                waveEnemiesKilled = Enemy.WaveKillCount;
                if (GameManager.Instance != null)
                    GameManager.Instance.UpdateWaveProgress(waveEnemiesKilled, waveEnemiesTotal);
            }

            yield return new WaitForSeconds(spawnInterval);
        }

        // Wait until all enemies are killed by typing
        yield return new WaitUntil(() =>
        {
            if (IsGameOver()) return true;

            // Update progress based on actual kills only
            if (Enemy.WaveKillCount != waveEnemiesKilled)
            {
                waveEnemiesKilled = Enemy.WaveKillCount;
                if (GameManager.Instance != null)
                    GameManager.Instance.UpdateWaveProgress(waveEnemiesKilled, waveEnemiesTotal);
            }
            return Enemy.WaveKillCount >= waveEnemiesTotal;
        });

        // Clean up any remaining active enemies (shouldn't happen, but safety)
        foreach (Enemy e in new List<Enemy>(Enemy.ActiveEnemies))
        {
            if (e != null) Destroy(e.gameObject);
        }
    }

    private void ShuffleQueue(List<WordList.HanziEntry> queue)
    {
        for (int i = queue.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            WordList.HanziEntry tmp = queue[i];
            queue[i] = queue[j];
            queue[j] = tmp;
        }
    }

    private float GetSpawnInterval()
    {
        return Mathf.Max(0.5f, 2.0f - currentWave * 0.08f);
    }

    // ── Spawning ─────────────────────────────────────────────────────

    private void SpawnEnemy(WordList.HanziEntry entry, bool isBoss = false)
    {
        Vector3 spawnPos = GetRandomEdgePosition();
        float speed = isBoss ? GetBossEnemySpeed() : GetEnemySpeed();
        Color color = isBoss ? GetBossEnemyColor() : GetEnemyColor();

        GameObject enemyObj = new GameObject("Enemy_" + entry.Character);
        Enemy enemy = enemyObj.AddComponent<Enemy>();
        enemy.Initialize(entry, speed, spawnPos, color);
    }

    /// <summary>
    /// Pick a random point just outside the visible screen edge.
    /// </summary>
    private Vector3 GetRandomEdgePosition()
    {
        Camera cam = Camera.main;
        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        float margin = 1.0f;

        int edge = Random.Range(0, 4);
        float x, y;

        switch (edge)
        {
            case 0: // Top
                x = Random.Range(-halfW, halfW);
                y = halfH + margin;
                break;
            case 1: // Bottom
                x = Random.Range(-halfW, halfW);
                y = -(halfH + margin);
                break;
            case 2: // Left
                x = -(halfW + margin);
                y = Random.Range(-halfH, halfH);
                break;
            default: // Right
                x = halfW + margin;
                y = Random.Range(-halfH, halfH);
                break;
        }

        return new Vector3(x, y, 0f);
    }

    /// <summary>
    /// Enemy speed scales with wave number.
    /// </summary>
    private int GetSpeedWave()
    {
        // Waves 1-10: linear scaling
        // After wave 10: cycle back to wave 6 base and scale up to wave 10 over 5 waves
        if (currentWave <= 10) return currentWave;
        return 6 + ((currentWave - 11) % 5); // 11→6, 12→7, 13→8, 14→9, 15→10, 16→6...
    }

    private float GetEnemySpeed()
    {
        float baseSpeed = 0.8f + GetSpeedWave() * 0.15f;
        // Add some variance
        return baseSpeed * Random.Range(0.8f, 1.2f);
    }

    /// <summary>
    /// Enemy colour varies by speed/difficulty.
    /// </summary>
    private Color GetEnemyColor()
    {
        if (currentWave <= 2)
        {
            // Easy – green tones
            return new Color(0.2f, 0.9f, 0.3f);
        }
        else if (currentWave <= 5)
        {
            // Medium – yellow / orange tones
            float t = Random.Range(0f, 1f);
            return Color.Lerp(new Color(1f, 0.9f, 0.2f), new Color(1f, 0.55f, 0.1f), t);
        }
        else
        {
            // Hard – red / magenta tones
            float t = Random.Range(0f, 1f);
            return Color.Lerp(new Color(1f, 0.2f, 0.2f), new Color(0.9f, 0.2f, 0.8f), t);
        }
    }

    /// <summary>Boss enemies move 1.5× faster than normal.</summary>
    private float GetBossEnemySpeed()
    {
        float baseSpeed = 0.8f + GetSpeedWave() * 0.15f;
        return baseSpeed * 1.5f * Random.Range(0.8f, 1.2f);
    }

    /// <summary>Boss enemies are purple/magenta.</summary>
    private Color GetBossEnemyColor()
    {
        float t = Random.Range(0f, 1f);
        return Color.Lerp(new Color(0.7f, 0.15f, 1f), new Color(1f, 0.3f, 0.8f), t);
    }
}
