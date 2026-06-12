using UnityEngine;

namespace OvertakeGame
{
    /// <summary>
    /// Awards points for positive driving behaviour.
    ///
    /// TRICKLE REWARDS only flow when the player is moving above movingSpeedThresholdKmh.
    /// Prevents farming by holding still.
    ///
    /// SPEED ZONE BONUS uses SpeedZoneManager.CurrentLimit so the sweet spot tracks
    /// whatever limit is active (town = 50, highway = 80, etc.).
    /// Sweet spot = [limit * speedLimitLowFrac .. limit * speedLimitHighFrac].
    ///
    /// OVERTAKE reward fires via OnOvertakeCompleted(), called by OvertakeDetector.
    /// </summary>
    public class RewardSystem : MonoBehaviour
    {
        [Header("Reward Toggles")]
        public bool rewardWhileMoving = true;
        public bool rewardOvertaking  = true;
        public bool rewardCorrectLane = true;
        public bool rewardInSpeedZone = true;

        [Header("Trickle Rates (points / second)")]
        public float movingPointsPerSecond      = 2f;
        public float correctLanePointsPerSecond = 2f;
        public float speedZoneBonusPerSecond    = 3f;

        [Header("Overtake Reward")]
        public int overtakePoints = 30;

        [Header("Thresholds")]
        [Tooltip("Player must exceed this speed (km/h) for any trickle reward to flow.")]
        public float movingSpeedThresholdKmh = 8f;
        [Range(0.5f, 1f)]
        [Tooltip("Lower bound of the speed zone sweet spot as a fraction of the posted limit.")]
        public float speedLimitLowFrac  = 0.80f;
        [Range(0.8f, 1.2f)]
        [Tooltip("Upper bound of the sweet spot. 1.0 = no reward when exceeding the limit.")]
        public float speedLimitHighFrac = 1.00f;

        [Header("References")]
        public ScoreManager      scoreManager;
        public WrongLaneDetector laneDetector;

        private float _movingDebt;
        private float _laneDebt;
        private float _zoneDebt;

        void Update()
        {
            if (GameManager.Instance?.CurrentState != GameManager.GameState.Playing) return;
            if (scoreManager == null || WorldSpeed.Instance == null) return;

            float kmh    = WorldSpeed.Instance.CurrentKmh;
            float dt     = Time.deltaTime;
            bool  moving = kmh >= movingSpeedThresholdKmh;

            if (rewardWhileMoving && moving)
                GiveOverTime(ref _movingDebt, movingPointsPerSecond, dt);

            if (rewardCorrectLane && moving
                && laneDetector != null && !laneDetector.IsInWrongLane)
                GiveOverTime(ref _laneDebt, correctLanePointsPerSecond, dt);

            if (rewardInSpeedZone && moving && SpeedZoneManager.Instance != null)
            {
                float limit = SpeedZoneManager.Instance.CurrentLimit;
                if (kmh >= limit * speedLimitLowFrac && kmh <= limit * speedLimitHighFrac)
                    GiveOverTime(ref _zoneDebt, speedZoneBonusPerSecond, dt);
            }
        }

        public void OnOvertakeCompleted()
        {
            if (!rewardOvertaking) return;
            if (GameManager.Instance?.CurrentState != GameManager.GameState.Playing) return;
            scoreManager?.AddPoints(overtakePoints, "INGEHAALD!");
        }

        private void GiveOverTime(ref float debt, float rate, float dt)
        {
            debt += rate * dt;
            int whole = Mathf.FloorToInt(debt);
            if (whole >= 1)
            {
                debt -= whole;
                scoreManager?.AddPoints(whole, "");
            }
        }
    }
}
