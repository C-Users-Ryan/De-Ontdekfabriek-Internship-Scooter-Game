using UnityEngine;
using System;
using System.Collections.Generic;

namespace OvertakeGame
{
    /// <summary>Single source of truth for the player's score.</summary>
    public class ScoreManager : MonoBehaviour
    {
        public enum DeductionReason { Collision, WrongLane, Speeding, Pothole, Rock }

        [Header("Score Settings")]
        public int startingScore = 1000;

        [Header("Instant Deductions")]
        public int collisionDeduction = 50;
        public int potholeDeduction   = 20;
        public int rockDeduction      = 15;

        [Header("Per-Second Deduction Rates")]
        public float wrongLaneDeductionPerSecond = 10f;
        public float speedingDeductionPerSecond  = 8f;

        [Header("Score Floor")]
        public int minimumScore = 0;

        // ── Public state ───────────────────────────────────────────────────────
        public int CurrentScore { get; private set; }

        public event Action<int> OnScoreChanged;
        public event Action<int, string> OnScoreEvent;

        private readonly Dictionary<DeductionReason, float> _fractionalDebt = new();

        // ── Lifecycle ──────────────────────────────────────────────────────────

        void Awake() => CurrentScore = startingScore;

        public void ResetScore()
        {
            CurrentScore = startingScore;
            _fractionalDebt.Clear();
            OnScoreChanged?.Invoke(CurrentScore);
        }

        // ── Public API ─────────────────────────────────────────────────────────

        public void AddPoints(int amount, string label = "")
        {
            if (amount == 0 && string.IsNullOrEmpty(label)) return;

            if (amount != 0)
            {
                CurrentScore = Mathf.Max(minimumScore, CurrentScore + amount);
                OnScoreChanged?.Invoke(CurrentScore);
            }

            if (!string.IsNullOrEmpty(label))
                OnScoreEvent?.Invoke(amount, label);
        }

        /// <summary>Fire a label popup without changing the score (e.g. "+SCHILD" grace event).</summary>
        public void SignalEvent(int amount, string label)
        {
            if (!string.IsNullOrEmpty(label))
                OnScoreEvent?.Invoke(amount, label);
        }

        /// <summary>Instant deduction by reason — used by GameManager hazard toggles.</summary>
        public void DeductPoints(DeductionReason reason)
        {
            var (amount, label) = reason switch
            {
                DeductionReason.Collision => (collisionDeduction, "CRASH!"),
                DeductionReason.Pothole   => (potholeDeduction,   "KUUKUA!"),
                DeductionReason.Rock      => (rockDeduction,      "STEEN!"),
                _                         => (0, "")
            };
            ApplyDeduction(amount, label);
        }

        /// <summary>Continuous per-second deduction (wrong lane, speeding). No popup label — WarningSystem handles visuals.</summary>
        public void DeductPointsOverTime(DeductionReason reason, float deltaTime)
        {
            float rate = reason switch
            {
                DeductionReason.WrongLane => wrongLaneDeductionPerSecond,
                DeductionReason.Speeding  => speedingDeductionPerSecond,
                _                         => 0f
            };
            _fractionalDebt.TryGetValue(reason, out float debt);
            debt += rate * deltaTime;
            int whole = Mathf.FloorToInt(debt);
            if (whole >= 1)
            {
                debt -= whole;
                ApplyDeduction(whole, ""); // silent — WarningSystem shows the text
            }
            _fractionalDebt[reason] = debt;
        }

        // ── Internal ───────────────────────────────────────────────────────────

        private void ApplyDeduction(int amount, string label)
        {
            if (amount <= 0) return;
            CurrentScore = Mathf.Max(minimumScore, CurrentScore - amount);
            OnScoreChanged?.Invoke(CurrentScore);
            if (!string.IsNullOrEmpty(label))
                OnScoreEvent?.Invoke(-amount, label); // negative = red in popup
        }
    }
}
