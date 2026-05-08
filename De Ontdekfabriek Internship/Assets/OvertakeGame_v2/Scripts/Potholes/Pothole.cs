using UnityEngine;

namespace OvertakeGame
{
    /// <summary>
    /// Placed on a pothole prefab.
    ///
    /// IMPORTANT — the pothole MUST be a trigger, not a physics collider.
    /// If the collider is not a trigger the Rigidbody on the player will
    /// receive a physics impulse and be pushed backwards.
    /// This script enforces IsTrigger = true on Awake as a safety net,
    /// but set it correctly on the prefab too.
    ///
    /// Also: the pothole prefab must NOT have a Rigidbody component.
    /// A static trigger (no Rigidbody) fires OnTriggerEnter on the player's
    /// Rigidbody correctly. A Rigidbody on the pothole would make it a
    /// dynamic body that can push others.
    /// </summary>
    public class Pothole : MonoBehaviour
    {
        [Header("Warning Indicator")]
        [Tooltip("Optional child object shown ahead of the pothole as a warning. Hidden on hit.")]
        public GameObject warningIndicator;

        [Header("Player Tag")]
        public string playerTag = "Player";

        private bool _active;
        private bool _triggered;

        void Awake()
        {
            // Safety net: force every collider on this object to be a trigger.
            // This prevents physics impulses being applied to the player.
            foreach (var col in GetComponentsInChildren<Collider>())
                col.isTrigger = true;

            // Safety net: remove any Rigidbody on the pothole.
            // A Rigidbody here would make it a dynamic body capable of pushing the player.
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                Debug.LogWarning("[Pothole] Rigidbody found on pothole prefab and removed. " +
                                 "Potholes must be static triggers — remove the Rigidbody from the prefab.");
                Destroy(rb);
            }
        }

        public void Activate(Vector3 position, float scale)
        {
            transform.position   = position;
            transform.localScale = new Vector3(scale, transform.localScale.y, scale);
            _triggered           = false;
            _active              = true;
            gameObject.SetActive(true);
            warningIndicator?.SetActive(true);
        }

        public void Deactivate()
        {
            _active = false;
            gameObject.SetActive(false);
        }

        void OnTriggerEnter(Collider other)
        {
            if (_triggered || !_active) return;
            if (!other.CompareTag(playerTag)) return;

            _triggered = true;
            warningIndicator?.SetActive(false);

            GameManager.Instance?.OnPlayerHitPothole();

            var pc = other.GetComponentInParent<PlayerController>()
                  ?? other.GetComponent<PlayerController>();
            pc?.ApplyPotholeHit();

            CameraShake.Instance?.Shake();
        }
    }
}
