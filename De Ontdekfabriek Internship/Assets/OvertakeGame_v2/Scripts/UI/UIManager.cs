using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace OvertakeGame
{
    public class UIManager : MonoBehaviour
    {
        [Header("HUD Elements")]
        public TMP_Text   scoreText;
        public TMP_Text   timerText;
        public GameObject timerPanel;

        [Header("Game Over Screen")]
        public GameObject gameOverPanel;
        public TMP_Text   gameOverScoreText;
        public Button     restartButtonGameOver;

        [Header("Finish Screen")]
        public GameObject finishPanel;
        public TMP_Text   finishScoreText;
        public Button     restartButtonFinish;

        [Header("Checkpoint Screen")]
        [Tooltip("Panel shown during the pause at a checkpoint between rounds.")]
        public GameObject checkpointPanel;
        [Tooltip("Text showing which round just completed.")]
        public TMP_Text   checkpointRoundText;
        [Tooltip("Text showing score at checkpoint.")]
        public TMP_Text   checkpointScoreText;
        [Tooltip("Countdown text showing seconds until next round starts.")]
        public TMP_Text   checkpointCountdownText;

        [Header("Manager References")]
        public ScoreManager scoreManager;
        public TimerManager timerManager;

        private float _checkpointCountdown;
        private bool  _checkpointActive;

        void Start()
        {
            if (scoreManager != null)
                scoreManager.OnScoreChanged += UpdateScoreDisplay;

            restartButtonGameOver?.onClick.AddListener(() => GameManager.Instance?.RestartGame());
            restartButtonFinish?.onClick.AddListener(()   => GameManager.Instance?.RestartGame());

            HideGameOverScreen();
            HideFinishScreen();
            HideCheckpointScreen();

            timerPanel?.SetActive(timerManager != null && timerManager.IsLimited);
            UpdateScoreDisplay(scoreManager?.CurrentScore ?? 0);
        }

        void Update()
        {
            // Timer display
            if (timerManager != null && timerManager.IsLimited && timerText != null)
                timerText.text = timerManager.GetFormattedTime();

            // Checkpoint countdown
            if (_checkpointActive && _checkpointCountdown > 0f)
            {
                _checkpointCountdown -= Time.deltaTime;
                if (checkpointCountdownText != null)
                    checkpointCountdownText.text = $"Next round in {Mathf.CeilToInt(Mathf.Max(0f, _checkpointCountdown))}s";
            }
        }

        private void UpdateScoreDisplay(int score)
        {
            if (scoreText != null) scoreText.text = $"Score: {score}";
        }

        // ─── Game Over ────────────────────────────────────────────────────────
        public void ShowGameOverScreen(int finalScore)
        {
            gameOverPanel?.SetActive(true);
            if (gameOverScoreText != null)
                gameOverScoreText.text = $"Game Over\nFinal Score: {finalScore}";
        }
        public void HideGameOverScreen() => gameOverPanel?.SetActive(false);

        // ─── Finish ───────────────────────────────────────────────────────────
        public void ShowFinishScreen(int finalScore)
        {
            finishPanel?.SetActive(true);
            if (finishScoreText != null)
                finishScoreText.text = $"Finished!\nFinal Score: {finalScore}";
        }
        public void HideFinishScreen() => finishPanel?.SetActive(false);

        // ─── Checkpoint ───────────────────────────────────────────────────────
        public void ShowCheckpointScreen(int roundJustCompleted, int score, float countdown)
        {
            checkpointPanel?.SetActive(true);
            _checkpointActive    = true;
            _checkpointCountdown = countdown;

            if (checkpointRoundText != null)
                checkpointRoundText.text = $"Round {roundJustCompleted} Complete!";
            if (checkpointScoreText != null)
                checkpointScoreText.text = $"Score: {score}";
            if (checkpointCountdownText != null)
                checkpointCountdownText.text = $"Next round in {Mathf.CeilToInt(countdown)}s";
        }

        public void HideCheckpointScreen()
        {
            _checkpointActive = false;
            checkpointPanel?.SetActive(false);
        }
    }
}
