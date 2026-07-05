using System.Collections.Generic;
using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;
using KenyaScooter.Roads;
using KenyaScooter.SafetyNet;

namespace KenyaScooter.Traffic
{
    /// <summary>
    /// Owns both traffic lanes (M9–M11): the prewarmed same-direction queue, the
    /// gap-guaranteed oncoming stream, per-prefab pools, and the single tick loop that
    /// drives every active vehicle. Spawning pauses at the checkpoint (Req §9.1) while
    /// existing traffic keeps flowing. The active TrafficBehaviourProfile follows the
    /// road sequence (Req §5.2), which is how later zones skew Aggressive (MDA D10).
    /// </summary>
    public sealed class TrafficSpawner : MonoBehaviour
    {
        [SerializeField] private TrafficConfig config;
        [SerializeField] private TrafficBehaviourProfile defaultProfile;
        [SerializeField] private TrafficVehicle[] sameDirectionPrefabs;
        [SerializeField] private TrafficVehicle[] oncomingPrefabs;
        [SerializeField] private Transform player;

        private readonly Dictionary<TrafficVehicle, ObjectPool<TrafficVehicle>> pools =
            new Dictionary<TrafficVehicle, ObjectPool<TrafficVehicle>>();

        private TrafficBehaviourProfile activeProfile;
        private float nextOncomingGap;

        private void Start()
        {
            activeProfile = defaultProfile;
            BuildPools(sameDirectionPrefabs);
            BuildPools(oncomingPrefabs);
        }

        private void OnEnable()
        {
            GameEvents.SessionReset += HandleSessionReset;
            GameEvents.SequenceChanged += HandleSequenceChanged;
            GameEvents.RewindCompleted += HandleRewindCompleted;
        }

        private void OnDisable()
        {
            GameEvents.SessionReset -= HandleSessionReset;
            GameEvents.SequenceChanged -= HandleSequenceChanged;
            GameEvents.RewindCompleted -= HandleRewindCompleted;
        }

        private void Update()
        {
            GameState state = GameManager.State;
            if (state != GameState.Playing && state != GameState.AtCheckpoint)
                return;

            float dt = Time.deltaTime;
            // Road space: the player's arc (road odometer) and lateral. Vehicles are placed and reasoned about
            // along the road, so traffic rides through bends instead of pausing on the straight +Z spawn line.
            float playerArc = PlayerArc;
            float playerLateral = RoadDirection.Lateral(player.position);

            for (int i = TrafficVehicle.Active.Count - 1; i >= 0; i--)
            {
                TrafficVehicle vehicle = TrafficVehicle.Active[i];
                vehicle.Tick(dt, config, playerArc, playerLateral);

                float ahead = vehicle.RoadArc - playerArc;
                if (-ahead > config.despawnBehindDistance)
                    Despawn(vehicle);
                else if (vehicle.Direction == LaneDirection.SameDirection
                    && ahead > config.sameDirectionSpawnDistance + 40f)
                    Despawn(vehicle); // pulled too far ahead (the player braked) — recycle so spawning never stalls
            }

            if (state == GameState.Playing)
            {
                // No curve gate any more: vehicles are placed in road space and follow the bend (see SpawnSingle /
                // RoadSequencer.TryGetRoadPose), so the stream no longer thins approaching a turn.
                SpawnSameDirection(playerArc);
                SpawnOncoming(playerArc);
            }
        }

        /// <summary>The player's progress along the road centreline (metres) — the rewound road odometer when a
        /// sequencer exists, else the world odometer. Shared with TrafficVehicle so placement uses one frame.</summary>
        private static float PlayerArc =>
            RoadSequencer.Instance != null ? RoadSequencer.Instance.PlayerArc
            : (WorldSpeed.Instance != null ? WorldSpeed.Instance.DistanceTravelled : 0f);

        // ---- Spawning -------------------------------------------------------------

