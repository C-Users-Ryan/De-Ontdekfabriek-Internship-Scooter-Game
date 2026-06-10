using UnityEngine;

namespace OvertakeGame
{
    public class Pothole : MonoBehaviour
    {
        [Header("Warning Indicator")]
        public GameObject warningIndicator;
        public string playerTag = "Player";

        private bool _active, _triggered;

        void Awake()
        {
            foreach (var col in GetComponentsInChildren<Collider>()) col.isTrigger = true;
            var rb = GetComponent<Rigidbody>();
            if (rb != null) { Debug.LogWarning("[Pothole] Removing Rigidbody."); Destroy(rb); }
        }

        public void Activate(Vector3 position, float scale)
        {
            transform.position   = position;
            transform.localScale = new Vector3(scale, transform.localScale.y, scale);
            _triggered = false; _active = true;
            gameObject.SetActive(true);
            warningIndicator?.SetActive(true);
        }

        public void Deactivate() { _active = false; gameObject.SetActive(false); }

        void Update()
        {
            if (!_active || WorldSpeed.Instance == null) return;
            transform.Translate(RoadDirection.Current * WorldSpeed.Instance.Current * Time.deltaTime, Space.World);
        }

        void OnTriggerEnter(Collider other)
        {
            if (_triggered || !_active || !other.CompareTag(playerTag)) return;
            _triggered = true;
            warningIndicator?.SetActive(false);
            GameManager.Instance?.OnPlayerHitPothole();
            HapticFeedback.Instance?.OnPotholeHit();
            CameraShake.Instance?.Shake();
            var pc = other.GetComponentInParent<PlayerController>() ?? other.GetComponent<PlayerController>();
            pc?.ApplyPotholeHit();
        }
    }
}
