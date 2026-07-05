using System;
using UnityEngine;
using KenyaScooter.Environment;

namespace KenyaScooter.Config
{
    /// <summary>
    /// One asset that fully describes the ROADSIDE LIFE feature (Oplevering insight 2 — "it feels empty").
    /// It carries a list of scenery prefabs (market stalls, people, windmills, solar panels, Big-5 animals,
    /// curb bollards...) and how densely they are scattered along the shoulders, which side of the road they
    /// sit on, and how the crowd thins or thickens over the session and across the day cycle. Mirrors the
    /// data-driven hazard/pedestrian configs (SC4): adding a prop, or making a zone busier, is an asset edit,
    /// not a code change.
    ///
    /// Placement is in ROAD SPACE (arc-length along the centreline + a lateral offset onto the shoulder), and
    /// RoadsidePropSpawner re-derives each prop's world pose every frame through RoadSequencer.TryGetRoadPose,
    /// so every prop rides the curve exactly like the tiles, hazards and pedestrians do (constant-frame safe).
    ///
    /// Props are PURE SCENERY by default (no colliders): they sit beyond the player's steer limit, so they
    /// dress the world without ever touching the drivable road or the scoring/hazard systems. (Future hook:
    /// a per-entry "soft obstacle" flag could route a clip through the existing HazardHit pipeline; left out
    /// for now to keep the straight-road defence build risk-free.)
    /// </summary>
    [CreateAssetMenu(menuName = "Kenya Scooter/Roadside Prop Config", fileName = "RoadsidePropConfig")]
    public sealed class RoadsidePropConfig : ScriptableObject
    {
        /// <summary>Which shoulder an entry may appear on. Either = both sides are allowed.</summary>
        public enum RoadSide { Either, LeftOnly, RightOnly }

        [Serializable]
        public sealed class PropEntry
        {
            [Tooltip("The scenery prefab. Needs a RoadsideProp component on the ROOT (a capsule placeholder works). " +
                     "For a turning windmill add a WindmillRotor and assign its blades child; for a reactive person " +
                     "add a RoadsideWaver and assign its arm/body child. Stalls and animals are plain static entries.")]
            public RoadsideProp prefab;

            [Tooltip("Relative chance this entry is chosen when several are eligible in the current zone. 0 = never.")]
            public float weight = 1f;

            [Tooltip("Pooled instances reserved for this entry. Bump it if this prop ever visibly pops in.")]
            public int poolSize = 6;

            [Tooltip("Which shoulder this prop may sit on. Either = both. (e.g. solar farms one side, a market the other.)")]
            public RoadSide side = RoadSide.Either;

            [Tooltip("Extra lateral DEPTH (m) added outward from the shoulder edge, randomised in this range. " +
                     "0 = right at the shoulder edge; larger pushes the prop further back from the road.")]
            public Vector2 lateralDepthRange = new Vector2(1.5f, 6f);

            [Tooltip("Uniform scale multiplier range applied per spawn, so identical prefabs vary a little.")]
            public Vector2 scaleRange = new Vector2(0.9f, 1.15f);

            [Tooltip("Random yaw (deg) applied around the road-facing direction per spawn, so a row of the same " +
                     "prefab does not all face the same way. 0 = always square to the road.")]
            public float yawJitter = 25f;

            [Tooltip("Only spawn this entry while the active road zone (RoadSequence) carries one of these tags " +
                     "(township, savanna, tsavo_wildlife...). Empty = any zone. This is how town reads CROWDED " +
                     "(stalls, people) and the countryside SPARSE (animals, acacia): the felt blend is density.")]
            public string[] requiredContextTags;
        }

        [Header("Master switch (facilitator)")]
        [Tooltip("Turns ALL roadside life on or off. The facilitator menu drives this; OFF spawns nothing and " +
                 "leaves gameplay completely untouched (the empty-berm straight-road build).")]
        public bool enabled = true;

        [Header("Density")]
        [Tooltip("Roadside props placed per 100 m of road, counting both shoulders. The facilitator 'drukte' dial scales this.")]
        public float propsPer100m = 6f;

        [Tooltip("Density multiplier over normalised session time (0 = start, 1 = end). Flat 1 = constant. " +
                 "Left as Constant(1) it does not ramp, so the roadside stays as busy at the end as the start.")]
        public AnimationCurve densityOverSession = AnimationCurve.Constant(0f, 1f, 1f);

        [Tooltip("Density multiplier per day-cycle phase, index-matched to DayCycleConfig.phases " +
                 "(0 morning, 1 midday, 2 afternoon, 3 evening). Defaults to a morning market rush and busy midday, " +
                 "calmer at dusk. Empty or shorter than the phase list = no day-cycle effect.")]
        public float[] phaseDensityMultipliers = { 1.15f, 1.1f, 0.9f, 0.7f };

        [Tooltip("Never closer than this (m) between two props on the SAME shoulder, so they cannot stack on top of each other.")]
        public float minGapPerSide = 5f;

        [Header("Placement")]
        [Tooltip("How far ahead of the player props are first placed. Keep it at or below the road's Spawn Horizon " +
                 "(default 160 m) so the road actually exists where the prop lands.")]
        public float spawnAheadDistance = 140f;

        [Tooltip("A prop is recycled once it has slipped this far (m) of road behind the player.")]
        public float despawnBehindDistance = 40f;

        [Tooltip("Extra clearance (m) from the shoulder EDGE to the nearest a prop may sit, so nothing crowds the " +
                 "drivable road. The shoulder edge is read live from RoadSideConfig (laneWidth + shoulderWidth), so " +
                 "props stay off the road even if the lane geometry changes per location.")]
        public float shoulderClearance = 1f;

        [Tooltip("Skip placing props while the road bends more than this (deg) within the spawn-ahead distance, so a " +
                 "prop is never stranded off a sharp curve (it would slide off the straight spawn estimate). Gentle " +
                 "bends still get props, which keeps a curve from looking bald.")]
        public float maxCurveAngle = 35f;

        [Tooltip("Hard cap on how many props are alive at once, a performance safety net for a weak tablet: the " +
                 "spawner stops placing new props above this and resumes as old ones recycle behind the player. " +
                 "0 = no cap. Each prop is a few instanced draw calls, so a few dozen is cheap; keep it generous.")]
        public int maxActiveProps = 90;

        [Header("Props")]
        [Tooltip("The scenery palette. Drag prop prefabs in here; each row is one kind of prop with its own side, " +
                 "density weight and zone filter. This is the whole authoring surface — 'just assign prefabs'.")]
        public PropEntry[] props;
    }
}
