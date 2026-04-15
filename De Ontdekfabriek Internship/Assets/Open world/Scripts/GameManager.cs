using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// GameManager — Central state machine for the Nairobi Scooter Game.
/// Attach to a persistent GameObject in your scene (DontDestroyOnLoad).
/// All other managers listen to the events fired here.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // ── Game States ──────────────────────────────────────────────
    public enum GameState
    {
        Attract,    // Waiting for first player / between sessions
        Playing,    // Active gameplay
        GameOver,   // Timer expired — showing score
        Handoff     // Break screen before next player
    }

    public GameState CurrentState { get; private set; } = GameState.Attract;

    // ── Session Settings ─────────────────────────────────────────
    [Header("Session Settings")]
    [Tooltip("How long each player's session lasts in seconds.")]
    public float sessionDuration = 120f;   // 2 minutes

    [Tooltip("How long the handoff/break screen is shown before auto-resetting.")]
    public float handoffDuration = 15f;

    // ── Events (other scripts subscribe to these) ─────────────────
    [Header("Events")]
    public UnityEvent OnGameStart;
    public UnityEvent OnGameOver;
    public UnityEvent OnHandoffBegin;
    public UnityEvent OnAttractBegin;

    // ── Internal ──────────────────────────────────────────────────
    private float _handoffTimer;

    // ─────────────────────────────────────────────────────────────
    #region Unity Lifecycle

    void Awake()
    {
        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        EnterAttract();
    }

    void Update()
    {
        if (CurrentState == GameState.Handoff)
        {
            _handoffTimer -= Time.deltaTime;
            if (_handoffTimer <= 0f)
                EnterAttract();
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region State Transitions

    /// <summary>
    /// Call this when the player presses Start on the attract/handoff screen.
    /// </summary>
    public void StartGame()
    {
        if (CurrentState != GameState.Attract && CurrentState != GameState.Handoff)
            return;

        CurrentState = GameState.Playing;
        Debug.Log("[GameManager] State → Playing");
        OnGameStart?.Invoke();
    }

    /// <summary>
    /// Called by TimerSystem when the countdown hits zero.
    /// </summary>
    public void TriggerGameOver()
    {
        if (CurrentState != GameState.Playing)
            return;

        CurrentState = GameState.GameOver;
        Debug.Log("[GameManager] State → GameOver");
        OnGameOver?.Invoke();

        // Automatically move to Handoff after a short delay
        Invoke(nameof(EnterHandoff), 5f);
    }

    private void EnterHandoff()
    {
        CurrentState = GameState.Handoff;
        _handoffTimer = handoffDuration;
        Debug.Log("[GameManager] State → Handoff");
        OnHandoffBegin?.Invoke();
    }

    private void EnterAttract()
    {
        CurrentState = GameState.Attract;
        Debug.Log("[GameManager] State → Attract");
        OnAttractBegin?.Invoke();
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Public Helpers

    public bool IsPlaying() => CurrentState == GameState.Playing;

    #endregion
}
