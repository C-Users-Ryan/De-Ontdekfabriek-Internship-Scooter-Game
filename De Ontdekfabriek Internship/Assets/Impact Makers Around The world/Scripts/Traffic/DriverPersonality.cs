using System;
using UnityEngine;

namespace KenyaScooter.Traffic
{
    /// <summary>
    /// The four driver archetypes (M12, Req §5.1). Personalities give traffic its
    /// social texture: the road reads as inhabited by people making decisions, not
    /// obstacles on a pattern (MDA D4).
    /// </summary>
    public enum DriverPersonality { Normal, Aggressive, Cautious, Distracted }

    /// <summary>Per-personality behaviour values, configured on a TrafficBehaviourProfile.</summary>
    [Serializable]
    public sealed class PersonalitySettings
    {
        public DriverPersonality personality = DriverPersonality.Normal;

        [Tooltip("Multiplies the vehicle's base speed. Aggressive > 1, Cautious < 1.")]
        public Vector2 speedMultiplierRange = new Vector2(0.95f, 1.05f);

        [Tooltip("Metres a same-direction vehicle shifts toward the verge when the player closes in. Cautious drivers also slow down proportionally to this value.")]
        public float yieldShift = 0.45f;

        [Tooltip("Metres an oncoming vehicle shifts away from the player at close range.")]
        public float swerveShift = 0.35f;

        [Tooltip("Amplitude of lane wander in metres. Distracted drivers drift visibly.")]
        public float driftAmplitude = 0.08f;

        [Tooltip("Lane wander frequency (cycles per second).")]
        public float driftFrequency = 0.3f;

        [Tooltip("± m/s slow speed oscillation. Distracted drivers vary speed unpredictably.")]
        public float speedJitter = 0f;
    }
}
