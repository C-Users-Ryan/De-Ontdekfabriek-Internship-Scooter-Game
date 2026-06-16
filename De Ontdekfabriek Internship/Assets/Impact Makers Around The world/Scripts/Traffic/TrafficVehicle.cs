using System.Collections.Generic;
using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;
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

            // Same-direction traffic sits just under the player's base (coasting) speed, so coasting keeps
            // pace, gas pulls ahead to overtake, and braking drops you back. Kept below base (not equal) so
            // cars still drift backwards and despawn — a car that matched exactly would never recycle.
            if (Direction == LaneDirection.SameDirection && !isStaticObstacle && WorldSpeed.Instance != null)
                baseSpeed = Mathf.Min(baseSpeed, WorldSpeed.Instance.BaseSpeed * Random.Range(0.82f, 0.97f));

            // Per-vehicle yield willingness, so two cars of the same personality still differ.
            yieldBias = Random.Range(0.5f, 1.25f);

            CurrentSpeed = baseSpeed;

            driftPhase = Random.value * 100f;
            vergeStopArmed = !isStaticObstacle && direction == LaneDirection.SameDirection && Random.value < vergeStopChance;
            vergeStopping = false;

            PassStarted = PassPending = PassDone = PassInvalidated = false;
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
