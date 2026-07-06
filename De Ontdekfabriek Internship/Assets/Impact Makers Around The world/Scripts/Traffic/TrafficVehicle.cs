using System.Collections.Generic;
using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;
using KenyaScooter.FX;
using KenyaScooter.Hazards;
using KenyaScooter.Roads;
using KenyaScooter.SafetyNet;

namespace KenyaScooter.Traffic
{
    /// <summary>
    /// One pooled traffic vehicle (M9, M12). The vehicle lives in road space: it carries an arc-length
    /// (RoadArc) along the centreline plus a lateral offset (RoadLateral), advances the arc by its own
    /// driving each tick (the world scroll is carried by the player's arc advancing), and re-derives its
    /// world pose every frame from RoadSequencer's curve mapping — the same one that places the tiles and
    /// hazards. So a vehicle rides a bend with the road instead of scrolling down the straight +Z line, and
    /// traffic keeps populating the road through curves (no thinning approaching a turn). Personality
    /// (assigned at activation from the active profile) drives speed, lane drift, yielding and swerving.
    /// Vehicles are ticked by TrafficSpawner rather than running their own Update (manager-tick pattern —
    /// one loop instead of dozens of Update callbacks).
    /// </summary>
    public sealed class TrafficVehicle : MonoBehaviour, IRewindable
    {
        /// <summary>The only lookup path for detectors — no scene scans during play (Req §17).</summary>
        public static readonly List<TrafficVehicle> Active = new List<TrafficVehicle>(32);

        // Facilitator hooks (SettingsCatalog): global gates over the per-prefab behaviour flags, so the menu can
        // calm the whole road for a young group without touching prefabs. Persisted by GameSettings overrides.
        public static bool OvertakingEnabled = true;   // master gate over canOvertake
        public static float BreakdownBoost = 1f;       // multiplies every prefab's breakdownChance (0 = never)
        private static bool _warnedNoPersonalities;     // one-shot latch: warn once, not once per pooled vehicle

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { OvertakingEnabled = true; BreakdownBoost = 1f; }

        [Header("Type (Req §5.3)")]
        public VehicleType type = VehicleType.Matatu;
        [Tooltip("Relative pick chance within the spawner's prefab list.")]
        public float spawnWeight = 1f;
        [Tooltip("Own driving speed range (m/s) when driving the player's direction.")]
        public Vector2 sameDirectionSpeedRange = new Vector2(6.5f, 8.5f);
        [Tooltip("Own driving speed range (m/s) when oncoming.")]
        public Vector2 oncomingSpeedRange = new Vector2(9f, 12f);
        public float length = 4.5f;
        public float width = 1.9f;
        [Tooltip("Base acceleration (m/s²): how fast this vehicle changes its driving speed. Heavy trucks low (~1.5), boda-boda high (~6). Scaled per driver by the personality's accelerationMult.")]
        public float acceleration = 3f;
        [Tooltip("Base points for overtaking this vehicle — matatu < truck < boda swarm (Req §7.1).")]
        public int overtakeScore = 150;
        [Tooltip("Broken-down vehicles (T04) never move.")]
        public bool isStaticObstacle = false;
        [Range(0f, 1f)]
        [Tooltip("Chance this vehicle performs a matatu passenger stop: drifts to the verge and halts without signalling (Req §5.3 M13 event).")]
        public float vergeStopChance = 0f;

        [Header("Overtaking (M13 — cars pass slower vehicles)")]
        [Tooltip("If on, this vehicle pulls into the oncoming lane to pass a clearly-slower car ahead — but only when the oncoming lane is clear, and it tucks back if an oncoming vehicle appears.")]
        public bool canOvertake = true;
        [Range(0f, 3f)]
        [Tooltip("Eagerness to commit to a pass (per-second chance, scaled by the driver: aggressive overtake readily, cautious/distracted rarely).")]
        public float overtakeUrgency = 0.6f;

        [Header("Breakdown (random stall + hazard flashers)")]
        [Range(0f, 1f)]
        [Tooltip("Chance per spawn that this vehicle breaks down mid-drive: it drifts to the verge, stops and flashes its hazards (TrafficVehicleLights). 0 = never.")]
        public float breakdownChance = 0f;

