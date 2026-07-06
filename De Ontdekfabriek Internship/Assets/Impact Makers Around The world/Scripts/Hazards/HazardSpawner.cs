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

        [Header("Fair placement")]
        [Tooltip("Don't drop a cluster in a turn — the player is already busy steering the bend, so a pothole " +
                 "there is a cheap, unfair hit. A cluster whose footprint rides road that bends more than this " +
                 "many degrees per metre waits until the road straightens. 0 = allow hazards in turns.")]
        [SerializeField] private float turnBendThreshold = 0.5f;
        [Tooltip("Minimum clear metres between ANY two hazard clusters, across every hazard type — stops a rock " +
                 "cluster landing on top of a pothole cluster from a different spawner. Each config also keeps " +
                 "its own (usually longer) min cluster gap.")]
        [SerializeField] private float minClusterSeparation = 12f;

        // How far to slide a blocked cluster before re-testing (turns + proximity), the sample step used to walk
        // a cluster's footprint looking for a bend, and the extra straight road required each side of a cluster
        // so it never sits right at a bend's mouth. Metres.
        private const float RetryStep = 6f;
        private const float TurnSampleStep = 3f;
        private const float TurnClearanceBuffer = 6f;

        private sealed class SpawnState
        {
            public HazardSpawnConfig config;
            public ObjectPool<Hazard> pool;
            public ObjectPool<Transform> markerPool; // warning cones for this hazard; null when none is assigned
            public float nextClusterAt;
            public int clustersThisSession;
        }

        private readonly List<SpawnState> states = new List<SpawnState>(4);
        private readonly List<Hazard> active = new List<Hazard>(64);
        private readonly List<HazardMarker> activeMarkers = new List<HazardMarker>(16);

        // Arc (road-metre) of the END of the most recently spawned cluster, of ANY hazard type — the frontier the
        // proximity gate keeps the next cluster clear of. NegativeInfinity = nothing spawned yet this session.
        private float lastClusterArc = float.NegativeInfinity;

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

                states.Add(new SpawnState { config = config, pool = pool, markerPool = BuildMarkerPool(config) });
            }
        }

        /// <summary>
        /// Builds the pool of warning markers (e.g. cones) for a config, or null when it has no warningPrefab.
        /// The prefab can be ANY plain GameObject: we pool it by its Transform and, as each instance is created,
        /// attach the bookkeeping <see cref="HazardMarker"/>, switch off every collider (a warning cone must never
        /// register a hit or block the scooter), stamp its pool, and register it for rewind — the same lifecycle
        /// a Hazard gets, so the cones scroll, curve and rewind exactly like the hazards they mark.
        /// </summary>
        private ObjectPool<Transform> BuildMarkerPool(HazardSpawnConfig config)
        {
            if (config.warningPrefab == null)
                return null;

            ObjectPool<Transform> markerPool = null;
            markerPool = new ObjectPool<Transform>(config.warningPrefab.transform, transform,
                Mathf.Max(1, config.warningPoolSize),
                created =>
                {
                    HazardMarker marker = created.GetComponent<HazardMarker>();
                    if (marker == null)
                        marker = created.gameObject.AddComponent<HazardMarker>();

                    Collider[] colliders = created.GetComponentsInChildren<Collider>();
                    for (int c = 0; c < colliders.Length; c++)
                        colliders[c].enabled = false;

                    // Instances made when the pool EXPANDS mid-game are stamped here; the initial batch is built
                    // before 'markerPool' is assigned, so the loop below covers those (mirrors the hazard pool).
                    if (markerPool != null)
                        marker.SourcePool = markerPool;
                    if (RewindSystem.Instance != null)
                        RewindSystem.Instance.Register(marker);
                });
            for (int j = 0; j < markerPool.AllInstances.Count; j++)
                markerPool.AllInstances[j].GetComponent<HazardMarker>().SourcePool = markerPool;

            return markerPool;
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
                PlaceOnRoad(hazard.transform, hazard.RoadArc, hazard.RoadLateral, hazard.GroundOffset);
            }

            // Warning markers ride the curve with their cluster and despawn behind the player, exactly like hazards.
            for (int i = activeMarkers.Count - 1; i >= 0; i--)
            {
                HazardMarker marker = activeMarkers[i];
                if (playerArc - marker.RoadArc > despawnBehindDistance)
                {
                    marker.SourcePool.Release(marker.transform);
                    activeMarkers.RemoveAt(i);
                    continue;
                }
                PlaceOnRoad(marker.transform, marker.RoadArc, marker.RoadLateral, marker.GroundOffset);
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

            // Hazards ride the curve in road space (see SpawnCluster / RoadSequencer.TryGetRoadPose), so nothing
            // thins the stream approaching a turn. The turn is instead kept CLEAR for fairness by the bend gate
            // just before SpawnCluster below (a hazard mid-bend is a cheap hit while the player is busy steering).

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

            // Per-tile hazard permission (2026-07-04): the tile itself decides which hazards may sit on it
            // (RoadTile.allowedHazards in the tile's inspector; an empty list allows everything). Checked at
            // the arc the cluster would actually land on, so the rule follows the tile, not the zone.
            RoadTile tileAhead = RoadSequencer.Instance != null
                ? RoadSequencer.Instance.TileAt(playerArc + config.spawnAheadDistance)
                : null;
            if (tileAhead != null && !tileAhead.AllowsHazard(config))
            {
                state.nextClusterAt = playerArc + 20f; // retry further along, on a tile that allows this hazard
                return;
            }

            // Per-tile density (2026-07-05): the tile ahead scales how many hazards land on it — the inspector
            // knob (RoadTile.hazardDensity) that makes a dirt stretch ride rough. 0 = keep this tile clear,
            // >1 = more potholes/rocks. It shortens the gap to the NEXT cluster, so a rough tile fills in.
            float tileDensity = tileAhead != null ? tileAhead.hazardDensity : 1f;
            if (tileDensity <= 0.001f)
            {
                state.nextClusterAt = playerArc + 20f; // this tile wants no hazards; try again past it
                return;
            }

            // Fairness gates (2026-07-06). Both defer by sliding this config's next attempt a little further
            // along, so a blocked cluster just moves onto the next stretch of clear, straight road.
            float clusterStart = playerArc + config.spawnAheadDistance;
            float clusterEnd = clusterStart + Mathf.Max(0, config.clusterMax - 1) * config.clusterSpacing;

            // (1) Not in a turn — the road bends through the cluster's footprint, so wait for it to straighten.
            if (turnBendThreshold > 0f && ClusterLandsInTurn(clusterStart, clusterEnd))
            {
                state.nextClusterAt = playerArc + RetryStep;
                return;
            }

            // (2) Not on top of another cluster — even one from a different hazard type — so the gap stays fair.
            if (Mathf.Abs(clusterStart - lastClusterArc) < minClusterSeparation)
            {
                state.nextClusterAt = playerArc + RetryStep;
                return;
            }

            SpawnCluster(state, playerArc);
            state.clustersThisSession++;
            // Scale the gap by the tile's density, but never below the config's minimum clear road — a rough
            // tile packs in more hazards yet still leaves room to thread through (Req §6.1).
            state.nextClusterAt = playerArc + Mathf.Max(config.minClusterGap, IntervalFor(config) / tileDensity);
        }

        private void SpawnCluster(SpawnState state, float playerArc)
        {
            HazardSpawnConfig config = state.config;
            float baseArc = playerArc + config.spawnAheadDistance;
            float baseLateral = PickLateral(config.zones);
            float limit = Mathf.Max(0f, config.lateralLimit); // keep the whole cluster on reachable road (no bank clipping)

            int count = Random.Range(config.clusterMin, config.clusterMax + 1);
            for (int i = 0; i < count; i++)
            {
                Hazard hazard = state.pool.Get();
                hazard.definition = config; // stamp so the hit carries this config's scoring/warning data (SC4)
                // Both the arc (longitudinal) and the lateral are road-space, so a staggered cluster — which
                // keeps a path through it (Req §6.1) — follows the bend instead of a straight line. The lateral is
                // clamped to ±lateralLimit so a shoulder pick plus stagger can't push a hazard onto the unreachable
                // raised bank at the road's edge, where its model would float or clip.
                hazard.RoadLateral = Mathf.Clamp(
                    baseLateral + (i == 0 ? 0f : (i % 2 == 0 ? 1f : -1f) * config.lateralStagger), -limit, limit);
                hazard.RoadArc = baseArc + i * config.clusterSpacing;
                hazard.transform.localScale = hazard.baseScale * Random.Range(config.scaleRange.x, config.scaleRange.y);
                hazard.gameObject.SetActive(true);
                // Resolve its curved world pose and rest it on the surface; cache the lift for the per-frame re-place.
                hazard.GroundOffset = GroundOnRoad(hazard.transform, hazard.RoadArc, hazard.RoadLateral, config.spawnHeight);
                active.Add(hazard);
            }

            // Remember where this cluster ENDS so the proximity gate keeps the next cluster (of any type) clear of it.
            lastClusterArc = baseArc + Mathf.Max(0, count - 1) * config.clusterSpacing;

            // One warning marker per cluster (e.g. a cone), set up-road of the potholes so the player spots them
            // coming (SC4). Cosmetic: it rides the curve and grounds exactly like a hazard, but never hits.
            if (state.markerPool != null)
            {
                Transform markerT = state.markerPool.Get();
                HazardMarker marker = markerT.GetComponent<HazardMarker>();
                marker.RoadLateral = Mathf.Clamp(baseLateral + config.warningLateralOffset, -limit, limit);
                marker.RoadArc = baseArc - config.warningLead; // ahead of the cluster, towards the player
                markerT.gameObject.SetActive(true);
                marker.GroundOffset = GroundOnRoad(markerT, marker.RoadArc, marker.RoadLateral, config.spawnHeight);
                activeMarkers.Add(marker);
            }
        }

        /// <summary>Re-derives a live object's world pose from its road-space point (arc + lateral) for this
        /// frame, then applies the constant vertical lift measured at spawn — so it rides the curve with the
        /// road. Shared by hazards and their warning markers.</summary>
        private static void PlaceOnRoad(Transform t, float arc, float lateral, float groundOffset)
        {
            ResolveWorldPose(arc, lateral, out Vector3 position, out Quaternion rotation);
            position.y += groundOffset;
            t.SetPositionAndRotation(position, rotation);
        }

        /// <summary>
        /// First placement of a freshly spawned object: resolve its curved world pose, measure how far to lift
        /// it so its LOWEST rendered point sits 'clearance' metres above the flat road (y = 0) whatever the
        /// prefab pivot is, and apply it — returning that lift so the caller can cache it. The road is flat and
        /// the spawn rotation is yaw-only, so the lift is constant: later frames reuse it via
        /// <see cref="PlaceOnRoad"/> with no per-frame bounds query. Falls back to a flat offset if there is no
        /// renderer to measure.
        /// </summary>
        private static float GroundOnRoad(Transform t, float arc, float lateral, float clearance)
        {
            ResolveWorldPose(arc, lateral, out Vector3 position, out Quaternion rotation);
            t.SetPositionAndRotation(position, rotation); // on the centreline (y≈0) so bounds measure true
            Renderer renderer = t.GetComponentInChildren<Renderer>();
            float groundOffset = renderer != null ? clearance - renderer.bounds.min.y : clearance;
            position.y += groundOffset;
            t.position = position;
            return groundOffset;
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

        /// <summary>
        /// True when the road anywhere across a cluster's footprint — plus a short buffer each side, so a cluster
        /// never sits right at a bend's mouth — turns more than <see cref="turnBendThreshold"/> degrees per metre.
        /// That means the cluster would land in (or right at the edge of) a turn, where dodging a pothole while
        /// steering the bend is an unfair, cheap hit. Sampled every few metres along the road. Returns false when
        /// there is no road yet, so a bare test scene still spawns.
        /// </summary>
        private bool ClusterLandsInTurn(float clusterStart, float clusterEnd)
        {
            RoadSequencer seq = RoadSequencer.Instance;
            if (seq == null)
                return false;

            for (float arc = clusterStart - TurnClearanceBuffer; arc <= clusterEnd + TurnClearanceBuffer + 0.001f; arc += TurnSampleStep)
            {
                RoadTile tile = seq.TileAt(arc);
                if (tile != null && Mathf.Abs(tile.CurveDegreesPerMetreAt(arc - tile.StartArc)) > turnBendThreshold)
                    return true;
            }
            return false;
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

            for (int i = 0; i < activeMarkers.Count; i++)
                activeMarkers[i].SourcePool.Release(activeMarkers[i].transform);
            activeMarkers.Clear();

            lastClusterArc = float.NegativeInfinity; // fresh session: the next cluster has no neighbour to clear

            for (int i = 0; i < states.Count; i++)
            {
                states[i].clustersThisSession = 0;
                states[i].nextClusterAt = IntervalFor(states[i].config);
            }
        }

        private void HandleRewindCompleted()
        {
            active.Clear();
            activeMarkers.Clear();
            for (int i = 0; i < states.Count; i++)
            {
                ObjectPool<Hazard> pool = states[i].pool;
                pool.ReconcileAvailability();
                for (int j = 0; j < pool.AllInstances.Count; j++)
                    if (pool.AllInstances[j].gameObject.activeInHierarchy)
                        active.Add(pool.AllInstances[j]);

                ObjectPool<Transform> markerPool = states[i].markerPool;
                if (markerPool == null)
                    continue;
                markerPool.ReconcileAvailability();
                for (int j = 0; j < markerPool.AllInstances.Count; j++)
                    if (markerPool.AllInstances[j].gameObject.activeInHierarchy)
                        activeMarkers.Add(markerPool.AllInstances[j].GetComponent<HazardMarker>());
            }
        }
    }
}
