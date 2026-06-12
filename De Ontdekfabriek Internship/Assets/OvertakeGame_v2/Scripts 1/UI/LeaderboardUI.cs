using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections;
using System.Collections.Generic;

namespace OvertakeGame
{
    /// <summary>
    /// Displays the group leaderboard at two moments in the game:
    ///   1. At each checkpoint — compact mid-session ranking panel.
    ///   2. At the final end screen — full leaderboard shown at the end of the
    ///      last student's turn (teacher triggers, or after the final checkpoint).
    ///
    /// DISPLAYS:
    ///
    ///   CHECKPOINT PANEL (compact, shown during handoff screen):
    ///     Top 5 entries + current group's position highlighted.
    ///     "JULLIE STAAN OP PLEK [N]!" large announcement.
    ///     Intended to motivate: "we're in 3rd, let's catch up next turn."
    ///
    ///   FINAL LEADERBOARD (full screen):
    ///     Top 10 entries as a scrollable list.
    ///     Each row: rank, group name, checkpoints reached, total score.
    ///     Current session's row is highlighted in a different colour.
    ///     "NIEUW RECORD!" banner fires if the group topped the board.
    ///     Per-checkpoint breakdown expandable (tap row to see stop-by-stop scores).
    ///
    /// ENTRY ROW PREFAB:
    ///   Create a prefab with these TMP_Text children:
    ///     rankText, nameText, scoreText, checkpointsText
    ///   Assign it to entryRowPrefab.
    ///   Optionally add a background Image for the highlight colour.
    ///
    /// ADMIN RESET:
    ///   The admin panel (hidden behind a long-press on the logo for 3 seconds)
    ///   shows a confirmation dialog and calls LeaderboardManager.ResetLeaderboard().
    ///   This is for facilitators at the start of a new exhibition period.
    ///
    /// SETUP:
    ///   Attach to the Canvas root. Assign all references in Inspector.
    ///   The two panels (checkpointPanel, finalPanel) should be separate GameObjects
    ///   that this component activates/deactivates as needed.
    /// </summary>
    public class LeaderboardUI : MonoBehaviour
    {
        // ── Checkpoint panel ───────────────────────────────────────────────────

        [Header("Checkpoint Panel (compact, mid-session)")]
        public GameObject  checkpointPanel;
        [Tooltip("'JULLIE STAAN OP PLEK X!' label.")]
        public TMP_Text    rankAnnouncementText;
        [Tooltip("Parent transform for the top-5 entry rows.")]
        public Transform   checkpointRowContainer;
        [Tooltip("Max entries shown in the compact checkpoint view.")]
        public int         checkpointRowCount = 5;

        // ── Final leaderboard panel ────────────────────────────────────────────

        [Header("Final Leaderboard Panel (end of session)")]
        public GameObject  finalPanel;
        public Transform   finalRowContainer;
        [Tooltip("Max entries in the full final view.")]
        public int         finalRowCount = 10;
        [Tooltip("'NIEUW RECORD!' banner — activate when current session is rank 1.")]
        public GameObject  newRecordBanner;

        // ── Shared ─────────────────────────────────────────────────────────────

        [Header("Entry Row")]
        [Tooltip("Prefab with TMP_Text children: rankText, nameText, scoreText, checkpointsText.")]
        public GameObject  entryRowPrefab;
        [Tooltip("Colour applied to the current session's row to make it stand out.")]
        public Color       highlightColor = new Color(1f, 0.85f, 0.2f, 1f);
        public Color       defaultColor   = new Color(1f, 1f, 1f, 0.85f);

        // ── Admin reset (long-press) ────────────────────────────────────────────

        [Header("Admin Reset")]
        [Tooltip("Assign the logo Image or any always-visible element. 3-second hold reveals reset.")]
        public Button      adminLongPressTarget;
        public GameObject  adminConfirmDialog;
        [Tooltip("Seconds to hold before admin panel opens.")]
        public float       adminHoldDuration = 3f;

        private float      _adminHoldTimer;
        private bool       _adminPanelOpen;

        // ── Pooled rows ────────────────────────────────────────────────────────

        private readonly List<GameObject> _checkpointRows = new();
        private readonly List<GameObject> _finalRows      = new();

        // ── Lifecycle ──────────────────────────────────────────────────────────

        void Awake()
        {
            checkpointPanel?.SetActive(false);
            finalPanel?.SetActive(false);
            newRecordBanner?.SetActive(false);
            adminConfirmDialog?.SetActive(false);

            PrewarmPool(_checkpointRows, checkpointRowContainer, checkpointRowCount);
            PrewarmPool(_finalRows,      finalRowContainer,      finalRowCount);
        }

        void Update()
        {
            HandleAdminLongPress();
        }

