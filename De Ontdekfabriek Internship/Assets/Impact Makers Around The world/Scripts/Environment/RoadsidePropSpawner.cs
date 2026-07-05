using System.Collections.Generic;
using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;
using KenyaScooter.Roads;
using KenyaScooter.SafetyNet;
using KenyaScooter.Session;
using KenyaScooter.Settings;

namespace KenyaScooter.Environment
{
    /// <summary>
    /// Scatters roadside scenery along the shoulders so the world stops feeling empty (Oplevering insight 2).
    /// It is the decorative twin of <see cref="KenyaScooter.Hazards.HazardSpawner"/>: distance-based,
    /// density-ramped, day-cycle aware, pooled, and placed in ROAD SPACE through RoadSequencer.TryGetRoadPose
    /// so every prop rides the curve exactly like the tiles and hazards do. The difference from the hazard
    /// spawner is that props are PURE SCENERY (no hit, no scoring), they sit on the shoulders beyond the
    /// player's steer limit, and the placement is SIDE-AWARE (left/right shoulders, per entry).
    ///
    /// Self-bootstraps after scene load if a config is discoverable, so it needs no scene wiring — and it does
    /// nothing at all if no RoadsidePropConfig is found, or if the facilitator has switched it off, so the
    /// proven straight-road build is never at risk. Everything here is ADDITIVE and gated behind the config's
    /// <see cref="RoadsidePropConfig.enabled"/> flag.
    /// </summary>
    public sealed class RoadsidePropSpawner : MonoBehaviour
    {
        [Tooltip("The roadside-life palette + density. Leave empty to let the spawner find the single loaded " +
                 "RoadsidePropConfig automatically (zero scene wiring).")]
        [SerializeField] private RoadsidePropConfig config;

        private sealed class EntryState
        {
            public RoadsidePropConfig.PropEntry entry;
            public ObjectPool<RoadsideProp> pool;
        }

        private readonly List<EntryState> entries = new List<EntryState>(8);
        private readonly List<RoadsideProp> active = new List<RoadsideProp>(64);

        private float nextPropAt;                       // player arc at which the next prop is scheduled
        private float lastLeftArc = float.NegativeInfinity;   // spawn arc of the last LEFT-shoulder prop (same-side gap)
        private float lastRightArc = float.NegativeInfinity;  // spawn arc of the last RIGHT-shoulder prop
        private int dayPhase;                           // current day-cycle phase index (from DayPhaseChanged)

        // Self-bootstrap: if the scene has no spawner placed by hand but a config can be located, create one,
        // mirroring HazardSpawner / PedestrianCrossingSpawner. Keeps scene wiring to zero. If no config is loaded,
        // nothing is created and gameplay is unaffected. Created even when the config is currently disabled, so a
        // facilitator can switch roadside life ON mid-session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<RoadsidePropSpawner>() != null)
                return;
            RoadsidePropConfig found = ConfigLocator.Find<RoadsidePropConfig>();
            if (found == null)
                return; // feature not configured — do nothing
            var go = new GameObject("RoadsidePropSpawner (auto)");
            go.AddComponent<RoadsidePropSpawner>().config = found;
        }

        private void Start()
        {
            if (config == null)
                config = ConfigLocator.Find<RoadsidePropConfig>();
            if (config == null || config.props == null)
                return;

            for (int i = 0; i < config.props.Length; i++)
            {
                RoadsidePropConfig.PropEntry entry = config.props[i];
                if (entry == null || entry.prefab == null)
                    continue;

                ObjectPool<RoadsideProp> pool = null;
                pool = new ObjectPool<RoadsideProp>(entry.prefab, transform, Mathf.Max(1, entry.poolSize),
                    created =>
                    {
                        // Capture the prefab's authored scale here (valid on the freshly instantiated clone),
                        // stamp SourcePool on pool-expansion instances (see HazardSpawner note), and register
                        // for rewind so a prop scrolls backward with the world during a rewind.
                        created.BaseScale = created.transform.localScale;
                        if (pool != null)
                            created.SourcePool = pool;
                        if (RewindSystem.Instance != null)
                            RewindSystem.Instance.Register(created);
                    });
                for (int j = 0; j < pool.AllInstances.Count; j++)
                    pool.AllInstances[j].SourcePool = pool;

                entries.Add(new EntryState { entry = entry, pool = pool });
            }
        }

