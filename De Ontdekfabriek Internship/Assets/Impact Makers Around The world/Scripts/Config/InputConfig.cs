using UnityEngine;

namespace KenyaScooter.Config
{
    /// <summary>
    /// How the controls feel, shared by every input provider (M5, M8, Req §3.1/§3.4). One asset
    /// covers both builds: the tablet (gyro + touch zones) and the desktop test build (keyboard/gamepad).
    /// </summary>
    [CreateAssetMenu(menuName = "Kenya Scooter/Input Config", fileName = "InputConfig")]
    public sealed class InputConfig : ScriptableObject
    {
        [Header("Gyro steering (M5, M8)")]
        [Tooltip("Tilt inside this band reads as zero — absorbs hand tremor.")]
        public float gyroDeadZoneDegrees = 2f;
        [Tooltip("Tilt that maps to full steer input.")]
        public float gyroMaxTiltDegrees = 28f;
        [Tooltip("Low-pass rate for the gravity vector. Higher = snappier, lower = smoother.")]
        public float gyroSmoothing = 12f;
        public bool invertGyro = false;

        [Header("Keyboard steering (desktop testing)")]
        [Tooltip("How fast held A/D ramps toward full steer — lower is more gradual (the scooter eases into the lean). ~2.5 takes about 0.4s to reach full.")]
        public float keyboardSteerRate = 2.5f;
        [Tooltip("How fast steer returns to centre when released.")]
        public float keyboardCentreRate = 7f;

        [Header("Accessibility (Req §16)")]
        [Range(0.3f, 1f)]
        [Tooltip("Scales gyro response and Real Rider camera roll for motion-sensitive players.")]
        public float motionSensitivity = 1f;
    }
}
