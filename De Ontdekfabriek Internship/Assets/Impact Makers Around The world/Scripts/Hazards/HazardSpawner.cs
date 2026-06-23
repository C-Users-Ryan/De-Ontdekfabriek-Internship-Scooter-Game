using System.Collections.Generic;
using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;
using KenyaScooter.Roads;
using KenyaScooter.SafetyNet;
using KenyaScooter.Session;

namespace KenyaScooter.Hazards
{
    /// <summary>
    /// Distance-based cluster spawner for all hazard families (M21, M22, Req §6).
    /// One spawner handles every HazardSpawnConfig: potholes everywhere with a density
    /// ramp, rocks only on murram/tsavo/construction sequences, speed bumps capped per
    /// session. Clusters are laterally staggered so the player can thread through.
    /// Spawning stops at the checkpoint; scrolling continues (Req §9.1).
    ///
    /// Each hazard is parametrised by its point on the road — arc-length along the centreline plus a
    /// lateral offset — and its world pose is re-derived every frame through RoadSequencer's curve mapping
    /// (the same one that places the tiles). So hazards ride a bend with the road instead of scrolling down
    /// the straight +Z line, and keep populating the road through curves (no thinning approaching a turn).
    /// </summary>
    public sealed class HazardSpawner : MonoBehaviour
    {
        [SerializeField] private HazardSpawnConfig[] configs;
        [SerializeField] private float despawnBehindDistance = 25f;

        private sealed class SpawnState
        {
            public HazardSpawnConfig config;
            public ObjectPool<Hazard> pool;
            public float nextClusterAt;
            public int clustersThisSession;
        }

        private readonly List<SpawnState> states = new List<SpawnState>(4);
        private readonly List<Hazard> active = new List<Hazard>(64);

        private void Start()
        {
            for (int i = 0; i < configs.Length; i++)
            {
                HazardSpawnConfig config = configs[i];
                if (config == null || config.prefab == null)
                    continue;

                ObjectPool<Hazard> pool = null;
                pool = new ObjectPool<Hazard>(config.prefab, transform, config.poolSize,
                    created =>
                    {
                        // Stamp SourcePool on instances created when the pool EXPANDS mid-game.
                        // The initial batch is built before 'pool' is assigned, so the loop below
                        // covers those; without this an expanded hazard has a null SourcePool and
                        // throws the moment it despawns.
                        if (pool != null)
                            created.SourcePool = pool;
                        if (RewindSystem.Instance != null)
                            RewindSystem.Instance.Register(created);
                    });
                for (int j = 0; j < pool.AllInstances.Count; j++)
                    pool.AllInstances[j].SourcePool = pool;

                states.Add(new SpawnState { config = config, pool = pool });
            }
        }

        private void OnEnable()
        {
            GameEvents.SessionReset += HandleSessionReset;
            GameEvents.RewindCompleted += HandleRewindCompleted;
        }

        private void OnDisable()
        {
            GameEvents.SessionReset -= HandleSessionReset;
            GameEvents.RewindCompleted -= HandleRewindCompleted;
        }

        private void Update()
        {
            GameState state = GameManager.State;
            if (state != GameState.Playing && state != GameState.AtCheckpoint)
                return;

            float playerArc = PlayerArc;

            // Re-place every active hazard from its fixed point on the road (arc + lateral) so it rides the
            // curve exactly like the tiles do (M1, M3): the road bends around the stationary player, so a
            // hazard scrolled down the straight +Z line would float off the surface through a bend. Despawn
            // once it has passed far enough behind the player (measured along the road, not in world Z).
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Hazard hazard = active[i];
                if (playerArc - hazard.RoadArc > despawnBehindDistance)
                {
                    hazard.SourcePool.Release(hazard);
                    active.RemoveAt(i);
                    continue;
                }
                PlaceOnRoad(hazard);
            }

            if (state != GameState.Playing)
                return;

            for (int i = 0; i < states.Count; i++)
                TrySpawnCluster(states[i], playerArc);
        }

