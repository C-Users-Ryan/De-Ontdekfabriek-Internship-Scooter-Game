using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

namespace OvertakeGame
{
    /// <summary>
    /// Displays floating "+25 INGEHAALD" / "-30 TE SNEL" text under the score
    /// whenever ScoreManager fires a significant event.
    ///
    /// HOW IT WORKS:
    ///   ScoreManager.OnScoreEvent fires with (amount, label).
    ///   This component filters events smaller than minAbsAmount (prevents spam from
    ///   trickle points) and label-only events (amount=0 but label not empty always show).
    ///   Pops a pre-built text slot into view, animates it upward, then fades it out.
    ///   Up to maxSimultaneous popups visible at once; oldest is recycled when queue is full.
    ///
    /// SETUP:
    ///   1. In the Canvas under the score display, create a child RectTransform called
    ///      "PopupContainer" (optional but helps with layout).
    ///   2. Create maxSimultaneous (default 3) children inside it:
    ///      each needs a TMP_Text component. Assign them to the slots[] array.
    ///   3. Assign this component's scoreManager reference in Inspector.
    ///
    /// POPUP FORMAT:
    ///   Positive amount  → green  → "+25 INGEHAALD"
    ///   Negative amount  → red    → "-30 KUUKUA!"
    ///   Zero amount      → white  → "+SCHILD"
    ///   No label         → not shown regardless of amount
    ///
    /// SLOT LAYOUT:
    ///   Slots are stacked. When a new popup fires, it takes the oldest-idle slot.
    ///   Anchor slots below the score text so they rise INTO the score area for visibility.
    /// </summary>
    public class ScorePopupUI : MonoBehaviour
    {
        [Header("References")]
        public ScoreManager scoreManager;

        [Header("Popup Slots")]
        [Tooltip("Pre-built TMP_Text GameObjects in the Canvas. Create at least 3.")]
        public TMP_Text[] slots;

        [Header("Filter")]
        [Tooltip("Minimum absolute point change that triggers a popup. Prevents trickle-point spam. "
               + "Events with a non-empty label always show regardless of this threshold.")]
        public int minAbsAmount = 8;

        [Header("Animation")]
        [Tooltip("Distance in canvas units the text travels upward during the animation.")]
        public float riseDistance = 55f;
        [Tooltip("How long the popup stays fully visible before fading.")]
        public float holdDuration = 0.7f;
        [Tooltip("Fade-out duration after holdDuration ends.")]
        public float fadeDuration = 0.6f;

        [Header("Colours")]
        public Color positiveColor = new Color(0.3f, 1f, 0.4f, 1f);   // green
        public Color negativeColor = new Color(1f, 0.35f, 0.35f, 1f); // red
        public Color neutralColor  = new Color(0.8f, 0.85f, 1f, 1f);  // pale blue

        // ── Internal state ─────────────────────────────────────────────────────
        private int        _nextSlot;
        private Coroutine[] _slotCoroutines;
        private Vector2[]  _slotBasePositions;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        void Awake()
        {
            if (slots == null || slots.Length == 0)
            {
                Debug.LogWarning("[ScorePopupUI] No slots assigned — popups will not show.");
                return;
            }
            _slotCoroutines   = new Coroutine[slots.Length];
            _slotBasePositions = new Vector2[slots.Length];

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null)
                {
                    slots[i].gameObject.SetActive(false);
                    _slotBasePositions[i] = slots[i].rectTransform.anchoredPosition;
                }
            }
        }

        void OnEnable()
        {
            if (scoreManager != null) scoreManager.OnScoreEvent += HandleScoreEvent;
        }

        void OnDisable()
        {
            if (scoreManager != null) scoreManager.OnScoreEvent -= HandleScoreEvent;
        }

        // ── Event handler ──────────────────────────────────────────────────────

        private void HandleScoreEvent(int amount, string label)
        {
            // Filter: skip events with no label AND below threshold
            if (string.IsNullOrEmpty(label)) return;
            if (amount != 0 && Mathf.Abs(amount) < minAbsAmount) return;

            string display = amount > 0  ? $"+{amount} {label}"
                           : amount < 0  ? $"{amount} {label}"  // amount already negative
                           : label;                               // label-only (e.g. "+SCHILD")

            Color color = amount > 0 ? positiveColor
                        : amount < 0 ? negativeColor
                        : neutralColor;

            ShowPopup(display, color);
        }

        // ── Popup display ──────────────────────────────────────────────────────

        private void ShowPopup(string text, Color color)
        {
            if (slots == null || slots.Length == 0) return;

            // Kill the previous coroutine on this slot and reuse it
            int idx = _nextSlot % slots.Length;
            _nextSlot++;

            TMP_Text slot = slots[idx];
            if (slot == null) return;

            if (_slotCoroutines[idx] != null)
                StopCoroutine(_slotCoroutines[idx]);

            _slotCoroutines[idx] = StartCoroutine(AnimateSlot(slot, idx, text, color));
        }

        private IEnumerator AnimateSlot(TMP_Text slot, int idx, string text, Color color)
        {
            // Reset position
            RectTransform rt = slot.rectTransform;
            rt.anchoredPosition = _slotBasePositions[idx];

            slot.text  = text;
            slot.color = color;
            slot.gameObject.SetActive(true);

            float elapsed = 0f;
            float totalDuration = holdDuration + fadeDuration;

            while (elapsed < totalDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / totalDuration;

                // Rise throughout the whole animation
                rt.anchoredPosition = _slotBasePositions[idx] + Vector2.up * (riseDistance * t);

                // Fade only after holdDuration
                float fadeT = Mathf.InverseLerp(holdDuration, totalDuration, elapsed);
                Color c = color;
                c.a = Mathf.Lerp(1f, 0f, fadeT);
                slot.color = c;

                yield return null;
            }

            slot.gameObject.SetActive(false);
            rt.anchoredPosition = _slotBasePositions[idx];
        }
    }
}
