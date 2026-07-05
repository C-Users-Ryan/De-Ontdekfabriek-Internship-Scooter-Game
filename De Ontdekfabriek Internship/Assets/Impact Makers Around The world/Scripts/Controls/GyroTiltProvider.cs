using UnityEngine;
using UnityEngine.InputSystem;
using KenyaScooter.Config;

namespace KenyaScooter.Controls
{
    /// <summary>
    /// Tilt steering through the New Input System (M5, M8). It reads the gravity vector (GravitySensor,
    /// or Accelerometer if that's missing) and turns it into a roll angle for the current screen
    /// orientation. Gravity doesn't drift for roll, which is why it's used instead of AttitudeSensor
    /// (decision logged in D17). Calibrating stores the current roll as zero, so any way of holding the
    /// tablet reads as neutral (M8).
    ///
    /// The raw roll is cleaned by a speed-adaptive low-pass (a One-Euro-style filter): when the tablet is
    /// held steady the smoothing is heavy, so hand tremor is swallowed; the instant you tilt deliberately
    /// the filter sharpens in proportion to how fast you are tilting, so a real steer tracks the hand with
    /// almost no lag. That removes the old fixed-smoothing trade-off (smooth OR responsive, never both).
    /// The cleaned tilt is then shaped by a response curve so small tilts steer gently while full lock is
    /// still reachable. The filter is frame-rate independent (alpha = 1 - e^(-k·dt)), so it feels identical
    /// on a slow tablet and a fast desktop.
    /// </summary>
    public sealed class GyroTiltProvider : IScooterInput
    {
        // How quickly the measured tilt speed itself is smoothed before it drives the adaptive cutoff, so
        // raw sensor noise can't masquerade as a fast, deliberate steer. Per second.
        private const float TiltSpeedSmoothing = 6f;

        private readonly InputConfig config;
        private float calibrationOffset;

        // Speed-adaptive low-pass state, all in the same "roll degrees" space as calibrationOffset.
        private float filteredRoll;
        private float lastRawRoll;
        private float tiltSpeedDegPerSec;
        private bool filterPrimed;

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
            // Paused (settings menu, Time.timeScale = 0): hold the last steer, and never divide by dt below.
            if (deltaTime <= 0f)
                return;

            Vector3 gravity;
            if (!TryReadGravity(out gravity))
            {
                Available = false;
                Lateral = 0f;
                TiltDegrees = 0f;
                return;
            }

            Available = true;

            float rawRoll = RollForOrientation(gravity.normalized);
            float roll = FilterRoll(rawRoll, deltaTime) - calibrationOffset;
            if (config.invertGyro) roll = -roll;
            TiltDegrees = roll;

            float magnitude = Mathf.Abs(roll);
            if (magnitude <= config.gyroDeadZoneDegrees)
            {
                Lateral = 0f;
                return;
            }

            float normalised = Mathf.InverseLerp(config.gyroDeadZoneDegrees, config.gyroMaxTiltDegrees, magnitude);
            normalised = ApplyResponseCurve(normalised);
            Lateral = Mathf.Sign(roll) * normalised * config.motionSensitivity;
        }

        /// <summary>
        /// Speed-adaptive low-pass on the raw roll (One-Euro style). The smoothing rate has a floor
        /// (<see cref="InputConfig.gyroSmoothing"/>) that swallows tremor when the hand is steady, and rises
        /// with how fast the tablet is being tilted (<see cref="InputConfig.gyroActiveResponse"/>) so a
        /// deliberate steer tracks the hand almost instantly. alpha = 1 - e^(-k·dt) makes the feel
        /// frame-rate independent.
        /// </summary>
        private float FilterRoll(float rawRoll, float dt)
        {
            if (!filterPrimed)
            {
                filteredRoll = rawRoll;
                lastRawRoll = rawRoll;
                tiltSpeedDegPerSec = 0f;
                filterPrimed = true;
                return filteredRoll;
            }

            // How fast the raw tilt is moving, itself smoothed so sensor noise doesn't fake a fast steer.
            float rawSpeed = Mathf.Abs(rawRoll - lastRawRoll) / dt;
            lastRawRoll = rawRoll;
            tiltSpeedDegPerSec = Mathf.Lerp(tiltSpeedDegPerSec, rawSpeed, 1f - Mathf.Exp(-TiltSpeedSmoothing * dt));

            float k = Mathf.Max(0f, config.gyroSmoothing) + Mathf.Max(0f, config.gyroActiveResponse) * tiltSpeedDegPerSec;
            float alpha = 1f - Mathf.Exp(-k * dt);
            filteredRoll = Mathf.Lerp(filteredRoll, rawRoll, alpha);
            return filteredRoll;
        }

        /// <summary>Shapes the normalised 0..1 steer so small tilts are gentle (curve &gt; 1) without
        /// capping full lock. 1 (or an invalid value) leaves it linear.</summary>
        private float ApplyResponseCurve(float t)
        {
            float curve = config.gyroResponseCurve;
            if (curve <= 0f || Mathf.Approximately(curve, 1f))
                return t;
            return Mathf.Pow(Mathf.Clamp01(t), curve);
        }

        public void Calibrate()
        {
            // Take a FRESH sensor read and zero against the live pose. This matters because the facilitator's
            // "recalibrate" action runs from the paused settings menu (Time.timeScale = 0), where Tick's
            // deltaTime is 0 and the filter is frozen (Tick early-returns) — so folding the stale TiltDegrees
            // would calibrate to the old pose, not how the tablet is being held right now.
            if (TryReadGravity(out Vector3 g))
            {
                float rawRoll = RollForOrientation(g.normalized);
                calibrationOffset = rawRoll; // next Tick reads ~0 at this pose
                // Reset the adaptive filter to the live pose so recalibration snaps to zero cleanly
                // instead of easing over from the pre-calibration roll.
                filteredRoll = rawRoll;
                lastRawRoll = rawRoll;
                tiltSpeedDegPerSec = 0f;
                filterPrimed = true;
                TiltDegrees = 0f;
            }
            else
            {
                // No sensor (desktop test): fall back to folding the last value, harmless.
                calibrationOffset += config.invertGyro ? -TiltDegrees : TiltDegrees;
            }
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
