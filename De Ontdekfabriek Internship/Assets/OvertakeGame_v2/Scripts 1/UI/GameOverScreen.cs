using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace OvertakeGame
{
    /// <summary>
    /// Game over screen controller.
    /// Wires together HighScoreManager, AnalyticsManager, SwahiliUI,
    /// and the "REKODI MPYA!" new record banner.
    ///
    /// SETUP:
    ///   1. Create a GameOverPanel in your Canvas (hidden by default).
    ///   2. Add TMP_Text fields for score, distance, best score, best distance.
    ///   3. Add a "REKODI MPYA!" banner Image/Panel (hidden by default).
    ///   4. Add a Play Again button.
    ///   5. Attach this script and wire all references in the Inspector.
    ///   6. Call GameOverScreen.I.Show(score, distance) from GameManager
    ///      when the game ends.
    /// </summary>
    public class GameOverScreen : MonoBehaviour
    {
        public static GameOverScreen I { get; private set; }

        [Header("Panel")]
        public GameObject panel;

        [Header("Score Labels")]
        public TMP_Text runScoreLabel;
        public TMP_Text runDistanceLabel;
        public TMP_Text bestScoreLabel;
        public TMP_Text bestDistanceLabel;
        public TMP_Text gameOverTitleLabel;
        public TMP_Text playAgainLabel;

        [Header("New Record Banner")]
        [Tooltip("Panel / Image that shows REKODI MPYA! — hidden by default.")]
        public GameObject newRecordBanner;
        [Tooltip("How long the banner stays visible before fading.")]
        public float bannerDuration = 2.5f;
        public float bannerFadeDuration = 0.5f;

        [Header("Play Again Button")]
        public Button playAgainButton;

        void Awake()
        {
            if (I != null && I != this) { Destroy(this); return; }
            I = this;
        }

        void Start()
        {
            panel?.SetActive(false);
            newRecordBanner?.SetActive(false);
            playAgainButton?.onClick.AddListener(OnPlayAgain);
            RefreshLabels();
        }

        /// <summary>
        /// Call from GameManager when a run ends.
        /// Submits score to HighScoreManager, logs to Analytics, updates UI.
        /// </summary>
        public void Show(int score, float distance)
        {
            // Submit to high score — returns true if new record
            bool isNewRecord = HighScoreManager.I != null
                && HighScoreManager.I.TrySubmit(score, distance);

            // Log to analytics
            AnalyticsManager.I?.OnRunEnd(score, distance);

            // Update UI labels
            if (runScoreLabel    != null) runScoreLabel.text    = score.ToString("D6");
            if (runDistanceLabel != null) runDistanceLabel.text = $"{Mathf.FloorToInt(distance):D4} M";
            if (bestScoreLabel   != null) bestScoreLabel.text   = HighScoreManager.I?.BestScoreDisplay    ?? "------";
            if (bestDistanceLabel!= null) bestDistanceLabel.text= HighScoreManager.I?.BestDistanceDisplay ?? "---- M";

            RefreshLabels();

            panel?.SetActive(true);

            AudioManager.I?.OnSessionEnd();

            if (isNewRecord && newRecordBanner != null)
                StartCoroutine(ShowNewRecordBanner());
        }

        public void Hide()
        {
            panel?.SetActive(false);
            newRecordBanner?.SetActive(false);
        }

        private void OnPlayAgain()
        {
            Hide();
            AudioManager.I?.OnRestart();
            GameManager.Instance?.RestartGame();
        }

        private void RefreshLabels()
        {
            if (SwahiliUI.I == null) return;
            if (gameOverTitleLabel != null) gameOverTitleLabel.text = SwahiliUI.I.Get("gameover");
            if (playAgainLabel     != null) playAgainLabel.text     = SwahiliUI.I.Get("playagain");
        }

        private IEnumerator ShowNewRecordBanner()
        {
            newRecordBanner.SetActive(true);

            // Get CanvasGroup if available for fade
            var cg = newRecordBanner.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 1f;

            yield return new WaitForSeconds(bannerDuration);

            if (cg != null)
            {
                float t = 0f;
                while (t < 1f)
                {
                    t        += Time.deltaTime / bannerFadeDuration;
                    cg.alpha  = Mathf.Lerp(1f, 0f, t);
                    yield return null;
                }
            }

            newRecordBanner.SetActive(false);
        }
    }
}