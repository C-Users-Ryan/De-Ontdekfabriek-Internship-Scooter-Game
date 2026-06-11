using UnityEngine;
using System.Collections;

namespace OvertakeGame
{
    /// <summary>
    /// Player scooter controller. The player is FIXED in world space — the road, traffic,
    /// and hazards translate toward the player (world-moves-player architecture).
    ///
    /// ── INPUT PIPELINE ──────────────────────────────────────────────────────────────
    ///   Desktop   │  W / S  → gas / brake   (KeyCode fields below)
    ///             │  A / D  → steer left / right
    ///   Mobile    │  mobileGas / mobileBrake  set by MobileInputUI  (touch zones)
    ///             │  mobileLateral            set by GyroscopeSteering (tilt)
    ///
    ///   All sources are added together and clamped, so keyboard + tilt work simultaneously
    ///   on desktop for testing the full gyro pipeline.
    ///
    /// ── SPEED ────────────────────────────────────────────────────────────────────────
    ///   Delegated to WorldSpeed singleton.  SetInput(gas, brake) is called each frame.
    ///   PlayerController never reads WorldSpeed.Current directly — it exposes helpers.
    ///
    /// ── LATERAL MOVEMENT ────────────────────────────────────────────────────────────
    ///   Velocity is smoothed through MoveTowards with lateralAcceleration, giving the
    ///   scooter real steering weight. Instant direction reversal is physically impossible
    ///   on a real scooter; the smoothing reproduces that constraint at a tunable level.
    ///
    ///   All axis math uses RoadDirection.SteerpAxis (not hardcoded Vector3.right) so
    ///   lateral movement stays correct after 90° road turns.
    ///
    /// ── LEAN ─────────────────────────────────────────────────────────────────────────
    ///   Delegated to ScooterLean on the mesh child. This script exposes CurrentLateralInput,
    ///   ExternalLeanAngle, and HasExternalLean for ScooterLean to read.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Lateral Movement")]
        [Tooltip("Maximum lateral speed in m/s at full input.")]
        public float lateralSpeed        = 6f;
        [Tooltip("How fast lateral velocity builds and changes direction.\n" +
                 "Lower = heavier, more realistic steering weight.\n" +
                 "Higher = snappier, arcade feel.\n" +
                 "Recommended range: 20–50 m/s²")]
        public float lateralAcceleration = 30f;
        [Tooltip("Half-width of the playable road in metres. Player is clamped to ±this.")]
        public float roadHalfWidth       = 4f;

        [Header("Pothole / Rock Hit Response")]
        public float potholeWobbleDuration = 0.6f;
        public float potholeWobbleAngle    = 12f;

        [Header("Key Bindings (Desktop Debug)")]
        public KeyCode gasKey   = KeyCode.W;
        public KeyCode brakeKey = KeyCode.S;
        public KeyCode leftKey  = KeyCode.A;
        public KeyCode rightKey = KeyCode.D;

        [Header("References")]
        public Speedometer speedometer;
        public ScooterLean scooterLean;

        // ── Public state (read by ScooterLean, OvertakeDetector, SpeedMonitor, etc.) ──

        /// <summary>Current world speed in km/h, sourced from WorldSpeed.</summary>
        public float CurrentSpeedKmh     => WorldSpeed.Instance != null ? WorldSpeed.Instance.CurrentKmh : 0f;
        /// <summary>Current world speed in m/s, sourced from WorldSpeed.</summary>
        public float CurrentSpeedMs      => WorldSpeed.Instance != null ? WorldSpeed.Instance.Current    : 0f;
        /// <summary>Steering input in [-1, 1]. Combines keyboard and gyro/touch contributions.</summary>
        public float CurrentLateralInput { get; private set; }
        /// <summary>Lean angle (degrees) driven by external events such as pothole hits.</summary>
        public float ExternalLeanAngle   { get; private set; }
        /// <summary>True while a pothole / rock wobble coroutine is running.</summary>
        public bool  HasExternalLean     { get; private set; }

        // ── Written by MobileInputUI (gas/brake) and GyroscopeSteering (lateral) ──────
        [HideInInspector] public bool  mobileGas;
        [HideInInspector] public bool  mobileBrake;
        [HideInInspector] public float mobileLateral;  // owned by GyroscopeSteering on mobile