        private void OnEnable()
        {
            GameEvents.SessionReset += HandleSessionReset;
            GameEvents.RewindCompleted += HandleRewindCompleted;
            GameEvents.DayPhaseChanged += HandleDayPhaseChanged;
        }

        private void OnDisable()
        {
            GameEvents.SessionReset -= HandleSessionReset;
            GameEvents.RewindCompleted -= HandleRewindCompleted;
            GameEvents.DayPhaseChanged -= HandleDayPhaseChanged;
        }

        private void Update()
        {
            if (config == null)
                return;

            GameState state = GameManager.State;
            if (state != GameState.Playing && state != GameState.AtCheckpoint)
                return;

            float playerArc = PlayerArc;
            bool on = config.enabled;

            // Re-place every active prop from its fixed road point so it rides the curve, and recycle it once it
            // has slipped far enough behind the player (measured along the road, not world Z). If the facilitator
            // has switched roadside life OFF, release every active prop so the berm clears immediately.
            for (int i = active.Count - 1; i >= 0; i--)
            {
                RoadsideProp prop = active[i];
                if (!on || playerArc - prop.RoadArc > config.despawnBehindDistance)
                {
                    prop.SourcePool.Release(prop);
                    active.RemoveAt(i);
                    continue;
                }
                PlaceOnRoad(prop);
            }

            if (!on)
                return;                 // switched off — actives cleared above, spawn nothing
            if (state != GameState.Playing)
                return;                 // keep scrolling at the checkpoint, but stop placing new props

            TrySpawn(playerArc);
        }

        private void TrySpawn(float playerArc)
        {
            if (playerArc < nextPropAt)
                return;
            // The active cap is scaled by the performance tier (VOLLEDIG ×1, GEBALANCEERD ×0.7, LICHT ×0.35):
            // roadside life is the biggest draw-call source, so it is the first thing an old tablet trades away.
            int activeCap = config.maxActiveProps > 0
                ? Mathf.Max(4, Mathf.RoundToInt(config.maxActiveProps * PerformanceMode.DensityScale))
                : 0;
            if (activeCap > 0 && active.Count >= activeCap)
            {
                nextPropAt = playerArc + 6f; // at the performance cap: hold off, retry as props recycle behind
                return;
            }

            // Fairness/geometry: do not place a prop while the road bends hard within the spawn-ahead distance —
            // it would slide off the straight spawn estimate. Wait for clearer road (mirrors the hazard guard).
            if (RoadSequencer.Instance != null &&
                Mathf.Abs(RoadSequencer.Instance.SharpestCurveWithin(config.spawnAheadDistance)) > config.maxCurveAngle)
            {
                nextPropAt = playerArc + 10f; // retry shortly, once the road straightens
                return;
            }

            EntryState chosen = PickEntry();
            if (chosen == null)
            {
                nextPropAt = playerArc + 15f; // no entry eligible in this zone — retry once the zone changes
                return;
            }

            Place(chosen, playerArc);
            nextPropAt = playerArc + IntervalFor();
        }

