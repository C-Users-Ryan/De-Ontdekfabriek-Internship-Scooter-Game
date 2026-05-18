using UnityEngine;
using System.Collections;

namespace OvertakeGame
{
    /// <summary>
    /// The player (scooter) stays at a FIXED Z position forever.
    /// Gas/brake controls WorldSpeed — the world moves toward the player.
    /// The player only moves on the X axis (left/right steering).
    /// This completely eliminates floating-point drift — the player never
    /// travels far from the origin.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Lateral Movement")]
        public float lateralSpeed  = 6f;
        public float roadHalfWidth = 4f;

        [Header("Pothole Hit Response")]
        public float potholeWobbleDuration = 0.6f;
        public float potholeWobbleAngle    = 12f;

        [Header("Mobile Controls")]
        [Tooltip("Enable on-screen touch controls for phone/tablet.")]
        public bool enableMobileControls = true;

        [Header("Key Bindings (Keyboard)")]
        public KeyCode gasKey   = KeyCode.W;
        public KeyCode brakeKey = KeyCode.S;
        public KeyCode leftKey  = KeyCode.A;
        public KeyCode rightKey = KeyCode.D;

        [Header("References")]
        public Speedometer speedometer;
        public ScooterLean scooterLean;
        public MobileInputUI mobileInputUI;

        // ── Public state ───────────────────────────────────────────────────────
        public float CurrentSpeedKmh     => WorldSpeed.Instance != null ? WorldSpeed.Instance.CurrentKmh : 0f;
        public float CurrentSpeedMs      => WorldSpeed.Instance != null ? WorldSpeed.Instance.Current    : 0f;
        public float CurrentLateralInput { get; private set; }
        public float ExternalLeanAngle   { get; private set; }
        public bool  HasExternalLean     { get; private set; }

        private Rigidbody _rb;
        private float     _lateralVelocity;

        // Mobile input state (set by MobileInputUI)
        [HideInInspector] public bool mobileGas;
        [HideInInspector] public bool mobileBrake;
        [HideInInspector] public float mobileLateral; // -1 left, +1 right

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            // Player only moves on X. Z is fixed — world moves instead.
            _rb.constraints   = RigidbodyConstraints.FreezeRotation
                              | RigidbodyConstraints.FreezePositionY
                              | RigidbodyConstraints.FreezePositionZ;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;

            if (scooterLean == null)
                scooterLean = GetComponentInChildren<ScooterLean>();
        }

        void Update()
        {
            if (GameManager.Instance?.CurrentState != GameManager.GameState.Playing) return;

            // ── Combine keyboard + mobile input ─────────────────────────────
            bool  gas    = Input.GetKey(gasKey)   || mobileGas;
            bool  brake  = Input.GetKey(brakeKey) || mobileBrake;
            float lateral = 0f;
            if (Input.GetKey(leftKey))  lateral -= 1f;
            if (Input.GetKey(rightKey)) lateral += 1f;
            lateral = Mathf.Clamp(lateral + mobileLateral, -1f, 1f);

            // ── Forward speed → world ────────────────────────────────────────
            WorldSpeed.Instance?.SetInput(gas, brake);
            speedometer?.UpdateSpeed(CurrentSpeedKmh);

            // ── Lateral ──────────────────────────────────────────────────────
            CurrentLateralInput = lateral;
            _lateralVelocity    = lateral * lateralSpeed;
        }

        void FixedUpdate()
        {
            // Clamp X position to road bounds
            float clampedX = Mathf.Clamp(_rb.position.x, -roadHalfWidth, roadHalfWidth);

            if ((clampedX <= -roadHalfWidth && _lateralVelocity < 0f) ||
                (clampedX >=  roadHalfWidth && _lateralVelocity > 0f))
                _lateralVelocity = 0f;

            _rb.linearVelocity = new Vector3(_lateralVelocity, 0f, 0f);

            if (_rb.position.x != clampedX)
                _rb.position = new Vector3(clampedX, _rb.position.y, _rb.position.z);
        }

        // ── Pothole hit ────────────────────────────────────────────────────────
        public void ApplyPotholeHit()
        {
            // No speed penalty here — WorldSpeed handles the slow (optional).
            // Just do the visual wobble.
            StartCoroutine(PotholeWobble());
            CameraShake.Instance?.Shake();
        }

        private IEnumerator PotholeWobble()
        {
            HasExternalLean = true;
            float elapsed = 0f;
            float dir     = Random.value > 0.5f ? 1f : -1f;
            while (elapsed < potholeWobbleDuration)
            {
                float decay       = 1f - (elapsed / potholeWobbleDuration);
                ExternalLeanAngle = Mathf.Sin(elapsed * 20f) * potholeWobbleAngle * decay * dir;
                elapsed          += Time.deltaTime;
                yield return null;
            }
            ExternalLeanAngle = 0f;
            HasExternalLean   = false;
        }
    }
}
