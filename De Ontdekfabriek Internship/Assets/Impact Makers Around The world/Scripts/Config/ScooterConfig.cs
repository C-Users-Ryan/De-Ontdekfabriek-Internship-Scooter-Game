using UnityEngine;

namespace KenyaScooter.Config
{
    /// <summary>
    /// All scooter motion tuning (M2, M5, M6). Speed values are m/s
    /// (10 m/s = 36 km/h, 30 m/s = 108 km/h).
    /// </summary>
    [CreateAssetMenu(menuName = "Kenya Scooter/Scooter Config", fileName = "ScooterConfig")]
    public sealed class ScooterConfig : ScriptableObject
    {
        [Header("Speed (M2)")]
        public float baseSpeed = 10f;
        public float maxSpeed = 30f;
        [Tooltip("Brake floor. 0 allows a full stop (needed for the checkpoint arrival).")]
        public float minSpeed = 0f;
        public float acceleration = 15f;
        public float brakeDeceleration = 20f;
        [Tooltip("Drift back toward base speed when there is no input.")]
        public float naturalDeceleration = 5f;

        [Header("Lateral movement (M5)")]
        [Tooltip("Lateral velocity ramps via MoveTowards — no instant reversal, simulating steering weight.")]
        public float lateralAcceleration = 30f;
        public float maxLateralSpeed = 8f;
        [Tooltip("Constant ride height of the scooter above the road.")]
        public float rideHeight = 0.5f;

        [Header("Visual lean (M6)")]
        [Tooltip("Roll at full steer input. The lean answers raw input immediately, before position changes.")]
        public float maxLeanAngle = 18f;
        [Tooltip("Degrees per second the lean moves toward its target.")]
        public float leanResponse = 240f;

        [Header("Hazard wobble (M21)")]
        public float wobbleAmplitude = 9f;
        public float wobbleFrequency = 11f;
        [Tooltip("Exponential decay rate of the wobble after a hit.")]
        public float wobbleDecay = 2.2f;
    }
}
