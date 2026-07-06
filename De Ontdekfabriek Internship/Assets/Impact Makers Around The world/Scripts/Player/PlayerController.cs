using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Controls;
using KenyaScooter.Core;
using KenyaScooter.FX;
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

        // Scripted-pose mode: the charge-station sequence takes the bike over to pull it into / out of the bay,
        // ignoring player input. Purely additive; when off the controller behaves exactly as before.
        private bool scripted;
        private float scriptedLateralTarget;
        private float scriptedYawDeg;
        private float scriptedLateralRate;
        private float scriptedYawRate;

        /// <summary>True while the charge-station sequence is driving the pose.</summary>
        public bool Scripted => scripted;
        /// <summary>Current signed lateral offset, so the sequence can ease from wherever the bike actually is.</summary>
        public float CurrentLateral => LateralOffset;
        /// <summary>True once a scripted pose has essentially settled (lateral within a few cm of its target).</summary>
        public bool ScriptedPoseReached => scripted && Mathf.Abs(LateralOffset - scriptedLateralTarget) < 0.04f;

        /// <summary>Hand the bike to the charge-station sequence: ease the lateral offset and a yaw (degrees off the
        /// travel heading, around up) toward a parked pose, at the given rates (m/s and deg/s). Input is ignored
        /// until <see cref="EndScriptedPose"/>.</summary>
        public void BeginScriptedPose(float lateralTarget, float yawDegrees, float lateralRate, float yawRate)
        {
            scripted = true;
            scriptedLateralTarget = lateralTarget;
            scriptedYawDeg = yawDegrees;
            scriptedLateralRate = Mathf.Max(0.01f, lateralRate);
            scriptedYawRate = Mathf.Max(1f, yawRate);
        }

        /// <summary>Return control to the player (end of the pull-out).</summary>
        public void EndScriptedPose() => scripted = false;

        /// <summary>Instantly place the bike in a scripted pose and hold it. Used to start a session already
        /// parked at the charge bay: in the Ready state Update does not run, so the pose cannot ease in — it has
        /// to simply BE there. Marks the pose scripted, so the session reset keeps it (same as the relay park).</summary>
        public void SnapScriptedPose(float lateralTarget, float yawDegrees)
        {
            BeginScriptedPose(lateralTarget, yawDegrees, 999f, 999f);
            LateralOffset = lateralTarget;
            LateralVelocity = 0f;
            bumpVelocity = 0f;
            transform.SetPositionAndRotation(
                ComposePosition(),
                Quaternion.LookRotation(RoadDirection.Current) * Quaternion.AngleAxis(yawDegrees, Vector3.up));
            body.position = transform.position;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            // Lock the scooter's height to wherever it is placed in the scene, so it never
            // drops below its authored Y when a session starts. (Editor placement wins over
            // ScooterConfig.rideHeight for the vertical position.)
            baseHeight = transform.position.y;

            // The bike model holder, the scooter model itself and the camera rig must all sit on the player's
            // lateral centre line: the collider, the camera and every lane/hazard system live on the root, so ANY
            // sideways offset in that holder chain makes the visible bike ride beside where the player actually is.
            // A stray editor drag keeps leaving the model shoved sideways — x = -3.554 on the holder in one scene,
            // and x = -3.53 on the model prefab NESTED inside "visual layer" in another (2026-07-06). The old guard
            // only checked the root's DIRECT children, so the nested drag slipped straight through and the tear kept
            // coming back. Snap the lateral X of the whole holder chain (children AND grandchildren) at boot so no
            // saved scene can bring it back — but stop before the model's own parts (wheels, mirrors, exhaust),
            // whose left/right positions are meant to be off-centre. Height/forward grounding offsets are untouched.
            SnapLateralChain(transform, 2);

            // SnapLateralChain only zeroes the LOCAL X of the holder transforms. It cannot see an offset
            // baked DEEPER — inside the model prefab, or into the FBX/mesh itself — so if the bike mesh is
            // authored off-centre the holders read x=0 yet the bike still RENDERS beside the player, and the
            // tear survives every play. That was the real cause of "aligned in the scene, breaks on Play"
            // (2026-07-06): the camera follows the ROOT's world X (RoadDirection.Lateral == worldPosition.x)
            // while the model's rendered centre sat ~3.5 m to the side, baked into the Planeta Sport prefab.
            // This measures the model's ACTUAL rendered centre and slides its holder so that centre sits on
            // the player's world X — wherever the offset is baked. It runs every boot, so no saved scene or
            // re-imported prefab can bring the tear back. Only X moves; height/forward grounding is untouched.
            CentreModelOnRoot();

            // The visual lean is a separate component (M6). Add it automatically if it is not
            // already on the scooter, so the model leans into steering with no manual wiring.
            // Added AFTER the centring above, so ScooterLean caches the model's corrected position.
            if (GetComponent<ScooterLean>() == null)
                gameObject.AddComponent<ScooterLean>();

            // The dirt-road shake source (2026-07-05) is auto-added the same way; ScooterLean composes its
            // roll + bob onto the model. It idles at zero until a tile's Surface is set to Dirt. It is kept
            // deliberately faint now — the dirt road is COMMUNICATED mainly by the dust below, not by shaking.
            if (GetComponent<DirtRumble>() == null)
                gameObject.AddComponent<DirtRumble>();

            // The player's own murram dust — the primary "this is a dirt road" cue. A world-space plume off the
            // rear wheel that blooms on a Dirt tile and stays clean on tarmac, matching the dust the traffic
            // kicks up. Auto-added so it needs no scene wiring; it self-gates on surface + speed + the dust toggle.
            if (GetComponent<ScooterDirtDust>() == null)
                gameObject.AddComponent<ScooterDirtDust>();
        }

        /// <summary>Zeroes the local X of the player's structural holder transforms — its children and their
        /// children (the model holder, the scooter model root, the camera rig and the camera) — so an accidental
        /// sideways drag anywhere in that chain can never leave the visible bike riding beside the player. It
        /// recurses only to <paramref name="maxDepth"/> levels, so it never reaches the model's own mesh parts,
        /// whose left/right positions (the two wheels, mirrors, exhaust) are legitimately off-centre.</summary>
        private static void SnapLateralChain(Transform parent, int maxDepth)
        {
            if (maxDepth <= 0)
                return;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (Mathf.Abs(child.localPosition.x) > 0.001f)
                {
                    Debug.LogWarning(
                        $"[PlayerController] '{child.name}' sat {child.localPosition.x:0.###} m sideways off the " +
                        "player centre line — snapped to 0 so the bike rides where the player actually is.", child);
                    Vector3 p = child.localPosition;
                    p.x = 0f;
                    child.localPosition = p;
                }
                SnapLateralChain(child, maxDepth - 1);
            }
        }

        /// <summary>Slides the visible scooter model sideways so its RENDERED centre sits on the player's
        /// world X — the line the collider, the camera and every lane system ride on. Unlike SnapLateralChain
        /// (which only zeroes holder LOCAL X), this reads the model's real renderer bounds, so it corrects an
        /// offset no matter where it is baked: a dragged holder, the model prefab, or the FBX/mesh pivot. The
        /// whole model subtree moves as one, so anything parented to it (e.g. the headlight) keeps its place on
        /// the bike. Only X is changed; the authored height and forward grounding are preserved.</summary>
        private void CentreModelOnRoot()
        {
            Transform holder = FindModelHolder();
            if (holder == null)
                return;

            Renderer[] rends = holder.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0)
                return;

            Bounds bounds = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++)
                bounds.Encapsulate(rends[i].bounds);

            float offsetX = bounds.center.x - transform.position.x;
            if (Mathf.Abs(offsetX) < 0.001f)
                return;

            Debug.LogWarning(
                $"[PlayerController] The scooter model rendered {offsetX:0.###} m off the player centre line " +
                "(a stray drag, or a lateral offset baked into the model prefab/FBX). Sliding its holder " +
                $"'{holder.name}' so the bike rides where the player — and the camera — actually are.", holder);

            Vector3 worldPos = holder.position;
            worldPos.x -= offsetX;
            holder.position = worldPos;
        }

        /// <summary>The visible-model holder: the first child that carries an actual mesh (Mesh or
        /// SkinnedMesh renderer). Never the camera rig, whose only renderer is the speed-line particles.</summary>
        private Transform FindModelHolder()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child.GetComponentInChildren<MeshRenderer>() != null
                    || child.GetComponentInChildren<SkinnedMeshRenderer>() != null)
                    return child;
            }
            return null;
        }

        private void Start()
        {
            if (RewindSystem.Instance != null)
                RewindSystem.Instance.Register(this);

            // If the charge-station relay already parked the bike at the bay (its Start runs TryParkAtReady, which
            // snaps a scripted pose in the Ready state), that park OWNS the pose — do not overwrite it with the lane
            // placement below. Same guard as at the top of HandleSessionReset: while scripted, leave the pose alone.
            // (Start order between this and ChargeStationSequence is undefined, so this must hold whichever ran first.)
            if (scripted)
                return;

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
        }

        private void OnDisable()
        {
            GameEvents.SessionReset -= HandleSessionReset;
            GameEvents.CollisionOccurred -= HandleBump;
        }

        private void Update()
        {
            GameState state = GameManager.State;
            if (state != GameState.Playing && state != GameState.AtCheckpoint)
                return;

            float dt = Time.deltaTime;
            if (scripted) { DriveScripted(dt); return; }

            ScooterInputRouter input = ScooterInputRouter.Instance;

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

        // The charge-station sequence owns the pose here. Keep the WorldSpeed override pipeline ticking (the
        // sequence sets the target: 0 while parked/charging, cruise while pulling out) but ignore the player's
        // input, and ease the bike toward the parked/centred pose and its yaw.
        private void DriveScripted(float dt)
        {
            WorldSpeed.Instance.ApplyThrottle(0f, 0f, dt);
            LateralOffset = Mathf.MoveTowards(LateralOffset, scriptedLateralTarget, scriptedLateralRate * dt);
            LateralVelocity = 0f;
            bumpVelocity = 0f;
            Quaternion target = Quaternion.LookRotation(RoadDirection.Current) * Quaternion.AngleAxis(scriptedYawDeg, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, scriptedYawRate * dt);
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

        private void HandleSessionReset()
        {
            // A charge-station pull-out keeps the bike at the bay across the session reset (the sequence eases it
            // back to the lane), so do not snap it to centre here while scripted.
            if (scripted)
                return;
            LateralOffset = RoadSideConfig.Active.OwnLaneCentre;
            LateralVelocity = 0f;
            bumpVelocity = 0f;
            transform.SetPositionAndRotation(ComposePosition(), Quaternion.LookRotation(RoadDirection.Current));
            body.position = transform.position;
        }
    }
}
