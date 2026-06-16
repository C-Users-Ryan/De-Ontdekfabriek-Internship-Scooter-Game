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

        private Vector3 baseLocalPosition;
        private Quaternion baseLocalRotation;
        private float currentYaw;
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
            float turnRate = 90f / Mathf.Max(0.05f, turnSmoothSeconds);
            currentYaw = Mathf.MoveTowardsAngle(currentYaw, TargetYaw(), turnRate * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0f, currentYaw, 0f);

            Vector3 shakeOffset = shake != null ? shake.CurrentOffset : Vector3.zero;
            float roll = realRider != null ? realRider.CurrentRoll : 0f;

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
