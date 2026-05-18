using UnityEngine;

namespace OvertakeGame
{
    /// <summary>
    /// Drives lateral steering from the iPad's gyroscope (device tilt).
    /// The player physically tilts the tablet left/right to steer the scooter.
    ///
    /// Counter-rotation: a camera rig parent rotates opposite to the tilt so
    /// the road horizon always appears level to the player's eyes, simulating
    /// the feeling of the scooter leaning without the screen appearing to tilt.
    ///
    /// SETUP:
    ///   1. Create a camera hierarchy:
    ///        CameraRig (empty GameObject)  ← assign to cameraRig
    ///          └── Main Camera             ← your actual camera
    ///   2. Attach this script to any persistent GameObject (e.g. player root).
    ///   3. Assign playerController and cameraRig in the Inspector.
    ///   4. Toggle enableGyroSteering on/off per session or per platform.
    ///
    /// TESTING ON PC:
    ///   Enable simulateOnDesktop and use the mouse X axis to simulate tilt.
    ///   This lets you test the full system without a physical device.
    /// </summary>
    public class GyroscopeSteering : MonoBehaviour
    {
        // ── Toggles ───────────────────────────────────────────────────────────
        [Header("Master Toggle")]
        [Tooltip("Enable gyroscope-based steering. When OFF, falls back to touch/keyboard input. " +
                 "Can be toggled at runtime from the Inspector or via SetEnabled().")]
        public bool enableGyroSteering = true;

        [Header("Counter-Rotation")]
        [Tooltip("Rotate the camera rig opposite to device tilt so the horizon stays level. " +
                 "Disable this to see the raw tilted view (useful for debugging).")]
        public bool enableHorizonLock = true;

        [Tooltip("How much of the tilt angle is cancelled out. " +
                 "1.0 = full cancel (horizon always perfectly level). " +
                 "0.5 = half cancel (slight lean visible). " +
                 "0.0 = no cancel (full tilt visible, probably nauseating).")]
        [Range(0f, 1f)]
        public float horizonLockStrength = 1f;

        [Tooltip("How smoothly the counter-rotation follows the tilt. " +
                 "Higher = snappier. Lower = more lag (can cause nausea).")]
        public float horizonLockSmoothing = 12f;

        // ── Steering ──────────────────────────────────────────────────────────
        [Header("Steering Settings")]
        [Tooltip("Device tilt in degrees at which steering reaches maximum lateral speed. " +
                 "Lower = more sensitive. Higher = requires more physical tilt.")]
        public float maxTiltAngle = 25f;

        [Tooltip("Dead zone in degrees. Small tilts within this range are ignored " +
                 "so the player can hold the device slightly imperfectly without drifting.")]
        public float deadZone = 3f;

        [Tooltip("Smoothing applied to the raw gyro input. Higher = snappier response. " +
                 "Lower = more dampened, harder to over-steer.")]
        public float steeringSmoothing = 8f;

        // ── Desktop Simulation ─────────────────────────────────────────────────
        [Header("Desktop / PC Testing")]
        [Tooltip("When true, simulates gyro tilt using mouse horizontal movement. " +
                 "Allows testing the full system on PC without a physical device. " +
                 "Automatically disabled on mobile builds.")]
        public bool simulateOnDesktop = true;

        [Tooltip("How much the mouse X movement translates to simulated tilt degrees.")]
        public float mouseSimulationSensitivity = 60f;

        // ── References ────────────────────────────────────────────────────────
        [Header("References")]
        public PlayerController playerController;

        [Tooltip("The parent of the camera. This is what gets counter-rotated. " +
                 "Must be a separate GameObject from the camera itself so the camera " +
                 "can be rotated independently of the camera rig's position.")]
        public Transform cameraRig;

        // ── State ─────────────────────────────────────────────────────────────
        /// <summary>Current tilt angle in degrees. Negative = left, positive = right.</summary>
        public float CurrentTiltDegrees { get; private set; }

        /// <summary>Normalised steering input in range [-1, 1].</summary>
        public float CurrentSteeringInput { get; private set; }

        private Gyroscope _gyro;
        private bool      _gyroAvailable;
        private float     _smoothedTilt;
        private float     _currentCameraRollAngle;
        private float     _simulatedTilt; // for desktop testing

        // ── Calibration ───────────────────────────────────────────────────────
        private Quaternion _calibrationOffset = Quaternion.identity;

        [Header("Calibration")]
        [Tooltip("Degrees offset applied to the raw gyro reading. " +
                 "Use CalibrateToCurrentAngle() at session start to zero out the " +
                 "player's natural holding angle.")]
        public float manualTiltOffset = 0f;

        void Start()
        {
            InitGyroscope();
        }

        void Update()
        {
            if (!enableGyroSteering) return;

            float rawTilt = GetRawTilt();
            float adjustedTilt = rawTilt - manualTiltOffset;

            // Apply dead zone
            if (Mathf.Abs(adjustedTilt) < deadZone)
                adjustedTilt = 0f;
            else
                adjustedTilt = Mathf.Sign(adjustedTilt) * (Mathf.Abs(adjustedTilt) - deadZone);

            // Smooth the tilt
            _smoothedTilt = Mathf.Lerp(_smoothedTilt, adjustedTilt, steeringSmoothing * Time.deltaTime);

            // Convert to normalised [-1, 1] steering input
            CurrentTiltDegrees   = _smoothedTilt;
            CurrentSteeringInput = Mathf.Clamp(_smoothedTilt / (maxTiltAngle - deadZone), -1f, 1f);

            // Push steering input to PlayerController
            if (playerController != null)
                playerController.mobileLateral = CurrentSteeringInput;

            // Counter-rotate the camera rig to keep the horizon level
            if (enableHorizonLock && cameraRig != null)
                ApplyHorizonLock(rawTilt);
        }

