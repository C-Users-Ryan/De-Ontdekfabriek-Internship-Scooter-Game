using UnityEngine;
using System.Collections;

namespace OvertakeGame
{
    /// <summary>
    /// Player scooter controller. Player Z is FIXED — the world moves toward the player.
    /// Gas/brake feeds into WorldSpeed. Lateral movement drives Rigidbody X velocity.
    /// Supports keyboard, mobile touch (via MobileInputUI), and gyroscope (via GyroscopeSteering).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Lateral Movement")]
        public float lateralSpeed        = 6f;
        public float lateralAcceleration = 30f;
        public float roadHalfWidth       = 4f;

        [Header("Pothole / Rock Hit Response")]
        public float potholeWobbleDuration = 0.6f;
        public float potholeWobbleAngle    = 12f;

        [Header("Key Bindings")]
        public KeyCode gasKey   = KeyCode.W;
        public KeyCode brakeKey = KeyCode.S;
        public KeyCode leftKey  = KeyCode.A;
        public KeyCode rightKey = KeyCode.D;

        [Header("References")]
        public Speedometer speedometer;
        public ScooterLean scooterLean;

        // Public state read by ScooterLean, OvertakeDetector, SpeedMonitor, etc.
        public float CurrentSpeedKmh     => WorldSpeed.Instance != null ? WorldSpeed.Instance.CurrentKmh : 0f;
        public float CurrentSpeedMs      => WorldSpeed.Instance != null ? WorldSpeed.Instance.Current    : 0f;
        public float CurrentLateralInput { get; private set; }
        public float ExternalLeanAngle   { get; private set; }
        public bool  HasExternalLean     { get; private set; }

        // Set by MobileInputUI
        [HideInInspector] public bool  mobileGas;
        [HideInInspector] public bool  mobileBrake;
        [HideInInspector] public float mobileLateral;

        private float     _lateralVelocity;
        private Rigidbody _rb;

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

            bool  gas    = Input.GetKey(gasKey)   || mobileGas;
            bool  brake  = Input.GetKey(brakeKey) || mobileBrake;
            float lateral = 0f;
            if (Input.GetKey(leftKey))  lateral -= 1f;
            if (Input.GetKey(rightKey)) lateral += 1f;
            lateral = Mathf.Clamp(lateral + mobileLateral, -1f, 1f);

            WorldSpeed.Instance?.SetInput(gas, brake);
            CurrentLateralInput = lateral;
            float targetVelocity = lateral * lateralSpeed;
            _lateralVelocity = Mathf.MoveTowards(_lateralVelocity, targetVelocity, lateralAcceleration * Time.deltaTime);

            // Update analytics distance each frame
            // Distance is approximated from world speed accumulation
            _distanceAccumulator += CurrentSpeedMs * Time.deltaTime;
            GameManager.Instance?.UpdateDistance(_distanceAccumulator);
        }

        void FixedUpdate()
        {
            Vector3 steerAxis    = RoadDirection.SteerpAxis;
            float   steerPos     = Vector3.Dot(_rb.position, steerAxis);
            float   clampedSteer = Mathf.Clamp(steerPos, -roadHalfWidth, roadHalfWidth);

            if ((clampedSteer <= -roadHalfWidth && _lateralVelocity < 0f) ||
                (clampedSteer >=  roadHalfWidth && _lateralVelocity > 0f))
                _lateralVelocity = 0f;

            _rb.linearVelocity = steerAxis * _lateralVelocity;

            if (steerPos != clampedSteer)
                _rb.position += steerAxis * (clampedSteer - steerPos);
        }

        private float _distanceAccumulator;

        public void ApplyPotholeHit() => StartCoroutine(PotholeWobble());

        private IEnumerator PotholeWobble()
        {
            HasExternalLean = true;
            float elapsed = 0f, dir = Random.value > 0.5f ? 1f : -1f;
            while (elapsed < potholeWobbleDuration)
            {
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