        // ── Private state ─────────────────────────────────────────────────────────────
        private float     _lateralVelocity;      // current lateral m/s — smoothed, not instant
        private float     _distanceAccumulator;  // metres driven this session
        private Rigidbody _rb;

        // ── Unity lifecycle ───────────────────────────────────────────────────────────

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            // Freeze all rotation (lean is visual-only on the mesh child) and Y + Z translation.
            // X (lateral) is the only free axis for the Rigidbody.
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

            // ── Speed input ───────────────────────────────────────────────────
            bool gas   = Input.GetKey(gasKey)   || mobileGas;
            bool brake = Input.GetKey(brakeKey) || mobileBrake;
            WorldSpeed.Instance?.SetInput(gas, brake);

            // ── Lateral input ─────────────────────────────────────────────────
            // Keyboard (A/D) and gyro (mobileLateral) contributions add together.
            // On mobile: keyboard reads as 0 (no physical keys). On desktop: mobileLateral
            // reads as 0 unless GyroscopeSteering.simulateOnDesktop is enabled.
            float lateral = 0f;
            if (Input.GetKey(leftKey))  lateral -= 1f;
            if (Input.GetKey(rightKey)) lateral += 1f;
            CurrentLateralInput = Mathf.Clamp(lateral + mobileLateral, -1f, 1f);

            // Smoothed lateral velocity — physical scooters cannot instantly reverse
            // lateral direction. MoveTowards with lateralAcceleration reproduces the
            // natural inertia of a two-wheeled vehicle without a full physics simulation.
            float targetVelocity = CurrentLateralInput * lateralSpeed;
            _lateralVelocity = Mathf.MoveTowards(
                _lateralVelocity, targetVelocity, lateralAcceleration * Time.deltaTime);

            // ── Distance tracking ─────────────────────────────────────────────
            _distanceAccumulator += CurrentSpeedMs * Time.deltaTime;
            GameManager.Instance?.UpdateDistance(_distanceAccumulator);
        }

        void FixedUpdate()
        {
            // Lateral movement must use SteerpAxis — not a hardcoded axis.
            // SteerpAxis = Vector3.right  on a straight (-Z travel)
            // SteerpAxis = Vector3.back   after a right-turn (-X travel)
            // This keeps lateral movement perpendicular to the road on any heading.
            Vector3 steerAxis    = RoadDirection.SteerpAxis;
            float   steerPos     = Vector3.Dot(_rb.position, steerAxis);
            float   clampedSteer = Mathf.Clamp(steerPos, -roadHalfWidth, roadHalfWidth);

            // Kill lateral velocity at road edges (kerb impact — no overshoot)
            if ((clampedSteer <= -roadHalfWidth && _lateralVelocity < 0f) ||
                (clampedSteer >=  roadHalfWidth && _lateralVelocity > 0f))
                _lateralVelocity = 0f;

            // Apply only along the steer axis — travel axis stays frozen by Rigidbody constraints
            Vector3 steerVel   = steerAxis * _lateralVelocity;
            _rb.linearVelocity = new Vector3(steerVel.x, 0f, steerVel.z);

            // Hard-clamp position to road bounds (handles edge-case tunnelling at high velocity)
            if (!Mathf.Approximately(steerPos, clampedSteer))
            {
                Vector3 travelComp = _rb.position - steerPos * steerAxis;
                _rb.position       = travelComp + clampedSteer * steerAxis;
            }
        }

        // ── External events ───────────────────────────────────────────────────────────

        /// <summary>Called by Pothole and RockObstacle on hit. Drives a decaying wobble lean.</summary>
        public void ApplyPotholeHit() => StartCoroutine(PotholeWobble());

        private IEnumerator PotholeWobble()
        {
            HasExternalLean = true;
            float elapsed = 0f;
            float dir     = Random.value > 0.5f ? 1f : -1f;
            while (elapsed < potholeWobbleDuration)
            {
                // Decaying oscillation: sin wave * (1 - t) fades out over the duration
                ExternalLeanAngle = Mathf.Sin(elapsed * 20f)
                    * potholeWobbleAngle * (1f - elapsed / potholeWobbleDuration) * dir;
                elapsed += Time.deltaTime;
                yield return null;
            }
            ExternalLeanAngle = 0f;
            HasExternalLean   = false;
        }
    }
}
