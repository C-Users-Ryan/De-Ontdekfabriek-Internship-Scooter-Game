using UnityEngine;
using UnityEngine.Rendering;

namespace KenyaScooter.SafetyNet
{
    /// <summary>
    /// The black-and-white rewind look (M17): a global URP Volume set to full desaturation (Color
    /// Adjustments, saturation -100) with a soft vignette. RewindSystem drives its weight 0→1→0; the
    /// look lives in the Volume profile, so the art direction stays out of code. The B&amp;W is on purpose —
    /// it tells the player "something out of the ordinary happened and is being undone" (MDA Chain 4).
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
