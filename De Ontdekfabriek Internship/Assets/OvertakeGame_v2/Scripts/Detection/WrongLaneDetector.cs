using UnityEngine;

namespace OvertakeGame
{
    public class WrongLaneDetector : MonoBehaviour
    {
        [Header("Lane Detection")]
        public RoadSideConfig roadConfig;

        [Tooltip("World X of the road centre line (divider). Usually 0.")]
        public float centreLaneX = 0f;

        [Tooltip("Grace buffer in world units before counting as wrong lane.")]
        public float graceBuffer = 0.2f;

        [Tooltip("Seconds the player must be in the wrong lane before penalties start.")]
        public float gracePeriod = 0.5f;

        public bool IsInWrongLane { get; private set; }

        private float _wrongLaneTimer;

        void Update()
        {
            if (GameManager.Instance?.CurrentState != GameManager.GameState.Playing) return;
            if (roadConfig == null) return;

            float px = transform.position.x;
            bool overCentre = roadConfig.driveOnRight
                ? px < (centreLaneX - graceBuffer)
                : px > (centreLaneX + graceBuffer);

            if (overCentre)
            {
                _wrongLaneTimer += Time.deltaTime;
                if (_wrongLaneTimer >= gracePeriod)
                {
                    IsInWrongLane = true;
                    GameManager.Instance?.OnPlayerInWrongLane();
                }
            }
            else
            {
                _wrongLaneTimer = 0f;
                IsInWrongLane   = false;
            }
        }
    }
}
