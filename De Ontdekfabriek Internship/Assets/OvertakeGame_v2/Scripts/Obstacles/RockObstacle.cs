using UnityEngine;
using System.Collections;

namespace OvertakeGame
{
    /// <summary>
    /// Rock obstacle that moves with the world and triggers a hit response.
    /// The player teleport bug was caused by CameraShake storing position at Awake —
    /// this script itself does not touch player or camera position.
    /// </summary>
    public class RockObstacle : MonoBehaviour
    {
        [Header("Hit Response")]
        public float speedPenalty = 2.5f;
        public string playerTag = "Player";

        private bool _active;
        private bool _triggered;

        void Awake()
        {
            foreach (var col in GetComponentsInChildren<Collider>())
                col.isTrigger = true;

            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                Debug.LogWarning("[RockObstacle] Rigidbody found and removed — rocks use trigger colliders only.");
                Destroy(rb);
            }
        }

        public void Activate(Vector3 position, float scale)
        {
            transform.position = position;
            transform.localScale = Vector3.one * scale;
            transform.rotation = Random.rotation;
            _triggered = false;
            _active = true;
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
            if (_triggered || !_active || !other.CompareTag(playerTag)) return;
            _triggered = true;

            GameManager.Instance?.OnPlayerHitRock();
            HapticFeedback.Instance?.OnRockHit();
            CameraShake.Instance?.Shake();  // safe now — shake captures position at call time

            if (WorldSpeed.Instance != null)
            {
                WorldSpeed.Instance.OverrideSpeed(
                    Mathf.Max(0f, WorldSpeed.Instance.Current - speedPenalty));
                StartCoroutine(ClearOverrideAfter(0.3f));
            }

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