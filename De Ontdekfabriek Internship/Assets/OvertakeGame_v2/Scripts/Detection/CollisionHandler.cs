using UnityEngine;

namespace OvertakeGame
{
    /// <summary>
    /// Attach to the player car. Detects hits with traffic and notifies GameManager.
    /// </summary>
    public class OvertakeCollisionHandler : MonoBehaviour
    {
        [Header("Collision Settings")]
        [Tooltip("Tag assigned to all traffic vehicles.")]
        public string trafficTag = "Traffic";

        [Tooltip("Seconds between registering repeated collisions (prevents rapid-fire deductions).")]
        public float collisionCooldown = 1.5f;

        private float _lastCollisionTime = -99f;

        void OnCollisionEnter(Collision collision)
        {
            if (!collision.gameObject.CompareTag(trafficTag)) return;
            RegisterHit();
        }

        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(trafficTag)) return;
            RegisterHit();
        }

        private void RegisterHit()
        {
            if (Time.time - _lastCollisionTime < collisionCooldown) return;
            _lastCollisionTime = Time.time;
            GameManager.Instance?.OnPlayerHitTraffic();
            HapticFeedback.Instance?.OnTrafficCollision();
        }
    }
}
