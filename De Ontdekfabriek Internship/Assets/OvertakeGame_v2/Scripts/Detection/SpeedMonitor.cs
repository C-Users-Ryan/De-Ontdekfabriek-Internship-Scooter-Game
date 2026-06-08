using UnityEngine;

namespace OvertakeGame
{
    public class SpeedMonitor : MonoBehaviour
    {
        [Header("Speed Limit")]
        public float speedLimitKmh       = 80f;
        public float speedingGracePeriod = 2f;

        public bool IsSpeeding { get; private set; }
        private float _speedingTimer;

        void Update()
        {
            if (GameManager.Instance?.CurrentState != GameManager.GameState.Playing) return;
            if (WorldSpeed.Instance == null) return;

            if (WorldSpeed.Instance.CurrentKmh > speedLimitKmh)
            {
                _speedingTimer += Time.deltaTime;
                if (_speedingTimer >= speedingGracePeriod)
                {
                    IsSpeeding = true;
                    GameManager.Instance?.OnPlayerSpeeding();
                }
            }
            else { _speedingTimer = 0f; IsSpeeding = false; }
        }
    }
}
