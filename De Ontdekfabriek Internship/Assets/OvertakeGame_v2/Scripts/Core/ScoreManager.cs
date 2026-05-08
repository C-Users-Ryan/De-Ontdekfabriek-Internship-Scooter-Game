using UnityEngine;
using System;
using System.Collections.Generic;

namespace OvertakeGame
{
    public class ScoreManager : MonoBehaviour
    {
        public enum DeductionReason { Collision, WrongLane, Speeding, Pothole }

        [Header("Score Settings")]
        public int startingScore = 1000;

        [Header("Instant Deduction Amounts")]
        public int collisionDeduction = 50;
        public int potholeDeduction   = 20;

        [Header("Per-Second Deduction Rates")]
        public float wrongLaneDeductionPerSecond = 10f;
        public float speedingDeductionPerSecond  = 8f;

        [Header("Score Clamping")]
        public int minimumScore = 0;

        public int CurrentScore { get; private set; }
        public event Action<int> OnScoreChanged;

        private readonly Dictionary<DeductionReason, float> _fractionalDebt
            = new Dictionary<DeductionReason, float>();

        void Awake() => CurrentScore = startingScore;

        public void ResetScore()
        {
            CurrentScore = startingScore;
            _fractionalDebt.Clear();
            OnScoreChanged?.Invoke(CurrentScore);
        }

        public void AddPoints(int amount)
        {
            CurrentScore += amount;
            OnScoreChanged?.Invoke(CurrentScore);
        }

        public void DeductPoints(DeductionReason reason)
        {
            int amount = reason switch
            {
                DeductionReason.Collision => collisionDeduction,
                DeductionReason.Pothole   => potholeDeduction,
                _ => 0
            };
            ApplyDeduction(amount);
        }

        public void DeductPointsOverTime(DeductionReason reason, float deltaTime)
        {
            float rate = reason switch
            {
                DeductionReason.WrongLane => wrongLaneDeductionPerSecond,
                DeductionReason.Speeding  => speedingDeductionPerSecond,
                _ => 0f
            };
            _fractionalDebt.TryGetValue(reason, out float debt);
            debt += rate * deltaTime;
            int whole = Mathf.FloorToInt(debt);
            if (whole >= 1) { debt -= whole; ApplyDeduction(whole); }
            _fractionalDebt[reason] = debt;
        }

        private void ApplyDeduction(int amount)
        {
            CurrentScore = Mathf.Max(minimumScore, CurrentScore - amount);
            OnScoreChanged?.Invoke(CurrentScore);
        }
    }
}
