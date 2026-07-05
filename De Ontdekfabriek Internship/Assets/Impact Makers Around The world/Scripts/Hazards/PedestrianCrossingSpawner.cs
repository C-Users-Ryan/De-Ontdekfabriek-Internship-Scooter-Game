using System.Collections.Generic;
using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;
using KenyaScooter.Roads;
using KenyaScooter.SafetyNet;
using KenyaScooter.Session;
using KenyaScooter.Settings;

namespace KenyaScooter.Hazards
{
    /// <summary>
    /// Spawns pedestrian crossings ahead of the player (M28 — yield to vulnerable road users). It is the
    /// pedestrian twin of <see cref="HazardSpawner"/>: distance-based, density-ramped over the session,
    /// pooled, and placed in ROAD SPACE through RoadSequencer.TryGetRoadPose so every crossing rides the
    /// curve exactly like the tiles and hazards do. Each crossing is TELEGRAPHED the moment it spawns
    /// (CrossingAhead → HUD caution banner) and appears warningLeadDistance ahead, so yielding is always a
    /// fair, anticipated choice and never a gotcha (design: anticipation, not punishment).
    ///
    /// Self-bootstraps after scene load if a config is discoverable, so it needs no scene wiring — and it
    /// does nothing at all if no PedestrianCrossingConfig is assigned/found, so gameplay is unaffected when
    /// the feature is absent or unconfigured.
    /// </summary>
    public sealed class PedestrianCrossingSpawner : MonoBehaviour
    {
        [Tooltip("Crossing definitions. One is the norm; an array allows several crossing kinds, like the hazard spawner.")]
        [SerializeField] private PedestrianCrossingConfig[] configs;
        [SerializeField] private float despawnBehindDistance = 30f;

        private sealed class SpawnState
        {
            public PedestrianCrossingConfig config;
            public ObjectPool<Pedestrian> pool;
            public float nextCrossingAt;
            public int spawnedThisSession;
        }

        private readonly List<SpawnState> states = new List<SpawnState>(2);
        private readonly List<Pedestrian> active = new List<Pedestrian>(8);

        // Self-bootstrap: if the scene has no spawner placed by hand but a config can be located, create one,
        // mirroring HapticFeedback / the scoring managers. Keeps scene wiring to zero (the feature ships if a
        // config exists). If nothing is found, no spawner is created and nothing changes.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<PedestrianCrossingSpawner>() != null)
                return;
            PedestrianCrossingConfig found = ConfigLocator.Find<PedestrianCrossingConfig>();
            if (found == null)
                return; // feature not configured — do nothing, gameplay is unaffected
            var go = new GameObject("PedestrianCrossingSpawner (auto)");
            var spawner = go.AddComponent<PedestrianCrossingSpawner>();
            spawner.configs = new[] { found };
        }