        private void Place(EntryState state, float playerArc)
        {
            RoadsidePropConfig.PropEntry entry = state.entry;
            float spawnArc = playerArc + config.spawnAheadDistance;

            int side = ChooseSide(entry, spawnArc);
            if (side == 0)
                return; // both shoulders are within the same-side gap right now — skip this one, cursor still advances

            float edge = ShoulderEdge();
            float depth = Random.Range(entry.lateralDepthRange.x, entry.lateralDepthRange.y);
            float lateral = side * (edge + config.shoulderClearance + Mathf.Max(0f, depth));

            RoadsideProp prop = state.pool.Get();
            prop.SourcePool = state.pool;
            prop.RoadArc = spawnArc;
            prop.RoadLateral = lateral;
            prop.YawOffset = Random.Range(-entry.yawJitter, entry.yawJitter);
            float scale = Random.Range(entry.scaleRange.x, entry.scaleRange.y);
            if (scale <= 0.001f) scale = 1f; // guard a zeroed scaleRange so a prop can never spawn invisibly
            prop.transform.localScale = prop.BaseScale * scale;
            prop.gameObject.SetActive(true);
            PlaceAndGround(prop);
            active.Add(prop);

            if (side < 0) lastLeftArc = spawnArc; else lastRightArc = spawnArc;
        }

        /// <summary>Picks a shoulder allowed by the entry that also clears the same-side minimum gap; 0 if neither can.</summary>
        private int ChooseSide(RoadsidePropConfig.PropEntry entry, float spawnArc)
        {
            bool leftOk = entry.side != RoadsidePropConfig.RoadSide.RightOnly
                          && spawnArc - lastLeftArc >= config.minGapPerSide;
            bool rightOk = entry.side != RoadsidePropConfig.RoadSide.LeftOnly
                           && spawnArc - lastRightArc >= config.minGapPerSide;
            if (leftOk && rightOk)
                return Random.value < 0.5f ? -1 : 1;
            if (leftOk)
                return -1;
            if (rightOk)
                return 1;
            return 0;
        }

        /// <summary>Weighted pick among the entries eligible in the current road zone (tag filter), or null if none.</summary>
        private EntryState PickEntry()
        {
            RoadSequence seq = RoadSequencer.Instance != null ? RoadSequencer.Instance.CurrentSequence : null;

            float total = 0f;
            for (int i = 0; i < entries.Count; i++)
                if (IsEligible(entries[i].entry, seq))
                    total += Mathf.Max(0f, entries[i].entry.weight);
            if (total <= 0f)
                return null;

            float roll = Random.value * total;
            EntryState last = null;
            for (int i = 0; i < entries.Count; i++)
            {
                EntryState es = entries[i];
                if (!IsEligible(es.entry, seq))
                    continue;
                float w = Mathf.Max(0f, es.entry.weight);
                if (w <= 0f)
                    continue;
                last = es;
                roll -= w;
                if (roll < 0f)
                    return es;
            }
            return last; // float rounding fallback
        }

        private static bool IsEligible(RoadsidePropConfig.PropEntry entry, RoadSequence seq)
        {
            if (!HasMeaningfulTags(entry.requiredContextTags))
                return true; // no zone filter — eligible everywhere
            return seq != null && seq.HasAnyTag(entry.requiredContextTags);
        }

        /// <summary>Metres until the next prop — shrinks with the density dial, the session ramp and the day phase.</summary>
        private float IntervalFor()
        {
            float t01 = TimerManager.Instance != null ? TimerManager.Instance.Normalized01 : 0f;
            float sessionMult = Mathf.Max(0f, config.densityOverSession != null ? config.densityOverSession.Evaluate(t01) : 1f);
            float density = config.propsPer100m * sessionMult * PhaseMultiplier();
            float interval = density > 0.001f ? 100f / density : 99999f;
            return Mathf.Max(1f, interval);
        }

        /// <summary>Day-cycle density multiplier for the current phase (morning rush, busy midday, calmer dusk). 1 if unset.</summary>
        private float PhaseMultiplier()
        {
            float[] m = config.phaseDensityMultipliers;
            if (m == null || m.Length == 0)
                return 1f;
            int i = Mathf.Clamp(dayPhase, 0, m.Length - 1);
            return Mathf.Max(0f, m[i]);
        }

