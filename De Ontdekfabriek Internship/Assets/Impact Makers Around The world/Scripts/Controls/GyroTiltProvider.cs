using UnityEngine;
using UnityEngine.InputSystem;
using KenyaScooter.Config;

namespace KenyaScooter.Controls
{
    /// <summary>
    /// Gyroscope steering via the New Input System (M5, M8). Reads the gravity vector
    /// (GravitySensor, falling back to Accelerometer) and converts it to a roll angle
    /// for the current screen orientation. Gravity is drift-free for roll, which is why
    /// it is preferred over AttitudeSensor here (decision logged in D17).
    /// Calibration stores the current roll as zero, so any grip style is neutral (M8).
    /// </summary>
    public sealed class GyroTiltProvider : IScooterInput
    {
        private readonly InputConfig config;
        private Vector3 filteredGravity = new Vector3(0f, -1f, 0f);
        private float calibrationOffset;

        public float Lateral { get; private set; }
        public float Gas => 0f;
        public float Brake => 0f;
        /// <summary>Roll after calibration, before dead zone — what Real Rider Mode counter-rotates (M7).</summary>
        public float TiltDegrees { get; private set; }
        public bool Available { get; private set; }

        public GyroTiltProvider(InputConfig config)
        {
            this.config = config;
            EnableSensors();
        }

        public void Tick(float deltaTime)
        {
            Vector3 gravity;
            if (!TryReadGravity(out gravity))
            {
                Available = false;
                Lateral = 0f;
                TiltDegrees = 0f;
                return;
            }

            Available = true;
            filteredGravity = Vector3.Lerp(filteredGravity, gravity.normalized,
                Mathf.Clamp01(config.gyroSmoothing * deltaTime));

            float roll = RollForOrientation(filteredGravity) - calibrationOffset;
            if (config.invertGyro) roll = -roll;
            TiltDegrees = roll;

            float magnitude = Mathf.Abs(roll);
            if (magnitude < config.gyroDeadZoneDegrees)
            {
                Lateral = 0f;
                return;
            }

            float normalised = Mathf.InverseLerp(config.gyroDeadZoneDegrees, config.gyroMaxTiltDegrees, magnitude);
            Lateral = Mathf.Sign(roll) * normalised * config.motionSensitivity;
        }

        public void Calibrate()
        {
            // Fold the current post-calibration roll back into the offset. The
            // inversion is applied after the offset, so it must be undone here.
            calibrationOffset += config.invertGyro ? -TiltDegrees : TiltDegrees;
        }

        private static void EnableSensors()
        {
            if (GravitySensor.current != null) InputSystem.EnableDevice(GravitySensor.current);
            if (Accelerometer.current != null) InputSystem.EnableDevice(Accelerometer.current);
        }

        private static bool TryReadGravity(out Vector3 gravity)
        {
            if (GravitySensor.current != null)
            {
                gravity = GravitySensor.current.gravity.ReadValue();
                return true;
            }
            if (Accelerometer.current != null)
            {
                // Raw acceleration ≈ gravity when the device is held steadily; the
                // low-pass filter in Tick removes most hand movement.
                gravity = Accelerometer.current.acceleration.ReadValue();
                return true;
            }
            gravity = Vector3.zero;
            return false;
        }

        /// <summary>
        /// Signed roll (positive = steering right) derived from the device-space
        /// gravity vector, per screen orientation. Derivations documented in the
        /// Unity Setup Guide.
        /// </summary>
        private static float RollForOrientation(Vector3 g)
        {
            switch (Screen.orientation)
            {
                case ScreenOrientation.Portrait:
                    return Mathf.Atan2(g.x, -g.y) * Mathf.Rad2Deg;
                case ScreenOrientation.PortraitUpsideDown:
                    return Mathf.Atan2(-g.x, g.y) * Mathf.Rad2Deg;
                case ScreenOrientation.LandscapeRight:
                    return Mathf.Atan2(g.y, g.x) * Mathf.Rad2Deg;
                case ScreenOrientation.LandscapeLeft:
                default:
                    return Mathf.Atan2(-g.y, -g.x) * Mathf.Rad2Deg;
            }
        }
    }
}
