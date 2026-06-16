using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Controls;
using KenyaScooter.Core;
using KenyaScooter.SafetyNet;

namespace KenyaScooter.Player
{
    /// <summary>
    /// The scooter (M2, M4, M5). It never actually moves forward — every physics step its position is
    /// rebuilt from a single sideways offset along RoadDirection.SteerAxis, so the "frozen travel axis" is
    /// built into the structure rather than enforced by a Rigidbody, and it survives 90° turns with no
    /// special axis-swap (decision logged in D17). Gas and brake feed WorldSpeed; the sideways velocity
    /// ramps with MoveTowards so steering has weight instead of snapping. A car hit adds a short sideways
    /// knock-back on top, and its height stays wherever it is placed in the scene.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PlayerController : MonoBehaviour, IRewindable
    {
        [SerializeField] private ScooterConfig config;
        [Tooltip("Degrees per second the scooter yaws to face a new travel direction after a turn (M3).")]
        [SerializeField] private float turnFaceRate = 300f;

        [Header("Bump repel (cartoony knock-back on a car/animal hit)")]
        [Tooltip("Sideways shove speed (m/s) away from whatever you hit, for a light/medium bump.")]
        [SerializeField] private float bumpRepelSpeed = 5f;
        [Tooltip("Sideways shove speed for a hard hit.")]
        [SerializeField] private float hardBumpRepelSpeed = 8f;
        [Tooltip("How quickly the shove fades (m/s²). Higher = a snappier, shorter pop.")]
        [SerializeField] private float bumpRepelDecay = 26f;

        /// <summary>Signed offset from the road centre along RoadDirection.SteerAxis.</summary>
        public float LateralOffset { get; private set; }
        public float LateralVelocity { get; private set; }

        /// <summary>The scooter tuning, shared with sibling components such as ScooterLean (M6).</summary>
        public ScooterConfig Config => config;

        private Rigidbody body;
        private float baseHeight;
        private float bumpVelocity;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            // Lock the scooter's height to wherever it is placed in the scene, so it never
            // drops below its authored Y when a session starts. (Editor placement wins over
            // ScooterConfig.rideHeight for the vertical position.)
            baseHeight = transform.position.y;

            // The visual lean is a separate component (M6). Add it automatically if it is not
            // already on the scooter, so the model leans into steering with no manual wiring.
            if (GetComponent<ScooterLean>() == null)
                gameObject.AddComponent<ScooterLean>();
        }

        private void Start()
        {
            if (RewindSystem.Instance != null)
                RewindSystem.Instance.Register(this);

            // Place the scooter on its own driving lane at ride height from the start, so it
            // sits correctly during the Ready state instead of in the middle of the road.
            // HandleSessionReset repeats this at the beginning of every turn.
            if (RoadSideConfig.Active != null)
                LateralOffset = RoadSideConfig.Active.OwnLaneCentre;
            transform.SetPositionAndRotation(ComposePosition(), Quaternion.LookRotation(RoadDirection.Current));
            body.position = transform.position;
        }

        private void OnEnable()
        {
            GameEvents.SessionReset += HandleSessionReset;
            GameEvents.CollisionOccurred += HandleBump;
            RoadDirection.DirectionChanged += HandleDirectionChanged;
        }

        private void OnDisable()
        {
            GameEvents.SessionReset -= HandleSessionReset;
            GameEvents.CollisionOccurred -= HandleBump;
            RoadDirection.DirectionChanged -= HandleDirectionChanged;
        }

        private void Update()
        {
            GameState state = GameManager.State;
            if (state != GameState.Playing && state != GameState.AtCheckpoint)
                return;

            ScooterInputRouter input = ScooterInputRouter.Instance;
            float dt = Time.deltaTime;

            // Throttle still runs at the checkpoint — WorldSpeed's override pipeline
            // takes over braking there without breaking this call path (Req §3.2).
            WorldSpeed.Instance.ApplyThrottle(input.Gas, input.Brake, dt);

            float targetVelocity = input.Lateral * config.maxLateralSpeed;
            LateralVelocity = Mathf.MoveTowards(LateralVelocity, targetVelocity, config.lateralAcceleration * dt);

            float limit = RoadSideConfig.Active.playerLateralLimit;
            float next = LateralOffset + (LateralVelocity + bumpVelocity) * dt;
            if (next > limit) { next = limit; LateralVelocity = Mathf.Min(LateralVelocity, 0f); bumpVelocity = Mathf.Min(bumpVelocity, 0f); }
            else if (next < -limit) { next = -limit; LateralVelocity = Mathf.Max(LateralVelocity, 0f); bumpVelocity = Mathf.Max(bumpVelocity, 0f); }
            LateralOffset = next;
            bumpVelocity = Mathf.MoveTowards(bumpVelocity, 0f, bumpRepelDecay * dt);

            // Face the travel axis; after a turn this sweeps round while the camera rig
            // does its own 0.35 s rotation (M3).
            Quaternion face = Quaternion.LookRotation(RoadDirection.Current);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, face, turnFaceRate * dt);
        }

        private void FixedUpdate()
        {
            body.MovePosition(ComposePosition());
        }

        private Vector3 ComposePosition()
        {
            return RoadDirection.SteerAxis * LateralOffset + Vector3.up * baseHeight;
        }

        public void CaptureSample(ref RewindSample sample)
        {
            sample.Position = body.position;
            sample.Rotation = transform.rotation;
            sample.Aux = LateralVelocity;
            sample.Active = true;
        }

        public void ApplySample(in RewindSample sample)
        {
            LateralOffset = RoadDirection.Lateral(sample.Position);
            LateralVelocity = sample.Aux;
            bumpVelocity = 0f;
            transform.SetPositionAndRotation(sample.Position, sample.Rotation);
            body.position = sample.Position;
        }

        /// <summary>
        /// Cartoony repel on contact: shove the scooter sideways away from whatever it hit,
        /// so a collision reads as a bump rather than a clean phase-through. The shove decays
        /// in Update; the lateral clamp and the collision cooldown keep it from flinging the
        /// player into oncoming traffic or re-triggering on the same car.
        /// </summary>
        private void HandleBump(CollisionSeverity severity, float relativeKmh, Vector3 contactPosition, bool absorbed)
        {
            float contactLateral = RoadDirection.Lateral(contactPosition);
            float awaySign = LateralOffset >= contactLateral ? 1f : -1f;
            bumpVelocity = awaySign * (severity == CollisionSeverity.Hard ? hardBumpRepelSpeed : bumpRepelSpeed);
        }

        /// <summary>
        /// Snap back to the lane centre when the road turns (M3). LateralOffset is a raw distance
        /// along the steer axis, and that axis flips at a turn, so without re-centring the player
        /// keeps an offset that now points at the kerb (or clean off the new road). Position is
        /// rebuilt from this in the next FixedUpdate; the rig sweeps the heading round on its own.
        /// </summary>
        private void HandleDirectionChanged(Vector3 previous, Vector3 next)
        {
            if (RoadSideConfig.Active != null)
                LateralOffset = RoadSideConfig.Active.OwnLaneCentre;
            LateralVelocity = 0f;
            bumpVelocity = 0f;
        }

        private void HandleSessionReset()
        {
            LateralOffset = RoadSideConfig.Active.OwnLaneCentre;
            LateralVelocity = 0f;
            bumpVelocity = 0f;
            transform.SetPositionAndRotation(ComposePosition(), Quaternion.LookRotation(RoadDirection.Current));
            body.position = transform.position;
        }
    }
}