        /// <summary>Outer edge of the shoulder (m from centre), read live so props stay off the road per location.</summary>
        private static float ShoulderEdge()
        {
            RoadSideConfig rs = RoadSideConfig.Active;
            return rs != null ? rs.laneWidth + rs.shoulderWidth : 4.75f;
        }

        /// <summary>Re-derives a live prop's world pose from its road point (arc + lateral) each frame, with the yaw
        /// jitter on top of the road heading and the spawn-time ground lift.</summary>
        private static void PlaceOnRoad(RoadsideProp prop)
        {
            ResolveWorldPose(prop.RoadArc, prop.RoadLateral, out Vector3 position, out Quaternion rotation);
            rotation *= Quaternion.Euler(0f, prop.YawOffset, 0f);
            position.y += prop.GroundOffset;
            prop.transform.SetPositionAndRotation(position, rotation);
        }

        /// <summary>First placement: resolve the curved pose (+yaw), rest the prop's lowest part on the road, and cache
        /// the scale-1 lift so later spawns of this pooled instance never re-walk the bounds.</summary>
        private static void PlaceAndGround(RoadsideProp prop)
        {
            ResolveWorldPose(prop.RoadArc, prop.RoadLateral, out Vector3 position, out Quaternion rotation);
            rotation *= Quaternion.Euler(0f, prop.YawOffset, 0f);
            prop.transform.SetPositionAndRotation(position, rotation); // on the centreline (y ~ 0) so bounds measure true

            float scaleY = Mathf.Max(0.0001f, prop.transform.localScale.y);
            if (prop.UnscaledLift < 0f)
            {
                // Measure ONCE per pooled instance, from the COMBINED bounds of every child renderer (a stall or
                // windmill must rest on its lowest part, not on whichever renderer happens to be first). A yaw-only
                // rotation does not change vertical extent, and the lift scales linearly, so caching it is exact.
                prop.GroundOffset = CombinedLift(prop);
                prop.UnscaledLift = prop.GroundOffset / scaleY;
            }
            else
            {
                prop.GroundOffset = prop.UnscaledLift * scaleY; // reuse the cached measurement, scaled to this spawn
            }
            position.y += prop.GroundOffset;
            prop.transform.position = position;
        }

        /// <summary>Distance from the prop's pivot down to the lowest point of its combined renderer bounds, in world
        /// units at the current scale (so the base can be rested on the road). 0 if it has no renderers.</summary>
        private static float CombinedLift(RoadsideProp prop)
        {
            Renderer[] renderers = prop.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return 0f;
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                b.Encapsulate(renderers[i].bounds);
            return prop.transform.position.y - b.min.y;
        }

        /// <summary>World pose of a road point (arc + lateral) via the sequencer's curve mapping, with the same flat
        /// +Z fallback the hazard/pedestrian spawners use so it still works in a bare test scene with no sequencer.</summary>
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
            nextPropAt = 0f;
            lastLeftArc = float.NegativeInfinity;
            lastRightArc = float.NegativeInfinity;
            dayPhase = 0;
        }

        private void HandleRewindCompleted()
        {
            // The rewind toggled prop actives/positions directly (RoadsideProp is IRewindable); rebuild the
            // active list from the actual scene state, exactly as the hazard/pedestrian spawners do. The spawn
            // cursor is left as-is so props already laid ahead are not duplicated.
            active.Clear();
            for (int i = 0; i < entries.Count; i++)
            {
                ObjectPool<RoadsideProp> pool = entries[i].pool;
                pool.ReconcileAvailability();
                for (int j = 0; j < pool.AllInstances.Count; j++)
                    if (pool.AllInstances[j].gameObject.activeInHierarchy)
                        active.Add(pool.AllInstances[j]);
            }
        }

        private void HandleDayPhaseChanged(int phaseIndex, string label) => dayPhase = phaseIndex;
    }
}
