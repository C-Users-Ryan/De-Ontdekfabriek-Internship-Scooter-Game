using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// RunnerGameManager — State machine for the endless runner.
///
/// EXECUTION ORDER: Set to -100 in Edit > Project Settings > Script Execution Order.
/// This guarantees Instance exists before any other script's Start() runs.
///
/// START SEQUENCE:
///   1. Awake()  — Instance is set (order -100, runs first)
///   2. Start()  — all other scripts' Start() have now subscribed to events
///   3. One frame later — EnterAttract() fires, panels respond correctly
///
/// STATES:
///   Attract  → waiting for player input
///   Playing  → active gameplay
///   Charging → stopped at green energy station
///   GameOver → collision happened, showing score
///   Handoff  → break screen before next player
/// </summary>
public class RunnerGameManager : MonoBehaviour
{
    public static RunnerGameManager Instance { get; private set; }

    [Header("Config")]
    public RunnerGameConfig config;

    public enum GameState { Attract, Playing, Charging, GameOver, Handoff }
    public GameState CurrentState { get; private set; } = GameState.Attract;

    [Header("Events")]
    public UnityEvent OnGameStart;
    public UnityEvent OnGameOver;
    public UnityEvent OnChargingBegin;
    public UnityEvent OnChargingEnd;
    public UnityEvent OnHandoffBegin;
    public UnityEvent OnAttractBegin;

    private float _handoffTimer;

    // ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (config == null)
            Debug.LogError("[RunnerGameManager] No RunnerGameConfig assigned! " +
                           "Right-click in Project > Create > Kenya Runner > Game Config.");
    }

    void Start()
    {
        // Wait one frame so all other scripts' Start() methods have run
        // and subscribed to events before we fire OnAttractBegin
        StartCoroutine(DelayedAttract());
    }

    System.Collections.IEnumerator DelayedAttract()
    {
        yield return null; // wait one frame
        EnterAttract();
    }

    void Update()
    {
        if (CurrentState == GameState.Handoff)
        {
            _handoffTimer -= Time.deltaTime;
            if (_handoffTimer <= 0f) EnterAttract();
        }
    }

    // ─────────────────────────────────────────────────────────────
    #region Public API

    /// <summary>Call this to begin a player's run. Works from Attract or Handoff state.</summary>
    public void StartGame()
    {
        if (CurrentState != GameState.Attract && CurrentState != GameState.Handoff)
        {
            Debug.LogWarning($"[RunnerGameManager] StartGame() called from {CurrentState} — ignored.");
            return;
        }

        CurrentState = GameState.Playing;
        Debug.Log("[RunnerGameManager] → Playing");
        OnGameStart?.Invoke();
    }

    /// <summary>Called by RunnerCollisionHandler when the player hits a vehicle.</summary>
    public void TriggerGameOver()
    {
        if (CurrentState != GameState.Playing && CurrentState != GameState.Charging) return;

        CurrentState = GameState.GameOver;
        Debug.Log("[RunnerGameManager] → GameOver");
        OnGameOver?.Invoke();
        Invoke(nameof(EnterHandoff), 5f);
    }

    /// <summary>Called by ChargingManager when the interval timer fires.</summary>
    public void BeginCharging()
    {
        if (CurrentState != GameState.Playing) return;
        CurrentState = GameState.Charging;
        Debug.Log("[RunnerGameManager] → Charging");
        OnChargingBegin?.Invoke();
    }

    /// <summary>Called by ChargingManager when the stop is complete.</summary>
    public void EndCharging()
    {
        if (CurrentState != GameState.Charging) return;
        CurrentState = GameState.Playing;
        Debug.Log("[RunnerGameManager] → Playing (resumed)");
        OnChargingEnd?.Invoke();
    }

    // ── Convenience checks ────────────────────────────────────────
    public bool IsPlaying() => CurrentState == GameState.Playing;
    public bool IsCharging() => CurrentState == GameState.Charging;
    public bool IsActive() => CurrentState == GameState.Playing
                              || CurrentState == GameState.Charging;

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Private Transitions

    void EnterHandoff()
    {
        CurrentState = GameState.Handoff;
        _handoffTimer = config != null ? config.handoffDuration : 15f;
        Debug.Log("[RunnerGameManager] → Handoff");
        OnHandoffBegin?.Invoke();
    }

    void EnterAttract()
    {
        CurrentState = GameState.Attract;
        Debug.Log("[RunnerGameManager] → Attract");
        OnAttractBegin?.Invoke();
    }

    #endregion
}