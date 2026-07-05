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
        [Tooltip("Hard cap on the horizon roll. Kept modest — a big roll disorients some players; clamped at runtime too.")]
        [SerializeField] private float maxRoll = 15f;

        /// <summary>PlayerPrefs key the facilitator "Meekantelen (Real Rider)" setting writes; read on Awake so the
        /// choice (e.g. off for a motion-sensitive group) applies on startup with no scene wiring.</summary>
        public const string PrefKey = "ksg.realRider";

        public float CurrentRoll { get; private set; }

        public bool RealRiderEnabled
        {
            get => realRiderEnabled;
            set => realRiderEnabled = value;
        }

        private void Awake()
        {
            realRiderEnabled = PlayerPrefs.GetInt(PrefKey, realRiderEnabled ? 1 : 0) == 1;
            maxRoll = Mathf.Min(maxRoll, 15f); // cap even if an older scene serialized a bigger, disorienting value
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
