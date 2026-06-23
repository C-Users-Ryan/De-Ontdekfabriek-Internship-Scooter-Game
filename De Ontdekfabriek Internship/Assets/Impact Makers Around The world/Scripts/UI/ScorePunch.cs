using System.Collections;
using TMPro;
using UnityEngine;
using KenyaScooter.Core;

namespace KenyaScooter.UI
{
    /// <summary>
    /// Punch + flash on a score gain: the score element scales up briefly and the
    /// digits flash to the gain colour, then settle. Listens to ScoreChanged and
    /// reacts to increases only (delta > 0). No popup, minimal and glanceable.
    /// Losses and the session reset (delta &lt;= 0) are ignored here; give those
    /// their own red treatment so up and down stay distinct.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScorePunch : MonoBehaviour
    {
        [Header("Targets")]
        [Tooltip("The RectTransform to scale. Set its pivot to (0.5, 0.5) so it grows from its centre.")]
        [SerializeField] private RectTransform punchTarget;
        [Tooltip("The score / digit texts to flash. Leave empty if the odometer already flashes during its roll.")]
        [SerializeField] private TMP_Text[] flashTexts;

        [Header("Punch")]
        [SerializeField, Range(1f, 1.5f)] private float punchScale = 1.15f;
        [SerializeField] private float duration = 0.22f;
        [Tooltip("Drives both the scale and the flash, evaluated 0..1 over the duration. A 0 -> 1 -> 0 shape punches out then settles.")]
        [SerializeField] private AnimationCurve shape =
            new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.35f, 1f), new Keyframe(1f, 0f));

        [Header("Flash")]
        [SerializeField] private Color baseColour = new Color(0.945f, 0.569f, 0.255f); // #f19141 accent
        [SerializeField] private Color gainColour = new Color(0.404f, 0.706f, 0.306f); // #67B44E success

        private Coroutine routine;

        private void OnEnable() => GameEvents.ScoreChanged += HandleScore;
        private void OnDisable() => GameEvents.ScoreChanged -= HandleScore;

        private void HandleScore(int total, int delta)
        {
            if (delta > 0) Play();
        }

        /// <summary>Plays the punch + flash once. Restarts if already running so rapid overtakes don't stack the scale.</summary>
        public void Play()
        {
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = shape.Evaluate(t / duration);
                if (punchTarget != null)
                    punchTarget.localScale = Vector3.one * (1f + (punchScale - 1f) * k);
                Color c = Color.Lerp(baseColour, gainColour, k);
                for (int i = 0; i < flashTexts.Length; i++)
                    if (flashTexts[i] != null) flashTexts[i].color = c;
                yield return null;
            }

            if (punchTarget != null) punchTarget.localScale = Vector3.one;
            for (int i = 0; i < flashTexts.Length; i++)
                if (flashTexts[i] != null) flashTexts[i].color = baseColour;
            routine = null;
        }
    }
}
