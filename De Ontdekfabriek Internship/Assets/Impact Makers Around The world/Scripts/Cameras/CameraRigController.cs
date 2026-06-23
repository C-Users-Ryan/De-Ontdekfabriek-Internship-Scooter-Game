using UnityEngine;
using KenyaScooter.Core;

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

        private Vector3 baseLocalPosition;
        private Quaternion baseLocalRotation;
        private float currentYaw;
        private float curveYaw;
        private float curveBank;
        private Camera cam;
        private float baseFov;

        private void Awake()
        {
            baseLocalPosition = cameraTransform.localPosition;
            baseLocalRotation = cameraTransform.localRotation;
            currentYaw = TargetYaw();
            cam = cameraTransform != null ? cameraTransform.GetComponent<Camera>() : null;
            if (cam != null)
                baseFov = cam.fieldOfView; // the authored FOV is "coasting"; speed widens out from here
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
            transform.rotation = Quaternion.Euler(0f, currentYaw, 0f);

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
            targetFov = Mathf.Clamp(targetFov, baseFov - 12f, baseFov + 24f);
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
            transform.rotation = Quaternion.Euler(0f, currentYaw, 0f);
        }
    }
}
