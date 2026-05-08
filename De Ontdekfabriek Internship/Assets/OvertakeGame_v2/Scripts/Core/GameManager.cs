using UnityEngine;
using UnityEngine.SceneManagement;

namespace OvertakeGame
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("═══ Session Toggles ═══")]
        [Tooltip("ON = Game over on collision. OFF = Deduct points instead.")]
        public bool gameOverOnCollision = true;
        public bool useSessionTimer  = true;
        public float sessionDuration = 120f;

        [Header("Point Deduction Toggles")]
        public bool deductOnCollision = true;
        public bool deductOnWrongLane = true;
        public bool deductOnSpeeding  = true;
        public bool deductOnPothole   = true;

        [Header("Warning Toggles")]
        public bool warnOnWrongLane = true;
        public bool warnOnSpeeding  = true;
        public bool warnOnCollision = true;
        public bool warnOnPothole   = true;

        [Header("═══ Manager References ═══")]
        public ScoreManager      scoreManager;
        public TimerManager      timerManager;
        public UIManager         uiManager;
        public TrafficManager    trafficManager;
        public WarningSystem     warningSystem;
        public CheckpointManager checkpointManager;
        public PotholeManager    potholeManager;

        public enum GameState { Playing, AtCheckpoint, GameOver, Finished }
        public GameState CurrentState { get; private set; } = GameState.Playing;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start() => StartGame();

        public void StartGame()
        {
            CurrentState = GameState.Playing;
            scoreManager?.ResetScore();
            timerManager?.StartTimer(useSessionTimer ? sessionDuration : -1f);
            uiManager?.HideGameOverScreen();
            uiManager?.HideFinishScreen();
            uiManager?.HideCheckpointScreen();
        }

        public void StartNextRound()
        {
            CurrentState = GameState.Playing;
            trafficManager?.ResumeSpawning();
            potholeManager?.ResumeSpawning();
            timerManager?.StartTimer(useSessionTimer ? sessionDuration : -1f);
        }

        public void OnPlayerHitTraffic()
        {
            if (CurrentState != GameState.Playing) return;
            if (gameOverOnCollision)
                TriggerGameOver();
            else
            {
                if (deductOnCollision)
                    scoreManager?.DeductPoints(ScoreManager.DeductionReason.Collision);
                if (warnOnCollision)
                    warningSystem?.ShowWarning(WarningSystem.WarningType.Collision);
            }
        }

        public void OnPlayerHitPothole()
        {
            if (CurrentState != GameState.Playing) return;
            if (deductOnPothole)
                scoreManager?.DeductPoints(ScoreManager.DeductionReason.Pothole);
            if (warnOnPothole)
                warningSystem?.ShowWarning(WarningSystem.WarningType.Pothole);
        }

        public void OnPlayerInWrongLane()
        {
            if (CurrentState != GameState.Playing) return;
            if (deductOnWrongLane)
                scoreManager?.DeductPointsOverTime(ScoreManager.DeductionReason.WrongLane, Time.deltaTime);
            if (warnOnWrongLane)
                warningSystem?.ShowWarning(WarningSystem.WarningType.WrongLane);
        }

        public void OnPlayerSpeeding()
        {
            if (CurrentState != GameState.Playing) return;
            if (deductOnSpeeding)
                scoreManager?.DeductPointsOverTime(ScoreManager.DeductionReason.Speeding, Time.deltaTime);
            if (warnOnSpeeding)
                warningSystem?.ShowWarning(WarningSystem.WarningType.Speeding);
        }

        public void OnTimerExpired()
        {
            if (CurrentState != GameState.Playing) return;
            CurrentState = GameState.AtCheckpoint;
            potholeManager?.StopSpawning();

            if (checkpointManager != null)
                checkpointManager.SpawnCheckpoint();
            else
            {
                CurrentState = GameState.Finished;
                trafficManager?.StopSpawning();
                uiManager?.ShowFinishScreen(scoreManager?.CurrentScore ?? 0);
            }
        }

        public void TriggerGameOver()
        {
            if (CurrentState == GameState.GameOver) return;
            CurrentState = GameState.GameOver;
            trafficManager?.StopSpawning();
            potholeManager?.StopSpawning();
            uiManager?.ShowGameOverScreen(scoreManager?.CurrentScore ?? 0);
        }

        public void RestartGame()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
