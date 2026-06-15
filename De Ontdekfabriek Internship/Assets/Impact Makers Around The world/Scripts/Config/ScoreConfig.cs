using System;
using UnityEngine;

namespace KenyaScooter.Config
{
    /// <summary>
    /// Every score value, deduction and toggle (M19, M20, Req §7.5, §16). Each event
    /// type can be switched off individually for workshop configuration. Overtake base
    /// points live on the vehicle prefab (they vary by vehicle type, Req §7.1); the
    /// streak multiplier defined here scales them (M20).
    /// </summary>
    [CreateAssetMenu(menuName = "Kenya Scooter/Score Config", fileName = "ScoreConfig")]
    public sealed class ScoreConfig : ScriptableObject
    {
        [Serializable]
        public struct StreakTier
        {
            [Tooltip("Streak count at which this tier starts.")]
            public int fromStreak;
            public float multiplier;

            public StreakTier(int fromStreak, float multiplier)
            {
                this.fromStreak = fromStreak;
                this.multiplier = multiplier;
            }
        }

        [Header("Event toggles (Req §16 — workshop configuration)")]
        public bool scoreOvertakes = true;
        public bool deductCollisions = true;
        public bool deductWrongLane = true;
        public bool deductSpeeding = true;
        public bool deductPotholes = true;
        public bool deductRocks = true;
        public bool deductSpeedBumps = true;
        [Tooltip("Small constant reward for keeping moving (Req §7.5).")]
        public bool trickleEnabled = true;
        [Tooltip("Periodic bonus for sustained own-lane driving. Off by default — the streak already rewards it (D17).")]
        public bool correctLaneBonusEnabled = false;
        [Tooltip("Bonus when a road sequence ends without a speeding violation. Off by default (D17).")]
        public bool cleanZoneBonusEnabled = false;

        [Header("Deductions")]
        public int collisionDeduction = 60;
        [Tooltip("Applied when a rewind completes (M17). Roughly the value of one clean overtake.")]
        public int rewindPenalty = 80;
        public int potholeDeduction = 50;
        public int rockDeduction = 35;
        public int speedBumpUnmarkedDeduction = 100;
        public int speedBumpPaintedDeduction = 60;
        public int wrongLanePerSecond = 25;
        public int speedingPerSecond = 15;

        [Header("Rewards")]
        public int tricklePerSecond = 2;
        public int correctLaneBonus = 10;
        public float correctLaneBonusInterval = 5f;
        public int cleanZoneBonus = 25;

        [Header("Speed scaling (Req §6.1 — rewards slowing down)")]
        [Tooltip("Maps speed ratio (0–1 of max) to the fraction of the pothole/speed-bump deduction applied. Zero below base speed, full at max.")]
        public AnimationCurve hazardSpeedScale = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.33f, 0f), new Keyframe(1f, 1f));

        [Header("Streak multiplier (M20)")]
        public StreakTier[] streakTiers =
        {
            new StreakTier(0, 1f),
            new StreakTier(2, 1.5f),
            new StreakTier(4, 2f),
            new StreakTier(6, 2.5f),
            new StreakTier(8, 3f)
        };

        [Header("Presentation")]
        [Tooltip("Score events below this absolute value do not spawn a popup (Req §7.5).")]
        public int popupMinPoints = 25;
        [Tooltip("Score never drops below this (M19 — score cannot go below 0).")]
        public int scoreFloor = 0;
    }
}
