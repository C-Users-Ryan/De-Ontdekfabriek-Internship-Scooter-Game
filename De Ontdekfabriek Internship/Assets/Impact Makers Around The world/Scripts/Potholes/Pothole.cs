using UnityEngine;

namespace OvertakeGame
{
    /// <summary>
    /// A pooled pothole hazard that scrolls with the world and triggers when the player enters.
    ///
    /// CHANGES FROM PREVIOUS VERSION:
    ///   - OnTriggerEnter now reads WorldSpeed.Instance.CurrentKmh and passes it to
    ///     GameManager.OnPlayerHitPothole(float speedKmh).
    ///   - GameManager performs the speed-scaled deduction calculation — Pothole itself
    ///     has no scoring logic, which keeps this class a pure physics/world object.
    ///
    /// WHY SPEED-SCALED DEDUCTION:
    ///   A player who slows down to navigate a pothole safely should be rewarded with
    ///   a lighter penalty than one who barrels through at full speed. This mirrors
    ///   real-world defensive driving: anticipation reduces damage.
    ///
    ///   The actual math lives in GameManager.OnPlayerHitPothole so all tuning is in one
    ///   place and exposed in the Inspector without touching this script.
    ///
    /// POOLING:
    ///   Never Instantiate or Destroy at runtime. PotholeManager calls Activate() /
    ///   Deactivate(). The object moves off-screen → Deactivate() returns it to the pool.
    /// </summary>
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

            // Pass the player's current speed so GameManager can scale the deduction.
            float speedKmh = WorldSpeed.Instance != null ? WorldSpeed.Instance.CurrentKmh : 0f;
            GameManager.Instance?.OnPlayerHitPothole(speedKmh);

            HapticFeedback.Instance?.OnPotholeHit();
            CameraShake.Instance?.Shake();

            var pc = other.GetComponentInParent<PlayerController>() ?? other.GetComponent<PlayerController>();
            pc?.ApplyPotholeHit();
        }
    }
}
