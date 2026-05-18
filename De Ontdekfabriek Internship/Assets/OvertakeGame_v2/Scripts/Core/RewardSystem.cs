using UnityEngine;

namespace OvertakeGame
{
    /// <summary>
    /// Awards points for positive player behaviour.
    /// All reward types and their amounts are individually toggleable in the Inspector.
    ///
    /// Attach to the same GameObject as PlayerController (or anywhere in the scene).
    /// Wire up the ScoreManager reference.
    /// </summary>
    public class RewardSystem : MonoBehaviour
    {
        // ─── Toggles ──────────────────────────────────────────────────────────
        [Header("Reward Toggles")]
        [Tooltip("Award points for every second the player survives while driving.")]
        public bool rewardSurvivalTime = true;

        [Tooltip("Award points each time the player successfully overtakes a same-direction car.")]
        public bool rewardOvertaking = true;

        [Tooltip("Award points for every second the player stays in the correct lane.")]
        public bool rewardCorrectLane = true;

        [Tooltip("Award points for every second the player maintains a 'good' speed range.")]
        public bool rewardGoodSpeed = true;

        // ─── Reward Amounts ───────────────────────────────────────────────────
        [Header("Reward Amounts")]
        [Tooltip("Points per second for staying alive.")]
        public float survivalPointsPerSecond = 2f;

        [Tooltip("Flat points per overtake.")]
        public int overtakePoints = 25;

        [Tooltip("Points per second for driving in the correct lane.")]
        public float correctLanePointsPerSecond = 3f;

        [Tooltip("Points per second for maintaining good speed.")]
        public float goodSpeedPointsPerSecond = 2f;

        // ─── Good Speed Window ────────────────────────────────────────────────
        [Header("Good Speed Window (km/h)")]
        [Tooltip("Minimum speed to be considered 'driving well'. Below this = too slow, no reward.")]
        public float goodSpeedMin = 40f;

        [Tooltip("Maximum speed for good speed reward. Above this the SpeedMonitor handles penalties.")]
        public float goodSpeedMax = 75f;

        // ─── References ───────────────────────────────────────────────────────
        [Header("References")]
        public ScoreManager    scoreManager;
        public WrongLaneDetector laneDetector;
        public PlayerController  playerController;

        // ─── Internal ─────────────────────────────────────────────────────────
        // Fractional accumulator so sub-1-pt/s rates still feel responsive
        private float _survivalDebt;
        private float _laneDebt;
        private float _speedDebt;

        void Update()
        {
            if (GameManager.Instance?.CurrentState != GameManager.GameState.Playing) return;
            if (scoreManager == null) return;

            float dt = Time.deltaTime;

            // ── Survival ──────────────────────────────────────────────────────
            if (rewardSurvivalTime)
                GiveOverTime(ref _survivalDebt, survivalPointsPerSecond, dt);

            // ── Correct lane ──────────────────────────────────────────────────
            if (rewardCorrectLane && laneDetector != null && !laneDetector.IsInWrongLane)
                GiveOverTime(ref _laneDebt, correctLanePointsPerSecond, dt);

            // ── Good speed ────────────────────────────────────────────────────
            if (rewardGoodSpeed && playerController != null)
            {
                float kmh = WorldSpeed.Instance != null ? WorldSpeed.Instance.CurrentKmh : 0f;
                if (kmh >= goodSpeedMin && kmh <= goodSpeedMax)
                    GiveOverTime(ref _speedDebt, goodSpeedPointsPerSecond, dt);
            }
        }

        /// <summary>
        /// Call this from OvertakeDetector when the player passes a same-direction car.
        /// </summary>
        public void OnOvertakeCompleted()
        {
            if (!rewardOvertaking) return;
            if (GameManager.Instance?.CurrentState != GameManager.GameState.Playing) return;
            scoreManager?.AddPoints(overtakePoints);
        }

        // Accumulates fractional points and awards whole points only,
        // so low per-second rates still register eventually.
        private void GiveOverTime(ref float debt, float rate, float dt)
        {
            debt += rate * dt;
            int whole = Mathf.FloorToInt(debt);
            if (whole >= 1)
            {
                debt -= whole;
                scoreManager?.AddPoints(whole);
            }
        }
    }
}
