using UnityEngine;

namespace OvertakeGame
{
    /// <summary>Awards points for positive driving behaviour.</summary>
    public class RewardSystem : MonoBehaviour
    {
        [Header("Reward Toggles")]
        public bool rewardSurvivalTime = true;
        public bool rewardOvertaking   = true;
        public bool rewardCorrectLane  = true;
        public bool rewardGoodSpeed    = true;

        [Header("Reward Amounts")]
        public float survivalPointsPerSecond    = 2f;
        public int   overtakePoints             = 25;
        public float correctLanePointsPerSecond = 3f;
        public float goodSpeedPointsPerSecond   = 2f;

        [Header("Good Speed Window (km/h)")]
        public float goodSpeedMin = 40f;
        public float goodSpeedMax = 75f;

        [Header("References")]
        public ScoreManager      scoreManager;
        public WrongLaneDetector laneDetector;

        private float _survivalDebt;
        private float _laneDebt;
        private float _speedDebt;

        void Update()
        {
            if (GameManager.Instance?.CurrentState != GameManager.GameState.Playing) return;
            if (scoreManager == null) return;

            float dt = Time.deltaTime;

            if (rewardSurvivalTime)
                GiveOverTime(ref _survivalDebt, survivalPointsPerSecond, dt);

            if (rewardCorrectLane && laneDetector != null && !laneDetector.IsInWrongLane)
                GiveOverTime(ref _laneDebt, correctLanePointsPerSecond, dt);

            if (rewardGoodSpeed && WorldSpeed.Instance != null)
            {
                float kmh = WorldSpeed.Instance.CurrentKmh;
                if (kmh >= goodSpeedMin && kmh <= goodSpeedMax)
                    GiveOverTime(ref _speedDebt, goodSpeedPointsPerSecond, dt);
            }
        }

        public void OnOvertakeCompleted()
        {
            if (!rewardOvertaking) return;
            if (GameManager.Instance?.CurrentState != GameManager.GameState.Playing) return;
            scoreManager?.AddPoints(overtakePoints);
        }

        private void GiveOverTime(ref float debt, float rate, float dt)
        {
            debt += rate * dt;
            int whole = Mathf.FloorToInt(debt);
            if (whole >= 1) { debt -= whole; scoreManager?.AddPoints(whole); }
        }
    }
}
