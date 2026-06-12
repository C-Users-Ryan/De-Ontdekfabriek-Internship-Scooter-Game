using UnityEngine;
using System.Collections;
#if UNITY_IOS
using System.Runtime.InteropServices;
#endif

namespace OvertakeGame
{
    /// <summary>
    /// Manages haptic feedback for iPad.
    /// Off-road rumble fires continuously while player X is beyond road edge.
    /// Single pulses on pothole hit, traffic collision, rock hit, speed bump.
    /// iOS uses Taptic Engine via native plugin (Plugins/iOS/HapticPlugin.mm).
    /// Android uses Vibrator service with duration control.
    /// Editor logs every call to Console — no device needed for testing.
    /// 
    /// SETUP: Attach to any persistent GameObject. Wire playerController reference.
    /// </summary>
    public class HapticFeedback : MonoBehaviour
    {
        public static HapticFeedback Instance { get; private set; }

        [Header("Haptic Toggles")]
        public bool hapticsEnabled          = true;
        public bool offRoadRumbleEnabled    = true;
        public bool potholeHapticEnabled    = true;
        public bool collisionHapticEnabled  = true;
        public bool speedBumpHapticEnabled  = true;
        public bool rockHapticEnabled       = true;

        [Header("Off-Road Rumble")]
        [Tooltip("Seconds between rumble pulses while off-road. 0.08–0.15 feels continuous.")]
        public float rumbleInterval      = 0.1f;
        [Tooltip("Match PlayerController.roadHalfWidth.")]
        public float roadHalfWidth       = 4f;
        public float offRoadGraceBuffer  = 0.3f;

        [Header("References")]
        public PlayerController playerController;

        private bool      _isOffRoad;
        private Coroutine _rumbleCoroutine;

        private enum ImpactStyle { Light, Medium, Heavy }

#if UNITY_IOS
        [DllImport("__Internal")]
        private static extern void _TriggerImpactHaptic(int style);
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

            float steerPos = Vector3.Dot(playerController.transform.position, RoadDirection.SteerAxis);
            bool  offRoad  = Mathf.Abs(steerPos) > (roadHalfWidth + offRoadGraceBuffer);

            if (offRoad && !_isOffRoad)
            {
                _isOffRoad       = true;
                _rumbleCoroutine = StartCoroutine(OffRoadRumble());
            }
            else if (!offRoad && _isOffRoad)
            {
                _isOffRoad = false;
                if (_rumbleCoroutine != null) StopCoroutine(_rumbleCoroutine);
                _rumbleCoroutine = null;
            }
        }

        private IEnumerator OffRoadRumble()
        {
            while (_isOffRoad)
            {
                TriggerImpact(ImpactStyle.Light);
                yield return new WaitForSeconds(rumbleInterval);
            }
        }

        public void OnPotholeHit()
        {
            if (!hapticsEnabled || !potholeHapticEnabled) return;
            TriggerImpact(ImpactStyle.Heavy);
        }

        public void OnTrafficCollision()
        {
            if (!hapticsEnabled || !collisionHapticEnabled) return;
            TriggerImpact(ImpactStyle.Heavy);
            StartCoroutine(DelayedImpact(0.1f, ImpactStyle.Medium));
        }

        public void OnSpeedBump()
        {
            if (!hapticsEnabled || !speedBumpHapticEnabled) return;
            TriggerImpact(ImpactStyle.Medium);
        }

        public void OnRockHit()
        {
            if (!hapticsEnabled || !rockHapticEnabled) return;
            TriggerImpact(ImpactStyle.Medium);
        }

        private void TriggerImpact(ImpactStyle style)
        {
#if UNITY_EDITOR
            Debug.Log($"[HapticFeedback] {style} impact");
#elif UNITY_IOS
            _TriggerImpactHaptic((int)style);
#elif UNITY_ANDROID
            long ms = style == ImpactStyle.Light ? 20L : style == ImpactStyle.Medium ? 40L : 80L;
            try {
                using var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var ac = up.GetStatic<AndroidJavaObject>("currentActivity");
                using var vb = ac.Call<AndroidJavaObject>("getSystemService", "vibrator");
                vb.Call("vibrate", ms);
            } catch { Handheld.Vibrate(); }
#endif
        }

        private IEnumerator DelayedImpact(float delay, ImpactStyle style)
        {
            yield return new WaitForSeconds(delay);
            TriggerImpact(style);
        }
    }
}