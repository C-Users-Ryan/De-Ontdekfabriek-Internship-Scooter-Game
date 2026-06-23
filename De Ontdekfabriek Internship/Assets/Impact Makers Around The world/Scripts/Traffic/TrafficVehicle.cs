using System.Collections.Generic;
using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;
using KenyaScooter.Hazards;
using KenyaScooter.SafetyNet;

namespace KenyaScooter.Traffic
{
    /// <summary>
    /// One pooled traffic vehicle (M9, M12). Movement is the net of its own driving
    /// speed and the world scroll, projected onto RoadDirection axes — axis-agnostic
    /// after turns. Personality (assigned at activation from the active profile)
    /// drives speed, lane drift, yielding and swerving. Vehicles are ticked by
    /// TrafficSpawner rather than running their own Update (manager-tick pattern —
    /// one loop instead of dozens of Update callbacks).
    /// </summary>
    public sealed class TrafficVehicle : MonoBehaviour, IRewindable
    {
        /// <summary>The only lookup path for detectors — no scene scans during play (Req §17).</summary>
        public static readonly List<TrafficVehicle> Active = new List<TrafficVehicle>(32);

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
        [Tooltip("Base points for overtaking this vehicle — matatu < truck < boda swarm (Req §7.1).")]
        public int overtakeScore = 150;
        [Tooltip("Broken-down vehicles (T04) never move.")]
        public bool isStaticObstacle = false;
        [Range(0f, 1f)]
        [Tooltip("Chance this vehicle performs a matatu passenger stop: drifts to the verge and halts without signalling (Req §5.3 M13 event).")]
        public float vergeStopChance = 0f;

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

        private static readonly int EmissionColourId = Shader.PropertyToID("_EmissionColor");

