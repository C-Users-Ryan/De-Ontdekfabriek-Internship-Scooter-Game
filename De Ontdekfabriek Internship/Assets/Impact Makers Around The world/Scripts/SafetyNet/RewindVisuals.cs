using UnityEngine;
using UnityEngine.Rendering;

namespace KenyaScooter.SafetyNet
{
    /// <summary>
    /// The B&amp;W rewind look (M17): a URP global Volume authored with full
    /// desaturation (Color Adjustments, saturation -100) and a soft vignette.
    /// RewindSystem drives the weight 0→1→0; the volume profile defines the look,
    /// so art direction stays out of code. The B&amp;W is deliberate communication:
    /// "something exceptional happened and is being undone" (MDA Chain 4).
    /// </summary>
    public sealed class RewindVisuals : MonoBehaviour
    {
        [SerializeField] private Volume rewindVolume;

        public void SetWeight(float weight)
        {
            if (rewindVolume != null)
                rewindVolume.weight = Mathf.Clamp01(weight);
        }
    }
}
