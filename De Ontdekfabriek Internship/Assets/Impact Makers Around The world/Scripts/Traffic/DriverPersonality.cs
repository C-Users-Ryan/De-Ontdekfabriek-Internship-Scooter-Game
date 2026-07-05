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
        [Tooltip("Display name for this driver archetype (e.g. \"Matatu tout\"). Cosmetic — used in tooling/debug.")]
        public string label = "";

        [Tooltip("Relative spawn chance of this archetype within its profile. To add a NEW archetype: add an element to " +
                 "the profile's Personalities list, give it a weight and its trait values — it appears in traffic, no code needed.")]
        public float weight = 1f;

        [Tooltip("Legacy category tag — behaviour now comes from the values below, not this enum. Leave as-is for the four built-ins.")]
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

        [Tooltip("Scales how hard this driver changes speed, on top of the vehicle's own acceleration (TrafficVehicle.acceleration). Aggressive > 1, Cautious < 1.")]
        public float accelerationMult = 1f;

        [Tooltip("Scales the car-following distance and minimum gap. Aggressive < 1 (tailgates), Cautious > 1 (hangs well back).")]
        public float followGapMult = 1f;

        [Tooltip("How quickly the driver corrects their lane position (scales lateral convergence). Distracted < 1 (sluggish, wanders); alert drivers >= 1.")]
        public float reactionMult = 1f;

        [Tooltip("Preferred position within the lane, in metres: + sits toward the centre line (assertive), - hugs the verge (cautious). Kept small so the car stays in its own lane.")]
        public float laneBias = 0f;

        [Tooltip("How readily this driver leans on the horn (scales the TrafficHorn chance). Aggressive > 1, Cautious < 1.")]
        public float hornEagerness = 1f;
    }
}
