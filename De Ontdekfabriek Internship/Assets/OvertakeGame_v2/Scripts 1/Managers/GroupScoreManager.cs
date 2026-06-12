using UnityEngine;
using System;
using System.Collections.Generic;

namespace OvertakeGame
{
    /// <summary>
    /// Tracks the collective score of the entire class group across all student turns.
    ///
    /// RELAY GAME CONTEXT:
    ///   Each class session consists of multiple student turns. One student plays for
    ///   ~120 seconds, reaches a charge station (checkpoint), hands the iPad to the next
    ///   student, and the cumulative group score carries forward. The final total is what
    ///   gets submitted to the leaderboard — students work TOGETHER, not against each other.
    ///
    /// HOW IT WORKS:
    ///   - At the start of each student turn, BeginTurn() is called.
    ///   - ScoreManager's OnScoreChanged event is monitored to track the delta from the
    ///     start of the turn.
    ///   - When a student reaches the checkpoint, CommitTurn() adds their turn score
    ///     (starting from ScoreManager's reset value) to the group total.
    ///   - TurnHistory stores each student's contribution for the per-checkpoint leaderboard.
    ///
    /// SCORE MODEL:
    ///   GroupTotal is the sum of all committed turn scores.
    ///   TurnScore is the current live score from ScoreManager (in progress, not committed).
    ///   The leaderboard shows GroupTotal + TurnScore for live ranking.
    ///
    /// IMPORTANT:
    ///   ScoreManager.ResetScore() is still called at the start of each turn (so the
    ///   speedometer HUD and score display feel fresh for each student). The group
    ///   accumulation happens separately here — the two systems are independent.
    ///
    /// SESSION LIFECYCLE:
    ///   ResetSession()  — called once when the Tattoo installation starts a new class
    ///   BeginTurn()     — called by CheckpointManager.StartNextRound()
    ///   CommitTurn()    — called by CheckpointManager.OnPlayerReachedCheckpoint()
    ///   EndSession()    — called by the final checkpoint or teacher trigger
    ///                     → submits to LeaderboardManager
    ///
    /// SETUP:
    ///   Attach to the same persistent GameObject as GameManager.
    ///   Assign scoreManager reference.
    /// </summary>
    public class GroupScoreManager : MonoBehaviour
    {
        public static GroupScoreManager Instance { get; private set; }

        [Header("References")]
        public ScoreManager scoreManager;

        // ── Session state ──────────────────────────────────────────────────────

        /// <summary>Sum of all completed turns this session.</summary>
        public int GroupTotal { get; private set; }

        /// <summary>The score the current student has earned this turn (live, from ScoreManager).</summary>
        public int TurnScore => scoreManager != null ? scoreManager.CurrentScore : 0;

        /// <summary>GroupTotal + TurnScore — the combined live figure for live leaderboard display.</summary>
        public int LiveGroupTotal => GroupTotal + TurnScore;

        /// <summary>Which turn (student) is currently playing. 1-indexed.</summary>
        public int CurrentTurn { get; private set; }

        /// <summary>Number of checkpoints reached this session.</summary>
        public int CheckpointsReached { get; private set; }

        /// <summary>Name set by the teacher/facilitator at session start. Used in leaderboard.</summary>
        public string GroupName { get; private set; } = "Groep";

        /// <summary>Per-turn contributions for display on checkpoint screens.</summary>
        public IReadOnlyList<TurnRecord> TurnHistory => _turnHistory;
        private readonly List<TurnRecord> _turnHistory = new();
        private bool _currentTurnCommitted;

        // ── Events ─────────────────────────────────────────────────────────────

        /// <summary>Fires when a turn is committed and the group total updates.</summary>
        public event Action<int> OnGroupTotalChanged;

        /// <summary>Fires when CommitTurn is called — CheckpointManager subscribes to trigger the screen.</summary>
        public event Action<TurnRecord> OnTurnCommitted;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        // ── Session API ────────────────────────────────────────────────────────

        /// <summary>
        /// Call once at the start of a new class session, before the first student plays.
        /// groupName is set by the teacher on the start screen (e.g. "Groep 3B").
        /// </summary>
        public void ResetSession(string groupName = "Groep")
        {
            GroupName         = groupName;
            GroupTotal        = 0;
            CurrentTurn       = 0;
            CheckpointsReached = 0;
            _turnHistory.Clear();
            OnGroupTotalChanged?.Invoke(GroupTotal);
        }

        /// <summary>
        /// Call at the beginning of each student's turn (after ScoreManager.ResetScore()).
        /// </summary>
        public void BeginTurn()
        {
            CurrentTurn++;
            _currentTurnCommitted = false;
        }

        /// <summary>
        /// Call when the student reaches the checkpoint.
        /// Adds their turn score to the group total and records it in TurnHistory.
        /// </summary>
        /// <param name="checkpointIndex">Which checkpoint was reached (1-indexed).</param>
        public void CommitTurn(int checkpointIndex)
        {
            int contribution = TurnScore;
            var record = new TurnRecord
            {
                turnNumber        = CurrentTurn,
                checkpointIndex   = checkpointIndex,
                scoreContribution = contribution,
                groupTotalAfter   = GroupTotal + contribution
            };

            GroupTotal += contribution;
            CheckpointsReached = checkpointIndex;
            _currentTurnCommitted = true;
            _turnHistory.Add(record);

            OnGroupTotalChanged?.Invoke(GroupTotal);
            OnTurnCommitted?.Invoke(record);
        }

        /// <summary>
        /// Call when the installation session ends (last checkpoint or teacher trigger).
        /// Submits the group result to the leaderboard.
        /// Returns the LeaderboardEntry that was submitted.
        /// </summary>
        public LeaderboardManager.LeaderboardEntry EndSession()
        {
            // Commit any in-progress turn score if not already committed by CheckpointManager
            if (!_currentTurnCommitted && scoreManager != null && scoreManager.CurrentScore > 0)
                CommitTurn(CheckpointsReached);

            var entry = LeaderboardManager.Instance?.SubmitSession(
                GroupName,
                GroupTotal,
                _turnHistory.Count,
                _turnHistory);

            return entry;
        }

        // ── Data types ─────────────────────────────────────────────────────────

        [Serializable]
        public struct TurnRecord
        {
            public int turnNumber;
            public int checkpointIndex;
            public int scoreContribution;
            public int groupTotalAfter;
        }
    }
}
