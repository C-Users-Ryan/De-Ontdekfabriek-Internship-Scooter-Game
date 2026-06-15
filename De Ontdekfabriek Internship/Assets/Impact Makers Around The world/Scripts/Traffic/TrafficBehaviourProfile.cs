using UnityEngine;

namespace KenyaScooter.Traffic
{
    /// <summary>
    /// Traffic character per road zone (M12, Req §5.2): personality distribution,
    /// spawn interval and speed multipliers, and jam mode. A RoadSequence can carry
    /// an override profile, which is how the personality mix shifts toward
    /// Aggressive in the later zones (the difficulty ramp, MDA D10).
    /// </summary>
    [CreateAssetMenu(menuName = "Kenya Scooter/Traffic Behaviour Profile", fileName = "TrafficProfile")]
    public sealed class TrafficBehaviourProfile : ScriptableObject
    {
        [Header("Personality weights (M12)")]
        public float normalWeight = 0.5f;
        public float aggressiveWeight = 0.15f;
        public float cautiousWeight = 0.25f;
        public float distractedWeight = 0.1f;

        [Header("Spawn and speed multipliers")]
        public float sameDirectionIntervalMult = 1f;
        public float oncomingIntervalMult = 1f;
        public float sameDirectionSpeedMult = 1f;
        public float oncomingSpeedMult = 1f;
        [Tooltip("Township jam: same-direction gaps shrink hard, queue speeds drop.")]
        public bool jamMode = false;

        [Header("Per-personality behaviour")]
        public PersonalitySettings[] personalities =
        {
            new PersonalitySettings { personality = DriverPersonality.Normal,     speedMultiplierRange = new Vector2(0.95f, 1.05f), yieldShift = 0.45f, swerveShift = 0.35f, driftAmplitude = 0.08f, driftFrequency = 0.3f,  speedJitter = 0f },
            new PersonalitySettings { personality = DriverPersonality.Aggressive, speedMultiplierRange = new Vector2(1.1f, 1.25f),  yieldShift = 0.1f,  swerveShift = 0.15f, driftAmplitude = 0.05f, driftFrequency = 0.2f,  speedJitter = 0f },
            new PersonalitySettings { personality = DriverPersonality.Cautious,   speedMultiplierRange = new Vector2(0.8f, 0.95f),  yieldShift = 0.9f,  swerveShift = 0.6f,  driftAmplitude = 0.06f, driftFrequency = 0.25f, speedJitter = 0f },
            new PersonalitySettings { personality = DriverPersonality.Distracted, speedMultiplierRange = new Vector2(0.85f, 1.1f),  yieldShift = 0.3f,  swerveShift = 0.25f, driftAmplitude = 0.5f,  driftFrequency = 0.55f, speedJitter = 1.5f }
        };

        /// <summary>Weighted random personality draw, applied at vehicle activation (M12).</summary>
        public DriverPersonality PickPersonality()
        {
            float total = normalWeight + aggressiveWeight + cautiousWeight + distractedWeight;
            float roll = Random.value * total;
            if ((roll -= normalWeight) < 0f) return DriverPersonality.Normal;
            if ((roll -= aggressiveWeight) < 0f) return DriverPersonality.Aggressive;
            if ((roll -= cautiousWeight) < 0f) return DriverPersonality.Cautious;
            return DriverPersonality.Distracted;
        }

        public PersonalitySettings SettingsFor(DriverPersonality personality)
        {
            for (int i = 0; i < personalities.Length; i++)
                if (personalities[i].personality == personality)
                    return personalities[i];
            return personalities.Length > 0 ? personalities[0] : null;
        }
    }
}