        private PersonalitySettings settings;
        private MaterialPropertyBlock propertyBlock;
        private TrafficHorn horn;
        private float baseSpeed;
        private float laneCentre;
        private float driftPhase;
        private float yieldBias = 1f;
        private float highlightAmount;
        private bool vergeStopArmed;
        private bool vergeStopping;
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

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
            horn = GetComponent<TrafficHorn>();
        }

        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);

        /// <summary>Configures a pooled instance. Caller activates the GameObject afterwards.</summary>
        public void Activate(LaneDirection direction, TrafficBehaviourProfile profile, float laneCentre, Vector3 position)
        {
            Direction = direction;
            this.laneCentre = laneCentre;
            Personality = profile.PickPersonality();
            settings = profile.SettingsFor(Personality);

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

            CurrentSpeed = baseSpeed;

            driftPhase = Random.value * 100f;
            vergeStopArmed = !isStaticObstacle && direction == LaneDirection.SameDirection && Random.value < vergeStopChance;
            vergeStopping = false;
            hazardHoldLat = RoadDirection.Lateral(position);
            hazardHoldTimer = 0f;

            PassStarted = PassPending = PassDone = PassInvalidated = false;
            PassOnCorrectSide = false;
            NearMissDone = false;
            NearMissPrevDelta = float.MaxValue;
            WasHitByPlayer = false;
            SetHighlight(0f);

            transform.position = position;
            Vector3 facing = direction == LaneDirection.SameDirection ? RoadDirection.Current : -RoadDirection.Current;
            transform.rotation = Quaternion.LookRotation(facing);
        }

        /// <summary>Per-frame behaviour, called by TrafficSpawner while the world runs.</summary>
        public void Tick(float deltaTime, TrafficConfig config, Vector3 playerPosition)
        {
            float selfLong = RoadDirection.Longitudinal(transform.position);
            float playerLong = RoadDirection.Longitudinal(playerPosition);
            float ahead = selfLong - playerLong;

            float targetSpeed = baseSpeed;
            float lateralTarget = laneCentre
                + Mathf.Sin((Time.time + driftPhase) * settings.driftFrequency * 2f * Mathf.PI) * settings.driftAmplitude;

            if (settings.speedJitter > 0f)
                targetSpeed += Mathf.Sin((Time.time + driftPhase) * 0.7f) * settings.speedJitter;

            if (Direction == LaneDirection.SameDirection && !isStaticObstacle)
            {
                if (vergeStopArmed && ahead > 0f && ahead < 35f)
                {
                    vergeStopArmed = false;
                    vergeStopping = true;
                }

                if (vergeStopping)
                {
                    // Passenger stop: drift to the verge and halt — a moving vehicle
                    // becomes an obstacle without signalling (Req §5.3).
                    lateralTarget = laneCentre + Mathf.Sign(laneCentre) * 1.2f;
                    targetSpeed = 0f;
                }
                else if (ahead > 0f && ahead < config.yieldDistance)
                {
                    // Yield GRADUALLY as the player closes from behind: ease toward the verge
                    // and off the gas, proportional to how close the player is and this driver's
                    // willingness. Aggressive drivers (low yieldShift) barely move; cautious ones
                    // make room; the per-vehicle yieldBias keeps two same-personality cars from
                    // reacting identically, so it reads as drivers, not a synchronised pattern.
                    float proximity = 1f - Mathf.Clamp01(ahead / config.yieldDistance);
                    float yield = settings.yieldShift * yieldBias * proximity;
                    lateralTarget += Mathf.Sign(laneCentre) * yield;
                    targetSpeed *= Mathf.Lerp(1f, 0.9f, Mathf.Clamp01(yield));
                }
            }
            else if (Direction == LaneDirection.Oncoming && ahead > 0f && ahead < config.swerveDistance)
            {
                float playerLat = RoadDirection.Lateral(playerPosition);
                float selfLat = RoadDirection.Lateral(transform.position);
                if (Mathf.Abs(selfLat - playerLat) < width * 0.5f + 1.2f)
                    lateralTarget += Mathf.Sign(selfLat - playerLat) * settings.swerveShift;
            }

            // Both lanes share the same hazard-dodge and anti-clip spacing: every moving car eases around
            // the hazards in its own lane and never slides into the car ahead of or beside it. Verge-stoppers
            // and parked obstacles are exempt — they are meant to sit still.
            if (!isStaticObstacle && !vergeStopping)
            {
                float selfLat = RoadDirection.Lateral(transform.position);
                lateralTarget = AvoidHazards(selfLong, lateralTarget, deltaTime);
                targetSpeed = Mathf.Min(targetSpeed, FollowSpeedCap(selfLong, selfLat)); // don't rear-end the car ahead
                lateralTarget = SpaceFromTraffic(selfLong, selfLat, lateralTarget);       // don't steer into a car beside me
            }

            CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, Mathf.Max(0f, targetSpeed), 3f * deltaTime);

            // Net longitudinal motion = own driving minus world scroll (M1).
            float signedOwn = Direction == LaneDirection.SameDirection ? CurrentSpeed : -CurrentSpeed;
            float longitudinalMove = (signedOwn - WorldSpeed.Instance.Current) * deltaTime;

            Vector3 position = transform.position;
            float lateral = RoadDirection.Lateral(position);
            float newLateral = Mathf.MoveTowards(lateral, lateralTarget, config.laneConvergeRate * deltaTime);
            position += RoadDirection.Current * longitudinalMove + RoadDirection.SteerAxis * (newLateral - lateral);
            transform.position = position;

            Vector3 facing = Direction == LaneDirection.SameDirection ? RoadDirection.Current : -RoadDirection.Current;
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, Quaternion.LookRotation(facing), config.headingTurnRate * deltaTime);

            if (highlightAmount > 0f)
                SetHighlight(highlightAmount - deltaTime * 3f);

            if (horn != null)
                horn.Tick(ahead, RoadDirection.Lateral(playerPosition) - RoadDirection.Lateral(position));
        }

        /// <summary>
        /// Steers the lateral target around the nearest hazard ahead in this car's own lane — works for both
        /// directions ("ahead" follows the car's heading). The swerve EASES in with proximity (a gentle lean
        /// far out, fully clear by the time it is alongside) and EASES back out over <see cref="HazardReleaseHold"/>
        /// seconds after passing, so it reads as a driver flowing around a pothole, not a snap step. It picks
        /// whichever side of the hazard leaves more room inside the lane, never crossing into oncoming. The dodge
        /// is only taken into clear space: if a car in the same lane sits in the path, it holds its line and rides
        /// over the hazard instead — so this can never push two cars into each other.
        /// </summary>
        private float AvoidHazards(float selfLong, float lateralTarget, float deltaTime)
        {
            float travelDir = Direction == LaneDirection.SameDirection ? 1f : -1f; // "ahead" is along my heading

            Hazard nearest = null;
            float nearestGap = HazardLookahead;
            List<Hazard> hazards = Hazard.Active;
            for (int i = 0; i < hazards.Count; i++)
            {
                Hazard hz = hazards[i];
                if (hz == null) continue;
                float hAhead = (RoadDirection.Longitudinal(hz.transform.position) - selfLong) * travelDir;
                if (hAhead <= 0f || hAhead >= nearestGap) continue;
                if (Mathf.Abs(RoadDirection.Lateral(hz.transform.position) - lateralTarget) > HazardLaneBand) continue;
                nearest = hz; nearestGap = hAhead;
            }

            if (nearest != null)
            {
                float dodge = LaneDodgeTarget(RoadDirection.Lateral(nearest.transform.position));
                float dodgeDir = Mathf.Sign(dodge - lateralTarget);
                if (!DodgeBlockedByTraffic(selfLong, lateralTarget, dodge, dodgeDir))
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

        private bool DodgeBlockedByTraffic(float selfLong, float fromLat, float toLat, float dodgeDir)
        {
            for (int i = 0; i < Active.Count; i++)
            {
                TrafficVehicle v = Active[i];
                if (v == this || v.Direction != Direction) continue; // only cars sharing my lane can be in the way
                if (Mathf.Abs(RoadDirection.Longitudinal(v.transform.position) - selfLong) > CarAvoidLongWindow) continue;
                float vLat = RoadDirection.Lateral(v.transform.position);
                if ((vLat - fromLat) * dodgeDir <= 0f) continue;          // car is not on the side I am dodging toward
                if ((vLat - toLat) * dodgeDir < CarAvoidGap) return true; // car sits inside the dodge path
            }
            return false;
        }

        // Car-following: cap my speed behind the nearest car ahead in my own lane, so I never rear-end it.
        // "Ahead" follows my heading, so this holds for oncoming traffic queueing up too.
        private float FollowSpeedCap(float selfLong, float selfLat)
        {
            float travelDir = Direction == LaneDirection.SameDirection ? 1f : -1f;
            float leaderGap = FollowDistance;
            float leaderSpeed = 0f;
            bool found = false;
            for (int i = 0; i < Active.Count; i++)
            {
                TrafficVehicle v = Active[i];
                if (v == this || v.Direction != Direction) continue;
                float gap = (RoadDirection.Longitudinal(v.transform.position) - selfLong) * travelDir;
                if (gap <= 0f || gap >= leaderGap) continue;
                if (Mathf.Abs(RoadDirection.Lateral(v.transform.position) - selfLat) > FollowLaneBand) continue;
                leaderGap = gap; leaderSpeed = v.CurrentSpeed; found = true;
            }
            if (!found)
                return float.MaxValue;
            float t = Mathf.InverseLerp(MinFollowGap, FollowDistance, leaderGap); // 0 at the min gap, 1 at the follow distance
            return Mathf.Lerp(leaderSpeed * 0.8f, leaderSpeed, t);
        }

        // Lateral spacing: clamp the lateral target so I never steer within a car's width of another car in my
        // lane alongside me — so two cars can never slide sideways into each other.
        private float SpaceFromTraffic(float selfLong, float selfLat, float lateralTarget)
        {
            for (int i = 0; i < Active.Count; i++)
            {
                TrafficVehicle v = Active[i];
                if (v == this || v.Direction != Direction) continue;
                if (Mathf.Abs(RoadDirection.Longitudinal(v.transform.position) - selfLong) > (length + v.length) * 0.5f) continue;
                float vLat = RoadDirection.Lateral(v.transform.position);
                float minGap = (width + v.width) * 0.5f + 0.3f;
                if (vLat >= selfLat) lateralTarget = Mathf.Min(lateralTarget, vLat - minGap);
                else                 lateralTarget = Mathf.Max(lateralTarget, vLat + minGap);
            }
            return lateralTarget;
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
            propertyBlock.SetColor(EmissionColourId, highlightColour * highlightAmount);
            bodyRenderer.SetPropertyBlock(propertyBlock);
        }

        public void CaptureSample(ref RewindSample sample)
        {
            sample.Position = transform.position;
            sample.Rotation = transform.rotation;
            sample.Aux = CurrentSpeed;
            sample.Active = gameObject.activeInHierarchy;
        }

        public void ApplySample(in RewindSample sample)
        {
            // Face the travel direction instead of replaying the recorded rotation: interpolating
            // the captured rotation during reverse playback can swing a car the long way round and
            // read as a 360 / sudden turn-around. Traffic always faces its lane direction anyway.
            Vector3 facing = Direction == LaneDirection.SameDirection ? RoadDirection.Current : -RoadDirection.Current;
            transform.SetPositionAndRotation(sample.Position, Quaternion.LookRotation(facing));
            CurrentSpeed = sample.Aux;
            if (gameObject.activeSelf != sample.Active)
                gameObject.SetActive(sample.Active);
        }
    }
}
