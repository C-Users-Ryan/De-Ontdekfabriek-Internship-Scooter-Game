using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using KenyaScooter.Core;
using KenyaScooter.Scoring;
using KenyaScooter.Session;

namespace KenyaScooter.UI
{
    /// <summary>
    /// Facilitator panel (Req §12.5): Tab on desktop (or a hidden inspector-wired
    /// button on device) toggles it. Shows the analytics report; buttons reset
    /// analytics, high scores, the leaderboard, commit the current group, or force-
    /// reset the session. Students never see this — it is workshop plumbing.
    /// </summary>
    public sealed class DebugPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text reportText;

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame)
                TogglePanel();
        }

        /// <summary>Also wired to the hidden on-device button.</summary>
        public void TogglePanel()
        {
            bool opening = !panel.activeSelf;
            panel.SetActive(opening);
            if (opening)
                RefreshReport();
        }

        public void ResetAnalytics()
        {
            if (AnalyticsManager.Instance != null)
                AnalyticsManager.Instance.ResetAnalytics();
            RefreshReport();
        }

        public void ResetHighScores()
        {
            HighScoreManager.ResetHighScore();
            RefreshReport();
        }

        public void ResetLeaderboard()
        {
            if (LeaderboardManager.Instance != null)
                LeaderboardManager.Instance.ResetLeaderboard();
            RefreshReport();
        }

        /// <summary>Closes out the current class: commits the group total to the leaderboard, then starts a fresh group.</summary>
        public void CommitGroupAndStartNew()
        {
            if (LeaderboardManager.Instance != null && GroupScoreManager.Instance != null)
            {
                LeaderboardManager.Instance.CommitGroup(GroupScoreManager.Instance.GroupTotal);
                GroupScoreManager.Instance.ResetGroup();
            }
            RefreshReport();
        }

        public void ForceReset()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.ForceReset();
        }

        private void RefreshReport()
        {
            if (reportText != null && AnalyticsManager.Instance != null)
                reportText.text = AnalyticsManager.Instance.GenerateReport();
        }
    }
}
