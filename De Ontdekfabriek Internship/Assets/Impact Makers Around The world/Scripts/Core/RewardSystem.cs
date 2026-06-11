using UnityEngine;

namespace OvertakeGame
{
    /// <summary>
    /// Awards points for positive driving behaviour.
    ///
    /// CHANGES FROM PREVIOUS VERSION:
    ///
    ///   MOVING-SPEED GATE
    ///     Trickle rewards only flow when the player is actually moving above
    ///     movingSpeedThresholdKmh. Standing still (heavy brake) earns nothing.
    ///     Prevents farming points by holding still at the edge of the course.
    ///
    ///   SPEED ZONE AWARENESS
    ///     The "good speed" window now tracks the current speed limit from
    ///     SpeedZoneManager rather than a hardcoded km/h range. Driving at or just
    ///     below the posted limit earns a bonus multiplier on the trickle reward.
    ///     This makes slowing down in a 50 km/h town zone feel actively rewarded.
    ///
    ///     Sweet spot: [limit * speedLimitLowFrac .. limit * speedLimitHighFrac]
    ///       Default: 80% to 100% of the limit (e.g. 64–80 km/h in an 80 zone).
    ///     Outside the sweet spot: base trickle rate (no bonus, no deduction).
    ///     Above the limit: SpeedMonitor handles deductions separately.
    ///
    ///   OVERTAKE LABEL
    ///     OnOvertakeCompleted now passes "INGEHAALD!" to AddPoints so a popup fires.
    ///
    /// UNCHANGED:
    ///   RewardOvertaking, RewardCorrectLane toggles still work as before.
    ///   Per-second debt accumulator prevents integer rounding loss.
    /// </summary>
    public class RewardSystem : MonoBehaviour
    {
        [Header("Reward Toggles")]
        public bool rewardWhileMoving = true;
        public bool rewardOvertaking  = true;
        public bool rewardCorrectLane = true;
        public bool rewardInSpeedZone = true;

        [Header("Base Trickle Rate (points / second)")]
        [Tooltip("Points per second while the player is moving and in the correct lane.")]
        public float movingPointsPerSecond = 2f;

        [Tooltip("Additional points per second when in the speed zone sweet spot (stacks with movingPoints).")]
        public float speedZoneBonusPerSecond = 3f;

        [Tooltip("Extra points per second for staying in the correct lane. Stacks with moving reward.")]
        public float correctLanePointsPerSecond = 2f;

        [Header("Overtake Reward")]
        public int overtakePoints = 30;

        [Header("Moving Speed Threshold")]
        [Tooltip("Player must be moving faster than this (km/h) for any trickle reward to flow.")]
        public float movingSpeedThresholdKmh = 8f;

        [Header("Speed Zone Sweet Spot")]
        [Tooltip("Lower bound of speed zone bonus as a fraction of the posted limit. "
               + "0.80 = player must be at least 80% of the limit to earn the bonus.")]
        [Range(0.5f, 1f)]
        public float speedLimitLowFrac  = 0.80f;

        [Tooltip("Upper bound — player must be AT or BELOW this fraction of the limit. "
               + "1.00 = player earns bonus only when not exceeding the limit.")]
        [Range(0.8f, 1.2f)]
        public float speedLimitHighFrac = 1.00f;

        [Header("References")]
        public ScoreManager      scoreManager;
        public WrongLaneDetector laneDetector;

        // ── Per-second debt accumulators ───────────────────────────────────────
        private float _movingDebt;
        private float _laneDebt;
        private float _zoneDebt;

        // ── Update ─────────────────────────────────────────────────────────────

        void Update()
        {
            if (GameManager.Instance?.CurrentState != GameManager.GameState.Playing) return;
            if (scoreManager == null || WorldSpeed.Instance == null) return;

            float kmh  = WorldSpeed.Instance.CurrentKmh;
            float dt   = Time.deltaTime;
            bool moving = kmh >= movingSpeedThresholdKmh;

            // ── Moving reward ──
            if (rewardWhileMoving && moving)
                GiveOverTime(ref _movingDebt, movingPointsPerSecond, dt, "");

            // ── Correct-lane reward ──
            if (rewardCorrectLane && moving
                && laneDetector != null && !laneDetector.IsInWrongLane)
                GiveOverTime(ref _laneDebt, correctLanePointsPerSecond, dt, "");

            // ── Speed zone bonus ──
            if (rewardInSpeedZone && moving && SpeedZoneManager.Instance != null)
            {
                float limit    = SpeedZoneManager.Instance.CurrentLimit;
                float lowBound = limit * speedLimitLowFrac;
                float hiBound  = limit * speedLimitHighFrac;
                if (kmh >= lowBound && kmh <= hiBound)
                    GiveOverTime(ref _zoneDebt, speedZoneBonusPerSecond, dt, "");
            }
        }

        // ── Overtake reward ────────────────────────────────────────────────────

        public void OnOvertakeCompleted()
        {
            if (!rewardOvertaking) return;
            if (GameManager.Instance?.CurrentState != GameManager.GameState.Playing) return;
            // Label fires the popup — overtake is a discrete, meaningful event
            scoreManager?.AddPoints(overtakePoints, "INGEHAALD!");
        }

        // ── Internal ───────────────────────────────────────────────────────────

        private void GiveOverTime(ref float debt, float rate, float dt, string label)
        {
            debt += rate * dt;
            int whole = Mathf.FloorToInt(debt);
            if (whole >= 1)
            {
                debt -= whole;
                scoreManager?.AddPoints(whole, label); // empty label = silent trickle
            }
        }
    }
}
