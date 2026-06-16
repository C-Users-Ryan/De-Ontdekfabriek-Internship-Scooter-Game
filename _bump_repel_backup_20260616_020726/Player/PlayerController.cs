using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Controls;
using KenyaScooter.Core;
using KenyaScooter.SafetyNet;

namespace KenyaScooter.Player
{
    /// <summary>
    /// The scooter (M2, M4, M5). The player never translates along the travel axis —
    /// the position is recomposed every physics step from a single lateral offset
    /// projected onto RoadDirection.SteerAxis, which makes "frozen travel axis"
    /// structural rather than a Rigidbody constraint and survives 90° turns with no
    /// axis-swap step (decision logged in D17). Gas/brake feed WorldSpeed; lateral
    /// velocity ramps via MoveTowards so steering has weight (no instant reversal).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PlayerController : MonoBehaviour, IRewindable
    {
        [SerializeField] private ScooterConfig config;
        [Tooltip("Degrees per second the scooter yaws to face a new travel direction after a turn (M3).")]
        [SerializeField] private float turnFaceRate = 300f;

        /// <summary>Signed offset from the road centre along RoadDirection.SteerAxis.</summary>
        public float LateralOffset { get; private set; }
        public float LateralVelocity { get; private set; }

        private Rigidbody body;
        private float baseHeight;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            // Lock the scooter's height to wherever it is placed in the scene, so it never
            // drops below its authored Y when a session starts. (Editor placement wins over
            // ScooterConfig.rideHeight for the vertical position.)
            baseHeight = transform.position.y;
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

        private void OnEnable() => GameEvents.SessionReset += HandleSessionReset;
        private void OnDisable() => GameEvents.SessionReset -= HandleSessionReset;

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
            float next = LateralOffset + LateralVelocity * dt;
            if (next > limit) { next = limit; LateralVelocity = Mathf.Min(LateralVelocity, 0f); }
            else if (next < -limit) { next = -limit; LateralVelocity = Mathf.Max(LateralVelocity, 0f); }
            LateralOffset = next;

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
            transform.SetPositionAndRotation(sample.Position, sample.Rotation);
            body.position = sample.Position;
        }

        private void HandleSessionReset()
        {
            LateralOffset = RoadSideConfig.Active.OwnLaneCentre;
            LateralVelocity = 0f;
            transform.SetPositionAndRotation(ComposePosition(), Quaternion.LookRotation(RoadDirection.Current));
            body.position = transform.position;
        }
    }
}
