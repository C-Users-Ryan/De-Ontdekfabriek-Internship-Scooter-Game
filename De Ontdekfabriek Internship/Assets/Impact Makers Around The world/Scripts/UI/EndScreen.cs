using TMPro;
using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;
using KenyaScooter.Scoring;

namespace KenyaScooter.UI
{
    /// <summary>
    /// Game-over and finish screens (Req §12.2) — one script, two configured
    /// instances (showOn = GameOver / Finished). Shows the score, distance, the
    /// session breakdown (a mirror, not a judgement — MDA A6), the group total and
    /// "REKODI MPYA!" on a new personal record. Population is deferred one frame so
    /// GameManager's turn commit always lands first. Auto-advances for the relay.
    /// </summary>
    public sealed class EndScreen : MonoBehaviour
    {
        [SerializeField] private GameState showOn = GameState.Finished;
        [SerializeField] private SessionConfig config;
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text breakdownText;
        [SerializeField] private TMP_Text groupTotalText;
        [SerializeField] private GameObject newRecordBadge;

        private bool pendingShow;
        private float shownAt;

        private void OnEnable() => GameEvents.StateChanged += HandleStateChanged;
        private void OnDisable() => GameEvents.StateChanged -= HandleStateChanged;

        private void Update()
        {
            if (pendingShow)
            {
                pendingShow = false;
                Populate();
                panel.SetActive(true);
                shownAt = Time.time;
                return;
            }

            if (panel.activeSelf && Time.time - shownAt > config.endScreenAutoAdvanceSeconds)
                Next();
        }

        private void HandleStateChanged(GameState from, GameState to)
        {
            if (to == showOn)
                pendingShow = true;
            else if (panel.activeSelf)
                panel.SetActive(false);
        }

        private void Populate()
        {
            SessionStats stats = GameManager.Instance.Stats;

            if (titleText != null)
                titleText.text = SwahiliUI.Get(showOn == GameState.GameOver ? "GAME_OVER" : "FINISH_TITLE");
            if (scoreText != null)
                scoreText.text = stats.FinalScore.ToString();

            if (breakdownText != null)
            {
                // The end screen is a mirror: here is exactly what you did (MDA A6).
                breakdownText.text =
                    $"INGEHAALD: {stats.Overtakes}   ×{stats.BestStreak} STREAK\n" +
                    $"NEAR MISS: {stats.NearMisses}   GRACE: {stats.GracesUsed}   REWIND: {stats.RewindsUsed}\n" +
                    $"{stats.DistanceMetres / 1000f:0.0} KM";
            }

            if (groupTotalText != null && GroupScoreManager.Instance != null)
                groupTotalText.text = $"{SwahiliUI.Get("GROUP_TOTAL")}: {GroupScoreManager.Instance.GroupTotal}";

            if (newRecordBadge != null)
                newRecordBadge.SetActive(HighScoreManager.TrySubmit(stats.FinalScore));
        }

        /// <summary>Wired to the screen's button; also fired by the auto-advance.</summary>
        public void Next()
        {
            panel.SetActive(false);
            GameManager.Instance.PrepareNextTurn();
        }
    }
}
