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
    /// </summary>
    public sealed class HazardSpawner : MonoBehaviour
    {
        [SerializeField] private HazardSpawnConfig[] configs;
        [SerializeField] private Transform player;
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

                var pool = new ObjectPool<Hazard>(config.prefab, transform, config.poolSize,
                    created =>
                    {
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

            // Central scroll (M1) and despawn.
            Vector3 delta = -RoadDirection.Current * (WorldSpeed.Instance.Current * Time.deltaTime);
            float playerLong = RoadDirection.Longitudinal(player.position);
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Hazard hazard = active[i];
                hazard.transform.position += delta;
                if (playerLong - RoadDirection.Longitudinal(hazard.transform.position) > despawnBehindDistance)
                {
                    hazard.SourcePool.Release(hazard);
                    active.RemoveAt(i);
                }
            }

            if (state != GameState.Playing)
                return;

            float travelled = WorldSpeed.Instance.DistanceTravelled;
            for (int i = 0; i < states.Count; i++)
                TrySpawnCluster(states[i], travelled, playerLong);
        }

        private void TrySpawnCluster(SpawnState state, float travelled, float playerLong)
        {
            HazardSpawnConfig config = state.config;
            if (travelled < state.nextClusterAt)
                return;
            if (config.maxPerSession > 0 && state.clustersThisSession >= config.maxPerSession)
                return;

            // Context filter (Req §6.2): rocks only appear on murram/tsavo/construction.
            if (config.requiredContextTags != null && config.requiredContextTags.Length > 0)
            {
                RoadSequence current = RoadSequencer.Instance != null ? RoadSequencer.Instance.CurrentSequence : null;
                if (current == null || !current.HasAnyTag(config.requiredContextTags))
                {
                    state.nextClusterAt = travelled + 20f; // retry once the zone changes
                    return;
                }
            }

            SpawnCluster(state, playerLong);
            state.clustersThisSession++;
            state.nextClusterAt = travelled + IntervalFor(config);
        }

        private void SpawnCluster(SpawnState state, float playerLong)
        {
            HazardSpawnConfig config = state.config;
            float baseLongitudinal = playerLong + config.spawnAheadDistance;
            float baseLateral = PickLateral(config.zones);

            int count = Random.Range(config.clusterMin, config.clusterMax + 1);
            for (int i = 0; i < count; i++)
            {
                Hazard hazard = state.pool.Get();
                hazard.definition = config; // stamp so the hit carries this config's scoring/warning data (SC4)
                // Staggered laterally so a path through always exists (Req §6.1).
                float lateral = baseLateral + (i == 0 ? 0f : (i % 2 == 0 ? 1f : -1f) * config.lateralStagger);
                float longitudinal = baseLongitudinal + i * config.clusterSpacing;

                hazard.transform.position = RoadDirection.Current * longitudinal
                    + RoadDirection.SteerAxis * lateral
                    + Vector3.up * config.spawnHeight;
                hazard.transform.rotation = Quaternion.LookRotation(RoadDirection.Current);
                hazard.transform.localScale = hazard.baseScale * Random.Range(config.scaleRange.x, config.scaleRange.y);
                hazard.gameObject.SetActive(true);
                active.Add(hazard);
            }
        }

        /// <summary>Metres until the next cluster — shrinks over the session via the density curve (M21, D10).</summary>
        private static float IntervalFor(HazardSpawnConfig config)
        {
            float t01 = TimerManager.Instance != null ? TimerManager.Instance.Normalized01 : 0f;
            float density = config.clustersPer100m * Mathf.Max(0f, config.densityOverSession.Evaluate(t01));
            float interval = density > 0.001f ? 100f / density : 99999f;
            return Mathf.Max(config.minClusterGap, interval);
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
