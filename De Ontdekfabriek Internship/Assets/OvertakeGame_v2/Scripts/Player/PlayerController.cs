using UnityEngine;
using System.Collections;

namespace OvertakeGame
{
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Speed Settings")]
        public float baseSpeed           = 10f;
        public float maxSpeed            = 30f;
        public float accelerationForce   = 15f;
        public float brakeForce          = 20f;
        public float naturalDeceleration = 5f;

        [Header("Lateral Movement")]
        public float lateralSpeed  = 6f;
        public float roadHalfWidth = 4f;

        [Header("Pothole Hit Response")]
        [Tooltip("How much to reduce forward speed on pothole hit (m/s). Does NOT move the player backwards.")]
        public float potholeSpeedPenalty   = 3f;
        [Tooltip("Duration of the scooter wobble after hitting a pothole (seconds).")]
        public float potholeWobbleDuration = 0.6f;
        [Tooltip("Max lean angle of the wobble (degrees).")]
        public float potholeWobbleAngle    = 12f;

        [Header("Key Bindings")]
        public KeyCode gasKey   = KeyCode.W;
        public KeyCode brakeKey = KeyCode.S;
        public KeyCode leftKey  = KeyCode.A;
        public KeyCode rightKey = KeyCode.D;

        [Header("References")]
        public Speedometer speedometer;
        public ScooterLean scooterLean;

        // ── Public read-only state ─────────────────────────────────────────────
        public float CurrentSpeedKmh     => _currentSpeed * 3.6f;
        public float CurrentSpeedMs      => _currentSpeed;
        public float CurrentLateralInput { get; private set; }
        public float ExternalLeanAngle   { get; private set; }
        public bool  HasExternalLean     { get; private set; }

        // ── Private ────────────────────────────────────────────────────────────
        private float     _currentSpeed;
        private float     _currentLateralVelocity;   // drives rb.linearVelocity.x
        private Rigidbody _rb;
        private bool      _speedOverrideActive;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.constraints  = RigidbodyConstraints.FreezeRotation
                             | RigidbodyConstraints.FreezePositionY;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            // Prevent the Rigidbody from being pushed by any physics collisions
            // (potholes must use trigger colliders, but this is a safety net)
            _rb.isKinematic   = false;
            _currentSpeed     = baseSpeed;

            if (scooterLean == null)
                scooterLean = GetComponentInChildren<ScooterLean>();
        }

        // Update: read input, compute desired speeds
        void Update()
        {
            speedometer?.UpdateSpeed(CurrentSpeedKmh);

            var state = GameManager.Instance?.CurrentState;
            if (state == GameManager.GameState.Playing && !_speedOverrideActive)
            {
                HandleSpeed();
                HandleLateral();
            }
            else
            {
                CurrentLateralInput        = 0f;
                _currentLateralVelocity    = 0f;
            }
        }

        // FixedUpdate: apply both forward and lateral velocity in one place.
        // This is the ONLY place position/velocity is written, which prevents
        // transform.position and Rigidbody fighting each other.
        void FixedUpdate()
        {
            // Clamp X position inside road bounds before applying velocity
            Vector3 pos = _rb.position;
            float clampedX = Mathf.Clamp(pos.x, -roadHalfWidth, roadHalfWidth);

            // If we're at the wall and still trying to push into it, zero lateral vel
            if ((clampedX <= -roadHalfWidth && _currentLateralVelocity < 0f) ||
                (clampedX >=  roadHalfWidth && _currentLateralVelocity > 0f))
            {
                _currentLateralVelocity = 0f;
            }

            // Apply combined velocity: X = lateral steering, Z = forward speed
            _rb.linearVelocity = new Vector3(
                _currentLateralVelocity,
                0f,
                _currentSpeed
            );

            // Hard-clamp position if we've drifted outside bounds
            if (pos.x != clampedX)
            {
                pos.x       = clampedX;
                _rb.position = pos;
            }
        }

        // ── Input handlers ─────────────────────────────────────────────────────

        private void HandleSpeed()
        {
            if (Input.GetKey(gasKey))
                _currentSpeed = Mathf.MoveTowards(_currentSpeed, maxSpeed,
                                                   accelerationForce * Time.deltaTime);
            else if (Input.GetKey(brakeKey))
                _currentSpeed = Mathf.MoveTowards(_currentSpeed, 0f,
                                                   brakeForce * Time.deltaTime);
            else
                _currentSpeed = Mathf.MoveTowards(_currentSpeed, baseSpeed,
                                                   naturalDeceleration * Time.deltaTime);
        }

        private void HandleLateral()
        {
            float input = 0f;
            if (Input.GetKey(leftKey))  input -= 1f;
            if (Input.GetKey(rightKey)) input += 1f;
            CurrentLateralInput     = input;
            _currentLateralVelocity = input * lateralSpeed;
        }

        // ── Pothole hit ────────────────────────────────────────────────────────
        public void ApplyPotholeHit()
        {
            // Only reduce forward speed — never go below 0, never add backwards velocity
            _currentSpeed = Mathf.Max(0f, _currentSpeed - potholeSpeedPenalty);
            StartCoroutine(PotholeWobble());
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

        // ── Speed override (used by CheckpointManager for braking) ─────────────
        public void OverrideSpeed(float speed)
        {
            _speedOverrideActive = true;
            _currentSpeed        = Mathf.Max(0f, speed);
        }

        public void ClearSpeedOverride()
        {
            _speedOverrideActive = false;
            _currentSpeed        = baseSpeed;
        }
    }
}