        private void Start()
        {
            if (configs == null)
                return;

            for (int i = 0; i < configs.Length; i++)
            {
                PedestrianCrossingConfig config = configs[i];
                if (config == null || config.prefab == null)
                    continue;

                ObjectPool<Pedestrian> pool = null;
                pool = new ObjectPool<Pedestrian>(config.prefab, transform, Mathf.Max(1, config.poolSize),
                    created =>
                    {
                        // Stamp SourcePool on pool-expansion instances (see HazardSpawner note) and register
                        // them for rewind so a crossing rewinds cleanly with the rest of the world.
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

            float dt = Time.deltaTime;
            float playerArc = PlayerArc;

            // Drive + re-place every active crossing from its point on the road so it rides the curve, and
            // despawn once it has both finished crossing and passed far enough behind the player.
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Pedestrian ped = active[i];
                ped.Tick(dt, playerArc);

                // Recycle once it has slipped far enough behind the player (measured along the road, not world Z).
                // A crossing that has finished + lingered is also recycled, but only once the player has actually
                // reached it — never while it is still ahead, or it would visibly pop out of an empty road.
                float behind = playerArc - ped.RoadArc;        // + once the player is past the crossing
                bool farBehind = behind > despawnBehindDistance;
                if (farBehind || (ped.ReadyToDespawn && behind > 0f))
                {
                    ped.SourcePool.Release(ped);
                    active.RemoveAt(i);
                    continue;
                }
                PlaceOnRoad(ped);
            }

            if (state != GameState.Playing)
                return; // keep scrolling/animating during checkpoint braking, but stop spawning new crossings

            for (int i = 0; i < states.Count; i++)
                TrySpawnCrossing(states[i], playerArc);
        }

        private void TrySpawnCrossing(SpawnState state, float playerArc)
        {
            PedestrianCrossingConfig config = state.config;
            if (playerArc < state.nextCrossingAt)
                return;
            if (config.maxPerSession > 0 && state.spawnedThisSession >= config.maxPerSession)
                return;

            // Fairness: never spawn a crossing into a bend the player cannot read around. A crossing on a sharp
            // curve would also slide off the straight spawn estimate; wait for clearer road.
            if (RoadSequencer.Instance != null &&
                Mathf.Abs(RoadSequencer.Instance.SharpestCurveWithin(config.warningLeadDistance)) > 30f)
            {
                state.nextCrossingAt = playerArc + 15f; // retry shortly, once the road straightens
                return;
            }

            // Context filter (e.g. town/village zones only). Blank entries are ignored.
            if (HasMeaningfulTags(config.requiredContextTags))
            {
                RoadSequence current = RoadSequencer.Instance != null ? RoadSequencer.Instance.CurrentSequence : null;
                if (current == null || !current.HasAnyTag(config.requiredContextTags))
                {
                    state.nextCrossingAt = playerArc + 20f; // retry once the zone changes
                    return;
                }
            }

            SpawnCrossing(state, playerArc);
            state.spawnedThisSession++;
            state.nextCrossingAt = playerArc + IntervalFor(config);
        }

        private void SpawnCrossing(SpawnState state, float playerArc)
        {
            PedestrianCrossingConfig config = state.config;
            float arc = playerArc + config.warningLeadDistance; // appears a full lead distance ahead — telegraphed and fair

            Pedestrian ped = state.pool.Get();
            ped.config = config;
            ped.RoadArc = arc;

            // Cross from one shoulder to the other; randomise which side per crossing so people come from both.
            float side = Random.value < 0.5f ? -1f : 1f;
            float from = side * config.walkFromLateral;
            ped.BeginWalk(from, -from);

            ped.gameObject.SetActive(true);
            PlaceAndGround(ped);
            active.Add(ped);

            // Telegraph: tell the HUD a crossing is coming so the player can anticipate the yield.
            if (!string.IsNullOrEmpty(config.crossingWarnKey))
                GameEvents.RaiseCrossingAhead(config.crossingWarnKey, config.warningLeadDistance);
        }

        /// <summary>Re-derives a live crossing's world pose from its road point (arc + current lateral) each frame.</summary>
        private static void PlaceOnRoad(Pedestrian ped)
        {
            ResolveWorldPose(ped.RoadArc, ped.RoadLateral, out Vector3 position, out Quaternion rotation);
            position.y += ped.GroundOffset;
            // Face the direction of the walk (along the road's local right/left), so the model walks where it moves.
            ped.transform.SetPositionAndRotation(position, rotation);
        }

        /// <summary>First placement: resolve the curved pose, measure the lift that rests the feet on the road, cache it.</summary>
        private static void PlaceAndGround(Pedestrian ped)
        {
            ResolveWorldPose(ped.RoadArc, ped.RoadLateral, out Vector3 position, out Quaternion rotation);
            ped.transform.SetPositionAndRotation(position, rotation);
            Renderer renderer = ped.GetComponentInChildren<Renderer>();
            ped.GroundOffset = renderer != null ? -renderer.bounds.min.y : 0f; // feet flush with y = 0
            position.y += ped.GroundOffset;
            ped.transform.position = position;
        }

        /// <summary>World pose of a road point (arc + lateral) via the sequencer's curve mapping, with the same flat
        /// +Z fallback HazardSpawner uses so it still works in a bare test scene with no sequencer.</summary>
        private static void ResolveWorldPose(float arc, float lateral, out Vector3 position, out Quaternion rotation)
        {
            if (RoadSequencer.Instance != null
                && RoadSequencer.Instance.TryGetRoadPose(arc, lateral, out position, out rotation))
                return;

            float ahead = arc - PlayerArc;
            position = RoadDirection.Current * ahead + RoadDirection.SteerAxis * lateral;
            rotation = Quaternion.LookRotation(RoadDirection.Current);
        }

        private static float PlayerArc =>
            RoadSequencer.Instance != null ? RoadSequencer.Instance.PlayerArc : WorldSpeed.Instance.DistanceTravelled;

        /// <summary>Metres until the next crossing — shrinks over the session via the density curve (matches the hazard ramp).</summary>
        private static float IntervalFor(PedestrianCrossingConfig config)
        {
            float t01 = TimerManager.Instance != null ? TimerManager.Instance.Normalized01 : 0f;
            float density = config.crossingsPer100m * Mathf.Max(0f, config.densityOverSession.Evaluate(t01));
            float interval = density > 0.001f ? 100f / density : 99999f;
            return Mathf.Max(config.minCrossingGap, interval);
        }

        private static bool HasMeaningfulTags(string[] tags)
        {
            if (tags == null)
                return false;
            for (int i = 0; i < tags.Length; i++)
                if (!string.IsNullOrWhiteSpace(tags[i]))
                    return true;
            return false;
        }

        private void HandleSessionReset()
        {
            for (int i = 0; i < active.Count; i++)
                active[i].SourcePool.Release(active[i]);
            active.Clear();

            for (int i = 0; i < states.Count; i++)
            {
                states[i].spawnedThisSession = 0;
                states[i].nextCrossingAt = IntervalFor(states[i].config);
            }
        }

        private void HandleRewindCompleted()
        {
            active.Clear();
            for (int i = 0; i < states.Count; i++)
            {
                ObjectPool<Pedestrian> pool = states[i].pool;
                pool.ReconcileAvailability();
                for (int j = 0; j < pool.AllInstances.Count; j++)
                    if (pool.AllInstances[j].gameObject.activeInHierarchy)
                        active.Add(pool.AllInstances[j]);
            }
        }
    }
}
