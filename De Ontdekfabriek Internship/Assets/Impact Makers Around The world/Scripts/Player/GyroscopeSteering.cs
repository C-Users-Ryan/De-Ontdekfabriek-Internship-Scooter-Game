using UnityEngine;

namespace OvertakeGame
{
    /// <summary>
    /// Tilt-steering via iPad gyroscope. Converts physical device roll into a lateral
    /// steering input [-1, 1] and optionally keeps the virtual horizon level
    /// ("Real Rider Mode") so the driving view matches how real scooter riding feels.
    ///
    /// ── REAL RIDER MODE — HORIZON LOCK ──────────────────────────────────────────────
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
    /// Toggle via SetHorizonLock(bool), GyroSettingsUI buttons, or H key during testing.
    ///
    /// ── CAMERA OWNERSHIP ───────────────────────────────────────────────────────────
    /// This script owns cameraRig.localEulerAngles.Z exclusively.
    /// Camera pitch (X) and yaw (Y) are read each LateUpdate() from whatever the
    /// follow-camera script set in its Update() — this script never touches them.
    /// Using [DefaultExecutionOrder(100)] ensures our LateUpdate runs AFTER all default
    /// camera follow scripts (execution order 0), so the roll is always the final write.
    ///
    /// ── DESKTOP TESTING ─────────────────────────────────────────────────────────────
    /// Default desktop mode: gyro does nothing. Use A/D keys in PlayerController.
    /// Optional: enable simulateOnDesktop to test the gyro pipeline (smoothing, dead-zone,
    /// horizon lock) using horizontal mouse movement to simulate tilt angle.
    /// </summary>
    [DefaultExecutionOrder(100)]   // run after default scripts so LateUpdate is the final camera write
    public class GyroscopeSteering : MonoBehaviour
    {
        [Header("Master Toggle")]
        [Tooltip("Master on/off for the entire gyro steering system.\n" +
                 "When disabled, mobileLateral is zeroed and camera roll is returned to 0.")]
        public bool enableGyroSteering = true;

        [Header("Real Rider Mode — Horizon Lock")]
        [Tooltip("Keep the virtual horizon level as the device tilts.\n\n" +
                 "ON (default): camera counter-rolls to match device tilt — road stays flat,\n" +
                 "             scooter mesh leans — matches real first-person riding POV.\n\n" +
                 "OFF: no compensation — road tilts with the device. Combined with the scooter\n" +
                 "     mesh lean this looks disorienting (confirmed in user tests Sprint 8).")]
        public bool  horizonLockEnabled   = true;
        [Range(0f, 1f)]
        [Tooltip("Compensation strength. 1 = full correction (horizon perfectly flat). " +
                 "0.8–1.0 is recommended; lower values leave a partial tilt.")]
        public float horizonLockStrength  = 1f;
        [Tooltip("Smoothing speed for the camera roll correction (degrees/second scale). " +
                 "Higher = faster snap to level. 10–15 recommended.")]
        public float horizonLockSmoothing = 12f;

        [Header("Steering Settings")]
        [Tooltip("Device tilt in degrees that maps to maximum lateral input (1.0).\n" +
                 "Smaller = more sensitive. Typical comfortable range: 20–30°.")]
        public float maxTiltAngle         = 25f;
        [Tooltip("Tilt angles smaller than this are ignored. Prevents drift when the device\n" +
                 "rests on a table or is held without deliberate steering.")]
        public float deadZone             = 3f;
        [Tooltip("Smoothing applied to the steering input. Higher = snappier response, " +
                 "more fatigue. 6–10 recommended for a 1–2 min session.")]
        public float steeringSmoothing    = 8f;
        [Tooltip("Manual tilt bias in degrees. Corrects for non-flat resting angles\n" +
                 "(e.g. iPad on a sloped stand). Prefer CalibrateToCurrentAngle() at runtime.")]
        public float manualTiltOffset     = 0f;

        [Header("Desktop Testing")]
        [Tooltip("Simulate tilt with horizontal mouse movement on PC.\n" +
                 "Leave OFF for normal development — use A/D keys in PlayerController instead.\n" +
                 "Enable to test the gyro pipeline (smoothing / dead-zone / horizon lock) on desktop.")]
        public bool  simulateOnDesktop          = false;
        public float mouseSimulationSensitivity = 60f;

        [Header("References")]
        public PlayerController playerController;
        [Tooltip("The camera rig Transform. This script owns ONLY its Z rotation (roll).\n" +
                 "Assign the camera rig parent, not the Camera itself.")]
        public Transform cameraRig;

        // ── Public state ──────────────────────────────────────────────────────────────
        /// <summary>Current smoothed tilt in degrees (post dead-zone). Read by GyroSettingsUI.</summary>
        public float CurrentTiltDegrees   { get; private set; }
        /// <summary>Current lateral steering input in [-1, 1]. Read by GyroSettingsUI.</summary>
        public float CurrentSteeringInput { get; private set; }
        /// <summary>Whether Real Rider horizon lock is currently active.</summary>
        public bool  HorizonLockEnabled   => horizonLockEnabled;

        // ── Private state ─────────────────────────────────────────────────────────────
        private Gyroscope  _gyro;
        private bool       _gyroAvailable;
        private float      _smoothedTilt;
        private float      _cameraRollAngle;  // current roll angle applied to cameraRig (degrees)
        private float      _simulatedTilt;
        private Quaternion _calibrationOffset = Quaternion.identity;

        // ── Unity lifecycle ───────────────────────────────────────────────────────────