        private void TrySpawnCluster(SpawnState state, float playerArc)
        {
            HazardSpawnConfig config = state.config;
            if (playerArc < state.nextClusterAt)
                return;
            if (config.maxPerSession > 0 && state.clustersThisSession >= config.maxPerSession)
                return;

            // No bend gate any more: hazards are placed in road space and follow the curve (see SpawnCluster /
            // RoadSequencer.TryGetRoadPose), so the road no longer thins approaching a turn.

            // Context filter (Req §6.2): rocks only appear on murram/tsavo/construction.
            // Blank/empty tag entries are ignored, so a stray empty element can't gate a hazard to never spawn.
            if (HasMeaningfulTags(config.requiredContextTags))
            {
                RoadSequence current = RoadSequencer.Instance != null ? RoadSequencer.Instance.CurrentSequence : null;
                if (current == null || !current.HasAnyTag(config.requiredContextTags))
                {
                    state.nextClusterAt = playerArc + 20f; // retry once the zone changes
                    return;
                }
            }

            SpawnCluster(state, playerArc);
            state.clustersThisSession++;
            state.nextClusterAt = playerArc + IntervalFor(config);
        }

        private void SpawnCluster(SpawnState state, float playerArc)
        {
            HazardSpawnConfig config = state.config;
            float baseArc = playerArc + config.spawnAheadDistance;
            float baseLateral = PickLateral(config.zones);

            int count = Random.Range(config.clusterMin, config.clusterMax + 1);
            for (int i = 0; i < count; i++)
            {
                Hazard hazard = state.pool.Get();
                hazard.definition = config; // stamp so the hit carries this config's scoring/warning data (SC4)
                // Both the arc (longitudinal) and the lateral are road-space, so a staggered cluster — which
                // keeps a path through it (Req §6.1) — follows the bend instead of a straight line.
                hazard.RoadLateral = baseLateral + (i == 0 ? 0f : (i % 2 == 0 ? 1f : -1f) * config.lateralStagger);
                hazard.RoadArc = baseArc + i * config.clusterSpacing;
                hazard.transform.localScale = hazard.baseScale * Random.Range(config.scaleRange.x, config.scaleRange.y);
                hazard.gameObject.SetActive(true);
                PlaceAndGround(hazard, config.spawnHeight); // resolve its curved world pose and rest it on the surface
                active.Add(hazard);
            }
        }

        /// <summary>Re-derives a live hazard's world pose from its road-space point (arc + lateral) for this
        /// frame, then applies the constant vertical lift measured at spawn — so it rides the curve with the road.</summary>
        private static void PlaceOnRoad(Hazard hazard)
        {
            ResolveWorldPose(hazard.RoadArc, hazard.RoadLateral, out Vector3 position, out Quaternion rotation);
            position.y += hazard.GroundOffset;
            hazard.transform.SetPositionAndRotation(position, rotation);
        }

        /// <summary>
        /// First placement of a freshly spawned hazard: resolve its curved world pose, measure how far to lift
        /// it so its LOWEST rendered point sits 'clearance' metres above the flat road (y = 0) whatever the
        /// prefab pivot is, cache that lift on the hazard, and apply it. The road is flat and the spawn
        /// rotation is yaw-only, so the lift is constant — later frames reuse it via <see cref="PlaceOnRoad"/>
        /// with no per-frame bounds query. Falls back to a flat offset if there is no renderer to measure.
        /// </summary>
        private static void PlaceAndGround(Hazard hazard, float clearance)
        {
            ResolveWorldPose(hazard.RoadArc, hazard.RoadLateral, out Vector3 position, out Quaternion rotation);
            hazard.transform.SetPositionAndRotation(position, rotation); // on the centreline (y≈0) so bounds measure true
            Renderer renderer = hazard.GetComponentInChildren<Renderer>();
            hazard.GroundOffset = renderer != null ? clearance - renderer.bounds.min.y : clearance;
            position.y += hazard.GroundOffset;
            hazard.transform.position = position;
        }

