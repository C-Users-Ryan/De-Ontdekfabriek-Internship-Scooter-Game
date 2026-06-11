using UnityEngine;

namespace OvertakeGame
{
    public class WrongLaneDetector : MonoBehaviour
    {
        [Header("Lane Detection")]
        public RoadSideConfig roadConfig;
        public float centreLaneX   = 0f;
        public float graceBuffer   = 0.2f;
        public float gracePeriod   = 0.5f;

        public bool IsInWrongLane { get; private set; }
        private float _wrongLaneTimer;

        void Update()
        {
            if (GameManager.Instance?.CurrentState != GameManager.GameState.Playing) return;
            if (roadConfig == null) return;

            // Use SteerpAxis projection — stays correct after 90° turns.
            // On a straight (Z travel) SteerpAxis == Vector3.right, so this is equivalent to .position.x.
            // After a 90° turn (X travel) SteerpAxis == Vector3.back, so the correct lateral axis is used.
            float steerPos = Vector3.Dot(transform.position, RoadDirection.SteerpAxis);
            bool overCentre = roadConfig.driveOnRight
                ? steerPos < (centreLaneX - graceBuffer)
                : steerPos > (centreLaneX + graceBuffer);

            if (overCentre)
            {
                _wrongLaneTimer += Time.deltaTime;
                if (_wrongLaneTimer >= gracePeriod)
                {
                    IsInWrongLane = true;
                    GameManager.Instance?.OnPlayerInWrongLane();
                }
            }
            else { _wrongLaneTimer = 0f; IsInWrongLane = false; }
        }
    }
}