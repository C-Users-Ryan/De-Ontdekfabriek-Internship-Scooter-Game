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
        [Tooltip("Tilt that maps to full steer input. Lower = more sensitive (less tilt for a full turn).")]
        public float gyroMaxTiltDegrees = 28f;
        [Tooltip("Base responsiveness of the tilt filter when the tablet is held steady. Higher = snappier but " +
                 "lets more hand tremor through; lower = calmer. This is the FLOOR: the filter sharpens itself " +
                 "the moment you tilt deliberately (see Gyro Active Response), so it no longer has to choose " +
                 "between smooth and responsive.")]
        public float gyroSmoothing = 12f;
        [Tooltip("How much the filter sharpens while you are actively tilting, added per deg/s of tilt speed. " +
                 "This is what lets a real steer track the hand with almost no lag while a resting hand still " +
                 "reads as smooth. 0 = fixed smoothing (old behaviour).")]
        public float gyroActiveResponse = 0.16f;
        [Tooltip("Shapes the tilt-to-steer response. 1 = linear. Above 1 = finer control near centre (small " +
                 "tilts steer gently, full lock still reachable) which makes holding a lane easier. Below 1 = " +
                 "more aggressive straight off centre.")]
        public float gyroResponseCurve = 1.25f;
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