        // ── Public show methods ────────────────────────────────────────────────

        /// <summary>
        /// Show the compact checkpoint panel during a handoff.
        /// Highlights the current group's row if it appears in the top N.
        /// Called by UIManager.ShowCheckpointScreen.
        /// </summary>
        public void ShowCheckpointLeaderboard(int currentGroupTotal, int currentRank)
        {
            checkpointPanel?.SetActive(true);

            if (rankAnnouncementText != null)
                rankAnnouncementText.text = $"JULLIE STAAN OP PLEK {currentRank}!";

            var top = LeaderboardManager.Instance?.GetTopEntries(checkpointRowCount)
                   ?? new List<LeaderboardManager.LeaderboardEntry>();

            PopulateRows(_checkpointRows, checkpointRowContainer, top,
                         currentRank, currentGroupTotal);
        }

        /// <summary>
        /// Show the full end-of-session leaderboard.
        /// Called by GameOverScreen or UIManager after the final checkpoint.
        /// </summary>
        public void ShowFinalLeaderboard(int currentGroupTotal, int currentRank)
        {
            finalPanel?.SetActive(true);

            bool isNewRecord = currentRank == 1;
            if (newRecordBanner != null)
                newRecordBanner.SetActive(isNewRecord);

            var top = LeaderboardManager.Instance?.GetTopEntries(finalRowCount)
                   ?? new List<LeaderboardManager.LeaderboardEntry>();

            PopulateRows(_finalRows, finalRowContainer, top,
                         currentRank, currentGroupTotal);
        }

        public void HideCheckpointLeaderboard() => checkpointPanel?.SetActive(false);
        public void HideFinalLeaderboard()       => finalPanel?.SetActive(false);

        // ── Row population ─────────────────────────────────────────────────────

        private void PopulateRows(
            List<GameObject>                         pool,
            Transform                                container,
            List<LeaderboardManager.LeaderboardEntry> entries,
            int                                       currentRank,
            int                                       currentGroupTotal)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                bool active = i < entries.Count;
                pool[i].SetActive(active);
                if (!active) continue;

                var entry = entries[i];
                int  rank  = i + 1;

                SetRowText(pool[i], rank, entry.groupName, entry.groupTotal, entry.checkpointsReached);

                // Highlight the current group's row
                bool isCurrentGroup = rank == currentRank;
                SetRowColor(pool[i], isCurrentGroup ? highlightColor : defaultColor);
            }
        }

        private void SetRowText(GameObject row, int rank, string name, int score, int checkpoints)
        {
            SetChildText(row, "rankText",        rank.ToString());
            SetChildText(row, "nameText",        name);
            SetChildText(row, "scoreText",       score.ToString("N0"));
            SetChildText(row, "checkpointsText", $"{checkpoints}x");
        }

        private void SetChildText(GameObject row, string childName, string value)
        {
            var t = row.transform.Find(childName);
            if (t == null) return;
            var tmp = t.GetComponent<TMP_Text>();
            if (tmp != null) tmp.text = value;
        }

        private void SetRowColor(GameObject row, Color color)
        {
            var img = row.GetComponent<Image>();
            if (img != null) img.color = color;
        }

        // ── Pool helpers ───────────────────────────────────────────────────────

        private void PrewarmPool(List<GameObject> pool, Transform container, int count)
        {
            if (entryRowPrefab == null || container == null) return;
            for (int i = 0; i < count; i++)
            {
                var row = Object.Instantiate(entryRowPrefab, container);
                row.SetActive(false);
                pool.Add(row);
            }
        }

        // ── Admin long-press ───────────────────────────────────────────────────

        private void HandleAdminLongPress()
        {
            if (adminLongPressTarget == null || _adminPanelOpen) return;

            bool pressed = (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
                        || (Mouse.current        != null && Mouse.current.leftButton.isPressed);

            if (pressed)
                _adminHoldTimer += Time.deltaTime;
            else
                _adminHoldTimer = 0f;

            if (_adminHoldTimer >= adminHoldDuration)
            {
                _adminHoldTimer = 0f;
                _adminPanelOpen = true;
                adminConfirmDialog?.SetActive(true);
            }
        }

        /// <summary>Called by the confirm button in the admin dialog.</summary>
        public void AdminConfirmReset()
        {
            LeaderboardManager.Instance?.ResetLeaderboard();
            adminConfirmDialog?.SetActive(false);
            _adminPanelOpen = false;
            HideFinalLeaderboard();
            Debug.Log("[LeaderboardUI] Leaderboard reset by facilitator.");
        }

        /// <summary>Called by the cancel button in the admin dialog.</summary>
        public void AdminCancelReset()
        {
            adminConfirmDialog?.SetActive(false);
            _adminPanelOpen = false;
        }
    }
}
