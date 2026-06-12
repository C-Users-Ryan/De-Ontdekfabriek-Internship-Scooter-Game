using UnityEngine;
using System;
using System.Collections.Generic;

namespace OvertakeGame
{
    /// <summary>
    /// Persists and ranks group scores across all class sessions on this installation.
    ///
    /// LEADERBOARD CONTEXT:
    ///   The Tattoo installation is a shared exhibit. Multiple school classes visit it
    ///   across a day, week, or exhibition period. Each class group plays the relay game
    ///   together and earns a collective score. The leaderboard shows how this group
    ///   ranks against all previous groups — visible at checkpoints and on the final screen.
    ///
    ///   This is a stakeholder requirement: the leaderboard creates social motivation
    ///   ("can our class beat the top score?") and gives teachers a tangible takeaway.
    ///
    /// STORAGE:
    ///   Local PlayerPrefs, serialised as JSON. No backend required — the iPad is the
    ///   single source of truth for one installation unit.
    ///   If the game runs on multiple iPads simultaneously, each has its own leaderboard.
    ///   Extension: a future networked backend could replace LoadEntries/SaveEntries
    ///   without changing the public API.
    ///
    /// LEADERBOARD ENTRY:
    ///   Each entry stores the group name, date, total score, number of checkpoints reached,
    ///   and per-checkpoint score contributions. The per-checkpoint data powers the
    ///   "compare at each stop" display the stakeholder requested.
    ///
    /// RANKING:
    ///   Entries are sorted by GroupTotal descending.
    ///   GetCurrentRank(score) returns where a live session would place RIGHT NOW —
    ///   used during the session for live ranking feedback at checkpoints.
    ///
    /// RESET:
    ///   ResetLeaderboard() wipes all entries. Intended for teacher/facilitator use via
    ///   LeaderboardUI's admin screen (password-protected in the UI layer).
    ///   Data loss is intentional: a new exhibition period starts fresh.
    ///
    /// SETUP:
    ///   Attach to the same persistent DontDestroyOnLoad GameObject as HighScoreManager.
    ///   No additional configuration needed — storage is automatic.
    ///
    /// MAX ENTRIES:
    ///   maxEntries caps stored history (default 50). When full, the lowest-scoring
    ///   entry is replaced only if the new entry scores higher. This ensures the board
    ///   always shows the best groups, not the most recent ones.
    /// </summary>
    public class LeaderboardManager : MonoBehaviour
    {
        public static LeaderboardManager Instance { get; private set; }

        private const string PREFS_KEY    = "tattoo_leaderboard_v1";
        private const int    MAX_ENTRIES  = 50;

        [Header("Settings")]
        [Tooltip("Maximum entries retained. When full, new entries only displace the lowest score.")]
        public int maxEntries = MAX_ENTRIES;

        // ── Data types ─────────────────────────────────────────────────────────

        [Serializable]
        public class LeaderboardEntry : IComparable<LeaderboardEntry>
        {
            public string groupName;
            public int    groupTotal;
            public int    checkpointsReached;
            public string date;             // ISO date string: "2026-06-10"

            /// <summary>Score at each checkpoint (index 0 = checkpoint 1's cumulative total).</summary>
            public int[]  checkpointTotals;

            public int CompareTo(LeaderboardEntry other)
            {
                // Descending by score
                return other.groupTotal.CompareTo(groupTotal);
            }
        }

        [Serializable]
        private class EntryList { public List<LeaderboardEntry> entries = new(); }

        // ── Internal state ─────────────────────────────────────────────────────

        private List<LeaderboardEntry> _entries = new();

        /// <summary>All entries, sorted best-first. Read-only snapshot.</summary>
        public IReadOnlyList<LeaderboardEntry> Entries => _entries;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadEntries();
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Submit a completed session. Called by GroupScoreManager.EndSession().
        /// Returns the entry that was added (null if score was too low to make the board
        /// and the board is already full).
        /// </summary>
        public LeaderboardEntry SubmitSession(
            string groupName,
            int    groupTotal,
            int    checkpointsReached,
            IReadOnlyList<GroupScoreManager.TurnRecord> turnHistory)
        {
            // Build per-checkpoint cumulative totals
            int[] cpTotals = new int[checkpointsReached];
            for (int i = 0; i < turnHistory.Count && i < checkpointsReached; i++)
                cpTotals[i] = turnHistory[i].groupTotalAfter;

            var entry = new LeaderboardEntry
            {
                groupName          = string.IsNullOrEmpty(groupName) ? "Groep" : groupName,
                groupTotal         = groupTotal,
                checkpointsReached = checkpointsReached,
                date               = DateTime.Now.ToString("yyyy-MM-dd"),
                checkpointTotals   = cpTotals
            };

            if (_entries.Count < maxEntries)
            {
                _entries.Add(entry);
            }
            else
            {
                // Replace the lowest entry if the new score is higher
                _entries.Sort();
                LeaderboardEntry lowest = _entries[_entries.Count - 1];
                if (groupTotal > lowest.groupTotal)
                    _entries[_entries.Count - 1] = entry;
                else
                    return null; // didn't make the board
            }

            _entries.Sort();
            SaveEntries();
            return entry;
        }

        /// <summary>
        /// Returns the rank (1-indexed) where a group with this live total would currently
        /// place. Used at checkpoints for "JULLIE STAAN OP PLEK X" feedback.
        /// Returns 1 if the board is empty (you're in first place by default).
        /// </summary>
        public int GetCurrentRank(int liveTotal)
        {
            if (_entries.Count == 0) return 1;
            int rank = 1;
            foreach (var e in _entries)
            {
                if (liveTotal <= e.groupTotal) rank++;
            }
            return Mathf.Clamp(rank, 1, _entries.Count + 1);
        }

        /// <summary>
        /// Returns the top N entries (sorted best-first).
        /// Used by LeaderboardUI to populate the display list.
        /// </summary>
        public List<LeaderboardEntry> GetTopEntries(int count = 10)
        {
            int n = Mathf.Min(count, _entries.Count);
            return _entries.GetRange(0, n);
        }

        /// <summary>
        /// Wipes all entries. Intended for facilitator use between exhibition periods.
        /// This is irreversible — confirm in the UI before calling.
        /// </summary>
        public void ResetLeaderboard()
        {
            _entries.Clear();
            PlayerPrefs.DeleteKey(PREFS_KEY);
            PlayerPrefs.Save();
            Debug.Log("[LeaderboardManager] Leaderboard wiped.");
        }

        // ── Persistence ────────────────────────────────────────────────────────

        private void LoadEntries()
        {
            string json = PlayerPrefs.GetString(PREFS_KEY, "");
            if (string.IsNullOrEmpty(json)) return;

            try
            {
                var list = JsonUtility.FromJson<EntryList>(json);
                if (list?.entries != null)
                {
                    _entries = list.entries;
                    _entries.Sort();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LeaderboardManager] Failed to parse saved data: {e.Message}");
                _entries = new List<LeaderboardEntry>();
            }
        }

        private void SaveEntries()
        {
            var list = new EntryList { entries = _entries };
            PlayerPrefs.SetString(PREFS_KEY, JsonUtility.ToJson(list));
            PlayerPrefs.Save();
        }
    }
}
