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

        [Header("Behaviour on hit (data-driven — add as many configs as you like, no code)")]
        [Tooltip("Category for stats/analytics bucketing only — does not gate behaviour.")]
        public HazardResponse response = HazardResponse.SurfaceDefect;
        [Tooltip("Whether hitting this deducts points at all (workshop toggle, Req §16).")]
        public bool deduct = true;
        [Tooltip("Points deducted on hit, before speed scaling.")]
        public int deduction = 50;
        [Tooltip("Scale the deduction by speed (0 below base, full at max) so braking pays (Req §6.1). Surface defects and bumps use this; static obstacles usually do not.")]
        public bool speedScaled = true;
        [Tooltip("SwahiliUI key for the floating score popup. Empty = no popup.")]
        public string popupKey = "POPUP_DEDUCT";
        [Tooltip("SwahiliUI key for the one-shot warning text (WARN_POTHOLE, WARN_ROCK, WARN_BUMP...). Empty = no warning.")]
        public string warnKey = "WARN_POTHOLE";

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
