using UnityEngine;
using System.Collections;
#if UNITY_IOS
using System.Runtime.InteropServices;
#endif

namespace OvertakeGame
{
    /// <summary>
    /// Manages haptic (vibration) feedback for the iPad.
    ///
    /// Triggers:
    ///   - Continuous rumble while the player is off-road (on the shoulder/dirt)
    ///   - Single sharp pulse on pothole hit
    ///   - Single sharp pulse on traffic collision
    ///   - Medium pulse on speed bump
    ///
    /// iOS uses the Taptic Engine via UIImpactFeedbackGenerator for nuanced
    /// haptic patterns. Android falls back to Handheld.Vibrate().
    /// In the Unity Editor / PC, vibration calls are logged to the Console
    /// so you can verify they fire correctly without a device.
    ///
    /// SETUP:
    ///   1. Attach to any persistent GameObject (e.g. player root or Managers).
    ///   2. Wire references in the Inspector.
    ///   3. The WrongLaneDetector is reused to detect off-road — if the player
    ///      is on the shoulder (past road edge), offroad rumble fires.
    ///      Wire a separate OffRoadDetector if you want a different boundary.
    ///   4. Call the public methods from collision/pothole/bump events.
    ///
    /// TOGGLES:
    ///   All haptic types are individually toggleable from the Inspector.
    /// </summary>
    public class HapticFeedback : MonoBehaviour
    {
        public static HapticFeedback Instance { get; private set; }

        // ── Toggles ───────────────────────────────────────────────────────────
        [Header("Haptic Toggles")]
        [Tooltip("Master switch. Disables all haptics when false.")]
        public bool hapticsEnabled = true;

        [Tooltip("Continuous rumble when driving on the dirt shoulder / off-road.")]
        public bool offRoadRumbleEnabled = true;

        [Tooltip("Sharp pulse when hitting a pothole.")]
        public bool potholeHapticEnabled = true;

        [Tooltip("Sharp pulse when colliding with traffic.")]
        public bool collisionHapticEnabled = true;

        [Tooltip("Medium pulse when going over a speed bump.")]
        public bool speedBumpHapticEnabled = true;

        [Tooltip("Light pulse when hitting a rock obstacle.")]
        public bool rockHapticEnabled = true;

        // ── Off-road rumble settings ───────────────────────────────────────────
        [Header("Off-Road Rumble")]
        [Tooltip("How often the rumble pulse fires while off-road (seconds between pulses). " +
                 "Lower = more continuous feeling. 0.08-0.15 works well on iPad.")]
        public float rumbleInterval = 0.1f;

        [Tooltip("Road half-width in world units. Player X beyond this = off-road. " +
                 "Match this to PlayerController.roadHalfWidth.")]
        public float roadHalfWidth = 4f;

        [Tooltip("How far past the road edge before off-road rumble starts (grace buffer).")]
        public float offRoadGraceBuffer = 0.3f;

        // ── References ────────────────────────────────────────────────────────
        [Header("References")]
        public PlayerController playerController;

        // ── Internal ──────────────────────────────────────────────────────────
        private bool      _isOffRoad;
        private float     _rumbleTimer;
        private Coroutine _rumbleCoroutine;

        // iOS Taptic Engine impact styles
        private enum ImpactStyle { Light, Medium, Heavy }

#if UNITY_IOS
        [DllImport("__Internal")]
        private static extern void _TriggerImpactHaptic(int style);
        // style: 0 = Light, 1 = Medium, 2 = Heavy

        [DllImport("__Internal")]
        private static extern void _TriggerNotificationHaptic(int type);
        // type: 0 = Success, 1 = Warning, 2 = Error
#endif

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void Update()
        {
            if (!hapticsEnabled || !offRoadRumbleEnabled) return;
            if (playerController == null) return;
            if (GameManager.Instance?.CurrentState != GameManager.GameState.Playing) return;

            float px = playerController.transform.position.x;
            bool offRoad = Mathf.Abs(px) > (roadHalfWidth + offRoadGraceBuffer);

            if (offRoad && !_isOffRoad)
            {
                _isOffRoad = true;
                _rumbleCoroutine = StartCoroutine(OffRoadRumble());
            }
            else if (!offRoad && _isOffRoad)
            {
                _isOffRoad = false;
                if (_rumbleCoroutine != null) StopCoroutine(_rumbleCoroutine);
                _rumbleCoroutine = null;
            }
        }

        // ── Continuous off-road rumble ─────────────────────────────────────────
        private IEnumerator OffRoadRumble()
        {
            while (_isOffRoad)
            {
                TriggerImpact(ImpactStyle.Light);
                yield return new WaitForSeconds(rumbleInterval);
            }
        }

        // ── Public trigger methods — call these from game events ──────────────

        /// <summary>Call from Pothole.OnTriggerEnter.</summary>
        public void OnPotholeHit()
        {
            if (!hapticsEnabled || !potholeHapticEnabled) return;
            TriggerImpact(ImpactStyle.Heavy);
        }

        /// <summary>Call from OvertakeCollisionHandler when player hits traffic.</summary>
        public void OnTrafficCollision()
        {
            if (!hapticsEnabled || !collisionHapticEnabled) return;
            TriggerImpact(ImpactStyle.Heavy);
            // Double pulse for collision — feels more impactful
            StartCoroutine(DelayedImpact(0.1f, ImpactStyle.Medium));
        }

        /// <summary>Call from SpeedBump trigger when player crosses a speed bump.</summary>
        public void OnSpeedBump()
        {
            if (!hapticsEnabled || !speedBumpHapticEnabled) return;
            TriggerImpact(ImpactStyle.Medium);
        }

        /// <summary>Call from RockObstacle when player hits a rock.</summary>
        public void OnRockHit()
        {
            if (!hapticsEnabled || !rockHapticEnabled) return;
            TriggerImpact(ImpactStyle.Medium);
        }

        // ── Platform-specific vibration ───────────────────────────────────────
        private void TriggerImpact(ImpactStyle style)
        {
#if UNITY_EDITOR
            // Log in Editor so you can verify haptic calls without a device
            Debug.Log($"[HapticFeedback] {style} impact triggered.");
#elif UNITY_IOS
            _TriggerImpactHaptic((int)style);
#elif UNITY_ANDROID
            // Android doesn't have graduated haptics without a plugin.
            // Handheld.Vibrate() triggers the default short vibration.
            // Duration varies: light = 20ms, medium = 40ms, heavy = 80ms.
            long duration = style == ImpactStyle.Light ? 20L :
                            style == ImpactStyle.Medium ? 40L : 80L;
            AndroidVibrate(duration);
#endif
        }

        private IEnumerator DelayedImpact(float delay, ImpactStyle style)
        {
            yield return new WaitForSeconds(delay);
            TriggerImpact(style);
        }

#if UNITY_ANDROID
        private void AndroidVibrate(long milliseconds)
        {
            // Uses Android Vibrator service directly for duration control
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                {
                    vibrator.Call("vibrate", milliseconds);
                }
            }
            catch
            {
                Handheld.Vibrate(); // fallback
            }
        }
#endif
    }
}