        private void SpawnSameDirection(float playerArc)
        {
            if (CountDirection(LaneDirection.SameDirection) >= config.maxSameDirection)
                return;

            float horizonArc = playerArc + config.sameDirectionSpawnDistance;
            TrafficVehicle furthest = FurthestInDirection(LaneDirection.SameDirection);
            if (furthest != null)
            {
                float gap = (config.minimumCarGap + Random.value * config.sameDirectionGapJitter)
                    * activeProfile.sameDirectionIntervalMult
                    * (activeProfile.jamMode ? 0.45f : 1f);

                // The spawn guarantee (M11): never closer than gap + car length
                // behind the queue's tail. Wait until the queue has drifted in.
                if (furthest.RoadArc + furthest.length + gap > horizonArc)
                    return;
            }

            SpawnVehicle(sameDirectionPrefabs, LaneDirection.SameDirection,
                RoadSideConfig.Active.OwnLaneCentre, horizonArc, allowSwarm: true);
        }

        private void SpawnOncoming(float playerArc)
        {
            if (CountDirection(LaneDirection.Oncoming) >= config.maxOncoming)
                return;

            float spawnArc = playerArc + config.oncomingSpawnDistance;
            TrafficVehicle furthest = FurthestInDirection(LaneDirection.Oncoming);

            // The previous oncoming vehicle must have closed nextOncomingGap metres
            // before another appears — every gap is a completable overtake window (M11).
            if (furthest != null && furthest.RoadArc > spawnArc - nextOncomingGap)
                return;

            SpawnVehicle(oncomingPrefabs, LaneDirection.Oncoming,
                RoadSideConfig.Active.OncomingLaneCentre, spawnArc, allowSwarm: true);
            nextOncomingGap = (config.oncomingMinGap + Random.value * config.oncomingGapJitter)
                * activeProfile.oncomingIntervalMult;
        }

        private void SpawnVehicle(TrafficVehicle[] prefabs, LaneDirection direction, float laneCentre,
            float arc, bool allowSwarm)
        {
            TrafficVehicle prefab = WeightedPick(prefabs);
            if (prefab == null)
                return;

            if (allowSwarm && prefab.type == VehicleType.BodaBoda)
            {
                int count = Random.Range(config.bodaSwarmMin, config.bodaSwarmMax + 1);
                for (int i = 0; i < count; i++)
                {
                    float lateral = laneCentre + Random.Range(-config.bodaSwarmLateralJitter, config.bodaSwarmLateralJitter);
                    SpawnSingle(prefab, direction, lateral, arc + i * config.bodaSwarmSpacing);
                }
                return;
            }

            SpawnSingle(prefab, direction, laneCentre, arc);
        }

        private void SpawnSingle(TrafficVehicle prefab, LaneDirection direction, float lateral, float arc)
        {
            TrafficVehicle vehicle = pools[prefab].Get();
            // Placed at a point on the road (arc + lateral); Activate derives the curved world pose.
            vehicle.Activate(direction, activeProfile, lateral, arc);
            vehicle.gameObject.SetActive(true);
        }

        private void Despawn(TrafficVehicle vehicle)
        {
            // A pending pass that was never invalidated completes when the vehicle
            // leaves play — the player did get past it (generous-confirm, D17).
            if (vehicle.PassPending && !vehicle.PassDone && !vehicle.PassInvalidated)
            {
                vehicle.PassDone = true;
                GameEvents.RaiseOvertakeCompleted(vehicle);
            }
            vehicle.SourcePool.Release(vehicle);
        }

        // ---- Session flow -----------------------------------------------------------

