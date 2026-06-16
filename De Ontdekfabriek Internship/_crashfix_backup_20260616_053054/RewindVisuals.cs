using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KenyaScooter.SafetyNet
{
    /// <summary>
    /// The black-and-white rewind look (M17). RewindSystem drives its weight 0→1→0 during a rewind. If a
    /// URP Volume is wired up in the scene it uses that; otherwise it builds its own global Volume at
    /// runtime (full desaturation + a soft vignette) and switches on the camera's post-processing, so the
    /// screen greys out with no manual setup. The B&amp;W is on purpose — it tells the player "something out
    /// of the ordinary happened and is being undone" (MDA Chain 4).
    /// </summary>
    public sealed class RewindVisuals : MonoBehaviour
    {
        [Tooltip("Optional. A pre-authored grey-out Volume. Leave empty to have one built at runtime.")]
        [SerializeField] private Volume rewindVolume;

        private void Awake()
        {
            if (rewindVolume == null)
                rewindVolume = BuildRuntimeVolume();
            EnsurePostProcessingOn();
        }

        /// <summary>Driven by RewindSystem: 0 = normal colour, 1 = full grey-out.</summary>
        public void SetWeight(float weight)
        {
            if (rewindVolume != null)
                rewindVolume.weight = Mathf.Clamp01(weight);
        }

        /// <summary>Builds a global Volume with full desaturation and a soft vignette, off by default.</summary>
        private Volume BuildRuntimeVolume()
        {
            var holder = new GameObject("RewindVolume (runtime)");
            holder.transform.SetParent(transform, false);

            Volume volume = holder.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100f; // sit above the day-cycle Volumes
            volume.weight = 0f;

            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();

            ColorAdjustments color = profile.Add<ColorAdjustments>();
            color.saturation.value = -100f;
            color.saturation.overrideState = true;

            Vignette vignette = profile.Add<Vignette>();
            vignette.intensity.value = 0.4f;
            vignette.intensity.overrideState = true;

            volume.profile = profile;
            return volume;
        }

        /// <summary>A URP Volume only shows if the camera has post-processing turned on, so make sure it is.</summary>
        private static void EnsurePostProcessingOn()
        {
            Camera main = Camera.main;
            if (main == null)
                return;
            UniversalAdditionalCameraData data = main.GetUniversalAdditionalCameraData();
            if (data != null)
                data.renderPostProcessing = true;
        }
    }
}
