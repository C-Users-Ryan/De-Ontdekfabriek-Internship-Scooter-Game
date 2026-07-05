using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KenyaScooter.FX
{
    /// <summary>
    /// The warm "breathe Kenya" colour grade, ON BY DEFAULT and self-bootstrapping. It builds a global URP Volume
    /// at runtime (white balance toward amber, a little contrast/saturation, a gentle high-threshold bloom so the
    /// bright sky/sun glows, and a warm vignette) at a LOW priority so the day-cycle phase Volumes still layer on
    /// top, and turns on the cameras' post-processing (the same step RewindVisuals does). No scene wiring and no
    /// asset, so it works in a build as soon as the scripts compile.
    ///
    /// Reversible: delete or disable the auto object to turn it off. The persistent, hand-tunable version is the
    /// KenyaWarmGrade profile asset the editor tool creates; if you place that, delete this auto object to avoid
    /// double-grading.
    /// </summary>
    public sealed class WarmGrade : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<WarmGrade>() != null)
                return;
            new GameObject("Kenya Warm Grade (auto)").AddComponent<WarmGrade>();
        }

        private void Awake()
        {
            BuildVolume();
            EnsurePostProcessingOn();
        }

        private void BuildVolume()
        {
            Volume volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = -50f; // a base grade; the day-cycle phase Volumes sit above it
            volume.weight = 1f;

            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();

            WhiteBalance wb = profile.Add<WhiteBalance>(true);
            wb.temperature.value = 12f;
            wb.tint.value = 4f;

            ColorAdjustments ca = profile.Add<ColorAdjustments>(true);
            ca.contrast.value = 6f;
            ca.saturation.value = 6f;
            ca.colorFilter.value = new Color(1f, 0.96f, 0.9f);

            Bloom bloom = profile.Add<Bloom>(true);
            bloom.threshold.value = 1.1f; // only the bright sky/sun blooms
            bloom.intensity.value = 0.6f;
            bloom.tint.value = new Color(1f, 0.93f, 0.82f);

            Vignette vig = profile.Add<Vignette>(true);
            vig.intensity.value = 0.18f;
            vig.color.value = new Color(0.25f, 0.12f, 0.06f); // warm, not black

            volume.profile = profile;
        }

        private static void EnsurePostProcessingOn()
        {
            // A URP Volume only renders if the camera drawing it has post-processing on (mirrors RewindVisuals).
            Camera[] cameras = Camera.allCameras;
            for (int i = 0; i < cameras.Length; i++)
            {
                UniversalAdditionalCameraData data = cameras[i].GetUniversalAdditionalCameraData();
                if (data != null)
                    data.renderPostProcessing = true;
            }
        }
    }
}
