using UnityEngine;

namespace OvertakeGame
{
    public class Pothole : MonoBehaviour
    {
        [Header("Warning Indicator")]
        public GameObject warningIndicator;
        public string playerTag = "Player";

        private bool _active;
        private bool _triggered;

        void Awake()
        {
            foreach (var col in GetComponentsInChildren<Collider>()) col.isTrigger = true;
            var rb = GetComponent<Rigidbody>();
            if (rb != null) { Debug.LogWarning("[Pothole] Removing Rigidbody — potholes must be static triggers."); Destroy(rb); }
        }

        public void Activate(Vector3 position, float scale)
        {
            transform.position   = position;
            transform.localScale = new Vector3(scale, transform.localScale.y, scale);
            _triggered = false;
            _active    = true;
            gameObject.SetActive(true);
            warningIndicator?.SetActive(true);
        }

        public void Deactivate()
        {
            _active = false;
            gameObject.SetActive(false);
        }

        // Move toward player each frame at world speed
        void Update()
        {
            if (!_active) return;
            if (WorldSpeed.Instance == null) return;
            transform.Translate(Vector3.back * WorldSpeed.Instance.Current * Time.deltaTime, Space.World);
        }

        void OnTriggerEnter(Collider other)
        {
            if (_triggered || !_active) return;
            if (!other.CompareTag(playerTag)) return;
            _triggered = true;
            warningIndicator?.SetActive(false);
            GameManager.Instance?.OnPlayerHitPothole();
            var pc = other.GetComponentInParent<PlayerController>() ?? other.GetComponent<PlayerController>();
            pc?.ApplyPotholeHit();
            CameraShake.Instance?.Shake();
            HapticFeedback.Instance?.OnPotholeHit();
        }
    }
}
