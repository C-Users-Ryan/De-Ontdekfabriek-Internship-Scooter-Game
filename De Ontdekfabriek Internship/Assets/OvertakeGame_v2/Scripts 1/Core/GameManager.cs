using UnityEngine;
using UnityEngine.SceneManagement;

namespace OvertakeGame
{
    /// <summary>
    /// Central coordinator for game state and hazard responses.
    /// All hazard handlers live here so workshop facilitators have one place to configure
    /// toggles (deductOnX, warnOnX, gameOverOnCollision) per session type.
    ///
    /// GAME STATES
    ///   Playing       → normal gameplay
    ///   Rewinding     → post-crash rewind in progress, input paused
    ///   AtCheckpoint  → timer expired, player driving to checkpoint
    ///   GameOver      → hard crash with no rewind remaining
    ///   Finished      → session ended via timer without checkpoint system
    ///
    /// Only GameManager transitions states. External systems call the public methods
    /// (OnPlayerHitTraffic, TriggerGameOver, etc.) or SetState() — never set CurrentState directly.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        // ── Session toggles ────────────────────────────────────────────────────

        [Header("Session")]
        public bool  gameOverOnCollision = true;
        public bool  useSessionTimer     = true;
        public float sessionDuration     = 120f;
        [Tooltip("When true: hard collisions rewind 3–4 seconds instead of ending the game. " +
                 "Keeps relay sessions alive. Set false for classic instant game-over.")]
        public bool  useRewindOnCrash    = true;

        [Header("Deduction Toggles")]
        public bool deductOnCollision = true;
        public bool deductOnWrongLane = true;
        public bool deductOnSpeeding  = true;
        public bool deductOnPothole   = true;
        public bool deductOnRock      = true;

        [Header("Warning Toggles")]
        public bool warnOnCollision = true;
        public bool warnOnWrongLane = true;
        public bool warnOnSpeeding  = true;
        public bool warnOnPothole   = true;
        public bool warnOnRock      = true;

        [Header("Pothole Speed-Scaled Deduction")]
        [Tooltip("Points deducted at maximum speed. Scales to 0 at potholeSlowThresholdKmh.")]
        public int   potholeMaxDeduction     = 40;
        [Tooltip("At or below this speed (km/h), pothole deduction is zero. Rewards slowing.")]
        public float potholeSlowThresholdKmh = 15f;
        [Tooltip("At or above this speed (km/h), full potholeMaxDeduction applies.")]
        public float potholeMaxSpeedKmh      = 80f;

        [Header("Manager References")]
        public ScoreManager      scoreManager;
        public GraceSystem       graceSystem;
        public RewindSystem      rewindSystem;
        public TimerManager      timerManager;
        public UIManager         uiManager;
        public TrafficManager    trafficManager;
        public WarningSystem     warningSystem;
        public CheckpointManager checkpointManager;
        public PotholeManager    potholeManager;
        public RockManager       rockManager;

        // ── State ──────────────────────────────────────────────────────────────

        public enum GameState { Playing, Rewinding, AtCheckpoint, GameOver, Finished }
        public GameState CurrentState { get; private set; } = GameState.Playing;

