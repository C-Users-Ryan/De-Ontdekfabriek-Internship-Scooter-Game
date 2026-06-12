using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

namespace OvertakeGame
{
    /// <summary>
    /// Player scooter controller. The player is FIXED in world space.
    /// Road, traffic, and hazards move toward the player — the player never moves in Z.
    ///
    /// INPUT PIPELINE
    ///   Desktop  │  W / S  → gas / brake    (Keyboard.current)
    ///            │  A / D  → steer left / right
    ///   Mobile   │  mobileGas / mobileBrake → set by MobileInputUI (touch zones)
    ///            │  mobileLateral           → set by GyroscopeSteering (tilt)
    ///   All sources add together and clamp to [-1, 1] so keyboard + gyro work simultaneously.
    ///
    /// SPEED      Delegated to WorldSpeed. SetInput(gas, brake) called each frame.
    /// LATERAL    Smoothed via MoveTowards (steering weight). Uses RoadDirection.SteerAxis.
    /// LEAN       Delegated to ScooterLean on the mesh child.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Lateral Movement")]
        [Tooltip("Maximum lateral speed in m/s at full input.")]
        public float lateralSpeed        = 6f;
        [Tooltip("How fast lateral velocity builds up and reverses. 20–50 m/s² recommended. " +
                 "Lower = heavier, more realistic. Higher = snappier.")]
        public float lateralAcceleration = 30f;
        [Tooltip("Half-width of the playable road in metres. Player is clamped to ±this.")]
        public float roadHalfWidth       = 4f;

        [Header("Pothole / Rock Hit Response")]
        public float wobbleDuration = 0.6f;
        public float wobbleAngle    = 12f;

        [Header("References")]
        public Speedometer speedometer;
        public ScooterLean scooterLean;

        // ── Public state (read by ScooterLean, OvertakeDetector, SpeedMonitor, etc.) ──

        public float CurrentSpeedKmh     => WorldSpeed.Instance != null ? WorldSpeed.Instance.CurrentKmh : 0f;
        public float CurrentSpeedMs      => WorldSpeed.Instance != null ? WorldSpeed.Instance.Current    : 0f;
        public float CurrentLateralInput { get; private set; }
        public float ExternalLeanAngle   { get; private set; }
        public bool  HasExternalLean     { get; private set; }

        // ── Written by MobileInputUI and GyroscopeSteering ────────────────────

        [HideInInspector] public bool  mobileGas;
        [HideInInspector] public bool  mobileBrake;
        [HideInInspector] public float mobileLateral;

        // ── Private ────────────────────────────────────────────────────────────

        private float     _lateralVelocity;
        private float     _distanceAccumulator;
        private Rigidbody _rb;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.constraints   = RigidbodyConstraints.FreezeRotation
                              | RigidbodyConstraints.FreezePositionY
                              | RigidbodyConstraints.FreezePositionZ;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            if (scooterLean == null) scooterLean = GetComponentInChildren<ScooterLean>();
        }

        void Update()
        {
            speedometer?.UpdateSpeed(CurrentSpeedKmh);
            if (GameManager.Instance?.CurrentState != GameManager.GameState.Playing) return;

            // ── Speed input (New Input System + mobile) ───────────────────────
            bool keyGas   = Keyboard.current != null && Keyboard.current.wKey.isPressed;
            bool keyBrake = Keyboard.current != null && Keyboard.current.sKey.isPressed;
            WorldSpeed.Instance?.SetInput(keyGas || mobileGas, keyBrake || mobileBrake);

            // ── Lateral input ─────────────────────────────────────────────────
            float keyLateral = 0f;
            if (Keyboard.current != null)
            {
                if (Keyboard.current.aKey.isPressed) keyLateral -= 1f;
                if (Keyboard.current.dKey.isPressed) keyLateral += 1f;
            }
            CurrentLateralInput = Mathf.Clamp(keyLateral + mobileLateral, -1f, 1f);

            _lateralVelocity = Mathf.MoveTowards(
                _lateralVelocity, CurrentLateralInput * lateralSpeed, lateralAcceleration * Time.deltaTime);

            // ── Distance tracking ─────────────────────────────────────────────
            _distanceAccumulator += CurrentSpeedMs * Time.deltaTime;
            GameManager.Instance?.UpdateDistance(_distanceAccumulator);
        }

        void FixedUpdate()
        {
            Vector3 steerAxis    = RoadDirection.SteerAxis;
            float   steerPos     = Vector3.Dot(_rb.position, steerAxis);
            float   clampedSteer = Mathf.Clamp(steerPos, -roadHalfWidth, roadHalfWidth);

            // Kill lateral velocity at road edges
            if ((clampedSteer <= -roadHalfWidth && _lateralVelocity < 0f) ||
                (clampedSteer >=  roadHalfWidth && _lateralVelocity > 0f))
                _lateralVelocity = 0f;

            Vector3 steerVel   = steerAxis * _lateralVelocity;
            _rb.linearVelocity = new Vector3(steerVel.x, 0f, steerVel.z);

            // Hard-clamp position against edge-case tunnelling at high velocity
            if (!Mathf.Approximately(steerPos, clampedSteer))
            {
                Vector3 travelComp = _rb.position - steerPos * steerAxis;
                _rb.position       = travelComp + clampedSteer * steerAxis;
            }
        }

        // ── External events ────────────────────────────────────────────────────

        public void ApplyPotholeHit() => StartCoroutine(PotholeWobble());

        private IEnumerator PotholeWobble()
        {
            HasExternalLean = true;
            float elapsed = 0f;
            float dir     = Random.value > 0.5f ? 1f : -1f;
            while (elapsed < wobbleDuration)
            {
                ExternalLeanAngle = Mathf.Sin(elapsed * 20f)
                    * wobbleAngle * (1f - elapsed / wobbleDuration) * dir;
                elapsed += Time.deltaTime;
                yield return null;
            }
            ExternalLeanAngle = 0f;
            HasExternalLean   = false;
        }
    }
}