        void Start()
        {
            if (Application.isMobilePlatform && SystemInfo.supportsGyroscope)
            {
                _gyro          = Input.gyro;
                _gyro.enabled  = true;
                _gyroAvailable = true;
            }
        }

        void Update()
        {
            if (!enableGyroSteering) return;

            float rawTilt      = GetRawTilt();
            float adjustedTilt = rawTilt - manualTiltOffset;

            // Dead-zone: ignore small tilts (resting-hand noise, surface vibration)
            // The dead-zone is subtracted from the magnitude to avoid a jump at the threshold.
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

            // Compute target camera roll. Use rawTilt (unsmoothed) for immediate compensation
            // — if the player tilts and we lag in the correction, the horizon "wobbles" before
            // settling, which is worse than a small overshoot.
            float targetRoll = horizonLockEnabled ? (-rawTilt * horizonLockStrength) : 0f;
            _cameraRollAngle  = Mathf.LerpAngle(_cameraRollAngle, targetRoll, horizonLockSmoothing * Time.deltaTime);
        }

        void LateUpdate()
        {
            // Apply camera roll AFTER all Update() camera follow scripts have run.
            // We read the camera's current X and Y (set by the follow script) and
            // only overwrite Z (roll). This way the follow script can do whatever it wants
            // with pitch and yaw without fighting us.
            if (cameraRig == null) return;

            float rollToApply = (enableGyroSteering && horizonLockEnabled)
                ? _cameraRollAngle : 0f;

            Vector3 euler = cameraRig.localEulerAngles;
            euler.z       = rollToApply;
            cameraRig.localEulerAngles = euler;
        }

        // ── Public API ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Enable or disable the entire gyroscope steering system.
        /// Called by GyroSettingsUI toggle button.
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            enableGyroSteering = enabled;
            if (!enabled)
            {
                // Reset all moving state so there is no snap when re-enabling
                CurrentSteeringInput = 0f;
                _smoothedTilt        = 0f;
                _cameraRollAngle     = 0f;
                if (playerController != null) playerController.mobileLateral = 0f;
                ResetCameraRoll();
            }
            if (enabled && _gyroAvailable && _gyro != null)
                _gyro.enabled = true;
        }

        /// <summary>
        /// Enable or disable Real Rider Mode (horizon lock).
        ///
        /// ON  → camera counter-rolls with device tilt; road stays flat; scooter mesh leans.
        /// OFF → no camera compensation; device tilt + mesh lean combine (disorienting).
        ///
        /// Called by GyroSettingsUI horizon lock button and H key shortcut.
        /// </summary>
        public void SetHorizonLock(bool enabled)
        {
            horizonLockEnabled = enabled;
            if (!enabled)
            {
                _cameraRollAngle = 0f;
                ResetCameraRoll();
            }
        }

        /// <summary>
        /// Set the current device orientation as the neutral (zero-tilt) reference.
        /// Call this at session start if the device is held at a non-flat angle.
        /// </summary>
        public void CalibrateToCurrentAngle()
        {
            if (_gyroAvailable && _gyro != null)
            {
                _calibrationOffset = Quaternion.Inverse(ConvertGyroToUnity(_gyro.attitude));
            }
            else
            {
                // Desktop: reset simulated state
                _simulatedTilt   = 0f;
                _smoothedTilt    = 0f;
                manualTiltOffset = 0f;
            }
        }

        // ── Private helpers ───────────────────────────────────────────────────────────

        private float GetRawTilt()
        {
            if (_gyroAvailable)
            {
                // Convert gyro quaternion from hardware space to Unity space,
                // apply calibration offset, then extract the roll (Z euler angle).
                Quaternion attitude = _calibrationOffset * ConvertGyroToUnity(_gyro.attitude);
                float roll          = attitude.eulerAngles.z;
                // Remap 0–360 → -180–180 so left/right tilt read as negative/positive
                if (roll > 180f) roll -= 360f;
                return -roll;   // negate: physical right-tilt → positive steering
            }

            if (simulateOnDesktop && !Application.isMobilePlatform)
            {
                // Accumulate mouse X as a simulated tilt angle
                _simulatedTilt += Input.GetAxis("Mouse X") * mouseSimulationSensitivity * Time.deltaTime;
                _simulatedTilt  = Mathf.Clamp(_simulatedTilt, -maxTiltAngle, maxTiltAngle);
                // Auto-return to centre when mouse stops (simulates righting a scooter)
                if (Mathf.Abs(Input.GetAxis("Mouse X")) < 0.01f)
                    _simulatedTilt = Mathf.MoveTowards(_simulatedTilt, 0f, 15f * Time.deltaTime);
                return _simulatedTilt;
            }

            return 0f;
        }

        /// <summary>Return the camera roll to zero without disturbing X and Y.</summary>
        private void ResetCameraRoll()
        {
            if (cameraRig == null) return;
            Vector3 e = cameraRig.localEulerAngles;
            e.z = 0f;
            cameraRig.localEulerAngles = e;
        }

        /// <summary>
        /// Unity gyroscope uses a different coordinate handedness than Unity's world space.
        /// This conversion maps the hardware quaternion to a Unity-compatible orientation.
        /// Reference: Unity documentation — Input.gyro.attitude.
        /// </summary>
        private static Quaternion ConvertGyroToUnity(Quaternion q) =>
            new Quaternion(q.x, q.y, -q.z, -q.w);

        void OnDisable()
        {
            if (_gyroAvailable && _gyro != null) _gyro.enabled = false;
        }
    }
}
