using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// Handles live keyboard input and auto-matches typed letters to enemy pinyin.
/// Uses the new Input System — listens for key presses via Keyboard.current.
/// No need to click or select — just start typing.
/// </summary>
public class TypingManager : MonoBehaviour
{
    private Enemy currentTarget;
    private Keyboard kb;
    private Coroutine clearCoroutine;

    /// <summary>The currently typed text (for HUD display).</summary>
    public string TypedText { get; private set; } = "";

    /// <summary>Fired whenever the typed text changes.</summary>
    public event System.Action<string> OnTypedTextChanged;

    void OnEnable()
    {
        kb = Keyboard.current;
    }

    void Update()
    {
        if (GameManager.Instance != null &&
            GameManager.Instance.GameState != GameManager.State.Playing)
            return;

        if (kb == null)
        {
            kb = Keyboard.current;
            if (kb == null) return;
        }

        // If current target was destroyed externally, clear it
        if (currentTarget != null && currentTarget.Equals(null))
        {
            currentTarget = null;
        }

        // Backspace — reset current target
        if (kb.backspaceKey.wasPressedThisFrame)
        {
            ResetTarget();
            SetTypedText("");
        }

        // Escape — reset current target
        if (kb.escapeKey.wasPressedThisFrame)
        {
            ResetTarget();
            SetTypedText("");
        }

        // Check all letter keys (a-z)
        for (Key k = Key.A; k <= Key.Z; k++)
        {
            if (kb[k].wasPressedThisFrame)
            {
                char c = (char)('a' + (k - Key.A));
                ProcessLetter(c);
            }
        }
    }

    // ── Core typing logic ────────────────────────────────────────────

    private void ProcessLetter(char c)
    {
        // 1. If we already have a target, try to advance the match
        if (currentTarget != null)
        {
            if (currentTarget.TryMatchLetter(c))
            {
                SetTypedText(TypedText + c);

                // Check for full word completion
                if (currentTarget.IsFullyMatched)
                {
                    currentTarget.Die();
                    currentTarget = null;
                    DelayedClear();
                }
                return;
            }
            else
            {
                // Wrong letter — don't switch target, just ignore
                return;
            }
        }

        // 2. No current target — find a new enemy whose word starts with this letter
        Enemy best = FindBestTarget(c);
        if (best != null)
        {
            currentTarget = best;
            currentTarget.IsTargeted = true;
            currentTarget.TryMatchLetter(c);
            SetTypedText("" + c);

            // Could already be a 1-letter word
            if (currentTarget.IsFullyMatched)
            {
                currentTarget.Die();
                currentTarget = null;
                DelayedClear();
            }
        }
    }

    /// <summary>
    /// Find the closest enemy (to centre) whose word starts with the given letter
    /// and that isn't already being targeted.
    /// </summary>
    private Enemy FindBestTarget(char c)
    {
        Enemy best = null;
        float bestDist = float.MaxValue;

        for (int i = Enemy.ActiveEnemies.Count - 1; i >= 0; i--)
        {
            Enemy e = Enemy.ActiveEnemies[i];
            if (e == null) continue;
            if (e.MatchedLetters > 0) continue; // already partially matched

            if (e.Word.Length > 0 &&
                char.ToLower(e.Word[0]) == char.ToLower(c))
            {
                float dist = Vector3.Distance(e.transform.position, Vector3.zero);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = e;
                }
            }
        }

        return best;
    }

    private void ResetTarget()
    {
        if (currentTarget != null)
        {
            currentTarget.ResetMatch();
            currentTarget = null;
        }
    }

    private void SetTypedText(string text)
    {
        if (clearCoroutine != null)
        {
            StopCoroutine(clearCoroutine);
            clearCoroutine = null;
        }
        TypedText = text;
        OnTypedTextChanged?.Invoke(text);
    }

    private void DelayedClear()
    {
        if (clearCoroutine != null)
            StopCoroutine(clearCoroutine);
        clearCoroutine = StartCoroutine(ClearAfterDelay());
    }

    private System.Collections.IEnumerator ClearAfterDelay()
    {
        yield return new WaitForSeconds(0.2f);
        SetTypedText("");
        clearCoroutine = null;
    }
}