        [Header("Near-miss highlight (M18)")]
        [SerializeField] private Renderer bodyRenderer;
        [SerializeField] private Color highlightColour = new Color(1f, 0.75f, 0.2f);

        // ---- Runtime state -------------------------------------------------------

        public LaneDirection Direction { get; private set; }
        public DriverPersonality Personality { get; private set; }
        /// <summary>Own driving speed in m/s (world scroll excluded).</summary>
        public float CurrentSpeed { get; private set; }

        // Pass/near-miss bookkeeping, owned by OvertakeDetector, NearMissDetector and
        // PlayerCollisionHandler. Kept as plain fields for zero-allocation iteration.
        [System.NonSerialized] public bool PassStarted;
        [System.NonSerialized] public bool PassPending;
        [System.NonSerialized] public bool PassDone;
        [System.NonSerialized] public bool PassInvalidated;
        /// <summary>True when the pass happened on the legal side (toward the oncoming lane). Set by OvertakeDetector.</summary>
        [System.NonSerialized] public bool PassOnCorrectSide;
        [System.NonSerialized] public bool NearMissDone;
        [System.NonSerialized] public float NearMissPrevDelta;
        [System.NonSerialized] public bool WasHitByPlayer;
        [System.NonSerialized] public ObjectPool<TrafficVehicle> SourcePool;

        /// <summary>Where this vehicle sits on the road, in road space: arc-length along the centreline plus a
        /// lateral offset (road-local +X, the player's right). The spawner stamps these at Activate; Tick advances
        /// RoadArc by the vehicle's own driving and converges RoadLateral onto its lane, then re-derives the world
        /// pose from them every frame via RoadSequencer so the vehicle rides the curve. They survive pooling.
        /// The overtake/near-miss/follow/spacing logic all compares these road-space values, so it stays correct
        /// through a bend (a straight-line world projection would skew once the road leaves the +Z axis).</summary>
        [System.NonSerialized] public float RoadArc;
        [System.NonSerialized] public float RoadLateral;

        private static readonly int EmissionColourId = Shader.PropertyToID("_EmissionColor");
        private static readonly Quaternion OncomingFlip = Quaternion.Euler(0f, 180f, 0f);

        private PersonalitySettings settings;
        private MaterialPropertyBlock propertyBlock;
        private TrafficHorn horn;
        private float baseSpeed;
        private float laneCentre;
        private float driftPhase;
        private float yieldBias = 1f;
        private float effYield, effSwerve;                                   // contrast+temperament-adjusted traits (Activate)
        private float effAccelMult = 1f, effGapMult = 1f, effReactionMult = 1f;
        private float highlightAmount;
        private bool vergeStopArmed;
        private bool vergeStopping;
        private bool breakdownArmed;
        private bool overtaking;          // currently out in the oncoming lane passing a leader
        private float overtakeCooldown;   // gap between overtake attempts (and a longer pause after a bail-out)
        /// <summary>True while broken down — TrafficVehicleLights reads this to flash the hazards.</summary>
        public bool HazardFlashers { get; private set; }
        private float hazardHoldLat;    // the lateral the last hazard dodge settled on
        private float hazardHoldTimer;  // counts down after a hazard passes, easing the car back to its lane

