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

        private float lastWeight;

        private void Awake()
        {
            if (rewindVolume == null)
                rewindVolume = BuildRuntimeVolume();
            EnsurePostProcessingOn();
        }

        /// <summary>Driven by RewindSystem: 0 = normal colour, 1 = full grey-out.</summary>
        public void SetWeight(float weight)
        {
            weight = Mathf.Clamp01(weight);

            // Weight rising off zero means a rewind is starting: make sure the rendering camera
            // has post-processing on right now. Doing it here as well as in Awake covers a camera
            // that was spawned or retagged after this component woke up.
            if (weight > 0f && lastWeight <= 0f)
                EnsurePostProcessingOn();
            lastWeight = weight;

            if (rewindVolume != null)
                rewindVolume.weight = weight;
        }

        /// <summary>Builds a global Volume with full desaturation and a soft vignette, off by default.</summary>
        private Volume BuildRuntimeVolume()
        {
            var holder = new GameObject("RewindVolume (runtime)");
            holder.transform.SetParent(transform, false);

            Volume volume = holder.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10000f; // sit far above the day-cycle Volumes so nothing overrides the grey-out
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
            // A URP Volume only renders if the camera drawing it has post-processing on. Turn it
            // on for every active camera, so the grey-out shows regardless of which camera renders
            // the game or whether the main one is tagged MainCamera.
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
