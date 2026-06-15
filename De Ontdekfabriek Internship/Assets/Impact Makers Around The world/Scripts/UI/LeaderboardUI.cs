using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using KenyaScooter.Scoring;

namespace KenyaScooter.UI
{
    /// <summary>
    /// Group leaderboard (Req §13): top-N stored groups with the current group's live
    /// total appended and highlighted. A facilitator long-press (3 s) anywhere on the
    /// panel reveals the reset confirmation; ConfirmReset wipes the leaderboard and
    /// the current group for a new exhibition period. Rows are pooled at Awake.
    /// </summary>
    public sealed class LeaderboardUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [Tooltip("Disabled TMP child used as the row blueprint.")]
        [SerializeField] private TMP_Text rowTemplate;
        [SerializeField] private int maxRows = 10;
        [SerializeField] private Color normalColour = Color.white;
        [SerializeField] private Color highlightColour = new Color(1f, 0.85f, 0.3f);
        [SerializeField] private GameObject resetConfirmPanel;
        [SerializeField] private float adminHoldSeconds = 3f;

        private TMP_Text[] rows;
        private float heldSince = float.NegativeInfinity;
        private bool holdConsumed;

        private void Awake()
        {
            rowTemplate.gameObject.SetActive(false);
            rows = new TMP_Text[maxRows + 1]; // +1 for the live current-group row
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i] = Instantiate(rowTemplate, rowTemplate.transform.parent);
                rows[i].gameObject.SetActive(false);
            }
            if (resetConfirmPanel != null)
                resetConfirmPanel.SetActive(false);
        }

        private void Update()
        {
            if (holdConsumed || float.IsNegativeInfinity(heldSince))
                return;

            if (Time.unscaledTime - heldSince >= adminHoldSeconds)
            {
                holdConsumed = true;
                if (resetConfirmPanel != null)
                    resetConfirmPanel.SetActive(true);
            }
        }

        /// <summary>Fills the panel: compact top-5 at the checkpoint, full top-10 at session end (Req §13).</summary>
        public void Refresh(int count)
        {
            int shown = 0;
            if (LeaderboardManager.Instance != null)
            {
                var entries = LeaderboardManager.Instance.Entries;
                int limit = Mathf.Min(count, Mathf.Min(entries.Count, maxRows));
                for (; shown < limit; shown++)
                {
                    rows[shown].text = $"{shown + 1}. {entries[shown].label}  {entries[shown].score}";
                    rows[shown].color = normalColour;
                    rows[shown].gameObject.SetActive(true);
                }
            }

            if (GroupScoreManager.Instance != null && LeaderboardManager.Instance != null)
            {
                int total = GroupScoreManager.Instance.GroupTotal;
                rows[shown].text = $"►  JULLIE  {total}  (PLEK {LeaderboardManager.Instance.RankOf(total)})";
                rows[shown].color = highlightColour;
                rows[shown].gameObject.SetActive(true);
                shown++;
            }

            for (int i = shown; i < rows.Length; i++)
                rows[i].gameObject.SetActive(false);
        }

        // ---- Facilitator admin (long-press) ------------------------------------------

        public void OnPointerDown(PointerEventData eventData)
        {
            heldSince = Time.unscaledTime;
            holdConsumed = false;
        }

        public void OnPointerUp(PointerEventData eventData) => heldSince = float.NegativeInfinity;

        /// <summary>Wired to the confirm button: archive nothing, wipe everything — new exhibition period.</summary>
        public void ConfirmReset()
        {
            if (LeaderboardManager.Instance != null)
                LeaderboardManager.Instance.ResetLeaderboard();
            if (GroupScoreManager.Instance != null)
                GroupScoreManager.Instance.ResetGroup();
            if (resetConfirmPanel != null)
                resetConfirmPanel.SetActive(false);
            Refresh(maxRows);
        }

        public void CancelReset()
        {
            if (resetConfirmPanel != null)
                resetConfirmPanel.SetActive(false);
        }
    }
}
