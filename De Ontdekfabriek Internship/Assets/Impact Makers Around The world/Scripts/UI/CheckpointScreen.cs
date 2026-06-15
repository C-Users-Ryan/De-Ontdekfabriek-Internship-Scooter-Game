using TMPro;
using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;
using KenyaScooter.Scoring;

namespace KenyaScooter.UI
{
    /// <summary>
    /// The relay checkpoint screen (Req §9.2, §13): the student's score, what it
    /// added to the class total, the rank announcement ("JULLIE STAAN OP PLEK X!")
    /// and the compact leaderboard. Population defers one frame after
    /// CheckpointReached so GameManager's commit lands first. Auto-advances to the
    /// next student.
    /// </summary>
    public sealed class CheckpointScreen : MonoBehaviour
    {
        [SerializeField] private SessionConfig config;
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text individualScoreText;
        [SerializeField] private TMP_Text groupTotalText;
        [SerializeField] private TMP_Text rankText;
        [SerializeField] private LeaderboardUI leaderboard;

        private bool pendingShow;
        private float shownAt;

        private void OnEnable()
        {
            GameEvents.CheckpointReached += QueueShow;
            GameEvents.StateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            GameEvents.CheckpointReached -= QueueShow;
            GameEvents.StateChanged -= HandleStateChanged;
        }

        private void QueueShow() => pendingShow = true;

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

            if (panel.activeSelf && Time.time - shownAt > config.checkpointAutoAdvanceSeconds)
                Next();
        }

        private void HandleStateChanged(GameState from, GameState to)
        {
            if ((to == GameState.Ready || to == GameState.Playing) && panel.activeSelf)
                panel.SetActive(false);
        }

        private void Populate()
        {
            SessionStats stats = GameManager.Instance.Stats;

            if (titleText != null)
                titleText.text = SwahiliUI.Get("CHECKPOINT_TITLE");
            if (individualScoreText != null)
                individualScoreText.text = $"+{stats.FinalScore}";

            if (GroupScoreManager.Instance != null)
            {
                if (groupTotalText != null)
                    groupTotalText.text = $"{SwahiliUI.Get("GROUP_TOTAL")}: {GroupScoreManager.Instance.GroupTotal}";
                if (rankText != null && LeaderboardManager.Instance != null)
                {
                    int rank = LeaderboardManager.Instance.RankOf(GroupScoreManager.Instance.GroupTotal);
                    rankText.text = string.Format(SwahiliUI.Get("RANK_ANNOUNCE"), rank);
                }
            }

            if (leaderboard != null)
                leaderboard.Refresh(5);
        }

        /// <summary>Wired to the "next player" button; also fired by the auto-advance.</summary>
        public void Next()
        {
            panel.SetActive(false);
            GameManager.Instance.PrepareNextTurn();
        }
    }
}
