using UnityEngine;
using UnityEngine.SceneManagement;

namespace OvertakeGame
{
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

        [Header("═══ Manager References ═══")]
        public ScoreManager      scoreManager;
        public TimerManager      timerManager;
        public UIManager         uiManager;
        public TrafficManager    trafficManager;
        public WarningSystem     warningSystem;
        public CheckpointManager checkpointManager;
        public PotholeManager    potholeManager;
        public RockManager       rockManager;

        public enum GameState { Playing, AtCheckpoint, GameOver, Finished }
        public GameState CurrentState { get; private set; } = GameState.Playing;

        /// <summary>Seconds elapsed since the current session started.</summary>
        public float SessionTime => timerManager != null
            ? Mathf.Max(0f, timerManager.SessionDuration - timerManager.TimeRemaining)
            : 0f;

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

        /// <summary>Call each frame with total distance this run (from a distance tracker or WorldSpeed accumulator).</summary>
        public void UpdateDistance(float distance)
        {
            _sessionDistance = distance;
            AnalyticsManager.I?.OnDistanceUpdate(distance);
        }

        public void OnPlayerHitTraffic()
        {
            if (CurrentState != GameState.Playing) return;
            AnalyticsManager.I?.OnCrash("Matatu", _sessionDistance);
            AudioManager.I?.OnCrash();
            if (gameOverOnCollision) TriggerGameOver();
            else
            {
                if (deductOnCollision) scoreManager?.DeductPoints(ScoreManager.DeductionReason.Collision);
                if (warnOnCollision)   warningSystem?.ShowWarning(WarningSystem.WarningType.Collision);
            }
        }

        public void OnPlayerHitPothole()
        {
            if (CurrentState != GameState.Playing) return;
            AnalyticsManager.I?.OnCrash("Pothole", _sessionDistance);
            if (deductOnPothole) scoreManager?.DeductPoints(ScoreManager.DeductionReason.Pothole);
            if (warnOnPothole)   warningSystem?.ShowWarning(WarningSystem.WarningType.Pothole);
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

        public void OnPlayerSpeeding()
        {
            if (CurrentState != GameState.Playing) return;
            if (deductOnSpeeding) scoreManager?.DeductPointsOverTime(ScoreManager.DeductionReason.Speeding, Time.deltaTime);
            if (warnOnSpeeding)   warningSystem?.ShowWarning(WarningSystem.WarningType.Speeding);
        }

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
    }
}
