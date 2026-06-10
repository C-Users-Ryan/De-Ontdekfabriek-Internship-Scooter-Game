using UnityEngine;

namespace OvertakeGame
{
    /// <summary>
    /// Attach to the player car. Detects hits with traffic and notifies GameManager.
    /// Uses both OnCollisionEnter and OnTriggerEnter for flexibility.
    /// Set collisionCooldown=0 in Inspector when debugging to verify hits register.
    /// </summary>
    public class OvertakeCollisionHandler : MonoBehaviour
    {
        [Tooltip("Tag on all traffic vehicles.")]
        public string trafficTag      = "Traffic";
        [Tooltip("Seconds between registering repeated collisions from the same area.")]
        public float  collisionCooldown = 1.5f;

        private float _lastCollisionTime = -99f;

        void OnCollisionEnter(Collision collision)
        {
            if (!collision.gameObject.CompareTag(trafficTag)) return;
            RegisterHit(collision.gameObject.name);
        }

        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(trafficTag)) return;
            RegisterHit(other.gameObject.name);
        }

        private void RegisterHit(string objName)
        {
            if (Time.time - _lastCollisionTime < collisionCooldown) return;
            _lastCollisionTime = Time.time;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[CollisionHandler] Hit: {objName}");
#endif
            GameManager.Instance?.OnPlayerHitTraffic();
            HapticFeedback.Instance?.OnTrafficCollision();
        }
    }
}