        // Hazard avoidance (M21 fairness): cars ease around hazards in their lane, which also telegraphs them.
        private const float HazardLookahead = 16f;     // first notice the hazard this far ahead and begin easing over
        private const float HazardDodgeFull = 6f;      // be fully alongside-clear by this distance (ease in between the two)
        private const float HazardLaneBand = 1.4f;     // a hazard counts as "in my lane" within this lateral distance
        private const float HazardClearance = 1.7f;    // aim this far to the clear side of the hazard centre
        private const float HazardReleaseHold = 0.5f;  // keep steering wide for this long after passing, then ease back
        private const float CarAvoidGap = 1.8f;        // keep at least this lateral gap from another car
        private const float CarAvoidLongWindow = 6f;   // only cars this close longitudinally can block the dodge
        private const float FollowDistance = 10f;      // start slowing behind a same-direction car within this gap
        private const float MinFollowGap = 4.5f;       // never close nearer than this (about a car length)
        private const float FollowLaneBand = 1.2f;     // a car counts as "ahead in my lane" within this lateral distance
        private const float OvertakeScanAhead = 30f;        // look this far ahead for a leader worth overtaking
        private const float OvertakeOncomingClearArc = 55f; // the oncoming lane must be clear at least this far to commit/continue
        private const float OvertakeSpeedBoost = 1.45f;     // pass at this multiple of base speed

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
            horn = GetComponent<TrafficHorn>();
            // Every MOVING vehicle kicks up road dust (base amount on tarmac, much more on a dirt tile via
            // RoadSurfaceFeel). Self-bootstrap it so ALL traffic dusts without each prefab needing the
            // "Add Dust Trail to Selection" tool run on it. Idempotent: a prefab that already carries one is
            // left alone. Skip permanently-parked wrecks (isStaticObstacle) so they don't trail dust while
            // sitting still. The plume itself stays gated by WeatherConfig.dustEnabled and world speed.
            // ONE dust system (VehicleDust). It replaces the old VehicleDustTrail + VehicleSandKick pair, which
            // drove emission through a CACHED particle module and silently threw "Do not create your own module
            // instances" every frame in this project — so no car dust ever appeared. VehicleDust emits via
            // ParticleSystem.Emit() (the same safe path as TrafficExhaust) and is heavier on a Dirt tile.
            if (!isStaticObstacle && GetComponentInChildren<VehicleDust>(true) == null)
                gameObject.AddComponent<VehicleDust>();
        }

        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);

        /// <summary>Configures a pooled instance at a point on the road (arc-length + lateral). Caller activates
        /// the GameObject afterwards. The world pose is derived here so the vehicle spawns on the curved road.</summary>
        public void Activate(LaneDirection direction, TrafficBehaviourProfile profile, float lateral, float arc)
        {
            Direction = direction;
            this.laneCentre = lateral;
            settings = profile.PickSettings(); // weighted draw over the archetype list (data-driven — any number of archetypes)
            if (settings == null)
            {
                // Empty Personalities list on the profile: fall back to a default archetype so Tick can dereference
                // settings safely every frame (its field defaults ARE the "Normaal" values). Warn once — a full pool
                // would otherwise spam one warning per vehicle per activation.
                settings = new PersonalitySettings();
                if (!_warnedNoPersonalities)
                {
                    _warnedNoPersonalities = true;
                    Debug.LogWarning("TrafficBehaviourProfile has no personalities; using default driver settings. Add at least one archetype to the profile.", profile);
                }
            }
            Personality = settings.personality;
            if (horn != null) horn.eagerness = settings.hornEagerness; // settings is guaranteed non-null past the guard above

            Vector2 range = direction == LaneDirection.SameDirection ? sameDirectionSpeedRange : oncomingSpeedRange;
            float profileMult = direction == LaneDirection.SameDirection ? profile.sameDirectionSpeedMult : profile.oncomingSpeedMult;
            baseSpeed = isStaticObstacle
                ? 0f
                : Random.Range(range.x, range.y) * Random.Range(settings.speedMultiplierRange.x, settings.speedMultiplierRange.y) * profileMult;

            // Same-direction traffic cruises clearly BELOW the player's base (coasting) speed, so the player
            // closes on it and can complete an overtake across the normal speed range — not only near max.
            // Coasting still overtakes (gently); gas overtakes faster; braking lets cars pull ahead. Held this
            // far under base so a pass actually finishes without flooring it, while cars still drift back and
            // despawn. (Previously 0.82-0.97 of base — almost the player's own coasting speed — which made the
            // closing speed near zero at cruise, so overtakes only scored at top speed.)
            if (Direction == LaneDirection.SameDirection && !isStaticObstacle && WorldSpeed.Instance != null)
                baseSpeed = Mathf.Min(baseSpeed, WorldSpeed.Instance.BaseSpeed * Random.Range(0.6f, 0.78f));

            // Per-vehicle yield willingness, so two cars of the same personality still differ.
            yieldBias = Random.Range(0.5f, 1.25f);

            // Effective per-spawn traits: the archetype's authored values, stretched from neutral by the facilitator
            // Contrast dial (1 = as authored; higher = personalities read further apart) and wobbled by a per-spawn
            // temperament so two drivers of the same archetype never behave identically (anti-robotic).
            float contrast = TrafficMood.Contrast;
            float temper = Random.Range(0.85f, 1.15f);
            effYield = Mathf.Min(1.4f, settings.yieldShift * temper * contrast);
            effSwerve = settings.swerveShift * contrast;
            effAccelMult = Mathf.Max(0.2f, Mathf.LerpUnclamped(1f, settings.accelerationMult, contrast) * temper);
            effGapMult = Mathf.Max(0.3f, Mathf.LerpUnclamped(1f, settings.followGapMult, contrast) * temper);
            effReactionMult = Mathf.Max(0.2f, Mathf.LerpUnclamped(1f, settings.reactionMult, contrast));

            CurrentSpeed = baseSpeed;

            driftPhase = Random.value * 100f;
            vergeStopArmed = !isStaticObstacle && direction == LaneDirection.SameDirection && Random.value < vergeStopChance;
            vergeStopping = false;
            breakdownArmed = !isStaticObstacle && direction == LaneDirection.SameDirection && Random.value < breakdownChance * BreakdownBoost;
            overtaking = false;
            overtakeCooldown = Random.Range(0.5f, 2f);
            HazardFlashers = false;
            hazardHoldLat = lateral;
            hazardHoldTimer = 0f;

            PassStarted = PassPending = PassDone = PassInvalidated = false;
            PassOnCorrectSide = false;
            NearMissDone = false;
            NearMissPrevDelta = float.MaxValue;
            WasHitByPlayer = false;
            SetHighlight(0f);

            RoadArc = arc;
            RoadLateral = lateral;
            ApplyRoadPose();
        }

        /// <summary>Per-frame behaviour, called by TrafficSpawner while the world runs. Works entirely in road
        /// space: <paramref name="playerArc"/> is the player's distance along the road centreline (the road's own
        /// odometer) and <paramref name="playerLateral"/> their lateral offset, so "ahead" and lane gaps stay
        /// correct through a bend. The vehicle's world transform is re-derived from RoadArc/RoadLateral at the end.</summary>
        public void Tick(float deltaTime, TrafficConfig config, float playerArc, float playerLateral)
        {
            float ahead = RoadArc - playerArc; // metres of road in front of the player (negative = behind)

            float targetSpeed = baseSpeed;
            float lateralTarget = laneCentre
                + Mathf.Sin((Time.time + driftPhase) * settings.driftFrequency * 2f * Mathf.PI) * settings.driftAmplitude
                - Mathf.Sign(laneCentre) * settings.laneBias; // personality lane position: + sits toward the centre line, - hugs the verge

            if (settings.speedJitter > 0f)
                targetSpeed += Mathf.Sin((Time.time + driftPhase) * 0.7f) * settings.speedJitter;

            bool overtakingNow = false;
            if (Direction == LaneDirection.SameDirection && !isStaticObstacle)
            {
                if (vergeStopArmed && ahead > 0f && ahead < 35f)
                {
                    vergeStopArmed = false;
                    vergeStopping = true;
                }
                // Break down somewhere visibly ahead of the player: drift to the verge, stop, and flash hazards.
                if (breakdownArmed && ahead > 12f && ahead < 60f)
                {
                    breakdownArmed = false;
                    vergeStopping = true;
                    HazardFlashers = true;
                }

                if (vergeStopping)
                {
                    // Passenger stop / breakdown: drift to the verge and halt — a moving vehicle becomes an obstacle.
                    lateralTarget = laneCentre + Mathf.Sign(laneCentre) * 1.2f;
                    targetSpeed = 0f;
                }
                else
                {
                    // Try to overtake a slower car ahead; if not passing, yield gradually as the player closes.
                    overtakingNow = UpdateOvertake(config, deltaTime, ref lateralTarget, ref targetSpeed);
                    if (!overtakingNow && ahead > 0f && ahead < config.yieldDistance)
                    {
                        // Yield GRADUALLY as the player closes from behind: ease toward the verge and off the gas,
                        // proportional to how close the player is and this driver's willingness (aggressive barely
                        // move; cautious make room), with yieldBias so two same-personality cars don't react in sync.
                        float proximity = 1f - Mathf.Clamp01(ahead / config.yieldDistance);
                        float yield = effYield * yieldBias * proximity * TrafficMood.YieldScale; // annoyed roads make less room
                        lateralTarget += Mathf.Sign(laneCentre) * yield;
                        targetSpeed *= Mathf.Lerp(1f, 0.9f, Mathf.Clamp01(yield));
                    }
                }
            }
            else if (Direction == LaneDirection.Oncoming && ahead > 0f && ahead < config.swerveDistance)
            {
                if (Mathf.Abs(RoadLateral - playerLateral) < width * 0.5f + 1.2f)
                    lateralTarget += Mathf.Sign(RoadLateral - playerLateral) * effSwerve * TrafficMood.SwerveScale; // wary oncoming gives a reckless player a wide berth
            }

            // Both lanes share the same hazard-dodge and anti-clip spacing: every moving car eases around
            // the hazards in its own lane and never slides into the car ahead of or beside it. Verge-stoppers
            // and parked obstacles are exempt — they are meant to sit still.
            if (!isStaticObstacle && !vergeStopping)
            {
                lateralTarget = AvoidHazards(RoadArc, lateralTarget, deltaTime);
                if (!overtakingNow)
                    targetSpeed = Mathf.Min(targetSpeed, FollowSpeedCap(RoadArc, RoadLateral)); // don't rear-end the car ahead (a pass must push through)
                lateralTarget = SpaceFromTraffic(RoadArc, RoadLateral, lateralTarget);       // don't steer into a car beside me
            }

            // Per-vehicle base acceleration scaled by the driver's accelerationMult, so a heavy truck lugs up to
            // speed while a boda darts, and an aggressive driver of either changes speed harder than a cautious one.
            CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, Mathf.Max(0f, targetSpeed), acceleration * effAccelMult * deltaTime);

            // Advance the vehicle's own progress along the road (M1). The world scroll is NOT subtracted here:
            // the player's arc grows by the scroll every frame (RoadSequencer), so the player-relative gap
            // (RoadArc - playerArc) still closes at (own speed − scroll) — the same net motion as before, now
            // expressed in the road's frame so it follows the curve. Oncoming drives down-arc, toward the player.
            float signedOwn = Direction == LaneDirection.SameDirection ? CurrentSpeed : -CurrentSpeed;
            RoadArc += signedOwn * deltaTime;
            // Reaction sluggishness: a distracted driver (reactionMult < 1) corrects their lane position slowly, so
            // they wander and are late to settle after a dodge/yield; alert drivers snap back to their line.
            RoadLateral = Mathf.MoveTowards(RoadLateral, lateralTarget, config.laneConvergeRate * effReactionMult * deltaTime);

            // Re-derive the world pose from the road point so the vehicle sits on (and banks with) the curve.
            ApplyRoadPose();

            if (highlightAmount > 0f)
                SetHighlight(highlightAmount - deltaTime * 3f);

            if (horn != null)
            {
                // Live horn mood: the driver's own eagerness scaled by how fed-up the road is with the player.
                horn.eagerness = (settings != null ? settings.hornEagerness : 1f) * TrafficMood.HornScale;
                horn.Tick(ahead, playerLateral - RoadLateral);
            }
        }

        /// <summary>
        /// Places the vehicle's transform from its road point (RoadArc + RoadLateral) via RoadSequencer's
        /// player-anchored curve mapping — the same one that positions the tiles — so it rides the bend. The
        /// heading is the road tangent there (flipped 180° for oncoming). Falls back to the straight +Z line
        /// (player at the origin) when there is no sequencer or the road is not built yet, so the vehicle still
        /// behaves in a bare test scene.
        /// </summary>
        private void ApplyRoadPose()
        {
            if (RoadSequencer.Instance != null
                && RoadSequencer.Instance.TryGetRoadPose(RoadArc, RoadLateral, out Vector3 pos, out Quaternion rot))
            {
                transform.SetPositionAndRotation(pos, Direction == LaneDirection.SameDirection ? rot : rot * OncomingFlip);
                return;
            }

            float ahead = RoadArc - PlayerArc; // the player rides at the world origin, so arc-ahead is world Z
            Vector3 fallback = RoadDirection.Current * ahead + RoadDirection.SteerAxis * RoadLateral;
            Vector3 facing = Direction == LaneDirection.SameDirection ? RoadDirection.Current : -RoadDirection.Current;
            transform.SetPositionAndRotation(fallback, Quaternion.LookRotation(facing));
        }

        /// <summary>The player's progress along the road centreline (metres) — the rewound road odometer when a
        /// sequencer exists, else the world odometer. Shared with the spawner so placement uses one frame.</summary>
        private static float PlayerArc =>
            RoadSequencer.Instance != null ? RoadSequencer.Instance.PlayerArc
            : (WorldSpeed.Instance != null ? WorldSpeed.Instance.DistanceTravelled : 0f);

        /// <summary>
        /// Steers the lateral target around the nearest hazard ahead in this car's own lane — works for both
        /// directions ("ahead" follows the car's heading). The swerve EASES in with proximity (a gentle lean
        /// far out, fully clear by the time it is alongside) and EASES back out over <see cref="HazardReleaseHold"/>
        /// seconds after passing, so it reads as a driver flowing around a pothole, not a snap step. It picks
        /// whichever side of the hazard leaves more room inside the lane, never crossing into oncoming. The dodge
        /// is only taken into clear space: if a car in the same lane sits in the path, it holds its line and rides
        /// over the hazard instead — so this can never push two cars into each other.
        /// </summary>
        private float AvoidHazards(float selfArc, float lateralTarget, float deltaTime)
        {
            float travelDir = Direction == LaneDirection.SameDirection ? 1f : -1f; // "ahead" is along my heading

            Hazard nearest = null;
            float nearestGap = HazardLookahead;
            List<Hazard> hazards = Hazard.Active;
            for (int i = 0; i < hazards.Count; i++)
            {
                Hazard hz = hazards[i];
                if (hz == null) continue;
                // Hazards also live in road space (HazardSpawner stamps RoadArc/RoadLateral), so compare directly —
                // both sides share the road's frame and the dodge stays correct through a bend.
                float hAhead = (hz.RoadArc - selfArc) * travelDir;
                if (hAhead <= 0f || hAhead >= nearestGap) continue;
                if (Mathf.Abs(hz.RoadLateral - lateralTarget) > HazardLaneBand) continue;
                nearest = hz; nearestGap = hAhead;
            }

            if (nearest != null)
            {
                float dodge = LaneDodgeTarget(nearest.RoadLateral);
                float dodgeDir = Mathf.Sign(dodge - lateralTarget);
                if (!DodgeBlockedByTraffic(selfArc, lateralTarget, dodge, dodgeDir))
                {
                    // Ease the swerve in as the hazard nears: a faint lean at HazardLookahead, fully over by HazardDodgeFull.
                    float reach = Mathf.Max(0.01f, HazardLookahead - HazardDodgeFull);
                    float proximity = Mathf.SmoothStep(0f, 1f, 1f - Mathf.Clamp01((nearestGap - HazardDodgeFull) / reach));
                    float target = Mathf.Lerp(lateralTarget, dodge, proximity);
                    hazardHoldLat = target;
                    hazardHoldTimer = HazardReleaseHold;
                    return target;
                }
            }

            // Past the hazard (or boxed out of the dodge): ease back from the line we were holding so the
            // car merges back into lane smoothly instead of snapping straight once the hazard clears.
            if (hazardHoldTimer > 0f)
            {
                hazardHoldTimer -= deltaTime;
                return Mathf.Lerp(lateralTarget, hazardHoldLat, Mathf.Clamp01(hazardHoldTimer / HazardReleaseHold));
            }
            return lateralTarget;
        }

        /// <summary>Lateral the car should aim for to clear a hazard: to whichever side of it leaves more room
        /// inside this car's own lane, kept between the centre buffer and the verge so it never crosses into oncoming.</summary>
        private float LaneDodgeTarget(float hazardLat)
        {
            float laneSide = laneCentre < 0f ? -1f : 1f; // toward my own verge (works for either lane)
            float limit = RoadSideConfig.Active != null ? RoadSideConfig.Active.playerLateralLimit : 4.2f;
            float buffer = RoadSideConfig.Active != null ? RoadSideConfig.Active.centreBuffer : 0.5f;
            float vergeEdge = laneSide * (limit - width * 0.5f);    // outer usable lateral, my side
            float centreEdge = laneSide * (buffer + width * 0.5f);  // inner usable lateral, just shy of the centre line
            float dodge = Mathf.Abs(vergeEdge - hazardLat) >= Mathf.Abs(centreEdge - hazardLat)
                ? hazardLat + laneSide * HazardClearance   // more room toward the verge
                : hazardLat - laneSide * HazardClearance;  // more room toward the centre line (still my lane)
            return Mathf.Clamp(dodge, Mathf.Min(vergeEdge, centreEdge), Mathf.Max(vergeEdge, centreEdge));
        }

        private bool DodgeBlockedByTraffic(float selfArc, float fromLat, float toLat, float dodgeDir)
        {
            for (int i = 0; i < Active.Count; i++)
            {
                TrafficVehicle v = Active[i];
                if (v == this || v.Direction != Direction) continue; // only cars sharing my lane can be in the way
                if (Mathf.Abs(v.RoadArc - selfArc) > CarAvoidLongWindow) continue;
                float vLat = v.RoadLateral;
                if ((vLat - fromLat) * dodgeDir <= 0f) continue;          // car is not on the side I am dodging toward
                if ((vLat - toLat) * dodgeDir < CarAvoidGap) return true; // car sits inside the dodge path
            }
            return false;
        }

        // Car-following: cap my speed behind the nearest car ahead in my own lane, so I never rear-end it.
        // "Ahead" follows my heading, so this holds for oncoming traffic queueing up too.
        private float FollowSpeedCap(float selfArc, float selfLat)
        {
            // Personality scales the gap (aggressive tailgate, cautious hang back), stretched by the contrast dial and
            // squeezed by the road's mood: an annoyed road crowds a reckless player.
            float gapMult = effGapMult * TrafficMood.FollowScale;
            float followDistance = FollowDistance * gapMult;
            float minFollowGap = MinFollowGap * gapMult;

            float travelDir = Direction == LaneDirection.SameDirection ? 1f : -1f;
            float leaderGap = followDistance;
            float leaderSpeed = 0f;
            bool found = false;
            for (int i = 0; i < Active.Count; i++)
            {
                TrafficVehicle v = Active[i];
                if (v == this || v.Direction != Direction) continue;
                float gap = (v.RoadArc - selfArc) * travelDir;
                if (gap <= 0f || gap >= leaderGap) continue;
                if (Mathf.Abs(v.RoadLateral - selfLat) > FollowLaneBand) continue;
                leaderGap = gap; leaderSpeed = v.CurrentSpeed; found = true;
            }
            if (!found)
                return float.MaxValue;
            float t = Mathf.InverseLerp(minFollowGap, followDistance, leaderGap); // 0 at the min gap, 1 at the follow distance
            return Mathf.Lerp(leaderSpeed * 0.8f, leaderSpeed, t);
        }

        // Lateral spacing: clamp the lateral target so I never steer within a car's width of another car in my
        // lane alongside me — so two cars can never slide sideways into each other.
        private float SpaceFromTraffic(float selfArc, float selfLat, float lateralTarget)
        {
            for (int i = 0; i < Active.Count; i++)
            {
                TrafficVehicle v = Active[i];
                if (v == this || v.Direction != Direction) continue;
                if (Mathf.Abs(v.RoadArc - selfArc) > (length + v.length) * 0.5f) continue;
                float vLat = v.RoadLateral;
                float minGap = (width + v.width) * 0.5f + 0.3f;
                if (vLat >= selfLat) lateralTarget = Mathf.Min(lateralTarget, vLat - minGap);
                else                 lateralTarget = Mathf.Max(lateralTarget, vLat + minGap);
            }
            return lateralTarget;
        }

        /// <summary>
        /// Car-to-car overtaking (M13): when held up behind a clearly-slower leader in my lane and the oncoming
        /// lane is clear, commit to a pass — pull across into the oncoming lane and push past — then tuck back once
        /// there is room ahead. Aborts (and pauses) the moment an oncoming vehicle enters the clear window, so it
        /// never sets up an AI head-on. Willingness scales with the driver (aggressive pass readily; cautious and
        /// distracted rarely). Returns true while a pass is active, so Tick skips the follow-speed cap and lets the
        /// car accelerate through. One pass over Active per call (zero allocation), like the other detectors.
        /// </summary>
        private bool UpdateOvertake(TrafficConfig config, float deltaTime, ref float lateralTarget, ref float targetSpeed)
        {
            if (!canOvertake || !OvertakingEnabled)
                return false;
            overtakeCooldown -= deltaTime;

            // Nearest same-lane leader ahead, and whether the oncoming lane is clear in the window I'd use.
            TrafficVehicle leader = null;
            float leaderGap = OvertakeScanAhead;
            bool oncomingClear = true;
            for (int i = 0; i < Active.Count; i++)
            {
                TrafficVehicle v = Active[i];
                if (v == this) continue;
                float gap = v.RoadArc - RoadArc; // + = ahead of me along the road
                if (v.Direction == Direction)
                {
                    if (gap > 0f && gap < leaderGap && Mathf.Abs(v.RoadLateral - laneCentre) < FollowLaneBand)
                    {
                        leader = v;
                        leaderGap = gap;
                    }
                }
                else if (gap > -8f && gap < OvertakeOncomingClearArc)
                {
                    oncomingClear = false; // an oncoming vehicle is inside the stretch I'd need to borrow
                }
            }

            float passLateral = RoadSideConfig.Active != null ? RoadSideConfig.Active.OncomingLaneCentre : -laneCentre;

            if (overtaking)
            {
                // Hold the pass until there is room to merge back; bail immediately if oncoming closes in.
                bool roomToMergeBack = leader == null || leaderGap > MinFollowGap + length;
                if (!oncomingClear || roomToMergeBack)
                {
                    overtaking = false;
                    overtakeCooldown = oncomingClear ? 1.2f : 2.5f; // a longer breather if we had to bail
                    return false;
                }
                lateralTarget = passLateral;
                targetSpeed = baseSpeed * OvertakeSpeedBoost;
                return true;
            }

            // Commit only when genuinely held up by a slower car, the lane is clear, and the cooldown has elapsed.
            if (overtakeCooldown <= 0f && oncomingClear && leader != null
                && leader.CurrentSpeed < baseSpeed * 0.85f && leaderGap < FollowDistance)
            {
                float willingness = overtakeUrgency * settings.reactionMult / Mathf.Max(0.1f, settings.followGapMult);
                if (Random.value < willingness * deltaTime)
                {
                    overtaking = true;
                    lateralTarget = passLateral;
                    targetSpeed = baseSpeed * OvertakeSpeedBoost;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Brief emission pulse on a near miss (M18). MaterialPropertyBlock — no material instancing.</summary>
        public void FlashHighlight() => SetHighlight(1f);

        private void SetHighlight(float amount)
        {
            highlightAmount = Mathf.Clamp01(amount);
            if (bodyRenderer == null)
                return;
            // Lazy-create: Activate can run before Awake on a prefab saved inactive.
            if (propertyBlock == null)
                propertyBlock = new MaterialPropertyBlock();
            bodyRenderer.GetPropertyBlock(propertyBlock); // preserve any per-spawn colour tint set on this renderer via MPB (TrafficVehicleAnimator)
            propertyBlock.SetColor(EmissionColourId, highlightColour * highlightAmount);
            bodyRenderer.SetPropertyBlock(propertyBlock);
        }

        public void CaptureSample(ref RewindSample sample)
        {
            // Record the ROAD point (arc, lateral) rather than the world transform: the world pose is a
            // derived value, and storing road space means the restore stays consistent with the rewinding
            // road (RoadSequencer keeps its anchor synced through the rewind). Rotation is unused — the
            // heading is re-derived from the road tangent in ApplyRoadPose.
            sample.Position = new Vector3(RoadArc, RoadLateral, 0f);
            sample.Rotation = Quaternion.identity;
            sample.Aux = CurrentSpeed;
            sample.Active = gameObject.activeInHierarchy;
        }

        public void ApplySample(in RewindSample sample)
        {
            RoadArc = sample.Position.x;
            RoadLateral = sample.Position.y;
            CurrentSpeed = sample.Aux;
            if (gameObject.activeSelf != sample.Active)
                gameObject.SetActive(sample.Active);
            // Re-derive the world pose on the (rewinding) road, so the car tracks the road back rather than
            // replaying a stale straight-line position. The whole-number frames blend smoothly because RoadArc
            // and RoadLateral interpolate linearly.
            ApplyRoadPose();
        }
    }
}
