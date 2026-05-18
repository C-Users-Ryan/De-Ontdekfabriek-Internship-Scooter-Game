using UnityEngine;
using System.Collections;

namespace OvertakeGame
{
    /// <summary>
    /// A single rock obstacle on the road.
    /// Activated by RockManager via object pool. Moves at WorldSpeed.
    ///
    /// On hit: haptic feedback, camera shake, speed penalty, point deduction.
    ///
    /// PREFAB SETUP:
    ///   - Rock mesh (sphere with rough material works as placeholder)
    ///   - Sphere or Box Collider — IsTrigger = true
    ///   - Tag: "Rock"  (create in Project Settings > Tags first)
    ///   - No Rigidbody
    /// </summary>
    public class RockObstacle : MonoBehaviour
    {
        [Header("Hit Response")]
        [Tooltip("Speed reduction applied to WorldSpeed on hit (m/s).")]
        public float speedPenalty = 2.5f;

        [Header("Tags")]
        public string playerTag = "Player";

        private bool _active;
        private bool _triggered;

        void Awake()
        {
            foreach (var col in GetComponentsInChildren<Collider>())
                col.isTrigger = true;
            var rb = GetComponent<Rigidbody>();
            if (rb != null) { Debug.LogWarning("[RockObstacle] Removing Rigidbody."); Destroy(rb); }
        }

        public void Activate(Vector3 position, float scale)
        {
            transform.position   = position;
            transform.localScale = Vector3.one * scale;
            transform.rotation   = Random.rotation;
            _triggered = false;
            _active    = true;
            gameObject.SetActive(true);
        }

        public void Deactivate()
        {
            _active = false;
            gameObject.SetActive(false);
        }

        void Update()
        {
            if (!_active || WorldSpeed.Instance == null) return;
            transform.Translate(Vector3.back * WorldSpeed.Instance.Current * Time.deltaTime, Space.World);
        }

        void OnTriggerEnter(Collider other)
        {
            if (_triggered || !_active) return;
            if (!other.CompareTag(playerTag)) return;

            _triggered = true;
            GameManager.Instance?.OnPlayerHitRock();
            HapticFeedback.Instance?.OnRockHit();
            CameraShake.Instance?.Shake();

            // Brief speed penalty — override then release
            if (WorldSpeed.Instance != null)
            {
                float reduced = Mathf.Max(0f, WorldSpeed.Instance.Current - speedPenalty);
                WorldSpeed.Instance.OverrideSpeed(reduced);
                StartCoroutine(ClearOverrideAfter(0.3f));
            }

            // Reuse scooter wobble
            var pc = other.GetComponentInParent<PlayerController>()
                  ?? other.GetComponent<PlayerController>();
            pc?.ApplyPotholeHit();
        }

        private IEnumerator ClearOverrideAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            WorldSpeed.Instance?.ClearOverride();
        }
    }
}
