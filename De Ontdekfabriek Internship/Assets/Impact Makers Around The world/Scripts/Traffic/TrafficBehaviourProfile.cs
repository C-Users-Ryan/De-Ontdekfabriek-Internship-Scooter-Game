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
            new PersonalitySettings { label = "Normaal",     weight = 0.5f,  personality = DriverPersonality.Normal,     speedMultiplierRange = new Vector2(0.95f, 1.05f), yieldShift = 0.45f, swerveShift = 0.35f, driftAmplitude = 0.08f, driftFrequency = 0.3f,  speedJitter = 0f,    accelerationMult = 1f,    followGapMult = 1f,    reactionMult = 1f,    laneBias = 0f,     hornEagerness = 1f },
            new PersonalitySettings { label = "Agressief",   weight = 0.15f, personality = DriverPersonality.Aggressive, speedMultiplierRange = new Vector2(1.12f, 1.32f), yieldShift = 0.08f, swerveShift = 0.12f, driftAmplitude = 0.04f, driftFrequency = 0.2f,  speedJitter = 0f,    accelerationMult = 1.5f,  followGapMult = 0.5f,  reactionMult = 1.2f,  laneBias = 0.45f,  hornEagerness = 2.2f },
            new PersonalitySettings { label = "Voorzichtig", weight = 0.25f, personality = DriverPersonality.Cautious,   speedMultiplierRange = new Vector2(0.72f, 0.9f),  yieldShift = 1.0f,  swerveShift = 0.65f, driftAmplitude = 0.05f, driftFrequency = 0.22f, speedJitter = 0f,    accelerationMult = 0.7f,  followGapMult = 1.8f,  reactionMult = 1.05f, laneBias = -0.4f,  hornEagerness = 0.3f },
            new PersonalitySettings { label = "Afgeleid",    weight = 0.1f,  personality = DriverPersonality.Distracted, speedMultiplierRange = new Vector2(0.82f, 1.12f), yieldShift = 0.3f,  swerveShift = 0.22f, driftAmplitude = 0.7f,  driftFrequency = 0.6f,  speedJitter = 1.9f,  accelerationMult = 1f,    followGapMult = 1.15f, reactionMult = 0.45f, laneBias = 0.1f,   hornEagerness = 0.7f }
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

        /// <summary>
        /// Weighted random archetype draw over the Personalities list, using each entry's own <c>weight</c>. This is
        /// what makes new archetypes trivial: drop another element into the list with a weight and its trait values
        /// and it joins the traffic — no enum, no extra weight field, no code. Returns the chosen settings directly.
        /// </summary>
        public PersonalitySettings PickSettings()
        {
            if (personalities == null || personalities.Length == 0)
                return null;
            float total = 0f;
            for (int i = 0; i < personalities.Length; i++)
                if (personalities[i] != null) total += Mathf.Max(0f, personalities[i].weight);
            if (total <= 0f)
                return personalities[0]; // no weights set — fall back to the first archetype
            float roll = Random.value * total;
            for (int i = 0; i < personalities.Length; i++)
            {
                if (personalities[i] == null) continue;
                roll -= Mathf.Max(0f, personalities[i].weight);
                if (roll < 0f) return personalities[i];
            }
            return personalities[personalities.Length - 1];
        }
    }
}
