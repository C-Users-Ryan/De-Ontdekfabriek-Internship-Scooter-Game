using UnityEngine;

namespace KenyaScooter.UI
{
    /// <summary>
    /// A gentle scale pulse so a tappable chip or button reads as "tap me" for young players. Added to the
    /// team-name chips after the 2026-06-23 play test, where a 10-year-old was unsure what to do or tap on the
    /// start screen. Pulses the transform scale (which Unity's Button does not control, so it never fights the
    /// button's colour tint). Unscaled time, so it animates even while the game is paused on a menu.
    /// </summary>
    public sealed class UiPulse : MonoBehaviour
    {
        [SerializeField] private float amplitude = 0.035f;
        [SerializeField] private float frequency = 1.4f;
        [SerializeField] private float phase;

        private Vector3 baseScale = Vector3.one;

        private void Awake() => baseScale = transform.localScale;

        private void Update()
        {
            float s = 1f + amplitude * Mathf.Sin((Time.unscaledTime * frequency + phase) * Mathf.PI * 2f);
            transform.localScale = baseScale * s;
        }

        /// <summary>Offset so a row of chips pulses as a lively wave instead of in lockstep.</summary>
        public void SetPhase(float p) => phase = p;

        /// <summary>Phase plus a custom strength/tempo — big cards want a much gentler breath than small chips
        /// (play-test 2026-07-05: the default amplitude read as aggressive on the 500px team cards).</summary>
        public void SetWave(float p, float amp, float freq) { phase = p; amplitude = amp; frequency = freq; }
    }
}