        private float _sessionDistance;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            if (timerManager != null)
                timerManager.OnTimerExpired += OnTimerExpired;
            StartGame();
        }

        void OnDestroy()
        {
            if (timerManager != null)
                timerManager.OnTimerExpired -= OnTimerExpired;
        }

        // ── Session control ────────────────────────────────────────────────────

        public void StartGame()
        {
            CurrentState     = GameState.Playing;
            _sessionDistance = 0f;
            scoreManager?.ResetScore();
            graceSystem?.ResetGrace();
            rewindSystem?.ResetRewindCount();
            timerManager?.StartTimer(useSessionTimer ? sessionDuration : -1f);
            uiManager?.HideGameOverScreen();
            uiManager?.HideFinishScreen();
            uiManager?.HideCheckpointScreen();
            AnalyticsManager.I?.OnRunStart();
            AudioManager.I?.OnRestart();
        }

        public void StartNextRound()
        {
            CurrentState = GameState.Playing;
            trafficManager?.ResumeSpawning();
            potholeManager?.ResumeSpawning();
            rockManager?.ResumeSpawning();
            timerManager?.StartTimer(useSessionTimer ? sessionDuration : -1f);
            AnalyticsManager.I?.OnRunStart();
        }

        public void UpdateDistance(float distance)
        {
            _sessionDistance = distance;
            AnalyticsManager.I?.OnDistanceUpdate(distance);
        }

        // ── Hazard responses ───────────────────────────────────────────────────

        public void OnPlayerHitTraffic(float relativeSpeedKmh = float.MaxValue)
        {
            if (CurrentState != GameState.Playing) return;
            AnalyticsManager.I?.OnCrash("Matatu", _sessionDistance);
            AudioManager.I?.OnCrash();

            if (gameOverOnCollision)
            {
                bool absorbed = graceSystem != null && graceSystem.TryAbsorb(relativeSpeedKmh);
                if (absorbed)
                {
                    if (warnOnCollision) warningSystem?.ShowWarning(WarningSystem.WarningType.Collision);
                    CameraShake.Instance?.Shake();
                }
                else
                {
                    bool rewinding = useRewindOnCrash && rewindSystem != null
                                  && rewindSystem.TriggerRewind();
                    if (!rewinding) TriggerGameOver();
                }
            }
            else
            {
                if (deductOnCollision) scoreManager?.DeductPoints(ScoreManager.DeductionReason.Collision);
                if (warnOnCollision)   warningSystem?.ShowWarning(WarningSystem.WarningType.Collision);
            }
        }

        public void OnPlayerHitPothole(float speedKmh = 0f)
        {
            if (CurrentState != GameState.Playing) return;
            AnalyticsManager.I?.OnCrash("Pothole", _sessionDistance);

            if (deductOnPothole)
            {
                float fraction  = Mathf.InverseLerp(potholeSlowThresholdKmh, potholeMaxSpeedKmh, speedKmh);
                int   deduction = Mathf.RoundToInt(Mathf.Lerp(0, potholeMaxDeduction, fraction));
                if (deduction > 0)
                    scoreManager?.AddPoints(-deduction, "KUUKUA!");
            }
            if (warnOnPothole) warningSystem?.ShowWarning(WarningSystem.WarningType.Pothole);
        }

        public void OnPlayerHitRock()
        {
            if (CurrentState != GameState.Playing) return;
            AnalyticsManager.I?.OnCrash("Rock", _sessionDistance);
            if (deductOnRock) scoreManager?.DeductPoints(ScoreManager.DeductionReason.Rock);
            if (warnOnRock)   warningSystem?.ShowWarning(WarningSystem.WarningType.Rock);
        }

        public void OnPlayerInWrongLane()
        {
            if (CurrentState != GameState.Playing) return;
            if (deductOnWrongLane) scoreManager?.DeductPointsOverTime(ScoreManager.DeductionReason.WrongLane, Time.deltaTime);
            if (warnOnWrongLane)   warningSystem?.ShowWarning(WarningSystem.WarningType.WrongLane);
        }

        public void OnPlayerLightSpeeding()
        {
            if (CurrentState != GameState.Playing) return;
            if (warnOnSpeeding) warningSystem?.ShowWarning(WarningSystem.WarningType.Speeding);
        }

        public void OnPlayerSpeeding(float rateMultiplier = 1f)
        {
            if (CurrentState != GameState.Playing) return;
            if (deductOnSpeeding)
                scoreManager?.DeductPointsOverTime(ScoreManager.DeductionReason.Speeding,
                                                   Time.deltaTime * rateMultiplier);
            if (warnOnSpeeding) warningSystem?.ShowWarning(WarningSystem.WarningType.Speeding);
        }

        // ── Timer / checkpoint ─────────────────────────────────────────────────

        private void OnTimerExpired()
        {
            if (CurrentState != GameState.Playing) return;
            CurrentState = GameState.AtCheckpoint;
            potholeManager?.StopSpawning();
            rockManager?.StopSpawning();
            if (checkpointManager != null)
                checkpointManager.SpawnCheckpoint();
            else
            {
                CurrentState = GameState.Finished;
                trafficManager?.StopSpawning();
                EndSession();
            }
        }

        public void TriggerGameOver()
        {
            if (CurrentState == GameState.GameOver) return;
            CurrentState = GameState.GameOver;
            trafficManager?.StopSpawning();
            potholeManager?.StopSpawning();
            rockManager?.StopSpawning();
            int score = scoreManager?.CurrentScore ?? 0;
            GameOverScreen.I?.Show(score, _sessionDistance);
            uiManager?.ShowGameOverScreen(score);
        }

        private void EndSession()
        {
            int score = scoreManager?.CurrentScore ?? 0;
            AnalyticsManager.I?.OnRunEnd(score, _sessionDistance);
            GameOverScreen.I?.Show(score, _sessionDistance);
            uiManager?.ShowFinishScreen(score);
        }

        public void RestartGame() =>
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

        /// <summary>Direct state override for RewindSystem and CheckpointManager.</summary>
        public void SetState(GameState newState) => CurrentState = newState;

        // ── Session time helpers ───────────────────────────────────────────────

        public float SessionTime => timerManager != null
            ? Mathf.Max(0f, timerManager.SessionDuration - timerManager.TimeRemaining)
            : 0f;

        public float SessionProgress
        {
            get
            {
                if (timerManager == null || !timerManager.IsLimited) return 0f;
                float d = timerManager.SessionDuration;
                return d > 0f ? Mathf.Clamp01(1f - timerManager.TimeRemaining / d) : 0f;
            }
        }
    }
}
