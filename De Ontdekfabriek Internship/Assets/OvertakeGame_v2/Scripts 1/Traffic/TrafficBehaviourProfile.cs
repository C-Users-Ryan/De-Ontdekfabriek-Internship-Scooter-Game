using UnityEngine;

namespace OvertakeGame
{
    /// <summary>
    /// ScriptableObject that configures how traffic behaves during a RoadSequence.
    /// TrafficManager reads the active profile each spawn cycle.
    /// RoadSequencer activates a new profile when a sequence with a trafficProfile assigned begins.
    /// </summary>
    [CreateAssetMenu(fileName = "TrafficProfile_New", menuName = "OvertakeGame/Traffic Behaviour Profile")]
    public class TrafficBehaviourProfile : ScriptableObject
    {
        [Header("Identity")]
        public string profileId;

        // ── Spawn rates ────────────────────────────────────────────────────────

        [Header("Spawn Rates")]
        [Tooltip("Multiplier on the base same-direction spawn interval. <1 = more frequent.")]
        [Range(0.1f, 5f)]
        public float sameDirectionIntervalMult = 1f;

        [Tooltip("Multiplier on the base oncoming spawn interval. 0 = no oncoming traffic.")]
        [Range(0f, 5f)]
        public float oncomingIntervalMult = 1f;

        // ── Traffic jam ────────────────────────────────────────────────────────

        [Header("Traffic Jam Mode")]
        [Tooltip("Pre-places a queue of slow same-direction vehicles for the player to navigate.")]
        public bool jamMode = false;

        [Tooltip("Speed (m/s) of jam vehicles.")]
        public float jamBaseSpeed = 1.5f;

        [Tooltip("Gap between vehicles in the jam (metres).")]
        public float jamGap = 6f;

        [Tooltip("Number of vehicles pre-placed in the jam.")]
        public int jamVehicleCount = 8;

        // ── Driver personalities ───────────────────────────────────────────────

        [Header("Driver Personality Mix")]
        [Range(0f, 1f)] public float aggressiveFraction  = 0.15f;
        [Range(0f, 1f)] public float cautiousFraction    = 0.30f;
        [Range(0f, 1f)] public float distractedFraction  = 0.20f;
        // Remainder = Normal personality.

        // ── Speed modifiers ────────────────────────────────────────────────────

        [Header("Speed")]
        [Range(0.3f, 2f)] public float sameDirectionSpeedMult = 1f;
        [Range(0.3f, 2f)] public float oncomingSpeedMult      = 1f;

        // ── Helper ─────────────────────────────────────────────────────────────

        public DriverPersonality PickPersonality()
        {
            float r = Random.value;
            if (r < aggressiveFraction)                                           return DriverPersonality.Aggressive;
            if (r < aggressiveFraction + cautiousFraction)                        return DriverPersonality.Cautious;
            if (r < aggressiveFraction + cautiousFraction + distractedFraction)   return DriverPersonality.Distracted;
            return DriverPersonality.Normal;
        }
    }

    /// <summary>Driver personality type shared by TrafficBehaviourProfile and TrafficVehicle.</summary>
    public enum DriverPersonality { Normal, Aggressive, Cautious, Distracted }
}