        private void HandleSessionReset()
        {
            // Clear every active vehicle. Pooled vehicles return to their pool; a vehicle
            // placed directly in the scene has no SourcePool, so it is simply deactivated
            // instead of dereferencing null — one hand-placed object must not be able to
            // throw and abort the whole session-reset chain.
            for (int i = TrafficVehicle.Active.Count - 1; i >= 0; i--)
            {
                TrafficVehicle vehicle = TrafficVehicle.Active[i];
                if (vehicle == null)
                    continue;
                if (vehicle.SourcePool != null)
                    vehicle.SourcePool.Release(vehicle);
                else
                    vehicle.gameObject.SetActive(false);
            }

            activeProfile = defaultProfile;
            nextOncomingGap = config.oncomingMinGap + Random.value * config.oncomingGapJitter;

            // Prewarm (M10): the player starts behind a populated queue, never an
            // empty road. Swarms are skipped here to keep prewarm spacing exact.
            // Placed in road space (the road resets to arc 0), so the prewarmed queue already sits on the
            // road's opening shape rather than the straight +Z line.
            float playerArc = PlayerArc;
            float distance = config.prewarmStartDistance;
            while (distance <= config.prewarmEndDistance)
            {
                SpawnVehicle(sameDirectionPrefabs, LaneDirection.SameDirection,
                    RoadSideConfig.Active.OwnLaneCentre, playerArc + distance, allowSwarm: false);
                distance += config.prewarmGap + Random.Range(-config.prewarmGapJitter, config.prewarmGapJitter);
            }
        }

        private void HandleSequenceChanged(RoadSequence sequence)
        {
            activeProfile = sequence != null && sequence.trafficProfileOverride != null
                ? sequence.trafficProfileOverride
                : defaultProfile;
        }

        private void HandleRewindCompleted()
        {
            // The rewind toggles vehicles active/inactive directly; rebuild pool
            // bookkeeping from the actual scene state. TrafficVehicle.Active stays
            // correct on its own via OnEnable/OnDisable.
            foreach (ObjectPool<TrafficVehicle> pool in pools.Values)
                pool.ReconcileAvailability();
        }

        // ---- Helpers ---------------------------------------------------------------

        private void BuildPools(TrafficVehicle[] prefabs)
        {
            for (int i = 0; i < prefabs.Length; i++)
            {
                TrafficVehicle prefab = prefabs[i];
                if (prefab == null || pools.ContainsKey(prefab))
                    continue;

                ObjectPool<TrafficVehicle> pool = null;
                pool = new ObjectPool<TrafficVehicle>(prefab, transform, config.poolSizePerPrefab,
                    created =>
                    {
                        // Covers pool expansion mid-game (see the note in HazardSpawner): an
                        // expanded vehicle with no SourcePool throws when it despawns.
                        if (pool != null)
                            created.SourcePool = pool;
                        if (RewindSystem.Instance != null)
                            RewindSystem.Instance.Register(created);
                    });
                for (int j = 0; j < pool.AllInstances.Count; j++)
                    pool.AllInstances[j].SourcePool = pool;
                pools.Add(prefab, pool);
            }
        }

        private static int CountDirection(LaneDirection direction)
        {
            int count = 0;
            for (int i = 0; i < TrafficVehicle.Active.Count; i++)
                if (TrafficVehicle.Active[i].Direction == direction)
                    count++;
            return count;
        }

        private static TrafficVehicle FurthestInDirection(LaneDirection direction)
        {
            TrafficVehicle furthest = null;
            float best = float.MinValue;
            for (int i = 0; i < TrafficVehicle.Active.Count; i++)
            {
                TrafficVehicle vehicle = TrafficVehicle.Active[i];
                if (vehicle.Direction != direction)
                    continue;
                if (vehicle.RoadArc > best)
                {
                    best = vehicle.RoadArc;
                    furthest = vehicle;
                }
            }
            return furthest;
        }

        private static TrafficVehicle WeightedPick(TrafficVehicle[] prefabs)
        {
            if (prefabs == null || prefabs.Length == 0)
                return null;

            float total = 0f;
            for (int i = 0; i < prefabs.Length; i++)
                total += prefabs[i] != null ? prefabs[i].spawnWeight : 0f;

            float roll = Random.value * total;
            for (int i = 0; i < prefabs.Length; i++)
            {
                if (prefabs[i] == null)
                    continue;
                roll -= prefabs[i].spawnWeight;
                if (roll < 0f)
                    return prefabs[i];
            }
            return prefabs[prefabs.Length - 1];
        }
    }
}
