using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Sensors;

namespace OvertakeGame
{
    /// <summary>
    /// Tilt-steering via iPad gyroscope. Converts physical device roll into a lateral
    /// steering input [-1, 1] and optionally keeps the virtual horizon level
    /// ("Real Rider Mode") so the driving view matches how real scooter riding feels.
    ///
    /// ── REAL RIDER MODE — HORIZON LOCK ───────────────────────────────────────────
    /// When a player tilts the iPad to steer, the screen physically rotates in their
    /// hands. Without compensation this creates a double-tilt effect:
    ///   • Physical: screen rotates N degrees in hands
    ///   • Visual: scooter mesh leans + road appears tilted on screen
    /// Combined, these two signals conflict. User tests confirmed this is disorienting.
    ///
    /// With Real Rider Mode enabled (default), the camera rolls by the inverse of the
    /// device tilt, neutralising the physical rotation's visual effect. The result:
    ///   • Road and horizon always appear flat on screen
    ///   • Only the scooter mesh leans (handled by ScooterLean)
    ///   → Exactly like the first-person view from a real scooter: YOU lean, not the road.
    ///
    /// ── CAMERA OWNERSHIP ─────────────────────────────────────────────────────────
    /// This script owns cameraRig.localEulerAngles.Z exclusively.
    /// Camera pitch (X) and yaw (Y) are left as the follow-camera script sets them.
    /// [DefaultExecutionOrder(100)] ensures LateUpdate runs after all default camera
    /// follow scripts (execution order 0) — this roll is always the final write.
    ///
    /// ── DESKTOP TESTING ──────────────────────────────────────────────────────────
    /// Enable simulateOnDesktop to test the gyro pipeline on PC using horizontal
    /// mouse movement to simulate tilt angle. Leave off for normal development.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class GyroscopeSteering : MonoBehaviour
    {
        [Header("Master Toggle")]
        public bool enableGyroSteering = true;

        [Header("Real Rider Mode — Horizon Lock")]
        [Tooltip("ON (default): camera counter-rolls to match device tilt — road stays flat.\n" +
                 "OFF: road tilts with the device — disorienting (confirmed in user tests).")]
        public bool  horizonLockEnabled   = true;
        [Range(0f, 1f)]
        public float horizonLockStrength  = 1f;
        public float horizonLockSmoothing = 12f;

        [Header("Steering Settings")]
        [Tooltip("Device tilt (degrees) that maps to maximum lateral input (1.0). 20–30° recommended.")]
        public float maxTiltAngle      = 25f;
        [Tooltip("Tilts smaller than this are ignored to prevent resting-hand drift.")]
        public float deadZone          = 3f;
        public float steeringSmoothing = 8f;
        [Tooltip("Manual tilt bias (degrees). Prefer CalibrateToCurrentAngle() at runtime.")]
        public float manualTiltOffset  = 0f;

        [Header("Desktop Testing")]
        [Tooltip("Simulate tilt with horizontal mouse movement on PC.\n" +
                 "Use A/D keys in PlayerController instead for normal development.")]
        public bool  simulateOnDesktop          = false;
        [Tooltip("Mouse delta sensitivity. Tune on target hardware; raw delta is in pixels.")]
        public float mouseSimulationSensitivity = 0.4f;

        [Header("References")]
        public PlayerController playerController;
        [Tooltip("Camera rig Transform. This script owns ONLY its Z rotation (roll).")]
        public Transform cameraRig;

        // ── Public state ───────────────────────────────────────────────────────────

        public float CurrentTiltDegrees   { get; private set; }
        public float CurrentSteeringInput { get; private set; }
        public bool  HorizonLockEnabled   => horizonLockEnabled;

        // ── Private state ──────────────────────────────────────────────────────────

        private AttitudeSensor _attitudeSensor;
        private bool           _gyroAvailable;
        private float          _smoothedTilt;
        private float          _cameraRollAngle;
        private float          _simulatedTilt;
        private Quaternion     _calibrationOffset = Quaternion.identity;

        // ── Lifecycle ──────────────────────────────────────────────────────────────

        void Start()
        {
            if (Application.isMobilePlatform && AttitudeSensor.current != null)
            {
                InputSystem.EnableDevice(AttitudeSensor.current);
                _attitudeSensor = AttitudeSensor.current;
                _gyroAvailable  = true;
            }
        }

        void Update()
        {
            if (!enableGyroSteering) return;

            float rawTilt      = GetRawTilt();
            float adjustedTilt = rawTilt - manualTiltOffset;

            if (Mathf.Abs(adjustedTilt) < deadZone)
                adjustedTilt = 0f;
            else
                adjustedTilt = Mathf.Sign(adjustedTilt) * (Mathf.Abs(adjustedTilt) - deadZone);

            _smoothedTilt        = Mathf.Lerp(_smoothedTilt, adjustedTilt, steeringSmoothing * Time.deltaTime);
            CurrentTiltDegrees   = _smoothedTilt;
            CurrentSteeringInput = Mathf.Clamp(
                _smoothedTilt / Mathf.Max(0.01f, maxTiltAngle - deadZone), -1f, 1f);

            if (playerController != null)
                playerController.mobileLateral = CurrentSteeringInput;

            // Use rawTilt (unsmoothed) for the correction — lagged correction causes horizon wobble
            float targetRoll = horizonLockEnabled ? (-rawTilt * horizonLockStrength) : 0f;
            _cameraRollAngle = Mathf.LerpAngle(_cameraRollAngle, targetRoll, horizonLockSmoothing * Time.deltaTime);
        }

        void LateUpdate()
        {
            if (cameraRig == null) return;
            float rollToApply = (enableGyroSteering && horizonLockEnabled) ? _cameraRollAngle : 0f;
            Vector3 euler = cameraRig.localEulerAngles;
            euler.z = rollToApply;
            cameraRig.localEulerAngles = euler;
        }

        // ── Public API ─────────────────────────────────────────────────────────────

        public void SetEnabled(bool enabled)
        {
            enableGyroSteering = enabled;
            if (!enabled)
            {
                CurrentSteeringInput = 0f;
                _smoothedTilt        = 0f;
                _cameraRollAngle     = 0f;
                if (playerController != null) playerController.mobileLateral = 0f;
                ResetCameraRoll();

                if (_gyroAvailable && _attitudeSensor != null)
                    InputSystem.DisableDevice(_attitudeSensor);
            }
            else if (_gyroAvailable && _attitudeSensor != null)
            {
                InputSystem.EnableDevice(_attitudeSensor);
            }
        }

        public void SetHorizonLock(bool enabled)
        {
            horizonLockEnabled = enabled;
            if (!enabled) { _cameraRollAngle = 0f; ResetCameraRoll(); }
        }

        public void CalibrateToCurrentAngle()
        {
            if (_gyroAvailable && _attitudeSensor != null)
            {
                _calibrationOffset = Quaternion.Inverse(
                    ConvertAttitudeToUnity(_attitudeSensor.attitude.ReadValue()));
            }
            else
            {
                _simulatedTilt   = 0f;
                _smoothedTilt    = 0f;
                manualTiltOffset = 0f;
            }
        }

        // ── Private helpers ────────────────────────────────────────────────────────

        private float GetRawTilt()
        {
            if (_gyroAvailable && _attitudeSensor != null)
            {
                Quaternion attitude = _calibrationOffset
                                    * ConvertAttitudeToUnity(_attitudeSensor.attitude.ReadValue());
                float roll = attitude.eulerAngles.z;
                if (roll > 180f) roll -= 360f;
                return -roll;
            }

            if (simulateOnDesktop && !Application.isMobilePlatform && Mouse.current != null)
            {
                float mouseDelta = Mouse.current.delta.ReadValue().x;
                _simulatedTilt  += mouseDelta * mouseSimulationSensitivity;
                _simulatedTilt   = Mathf.Clamp(_simulatedTilt, -maxTiltAngle, maxTiltAngle);

                if (Mathf.Abs(mouseDelta) < 1f)
                    _simulatedTilt = Mathf.MoveTowards(_simulatedTilt, 0f, 15f * Time.deltaTime);

                return _simulatedTilt;
            }

            return 0f;
        }

        private void ResetCameraRoll()
        {
            if (cameraRig == null) return;
            Vector3 e = cameraRig.localEulerAngles;
            e.z = 0f;
            cameraRig.localEulerAngles = e;
        }

        /// <summary>
        /// Maps AttitudeSensor's output quaternion to Unity landscape screen space.
        /// Equivalent to the legacy Input.gyro.attitude conversion on iOS.
        /// </summary>
        private static Quaternion ConvertAttitudeToUnity(Quaternion q) =>
            Quaternion.Euler(90f, 0f, 0f) * q;

        void OnDisable()
        {
            if (_gyroAvailable && _attitudeSensor != null)
                InputSystem.DisableDevice(_attitudeSensor);
        }
    }
}
