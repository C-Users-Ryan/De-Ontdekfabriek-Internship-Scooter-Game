using UnityEngine;

namespace OvertakeGame
{
    /// <summary>
    /// Detects collisions between the player and traffic vehicles and classifies the
    /// hit as light or fatal based on relative speed before handing off to GameManager.
    ///
    /// CHANGES FROM PREVIOUS VERSION:
    ///   - Removed the parameterless GameManager.OnPlayerHitTraffic() call.
    ///   - Now calculates relative speed and calls GameManager.OnPlayerHitTraffic(float kmh).
    ///   - Switched from tag-based detection to layer-mask detection (no string allocations).
    ///
    /// RELATIVE SPEED DEFINITION:
    ///   ONCOMING traffic:   relativeKmh = playerKmh + vehicleKmh  (head-on is worst case)
    ///   SAME-DIR traffic:   relativeKmh = |playerKmh - vehicleKmh| (rear-end at same speed = minor)
    ///   This value is what GraceSystem uses to decide whether the hit can be absorbed.
    ///
    /// DECISION FLOW:
    ///   OnCollisionEnter / OnTriggerEnter
    ///     → GetRelativeSpeedKmh()
    ///     → GameManager.OnPlayerHitTraffic(relKmh)
    ///       → GraceSystem.TryAbsorb(relKmh)
    ///           true  → grace absorbed → visual/audio warning, score deduction, no game over
    ///           false → TriggerGameOver()
    ///
    /// DIRECTION DETECTION:
    ///   Uses Vector3.Dot(vehicle.forward, RoadDirection.Current.Forward).
    ///   Positive dot = same direction as road travel.
    ///   Avoids raw position comparisons — correct for both X and Z road orientations.
    ///
    /// SETUP:
    ///   Attach to the player collider object.
    ///   Set trafficLayer to the layer used by spawnable traffic vehicles.
    ///   Each traffic vehicle needs a TrafficVehicle component exposing CurrentKmh.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class OvertakeCollisionHandler : MonoBehaviour
    {
        [Header("Layer Detection")]
        [Tooltip("Set to the layer used by spawnable traffic vehicles. Layer mask avoids string tag allocations.")]
        public LayerMask trafficLayer;

        [Header("Cooldown")]
        [Tooltip("Minimum seconds between two hits being registered. Prevents multi-frame double-triggers on the same vehicle.")]
        public float hitCooldown = 0.4f;

        private float _lastHitTime = -99f;

        void Awake()
        {
            if (trafficLayer.value == 0)
                Debug.LogWarning("[OvertakeCollisionHandler] trafficLayer is not assigned. "
                               + "All traffic collisions will be silently ignored. "
                               + "Assign the Traffic layer in the Inspector.");
        }

        // ── Collision entry points ─────────────────────────────────────────────

        void OnCollisionEnter(Collision col)
        {
            if (!IsTraffic(col.gameObject)) return;
            ProcessHit(col.gameObject);
        }

        void OnTriggerEnter(Collider other)
        {
            if (!IsTraffic(other.gameObject)) return;
            ProcessHit(other.gameObject);
        }

        // ── Core logic ─────────────────────────────────────────────────────────

        private void ProcessHit(GameObject vehicle)
        {
            if (Time.time - _lastHitTime < hitCooldown) return;
            _lastHitTime = Time.time;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[CollisionHandler] Hit: {vehicle.name} — relative {GetRelativeSpeedKmh(vehicle):F0} km/h");
#endif
            float relKmh = GetRelativeSpeedKmh(vehicle);
            GameManager.Instance?.OnPlayerHitTraffic(relKmh);
            HapticFeedback.Instance?.OnTrafficCollision();
        }

        private float GetRelativeSpeedKmh(GameObject vehicle)
        {
            float playerKmh = WorldSpeed.Instance != null ? WorldSpeed.Instance.CurrentKmh : 0f;

            var tv = vehicle.GetComponentInParent<TrafficVehicle>();
            if (tv == null)
                return playerKmh; // conservative: no data → treat as head-on at player speed

            float vehicleKmh = tv.ownSpeed * 3.6f; // TrafficVehicle exposes ownSpeed in m/s
            bool  same       = IsSameDirection(vehicle.transform);

            return same
                ? Mathf.Abs(playerKmh - vehicleKmh)   // rear-end: difference
                : playerKmh + vehicleKmh;              // head-on: sum
        }

        private bool IsSameDirection(Transform vehicleTransform)
        {
            // RoadDirection.Current is a Vector3 (value type) — use it directly
            return Vector3.Dot(vehicleTransform.forward, RoadDirection.Current) > 0f;
        }

        private bool IsTraffic(GameObject go)
        {
            return ((1 << go.layer) & trafficLayer.value) != 0;
        }
    }
}
