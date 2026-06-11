using UnityEngine;
using UnityEngine.SceneManagement;

namespace OvertakeGame
{
    /// <summary>
    /// Central coordinator for game state, hazard responses, and manager wiring.
    ///
    /// CHANGES FROM PREVIOUS VERSION:
    ///
    ///   GRACE SYSTEM INTEGRATION
    ///     graceSystem field added. StartGame() calls graceSystem.ResetGrace() so the
    ///     player always starts fresh even on a restart.
    ///
    ///   OnPlayerHitTraffic(float relativeSpeedKmh)
    ///     Replaces the parameterless version. OvertakeCollisionHandler now supplies the
    ///     relative speed (oncoming = sum, same-dir = difference). GameManager passes it
    ///     to GraceSystem.TryAbsorb():
    ///       • Absorbed  → warning + camera shake, NO game over.
    ///       • Not absorbed → TriggerGameOver() as before.
    ///     The old deductOnCollision / warnOnCollision path is preserved for the
    ///     gameOverOnCollision = false mode (used in accessibility/easy playthroughs).
    ///
    ///   OnPlayerHitPothole(float speedKmh)
    ///     Replaces the parameterless version. Computes a speed-scaled deduction:
    ///       fraction = InverseLerp(potholeSlowThresholdKmh, potholeMaxSpeedKmh, speedKmh)
    ///       deduction = Lerp(0, potholeMaxDeduction, fraction)
    ///     At or below potholeSlowThresholdKmh → 0 deduction (pure warning).
    ///     At or above potholeMaxSpeedKmh      → full potholeMaxDeduction.
    ///     The label "KUUKUA!" fires ScorePopupUI only if deduction > 0.
    ///
    ///   OnPlayerLightSpeeding()  [NEW]
    ///     Called by SpeedMonitor for tier-1 speeding (0–25% over). Warns only, no deduction.
    ///
    ///   OnPlayerSpeeding(float rateMultiplier = 1f)  [CHANGED signature]
    ///     Called by SpeedMonitor for tier-2/3 speeding. Multiplier allows extreme speeding
    ///     (tier 3) to deduct at a higher rate without a separate method.
    ///
    /// TUNING (all in Inspector):
    ///   potholeMaxDeduction        — deduction at full speed (default 40)
    ///   potholeSlowThresholdKmh    — below this = 0 deduction (default 15)
    ///   potholeMaxSpeedKmh         — at/above this = full deduction (default 80)
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("═══ Session Toggles ═══")]
        public bool  gameOverOnCollision = true;
        public bool  useSessionTimer     = true;
        public float sessionDuration     = 120f;

        [Header("Point Deduction Toggles")]
        public bool deductOnCollision = true;
        public bool deductOnWrongLane = true;
        public bool deductOnSpeeding  = true;
        public bool deductOnPothole   = true;
        public bool deductOnRock      = true;

        [Header("Warning Toggles")]
        public bool warnOnWrongLane = true;
        public bool warnOnSpeeding  = true;
        public bool warnOnCollision = true;
        public bool warnOnPothole   = true;
        public bool warnOnRock      = true;

        [Header("Pothole Speed-Scaled Deduction")]
        [Tooltip("Maximum points deducted when the player hits a pothole at full speed.")]
        public int   potholeMaxDeduction       = 40;
        [Tooltip("At or below this speed (km/h) the pothole deduction is 0. Rewards slowing down.")]
        public float potholeSlowThresholdKmh   = 15f;
        [Tooltip("At or above this speed (km/h) the deduction is at maximum.")]
        public float potholeMaxSpeedKmh        = 80f;

        [Header("Rewind on Crash")]
        [Tooltip("When true: hard collisions trigger a time rewind instead of game over. "
               + "Set false to restore classic instant-game-over behaviour.")]
        public bool useRewindOnCrash = true;

        [Header("═══ Manager References ═══")]
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

        public enum GameState { Playing, Rewinding, AtCheckpoint, GameOver, Finished }
        public GameState CurrentState { get; private set; } = GameState.Playing;

        private float _sessionDistance;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start() => StartGame();

        public void StartGame()
        {
            CurrentState     = GameState.Playing;
            _sessionDistance = 0f;
            scoreManager?.ResetScore();
            graceSystem?.ResetGrace();    // always start with full grace shield
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

        /// <summary>
        /// Called by OvertakeCollisionHandler with the relative speed of the impact.
        /// Relative speed drives GraceSystem's light/hard classification:
        ///   • Light hit (absorbed) → warn + shake, no game over
        ///   • Hard hit (not absorbed) → TriggerGameOver
        /// When gameOverOnCollision = false (accessibility mode) the grace path is skipped
        /// and the legacy deduct-only path runs instead.
        /// </summary>
        public void OnPlayerHitTraffic(float relativeSpeedKmh = float.MaxValue)
        {
            if (CurrentState != GameState.Playing) return;
            AnalyticsManager.I?.OnCrash("Matatu", _sessionDistance);
            AudioManager.I?.OnCrash();

            if (gameOverOnCollision)
            {
                bool absorbed = graceSystem != null && graceSystem.TryAbsorb(relativeSpeedKmh);
                if (!absorbed)
                {
                    // Grace failed — try rewind before triggering game over
                    bool rewinding = useRewindOnCrash && rewindSystem != null
                                  && rewindSystem.TriggerRewind();
                    if (!rewinding)
                        TriggerGameOver();
                }
                else
                {
                    // Grace absorbed — give the player a visual signal
                    if (warnOnCollision) warningSystem?.ShowWarning(WarningSystem.WarningType.Collision);
                    CameraShake.Instance?.Shake();
                }
            }
            else
            {
                // Accessibility mode: no game over ever — just deduct + warn
                if (deductOnCollision) scoreManager?.DeductPoints(ScoreManager.DeductionReason.Collision);
                if (warnOnCollision)   warningSystem?.ShowWarning(WarningSystem.WarningType.Collision);
            }
        }

        /// <summary>
        /// Called by Pothole with the player's speed at the moment of impact.
        /// Deduction scales from 0 (at slow threshold) to potholeMaxDeduction (at max speed).
        /// "KUUKUA!" popup fires only when the deduction is greater than 0.
        /// </summary>
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

        /// <summary>
        /// Tier-1 speeding (0–25% over limit). Warns only — no deduction.
        /// Called by SpeedMonitor when the player is slightly over the limit.
        /// </summary>
        public void OnPlayerLightSpeeding()
        {
            if (CurrentState != GameState.Playing) return;
            if (warnOnSpeeding) warningSystem?.ShowWarning(WarningSystem.WarningType.Speeding);
        }

        /// <summary>
        /// Tier-2/3 speeding (25%+ over limit). Warns and deducts points over time.
        /// rateMultiplier > 1 for extreme speeding (tier 3 = 2× deduction rate).
        /// </summary>
        public void OnPlayerSpeeding(float rateMultiplier = 1f)
        {
            if (CurrentState != GameState.Playing) return;
            if (deductOnSpeeding)
            {
                float dt = Time.deltaTime * rateMultiplier;
                scoreManager?.DeductPointsOverTime(ScoreManager.DeductionReason.Speeding, dt);
            }
            if (warnOnSpeeding) warningSystem?.ShowWarning(WarningSystem.WarningType.Speeding);
        }

        // ── Timer / checkpoint ─────────────────────────────────────────────────

        public void OnTimerExpired()
        {
            if (CurrentState != GameState.Playing) return;
            CurrentState = GameState.AtCheckpoint;
            potholeManager?.StopSpawning();
            rockManager?.StopSpawning();
            if (checkpointManager != null) checkpointManager.SpawnCheckpoint();
            else { CurrentState = GameState.Finished; trafficManager?.StopSpawning(); EndSession(); }
        }

        public void TriggerGameOver()
        {
            if (CurrentState == GameState.GameOver) return;
            CurrentState = GameState.GameOver;
            trafficManager?.StopSpawning();
            potholeManager?.StopSpawning();
            rockManager?.StopSpawning();
            AudioManager.I?.OnCrash();
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

        public void RestartGame()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>
        /// Allows external systems (RewindSystem, CheckpointManager) to transition
        /// game state directly. Prefer the specific public methods above where possible.
        /// </summary>
        public void SetState(GameState newState)
        {
            CurrentState = newState;
        }

        // ── Session time helpers ───────────────────────────────────────────────

        public float SessionTime => timerManager != null
            ? Mathf.Max(0f, timerManager.SessionDuration - timerManager.TimeRemaining)
            : 0f;

        public float SessionProgress
        {
            get
            {
                if (timerManager == null || !timerManager.IsLimited) return 0f;
                float duration = timerManager.SessionDuration;
                if (duration <= 0f) return 0f;
                return Mathf.Clamp01(1f - (timerManager.TimeRemaining / duration));
            }
        }
    }
}
