using UnityEngine;

namespace OvertakeGame
{
    /// <summary>
    /// ScriptableObject that describes how traffic behaves during a specific RoadSequence.
    /// Assign one to each RoadSequence. When that sequence goes active, TrafficManager
    /// calls ApplyProfile() and adjusts spawning and driver behaviour accordingly.
    ///
    /// EXAMPLES:
    ///   Profile_Savanna     — sparse oncoming, light same-dir, normal speed, no intersections
    ///   Profile_TrafficJam  — dense same-dir, jam mode on, slow speed, no oncoming
    ///   Profile_Town        — medium density, intersection vehicles, pedestrians, lower speed
    ///   Profile_HighwayRun  — fast oncoming, sparse same-dir, aggressive drivers
    ///   Profile_Alleyway    — same-dir only, very close quarters, no oncoming
    /// </summary>
    [CreateAssetMenu(fileName = "Profile_New", menuName = "OvertakeGame/Traffic Behaviour Profile")]
    public class TrafficBehaviourProfile : ScriptableObject
    {
        [Header("Identity")]
        public string profileId;

        // ── Density ───────────────────────────────────────────────────────────

        [Header("Spawn Rates")]
        [Tooltip("Multiplier on same-direction spawn interval. <1 = more frequent, >1 = rarer.")]
        [Range(0.1f, 5f)]
        public float sameDirectionIntervalMultiplier = 1f;

        [Tooltip("Multiplier on oncoming spawn interval. 0 = no oncoming traffic.")]
        [Range(0f, 5f)]
        public float oncomingIntervalMultiplier = 1f;

        [Tooltip("Enable vehicles crossing from the side at intersections.")]
        public bool enableIntersectionTraffic = false;

        [Tooltip("Seconds between intersection vehicle spawns. Only used if enableIntersectionTraffic = true.")]
        public float intersectionSpawnInterval = 8f;

        // ── Traffic jam ───────────────────────────────────────────────────────

        [Header("Traffic Jam Mode")]
        [Tooltip("If true, same-direction vehicles are pre-placed in a slow dense queue.")]
        public bool trafficJamMode = false;

        [Tooltip("Base speed (m/s) of jam vehicles. Player must weave between them.")]
        public float jamBaseSpeed = 1.5f;

        [Tooltip("Gap between vehicles in a jam (metres).")]
        public float jamGap = 6f;

        [Tooltip("How many vehicles to pre-place in the jam.")]
        public int jamVehicleCount = 8;

        // ── Driver personalities ───────────────────────────────────────────────

        [Header("Driver Personality Mix")]
        [Range(0f, 1f)]
        [Tooltip("Fraction of aggressive drivers (fast, small gaps, don't yield).")]
        public float aggressiveFraction = 0.15f;

        [Range(0f, 1f)]
        [Tooltip("Fraction of cautious drivers (slow, large gaps, early yield).")]
        public float cautiousFraction = 0.30f;

        [Range(0f, 1f)]
        [Tooltip("Fraction of distracted drivers (variable speed, lane drift, delayed reactions).")]
        public float distractedFraction = 0.20f;

        // Remainder = normal drivers

        // ── Speed modifiers ───────────────────────────────────────────────────

        [Header("Speed")]
        [Tooltip("Multiplier on base ownSpeed for same-direction vehicles.")]
        [Range(0.3f, 2f)]
        public float sameDirectionSpeedMultiplier = 1f;

        [Tooltip("Multiplier on base ownSpeed for oncoming vehicles.")]
        [Range(0.3f, 2f)]
        public float oncomingSpeedMultiplier = 1f;

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>Pick a random personality based on this profile's fractions.</summary>
        public DriverPersonality PickPersonality()
        {
            float r = Random.value;
            if (r < aggressiveFraction)                                      return DriverPersonality.Aggressive;
            if (r < aggressiveFraction + cautiousFraction)                   return DriverPersonality.Cautious;
            if (r < aggressiveFraction + cautiousFraction + distractedFraction) return DriverPersonality.Distracted;
            return DriverPersonality.Normal;
        }
    }

    /// <summary>Driver personality type — shared between TrafficBehaviourProfile and TrafficVehicle.</summary>
    public enum DriverPersonality { Normal, Aggressive, Cautious, Distracted }
}
