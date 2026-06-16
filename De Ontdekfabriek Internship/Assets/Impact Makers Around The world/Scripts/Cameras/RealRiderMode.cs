using UnityEngine;
using KenyaScooter.Controls;

namespace KenyaScooter.Cameras
{
    /// <summary>
    /// Real Rider Mode (M7): the camera rolls the opposite way to the tablet's tilt, so the horizon
    /// stays level on screen while the player physically leans the tablet — your body leans, the road
    /// doesn't. On desktop the tilt is faked from steering input so you can preview it during PC testing
    /// (the R key toggles it in editor/standalone builds). It only computes CurrentRoll; CameraRigController
    /// applies it.
    /// </summary>
    public sealed class RealRiderMode : MonoBehaviour
    {
        [SerializeField] private bool realRiderEnabled = true;
        [Tooltip("Degrees per second the roll follows the tilt.")]
        [SerializeField] private float rollResponse = 160f;
        [SerializeField] private float maxRoll = 30f;

        public float CurrentRoll { get; private set; }

        public bool RealRiderEnabled
        {
            get => realRiderEnabled;
            set => realRiderEnabled = value;
        }

        private void Update()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
                realRiderEnabled = !realRiderEnabled;
#endif
            // Router already scales gyro tilt by motionSensitivity (Req §16).
            float target = realRiderEnabled
                ? Mathf.Clamp(-ScooterInputRouter.Instance.TiltDegrees, -maxRoll, maxRoll)
                : 0f;

            CurrentRoll = Mathf.MoveTowardsAngle(CurrentRoll, target, rollResponse * Time.deltaTime);
        }
    }
}
