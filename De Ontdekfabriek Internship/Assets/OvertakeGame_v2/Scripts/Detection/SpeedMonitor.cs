using UnityEngine;

namespace OvertakeGame
{
    [RequireComponent(typeof(PlayerController))]
    public class SpeedMonitor : MonoBehaviour
    {
        [Header("Speed Limit Settings")]
        [Tooltip("Speed in km/h above which speeding penalties apply.")]
        public float speedLimitKmh = 80f;

        [Tooltip("Seconds the player can exceed the limit before deductions start.")]
        public float speedingGracePeriod = 2f;

        public bool IsSpeeding { get; private set; }

        private PlayerController _player;
        private float _speedingTimer;

        void Awake() => _player = GetComponent<PlayerController>();

        void Update()
        {
            if (GameManager.Instance?.CurrentState != GameManager.GameState.Playing) return;

            if (_player.CurrentSpeedKmh > speedLimitKmh)
            {
                _speedingTimer += Time.deltaTime;
                if (_speedingTimer >= speedingGracePeriod)
                {
                    IsSpeeding = true;
                    GameManager.Instance?.OnPlayerSpeeding();
                }
            }
            else
            {
                _speedingTimer = 0f;
                IsSpeeding     = false;
            }
        }
    }
}
