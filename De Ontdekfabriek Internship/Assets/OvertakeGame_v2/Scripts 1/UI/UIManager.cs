using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace OvertakeGame
{
    /// <summary>
    /// Manages all HUD elements and overlay screens.
    /// Wire every reference in the Inspector.
    /// Uses SwahiliUI for localised button/label text where available.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [Header("HUD")]
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
        public GameObject checkpointPanel;
        public TMP_Text   checkpointRoundText;
        public TMP_Text   checkpointScoreText;
        public TMP_Text   checkpointCountdownText;

        [Header("Manager References")]
        public ScoreManager scoreManager;
        public TimerManager timerManager;

        private float _checkpointCountdown;
        private bool  _checkpointActive;

        void Start()
        {
            if (scoreManager != null) scoreManager.OnScoreChanged += UpdateScoreDisplay;

            restartButtonGameOver?.onClick.AddListener(() => GameManager.Instance?.RestartGame());
            restartButtonFinish  ?.onClick.AddListener(() => GameManager.Instance?.RestartGame());

            HideGameOverScreen();
            HideFinishScreen();
            HideCheckpointScreen();

            timerPanel?.SetActive(timerManager != null && timerManager.IsLimited);
            UpdateScoreDisplay(scoreManager?.CurrentScore ?? 0);
            RefreshSwahiliLabels();
        }

        void Update()
        {
            if (timerManager != null && timerManager.IsLimited && timerText != null)
                timerText.text = timerManager.GetFormattedTime();

            if (_checkpointActive && _checkpointCountdown > 0f)
            {
                _checkpointCountdown -= Time.deltaTime;
                if (checkpointCountdownText != null)
                    checkpointCountdownText.text = $"{SwahiliUI.I?.Get("playagain") ?? "Next round"} in {Mathf.CeilToInt(Mathf.Max(0f, _checkpointCountdown))}s";
            }
        }

        private void UpdateScoreDisplay(int score)
        {
            if (scoreText != null)
                scoreText.text = $"{SwahiliUI.I?.Get("score") ?? "SCORE"}  {score}";
        }

        private void RefreshSwahiliLabels()
        {
            if (SwahiliUI.I == null) return;
            UpdateScoreDisplay(scoreManager?.CurrentScore ?? 0);
        }

        public void ShowGameOverScreen(int finalScore)
        {
            gameOverPanel?.SetActive(true);
            if (gameOverScoreText != null)
                gameOverScoreText.text = $"{SwahiliUI.I?.Get("gameover") ?? "GAME OVER"}\n{finalScore}";
        }
        public void HideGameOverScreen() => gameOverPanel?.SetActive(false);

        public void ShowFinishScreen(int finalScore)
        {
            finishPanel?.SetActive(true);
            if (finishScoreText != null)
                finishScoreText.text = $"{SwahiliUI.I?.Get("score") ?? "SCORE"}: {finalScore}";
        }
        public void HideFinishScreen() => finishPanel?.SetActive(false);

        public void ShowCheckpointScreen(int roundCompleted, int turnScore, int groupTotal, int rank, float countdown)
        {
            checkpointPanel?.SetActive(true);
            _checkpointActive    = true;
            _checkpointCountdown = countdown;
            if (checkpointRoundText  != null)
                checkpointRoundText.text = $"Round {roundCompleted} Complete!";
            if (checkpointScoreText  != null)
                checkpointScoreText.text = $"Turn: {turnScore}  |  Groep: {groupTotal}  |  Plek #{rank}";
            if (checkpointCountdownText != null)
                checkpointCountdownText.text = $"Next round in {Mathf.CeilToInt(countdown)}s";
        }
        public void HideCheckpointScreen() { _checkpointActive = false; checkpointPanel?.SetActive(false); }
    }
}