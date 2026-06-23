using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace KenyaScooter.Feedback
{
    /// <summary>
    /// Thin cross-platform wrapper around the device's vibration motor, with a 0..1 strength input so the
    /// caller can vibrate harder for a harder hit. Implementations per platform:
    ///   - Android (API 26+): VibrationEffect.createOneShot(durationMs, amplitude 1..255) — true variable strength
    ///     where the device supports amplitude control; older devices fall back to a plain timed buzz.
    ///   - iOS (13+): a UIImpactFeedbackGenerator fired with an intensity (see Plugins/iOS/KenyaHaptics.mm);
    ///     older iOS falls back to a fixed impact or the system vibrate.
    ///   - Editor / desktop: a silent no-op (there is no motor to drive).
    /// The whole thing is wrapped in try/catch so a missing motor or permission can never throw into gameplay.
    /// </summary>
    public sealed class HapticDriver
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject vibrator;
        private int sdkInt;
        private bool amplitudeControl;
        private const int DefaultAmplitude = -1; // android.os.VibrationEffect.DEFAULT_AMPLITUDE
#endif
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void _KenyaHapticImpact(float intensity);
#endif

        /// <summary>Builds the driver for the running platform and caches any native handles once.</summary>
        public static HapticDriver Create()
        {
            var driver = new HapticDriver();
#if UNITY_ANDROID && !UNITY_EDITOR
            driver.InitAndroid();
#endif
            return driver;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void InitAndroid()
        {
            try
            {
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                    sdkInt = version.GetStatic<int>("SDK_INT");
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                if (vibrator != null && sdkInt >= 26)
                    amplitudeControl = vibrator.Call<bool>("hasAmplitudeControl");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[HapticDriver] Android vibrator unavailable: " + e.Message);
            }
        }
#endif

        /// <summary>Fires one impact buzz. <paramref name="intensity01"/> is 0..1 (mapped to amplitude where the
        /// device supports it); <paramref name="durationMs"/> is the buzz length in milliseconds.</summary>
        public void Vibrate(float intensity01, int durationMs)
        {
            intensity01 = Mathf.Clamp01(intensity01);
            if (durationMs < 1) durationMs = 1;

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (vibrator == null) { Handheld.Vibrate(); return; }
                if (sdkInt >= 26)
                {
                    int amplitude = amplitudeControl
                        ? Mathf.Clamp(Mathf.RoundToInt(intensity01 * 255f), 1, 255)
                        : DefaultAmplitude; // no amplitude control: the OS picks a fixed strength, length still scales
                    using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                    using (var effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", (long)durationMs, amplitude))
                        vibrator.Call("vibrate", effect);
                }
                else
                {
                    vibrator.Call("vibrate", (long)durationMs); // pre-Oreo: timed buzz only (no amplitude API)
                }
            }
            catch
            {
                Handheld.Vibrate(); // last resort; also makes Unity include the Android VIBRATE permission
            }
#elif UNITY_IOS && !UNITY_EDITOR
            // iOS impact haptics are transient (the system owns the envelope), so duration is not used; the
            // native side maps intensity straight onto UIImpactFeedbackGenerator.impactOccurred(intensity:).
            try { _KenyaHapticImpact(intensity01); } catch { /* generator unavailable */ }
#else
            // Editor and desktop have no haptic motor — intentionally a no-op so testing in-editor is silent.
#endif
        }
    }
}
