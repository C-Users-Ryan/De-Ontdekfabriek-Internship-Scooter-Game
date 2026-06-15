using UnityEngine;
using KenyaScooter.Core;
using KenyaScooter.Hazards;

namespace KenyaScooter.Config
{
    /// <summary>
    /// One asset per hazard family (potholes, rocks, speed bumps — M21, M22, Req §6).
    /// HazardSpawner takes an array of these, so adding a hazard type is an asset,
    /// not a code change.
    /// </summary>
    [CreateAssetMenu(menuName = "Kenya Scooter/Hazard Spawn Config", fileName = "HazardSpawnConfig")]
    public sealed class HazardSpawnConfig : ScriptableObject
    {
        public Hazard prefab;
        public int poolSize = 12;

        [Header("Density (M21 — ramps over the session)")]
        [Tooltip("Expected clusters per 100 m at curve value 1.")]
        public float clustersPer100m = 3f;
        [Tooltip("Multiplier over normalised session time (0 = start, 1 = end). The natural difficulty ramp, D10.")]
        public AnimationCurve densityOverSession = new AnimationCurve(
            new Keyframe(0f, 0.25f), new Keyframe(0.4f, 0.6f), new Keyframe(1f, 1f));
        [Tooltip("Minimum clear road between clusters.")]
        public float minClusterGap = 25f;
        [Tooltip("Hard cap per session. 0 = unlimited. The unmarked speed bump uses 1 (Req §6.3).")]
        public int maxPerSession = 0;

        [Header("Cluster shape (Req §6.1 — staggered so the player can thread through)")]
        public int clusterMin = 1;
        public int clusterMax = 3;
        [Tooltip("Longitudinal spacing between cluster members.")]
        public float clusterSpacing = 4f;
        [Tooltip("Lateral offset alternation between cluster members.")]
        public float lateralStagger = 1.2f;
        [Tooltip("Random scale range applied per instance (rocks vary, M22).")]
        public Vector2 scaleRange = new Vector2(1f, 1f);

        [Header("Placement")]
        public SpawnZones zones = SpawnZones.OwnLane;
        public float spawnAheadDistance = 95f;
        [Tooltip("Only spawn while the active RoadSequence carries one of these context tags. Empty = everywhere. Rocks use murram/tsavo/construction (Req §6.2).")]
        public string[] requiredContextTags;
    }
}
