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

            float px = Vector3.Dot(transform.position, RoadDirection.SteerpAxis);
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
            else { _wrongLaneTimer = 0f; IsInWrongLane = false; }
        }
    }
}
