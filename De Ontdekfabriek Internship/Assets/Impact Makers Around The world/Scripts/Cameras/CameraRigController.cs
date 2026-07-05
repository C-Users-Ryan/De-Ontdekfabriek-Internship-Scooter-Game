using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;
using KenyaScooter.Player;

namespace KenyaScooter.Cameras
{
    /// <summary>
    /// The only script that moves the camera rig, and it does it in LateUpdate (architecture rule,
    /// Req §2). Each frame it combines three things: the rig's yaw following RoadDirection.Current with
    /// the 0.35 s turn sweep (M3), the shake offset from CameraShake, and the horizon roll from
    /// RealRiderMode (M7). Those two only calculate values — they never touch a transform themselves.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class CameraRigController : MonoBehaviour
    {
        [Tooltip("The camera child of this rig. Its authored local pose is the base the offsets are applied to.")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private CameraShake shake;
        [SerializeField] private RealRiderMode realRider;
        [Tooltip("Seconds a 90° road turn takes on screen (M3).")]
        [SerializeField] private float turnSmoothSeconds = 0.35f;

        [Header("Speed feel (FOV)")]
        [Tooltip("How much the field of view widens with speed — the main visual cue that you are accelerating or braking. Accelerating widens it, braking narrows it. 0 disables it.")]
        [SerializeField] private float fovSpeedGain = 27f;
        [Tooltip("How quickly the FOV chases its target.")]
        [SerializeField] private float fovLerpRate = 5f;


        [Header("Curve feel (M3) — the road bends around the player, so lean the view into it")]
        // The gains are per deg/s of road bend, and a real bend's rate is small: a 90° tile over 200 m at 10-20 m/s
        // is only ~4.5-9 deg/s. The old 0.18/0.16 gains turned that into <2° of yaw/bank — invisible. These reach
        // most of the cap on a cruise-speed bend so a turn actually reads; the clamps stop a fast sharp bend overdoing it.
        [Tooltip("Degrees the rig yaws into the turn per deg/s of road bend (a slight look-ahead). 0 disables. Flip the sign if it leans the wrong way.")]
        [SerializeField] private float curveYawLeadGain = 0.8f;
        [Tooltip("Most the rig will yaw into a turn.")]
        [SerializeField] private float maxCurveYaw = 7f;
        [Tooltip("Degrees the camera banks (rolls) into the turn per deg/s of road bend. 0 disables.")]
        [SerializeField] private float curveBankGain = 0.8f;
        [Tooltip("Most the camera will bank into a turn.")]
        [SerializeField] private float maxCurveBank = 6f;
        [Tooltip("How quickly the curve yaw/bank ease in and out.")]
        [SerializeField] private float curveFeelLerp = 4f;

        [Header("Ride WITH the bike — the camera copies the bike's RENDERED sideways position 1:1")]
        // No follow fraction and no catch-up smoothing here, deliberately (2026-07-05). The bike's body moves
        // in FixedUpdate and renders through Rigidbody interpolation, so its LOGICAL lateral is always a beat
        // ahead of what is on screen. Any camera that chases the logical value — at any lerp rate — therefore
        // lags AND flutters against the physics tick, which reads as the bike jittering / "being pulled back"
        // while steering. The camera instead copies the bike transform's interpolated (rendered) lateral every
        // LateUpdate: one source of truth, zero lag, nothing to beat against. The pleasant steering ease
        // already lives in the bike's own lateral acceleration.
        [Tooltip("The scooter to follow sideways. Auto-found from the scene on Awake if left empty.")]
        [SerializeField] private PlayerController player;

        [Header("Charge-bay follow — while the relay parks the bike, the view rides the bike exactly")]
        [Tooltip("How quickly the view locks onto / releases from the bike around the charge-bay park and " +
                 "pull-out. While the bike is in the scripted bay pose the camera copies its FULL sideways " +
                 "offset and its yaw, so the view is always where the player actually is.")]
        [SerializeField] private float scriptedFollowLerp = 5f;

        private Vector3 baseLocalPosition;
        private Quaternion baseLocalRotation;
        private float currentYaw;
        private float curveYaw;
        private float curveBank;
        private Camera cam;
        private float baseFov;
        private Vector3 baseRigPosition;
        private float scriptedBlend; // 0 = normal rig yaw, 1 = the parked / pulling-out bike's own yaw

        private void Awake()
        {
            baseLocalPosition = cameraTransform.localPosition;
            baseLocalRotation = cameraTransform.localRotation;
            baseRigPosition = transform.position; // the authored rig position; the lateral follow slides out from here
            if (player == null)
                player = FindObjectOfType<PlayerController>();
            currentYaw = TargetYaw();
            cam = cameraTransform != null ? cameraTransform.GetComponent<Camera>() : null;
            if (cam != null)
                baseFov = cam.fieldOfView; // the authored FOV is "coasting"; speed widens out from here
            // Keep the speed FOV gentle: a big widening shrinks the first-person handlebars toward the centre
            // and blows the view wide at speed. Cap the gain so the framing stays close to the coasting look.
            fovSpeedGain = Mathf.Min(fovSpeedGain, 12f);
        }

        private void OnEnable() => GameEvents.SessionReset += SnapToTravelDirection;
        private void OnDisable() => GameEvents.SessionReset -= SnapToTravelDirection;

        private void LateUpdate()
        {
            float dt = Time.deltaTime;

            // Lean the view into the bend. The travel frame is constant now (the road bends around the
            // player), so this is purely a camera flourish that makes a curve read on screen: a small yaw
            // look-ahead plus a banked roll, both eased in and out from the road's live bend rate (M3).
            float curveRate = RoadDirection.CurveRate; // signed deg/s, + bends right
            float follow = 1f - Mathf.Exp(-curveFeelLerp * dt);
            curveYaw = Mathf.Lerp(curveYaw, Mathf.Clamp(curveRate * curveYawLeadGain, -maxCurveYaw, maxCurveYaw), follow);
            curveBank = Mathf.Lerp(curveBank, Mathf.Clamp(curveRate * curveBankGain, -maxCurveBank, maxCurveBank), follow);

            float turnRate = 90f / Mathf.Max(0.05f, turnSmoothSeconds);
            currentYaw = Mathf.MoveTowardsAngle(currentYaw, TargetYaw() + curveYaw, turnRate * dt);

            // While the charge-station relay owns the bike (parked at the bay / pulling out), weld the view
            // to the bike: its own yaw and (below) its full sideways offset. Without this the camera keeps
            // looking straight down the road while the bike sits yawed at the shoulder — the view visibly
            // tears off the model even though bike + hitbox are together (reported 2026-07-05).
            bool scriptedNow = player != null && player.Scripted;
            scriptedBlend = Mathf.Lerp(scriptedBlend, scriptedNow ? 1f : 0f, 1f - Mathf.Exp(-scriptedFollowLerp * dt));

            float rigYaw = currentYaw;
            if (scriptedBlend > 0.001f && player != null)
                rigYaw = Mathf.LerpAngle(currentYaw, player.transform.eulerAngles.y, scriptedBlend);
            transform.rotation = Quaternion.Euler(0f, rigYaw, 0f);

            // Ride WITH the bike: the rig copies the bike transform's RENDERED sideways position 1:1 every
            // frame. The transform holds the Rigidbody-interpolated pose in LateUpdate — the exact thing on
            // screen — so view and bike share one source and can never lag, lead or beat against the physics
            // tick (chasing the logical lateral with a lerp did exactly that: jitter + "pulled back" while
            // steering, 2026-07-05). This also covers the charge-bay park/pull-out for free: wherever the
            // bike is, the view is.
            float bikeLateral = player != null ? RoadDirection.Lateral(player.transform.position) : 0f;
            transform.position = RigBaseWithoutLateral() + RoadDirection.SteerAxis * bikeLateral;

            Vector3 shakeOffset = shake != null ? shake.CurrentOffset : Vector3.zero;
            float roll = (realRider != null ? realRider.CurrentRoll : 0f) - curveBank;

            cameraTransform.localPosition = baseLocalPosition + shakeOffset;
            cameraTransform.localRotation = baseLocalRotation * Quaternion.Euler(0f, 0f, roll);

            DriveSpeedFov();
        }

        /// <summary>
        /// Widens the field of view as the player speeds up and narrows it when braking, measured against
        /// the base (coasting) speed. This is the main visual cue that the speed is changing — without it,
        /// accelerating and braking barely read on screen.
        /// </summary>
        private void DriveSpeedFov()
        {
            if (cam == null || fovSpeedGain == 0f || WorldSpeed.Instance == null)
                return;

            float maxSpeed = WorldSpeed.Instance.MaxSpeed;
            float baseRatio = maxSpeed > 0f ? WorldSpeed.Instance.BaseSpeed / maxSpeed : 0.33f;
            float targetFov = baseFov + (WorldSpeed.Instance.SpeedRatio - baseRatio) * fovSpeedGain;
            targetFov = Mathf.Clamp(targetFov, baseFov - 6f, baseFov + 8f);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, 1f - Mathf.Exp(-fovLerpRate * Time.deltaTime));
        }

        private static float TargetYaw()
        {
            Vector3 travel = RoadDirection.Current;
            return Mathf.Atan2(travel.x, travel.z) * Mathf.Rad2Deg;
        }

        private void SnapToTravelDirection()
        {
            currentYaw = TargetYaw();

            // If the bike sits in the scripted bay pose across this reset, snap the yaw weld too — the very
            // first visible frame already looks along the parked bike instead of easing onto it.
            scriptedBlend = (player != null && player.Scripted) ? 1f : 0f;
            float yaw = scriptedBlend > 0f && player != null ? player.transform.eulerAngles.y : currentYaw;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            // Position needs no special casing: it is the bike's rendered lateral, same as every frame.
            float bikeLateral = player != null ? RoadDirection.Lateral(player.transform.position) : 0f;
            transform.position = RigBaseWithoutLateral() + RoadDirection.SteerAxis * bikeLateral;
        }

        /// <summary>The rig's authored position with its own sideways component removed — the lateral is
        /// re-supplied every frame straight from the bike, so the view rides the bike whichever side of the
        /// road it drives on.</summary>
        private Vector3 RigBaseWithoutLateral()
        {
            return baseRigPosition - RoadDirection.SteerAxis * Vector3.Dot(baseRigPosition, RoadDirection.SteerAxis);
        }
    }
}
