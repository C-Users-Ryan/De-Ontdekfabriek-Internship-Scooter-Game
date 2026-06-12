using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace OvertakeGame
{
    /// <summary>
    /// Shows pulsing left/right arrows on first play only.
    /// Uses a PlayerPrefs flag so it shows exactly once across all sessions
    /// on the device — not once per app launch.
    ///
    /// SETUP:
    ///   1. Create a Canvas child of your HUD Canvas named TutorialOverlay.
    ///      Screen Space Overlay, sort order above the HUD (e.g. 10).
    ///   2. Inside it place two Image components: SwipeLeft and SwipeRight.
    ///      Use arrow sprites or the ti-arrow-left / ti-arrow-right Tabler icons.
    ///      Position left-centre and right-centre of the screen.
    ///   3. Add a CanvasGroup component to TutorialOverlay.
    ///   4. Attach this script to TutorialOverlay.
    ///      Drag CanvasGroup, arrowLeft, and arrowRight into the Inspector slots.
    ///
    /// The left arrow pulses outward to the left.
    /// The right arrow pulses outward to the right.
    /// Any touch dismisses it. Auto-dismisses after showDuration seconds.
    /// </summary>
    public class TutorialHint : MonoBehaviour
    {
        [Header("UI References")]
        public CanvasGroup    canvasGroup;
        public RectTransform  arrowLeft;
        public RectTransform  arrowRight;

        [Header("Timing")]
        [Tooltip("Seconds before auto-dismissal if player does nothing.")]
        public float showDuration  = 3f;
        [Tooltip("Seconds to fade out.")]
        public float fadeDuration  = 0.4f;

        [Header("Pulse")]
        [Tooltip("How far each arrow travels outward in pixels.")]
        public float pulseAmount = 12f;
        [Tooltip("Pulse oscillation speed.")]
        public float pulseSpeed  = 1.8f;

        private const string PREF_KEY = "roads_tutorial_done";

        private bool    _dismissed;
        private Vector2 _leftOrigin;
        private Vector2 _rightOrigin;

        void Start()
        {
            // Already seen — hide immediately without animation
            if (PlayerPrefs.GetInt(PREF_KEY, 0) == 1)
            {
                gameObject.SetActive(false);
                return;
            }

            canvasGroup.alpha = 1f;
            _leftOrigin  = arrowLeft .anchoredPosition;
            _rightOrigin = arrowRight.anchoredPosition;
            StartCoroutine(AutoDismiss());
        }

        void Update()
        {
            if (_dismissed) return;

            // Pulse arrows outward in their respective directions
            float offset = Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
            arrowLeft .anchoredPosition = _leftOrigin  + Vector2.left  * Mathf.Abs(offset);
            arrowRight.anchoredPosition = _rightOrigin + Vector2.right * Mathf.Abs(offset);

            // Any touch dismisses
            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
                Dismiss();
        }

        private IEnumerator AutoDismiss()
        {
            yield return new WaitForSeconds(showDuration);
            Dismiss();
        }

        public void Dismiss()
        {
            if (_dismissed) return;
            _dismissed = true;
            PlayerPrefs.SetInt(PREF_KEY, 1);
            PlayerPrefs.Save();
            StartCoroutine(FadeOut());
        }

        private IEnumerator FadeOut()
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / fadeDuration;
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, t);
                yield return null;
            }
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Reset the tutorial flag — call from a debug or settings menu.
        /// The hint will show again on the next session start.
        /// </summary>
        public static void ResetForTesting()
        {
            PlayerPrefs.DeleteKey(PREF_KEY);
            Debug.Log("[TutorialHint] Flag reset — hint will show on next play.");
        }
    }
}
