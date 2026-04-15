using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// TimerSystem — Manages the session countdown timer.
/// Fires events for time warnings and game over.
/// Other systems (PassengerManager, ScoringSystem) call AddTime() on successful delivery.
/// </summary>
public class TimerSystem : MonoBehaviour
{
    public static TimerSystem Instance { get; private set; }

    // ── Settings ──────────────────────────────────────────────────
    [Header("Timer")]
    [Tooltip("Starting time in seconds. Pulled from GameManager.sessionDuration at runtime.")]
    public float timeRemaining;

    [Tooltip("Time (seconds) added when a passenger is delivered.")]
    public float timeExtensionOnDelivery = 15f;

    [Tooltip("When time drops below this, the warning state triggers.")]
    public float warningThreshold = 20f;

    // ── Events ────────────────────────────────────────────────────
    [Header("Events")]
    public UnityEvent OnTimerWarning;   // Fires once when time < warningThreshold
    public UnityEvent OnTimerExpired;   // Fires when time hits 0

    // ── Public State ──────────────────────────────────────────────
    public bool IsWarning  { get; private set; }
    public bool IsExpired  { get; private set; }
    public float Normalised => Mathf.Clamp01(timeRemaining /
        GameManager.Instance.sessionDuration);

    // ── Private ───────────────────────────────────────────────────
    private bool _running;

    // ─────────────────────────────────────────────────────────────
    #region Unity Lifecycle

    void Awake()
    {
        Instance = this;
    }

    void OnEnable()
    {
        GameManager.Instance.OnGameStart.AddListener(StartTimer);
        GameManager.Instance.OnGameOver.AddListener(StopTimer);
    }

    void OnDisable()
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.OnGameStart.RemoveListener(StartTimer);
        GameManager.Instance.OnGameOver.RemoveListener(StopTimer);
    }

    void Update()
    {
        if (!_running) return;

        timeRemaining -= Time.deltaTime;

        // Warning check
        if (!IsWarning && timeRemaining <= warningThreshold)
        {
            IsWarning = true;
            OnTimerWarning?.Invoke();
        }

        // Expiry check
        if (timeRemaining <= 0f)
        {
            timeRemaining = 0f;
            Expire();
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Public API

    /// <summary>Add seconds to the clock (called on passenger delivery).</summary>
    public void AddTime(float seconds)
    {
        timeRemaining = Mathf.Min(
            timeRemaining + seconds,
            GameManager.Instance.sessionDuration); // cap at session max

        // Reset warning if we recovered enough time
        if (IsWarning && timeRemaining > warningThreshold)
            IsWarning = false;
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Internal

    void StartTimer()
    {
        timeRemaining = GameManager.Instance.sessionDuration;
        IsWarning     = false;
        IsExpired     = false;
        _running      = true;
    }

    void StopTimer()
    {
        _running = false;
    }

    void Expire()
    {
        IsExpired = true;
        _running  = false;
        OnTimerExpired?.Invoke();
        GameManager.Instance.TriggerGameOver();
    }

    #endregion
}