        /// <summary>
        /// World pose of a point on the road (arc-length + lateral) via RoadSequencer's player-anchored mapping,
        /// so it follows the curve (M3). Falls back to the old straight +Z line only when there is no sequencer
        /// or the road is not built yet, so the spawner still behaves in a bare test scene.
        /// </summary>
        private static void ResolveWorldPose(float arc, float lateral, out Vector3 position, out Quaternion rotation)
        {
            if (RoadSequencer.Instance != null
                && RoadSequencer.Instance.TryGetRoadPose(arc, lateral, out position, out rotation))
                return;

            float ahead = arc - PlayerArc; // the player rides at the world origin, so a hazard's Z is its distance ahead
            position = RoadDirection.Current * ahead + RoadDirection.SteerAxis * lateral;
            rotation = Quaternion.LookRotation(RoadDirection.Current);
        }

        /// <summary>The player's progress along the road centreline (metres). Prefers RoadSequencer's rewound
        /// odometer so placement and scheduling share the road's frame; falls back to the world odometer.</summary>
        private static float PlayerArc =>
            RoadSequencer.Instance != null ? RoadSequencer.Instance.PlayerArc : WorldSpeed.Instance.DistanceTravelled;

        /// <summary>Metres until the next cluster — shrinks over the session via the density curve (M21, D10).</summary>
        private static float IntervalFor(HazardSpawnConfig config)
        {
            float t01 = TimerManager.Instance != null ? TimerManager.Instance.Normalized01 : 0f;
            float density = config.clustersPer100m * Mathf.Max(0f, config.densityOverSession.Evaluate(t01));
            float interval = density > 0.001f ? 100f / density : 99999f;
            return Mathf.Max(config.minClusterGap, interval);
        }

        /// <summary>True only if the array has at least one non-blank tag, so a stray empty entry can't gate a hazard to nothing.</summary>
        private static bool HasMeaningfulTags(string[] tags)
        {
            if (tags == null)
                return false;
            for (int i = 0; i < tags.Length; i++)
                if (!string.IsNullOrWhiteSpace(tags[i]))
                    return true;
            return false;
        }

        /// <summary>Reservoir-samples one lateral band from the configured zone flags — no allocation.</summary>
        private static float PickLateral(SpawnZones zones)
        {
            RoadSideConfig road = RoadSideConfig.Active;
            int options = 0;
            float chosen = 0f;

            if ((zones & SpawnZones.OwnLane) != 0 && Reservoir(ref options))
                chosen = road.OwnLaneCentre + Random.Range(-(road.HalfLane - 0.6f), road.HalfLane - 0.6f);
            if ((zones & SpawnZones.OncomingLane) != 0 && Reservoir(ref options))
                chosen = road.OncomingLaneCentre + Random.Range(-(road.HalfLane - 0.6f), road.HalfLane - 0.6f);
            if ((zones & SpawnZones.Shoulders) != 0 && Reservoir(ref options))
                chosen = (Random.value < 0.5f ? -1f : 1f)
                    * Random.Range(road.laneWidth + 0.3f, road.laneWidth + road.shoulderWidth - 0.3f);

            return chosen;
        }

        private static bool Reservoir(ref int options)
        {
            options++;
            return Random.value < 1f / options;
        }

        private void HandleSessionReset()
        {
            for (int i = 0; i < active.Count; i++)
                active[i].SourcePool.Release(active[i]);
            active.Clear();

            for (int i = 0; i < states.Count; i++)
            {
                states[i].clustersThisSession = 0;
                states[i].nextClusterAt = IntervalFor(states[i].config);
            }
        }

        private void HandleRewindCompleted()
        {
            active.Clear();
            for (int i = 0; i < states.Count; i++)
            {
                ObjectPool<Hazard> pool = states[i].pool;
                pool.ReconcileAvailability();
                for (int j = 0; j < pool.AllInstances.Count; j++)
                    if (pool.AllInstances[j].gameObject.activeInHierarchy)
                        active.Add(pool.AllInstances[j]);
            }
        }
    }
}
