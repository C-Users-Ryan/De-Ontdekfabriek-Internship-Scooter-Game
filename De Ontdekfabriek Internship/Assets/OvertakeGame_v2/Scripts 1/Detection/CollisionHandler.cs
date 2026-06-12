using UnityEngine;

namespace OvertakeGame
{
    /// <summary>
    /// Detects collisions between the player and traffic vehicles.
    /// Calculates relative speed and passes it to GameManager.OnPlayerHitTraffic(float kmh).
    ///
    /// RELATIVE SPEED
    ///   Oncoming:   relativeKmh = playerKmh + vehicleKmh  (head-on is worst case)
    ///   Same-dir:   relativeKmh = |playerKmh - vehicleKmh|  (same speed = minor bump)
    ///   GraceSystem uses this value to decide whether to absorb the hit.
    ///
    /// DETECTION
    ///   Layer-mask based (no string tag allocations in the hot path).
    ///   Set trafficLayer in the Inspector to the layer used by traffic vehicles.
    ///
    /// DIRECTION
    ///   Vector3.Dot(vehicle.forward, RoadDirection.Current) > 0 means same direction.
    ///   Works on both X and Z road axes — correct after turns.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class OvertakeCollisionHandler : MonoBehaviour
    {
        [Header("Layer Detection")]
        [Tooltip("Layer used by spawnable traffic vehicles. Avoids string tag allocations.")]
        public LayerMask trafficLayer;

        [Header("Cooldown")]
        [Tooltip("Minimum seconds between registered hits. Prevents double-triggers.")]
        public float hitCooldown = 0.4f;

        private float _lastHitTime = -99f;

        void Awake()
        {
            if (trafficLayer.value == 0)
                Debug.LogWarning("[OvertakeCollisionHandler] trafficLayer not assigned — " +
                                 "traffic collisions will be silently ignored.");
        }

        void OnCollisionEnter(Collision col)
        {
            if (IsTraffic(col.gameObject)) ProcessHit(col.gameObject);
        }

        void OnTriggerEnter(Collider other)
        {
            if (IsTraffic(other.gameObject)) ProcessHit(other.gameObject);
        }

        private void ProcessHit(GameObject vehicle)
        {
            if (Time.time - _lastHitTime < hitCooldown) return;
            _lastHitTime = Time.time;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[CollisionHandler] Hit: {vehicle.name}  " +
                      $"relative {GetRelativeSpeedKmh(vehicle):F0} km/h");
#endif
            GameManager.Instance?.OnPlayerHitTraffic(GetRelativeSpeedKmh(vehicle));
            HapticFeedback.Instance?.OnTrafficCollision();
        }

        private float GetRelativeSpeedKmh(GameObject vehicle)
        {
            float playerKmh = WorldSpeed.Instance != null ? WorldSpeed.Instance.CurrentKmh : 0f;
            var   tv        = vehicle.GetComponentInParent<TrafficVehicle>();
            if (tv == null) return playerKmh; // no data → treat as head-on at player speed

            float vehicleKmh = tv.ownSpeed * 3.6f;
            bool  same       = Vector3.Dot(vehicle.transform.forward, RoadDirection.Current) > 0f;
            return same ? Mathf.Abs(playerKmh - vehicleKmh) : playerKmh + vehicleKmh;
        }

        private bool IsTraffic(GameObject go) =>
            ((1 << go.layer) & trafficLayer.value) != 0;
    }
}
