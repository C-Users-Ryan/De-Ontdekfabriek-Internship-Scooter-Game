using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// LaneSafetyMonitor — Watches whether the player is on the correct side of the road.
///
/// In Kenya, drive on the LEFT side of the road.
/// Left of the centre line = correct. Right of centre line = wrong side.
///
/// EFFECTS:
///   Correct side — safe-driving streak builds, score multiplier increases
///   Wrong side   — streak breaks, multiplier drops, warning shown
///
/// This is the core road-safety teaching mechanism.
/// The player is NEVER punished harshly — they just earn fewer points.
/// Getting back to the correct side immediately starts recovering the streak.
/// </summary>
public class LaneSafetyMonitor : MonoBehaviour
{
    public static LaneSafetyMonitor Instance { get; private set; }

    [Header("Config")]
    public RunnerGameConfig config;

    // ── Events ────────────────────────────────────────────────────
    [Header("Events")]
    [Tooltip("Fires when the player moves to the wrong side. Bool = isOnWrongSide.")]
    public UnityEvent<bool> OnSideChanged;

    // ── Public State ──────────────────────────────────────────────
    public float CurrentMultiplier { get; private set; } = 1f;
    public bool  IsOnWrongSide     { get; private set; }
    public float StreakSeconds     { get; private set; }

    // ── Private ───────────────────────────────────────────────────
    private bool _wasOnWrongSide;
    private float _recoveryTimer;

    // ─────────────────────────────────────────────────────────────
    #region Unity Lifecycle

    void Awake() => Instance = this;

    void Start()
    {
        RunnerGameManager.Instance.OnGameStart.AddListener(ResetStreak);
    }

    void OnDestroy()
    {
        if (RunnerGameManager.Instance != null)
            RunnerGameManager.Instance.OnGameStart.RemoveListener(ResetStreak);
    }

    void Update()
    {
        if (!RunnerGameManager.Instance.IsPlaying()) return;
        if (RunnerPlayerController.Instance == null) return;
        if (config == null) return;

        CheckSide();
        UpdateMultiplier();
    }

    #endregion

    // ─────────────────────────────────────────────────────────────

    void CheckSide()
    {
        bool onWrongSide = !RunnerPlayerController.Instance.IsOnCorrectSide();

        if (onWrongSide != _wasOnWrongSide)
        {
            IsOnWrongSide = onWrongSide;
            _wasOnWrongSide = onWrongSide;
            OnSideChanged?.Invoke(onWrongSide);

            if (onWrongSide)
            {
                // Wrong side — immediately breaks streak
                StreakSeconds = 0f;
                Debug.Log("[LaneSafetyMonitor] Wrong side! Streak broken.");
            }
            else
            {
                // Back on correct side — start recovery delay
                _recoveryTimer = config.streakRecoveryDelay;
            }
        }

        if (!onWrongSide && _recoveryTimer > 0f)
        {
            _recoveryTimer -= Time.deltaTime;
        }

        // Only build streak if on correct side and recovery done
        if (!onWrongSide && _recoveryTimer <= 0f)
            StreakSeconds += Time.deltaTime;
    }

    void UpdateMultiplier()
    {
        if (IsOnWrongSide)
        {
            // Apply penalty multiplier instantly on wrong side
            CurrentMultiplier = config.wrongSidePenaltyMultiplier;
        }
        else
        {
            // Build up multiplier from streak
            float t = Mathf.Clamp01(StreakSeconds / config.secondsToMaxMultiplier);
            CurrentMultiplier = Mathf.Lerp(1f, config.maxStreakMultiplier, t);
        }
    }

    void ResetStreak()
    {
        CurrentMultiplier = 1f;
        StreakSeconds      = 0f;
        IsOnWrongSide      = false;
        _wasOnWrongSide    = false;
        _recoveryTimer     = 0f;
    }
}