        // ── Gyroscope initialisation ───────────────────────────────────────────
        private void InitGyroscope()
        {
            bool isMobile = Application.isMobilePlatform;
            bool useSimulation = simulateOnDesktop && !isMobile;

            if (isMobile && SystemInfo.supportsGyroscope)
            {
                _gyro           = Input.gyro;
                _gyro.enabled   = true;
                _gyroAvailable  = true;
                Debug.Log("[GyroscopeSteering] Hardware gyroscope initialised.");
            }
            else if (useSimulation)
            {
                _gyroAvailable = false; // use mouse simulation path
                Debug.Log("[GyroscopeSteering] Desktop simulation mode active. Move mouse horizontally to steer.");
            }
            else
            {
                _gyroAvailable = false;
                Debug.Log("[GyroscopeSteering] No gyroscope available and simulation disabled.");
            }
        }

        // ── Raw tilt reading ───────────────────────────────────────────────────
        private float GetRawTilt()
        {
            if (_gyroAvailable)
            {
                // On iPad held in landscape: the gyro gravity vector's X component
                // gives us the tilt angle. When flat: gravity.x = 0.
                // Tilted right: gravity.x > 0. Tilted left: gravity.x < 0.
                // We use Attitude (orientation quaternion) for a more stable reading.
                Quaternion attitude = _calibrationOffset * ConvertGyroToUnity(_gyro.attitude);
                Vector3 eulers      = attitude.eulerAngles;

                // Extract roll angle (Z in Unity = roll when device is in landscape)
                float roll = eulers.z;
                // Normalise from 0-360 to -180-180
                if (roll > 180f) roll -= 360f;
                return -roll; // negate so right tilt = positive
            }
            else if (simulateOnDesktop && !Application.isMobilePlatform)
            {
                // Mouse X simulation: drag left = tilt left, drag right = tilt right
                _simulatedTilt += Input.GetAxis("Mouse X") * mouseSimulationSensitivity * Time.deltaTime;
                _simulatedTilt  = Mathf.Clamp(_simulatedTilt, -maxTiltAngle, maxTiltAngle);

                // Drift back toward 0 when no mouse movement (simulates returning device to level)
                if (Mathf.Abs(Input.GetAxis("Mouse X")) < 0.01f)
                    _simulatedTilt = Mathf.MoveTowards(_simulatedTilt, 0f, 15f * Time.deltaTime);

                return _simulatedTilt;
            }

            return 0f;
        }

        // ── Counter-rotation (horizon lock) ────────────────────────────────────
        private void ApplyHorizonLock(float rawTiltDegrees)
        {
            // Target camera roll = opposite to device tilt, scaled by lock strength
            float targetRoll      = -rawTiltDegrees * horizonLockStrength;
            _currentCameraRollAngle = Mathf.LerpAngle(
                _currentCameraRollAngle, targetRoll, horizonLockSmoothing * Time.deltaTime);

            // Apply only the roll (Z) to the camera rig, preserving its existing X/Y rotation
            Vector3 rigEulers   = cameraRig.localEulerAngles;
            rigEulers.z         = _currentCameraRollAngle;
            cameraRig.localEulerAngles = rigEulers;
        }

        // ── Calibration ───────────────────────────────────────────────────────
        /// <summary>
        /// Call this at session start (or via a UI button) to zero out the player's
        /// natural holding angle. Whatever angle the device is at when this is called
        /// becomes the "neutral" steering position.
        /// </summary>
        public void CalibrateToCurrentAngle()
        {
            if (_gyroAvailable)
            {
                _calibrationOffset = Quaternion.Inverse(ConvertGyroToUnity(_gyro.attitude));
                Debug.Log("[GyroscopeSteering] Calibrated to current device angle.");
            }
            else
            {
                // Desktop: zero out simulated tilt
                _simulatedTilt  = 0f;
                _smoothedTilt   = 0f;
                manualTiltOffset = 0f;
                Debug.Log("[GyroscopeSteering] Desktop calibration reset.");
            }
        }

        // ── Unity gyro coordinate conversion ──────────────────────────────────
        // Unity's gyroscope uses a right-handed coordinate system different from
        // the Unity scene coordinate system. This converts between them.
        private static Quaternion ConvertGyroToUnity(Quaternion q)
        {
            return new Quaternion(q.x, q.y, -q.z, -q.w);
        }

        // ── Public API ────────────────────────────────────────────────────────
        /// <summary>Enable or disable gyro steering at runtime (e.g. from a settings menu).</summary>
        public void SetEnabled(bool enabled)
        {
            enableGyroSteering = enabled;

            if (!enabled)
            {
                // Clear steering input so the scooter doesn't lock into a direction
                CurrentSteeringInput = 0f;
                if (playerController != null)
                    playerController.mobileLateral = 0f;

                // Return camera rig to upright
                if (cameraRig != null)
                {
                    Vector3 e = cameraRig.localEulerAngles;
                    e.z = 0f;
                    cameraRig.localEulerAngles = e;
                }
            }

            if (enabled && _gyroAvailable && _gyro != null)
                _gyro.enabled = true;
        }

        void OnDisable()
        {
            // Clean up: disable hardware gyro when script is disabled to save battery
            if (_gyroAvailable && _gyro != null)
                _gyro.enabled = false;
        }
    }
}
