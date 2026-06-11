using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace OvertakeGame
{
    /// <summary>
    /// On-screen touch controls for tablet. Builds two full-height zones at runtime.
    ///
    /// ── LAYOUT (landscape) ────────────────────────────────────────────────────────
    ///   Left  half  →  BRAKE  (decelerate)
    ///   Right half  →  GAS    (accelerate)
    ///
    /// ── WHAT THIS SCRIPT DOES NOT DO ────────────────────────────────────────────
    ///   Lateral steering is NOT handled here. Steering is exclusively via device tilt,
    ///   managed by GyroscopeSteering. This separation is intentional:
    ///     - Tilt and gas/brake are independent physical actions (one hand can hold the
    ///       tablet while the other presses; or two thumbs on the respective sides).
    ///     - Touch steering was removed after user tests showed it conflicted with the
    ///       physical tilt sensation and produced confusing double-input. See:
    ///       [[SC2 — Real Rider Mode — Horizon Lock]] devlog.
    ///
    /// ── SETUP ─────────────────────────────────────────────────────────────────────
    ///   1. Add an empty child of your Canvas named MobileControls.
    ///   2. Attach this component. Assign playerController.
    ///   3. Done — zones are built at runtime, auto-hidden on desktop.
    ///
    /// DESKTOP: This component hides itself unless forceShowOnDesktop = true.
    ///          Use W/S keys in PlayerController for gas/brake during development.
    /// </summary>
    public class MobileInputUI : MonoBehaviour
    {
        [Header("References")]
        public PlayerController playerController;

        [Header("Settings")]
        [Tooltip("Show the touch overlay on desktop. Useful for testing layout without a device.")]
        public bool  forceShowOnDesktop = false;
        [Range(0f, 1f)]
        [Tooltip("Opacity of the touch zone overlays. Low enough to see through, high enough to confirm zone position.")]
        public float overlayAlpha       = 0.12f;

        private bool _gasHeld, _brakeHeld;

        void Start()
        {
            if (!Application.isMobilePlatform && !forceShowOnDesktop)
            {
                gameObject.SetActive(false);
                return;
            }
            BuildControls();
        }

        void Update()
        {
            if (playerController == null) return;
            playerController.mobileGas   = _gasHeld;
            playerController.mobileBrake = _brakeHeld;
            // NOTE: playerController.mobileLateral is NOT set here.
            //       GyroscopeSteering owns mobileLateral (tilt input).
        }

        private void BuildControls()
        {
            // Left half → Brake
            CreateZone(
                zoneName:  "Brake",
                anchorMin: new Vector2(0f,   0f),
                anchorMax: new Vector2(0.5f, 1f),
                color:     new Color(1f, 0.25f, 0.1f, overlayAlpha),
                label:     "BRAKE",
                onDown:    () => _brakeHeld = true,
                onUp:      () => _brakeHeld = false);

            // Right half → Gas
            CreateZone(
                zoneName:  "Gas",
                anchorMin: new Vector2(0.5f, 0f),
                anchorMax: new Vector2(1f,   1f),
                color:     new Color(0f, 0.9f, 0.2f, overlayAlpha),
                label:     "GAS",
                onDown:    () => _gasHeld = true,
                onUp:      () => _gasHeld = false);
        }

        // ── Zone builder ──────────────────────────────────────────────────────────────

        private void CreateZone(string zoneName, Vector2 anchorMin, Vector2 anchorMax,
            Color color, string label, System.Action onDown, System.Action onUp)
        {
            var go = new GameObject(zoneName,
                typeof(RectTransform), typeof(Image), typeof(EventTrigger));
            go.transform.SetParent(transform, false);

            var rt       = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var img           = go.GetComponent<Image>();
            img.color         = color;
            img.raycastTarget = true;

            // Centred label — legible at arm's length on a tablet
            var labelGo         = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var labelRT         = labelGo.GetComponent<RectTransform>();
            labelRT.anchorMin   = Vector2.zero;
            labelRT.anchorMax   = Vector2.one;
            labelRT.offsetMin   = labelRT.offsetMax = Vector2.zero;
            var text            = labelGo.AddComponent<Text>();
            text.text           = label;
            text.alignment      = TextAnchor.MiddleCenter;
            text.fontSize       = 40;
            text.color          = new Color(1f, 1f, 1f, 0.55f);
            text.font           = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Pointer events — register press, release, and finger-slide-off
            var et = go.GetComponent<EventTrigger>();
            AddTrigger(et, EventTriggerType.PointerDown, onDown);
            AddTrigger(et, EventTriggerType.PointerUp,   onUp);
            AddTrigger(et, EventTriggerType.PointerExit, onUp);
        }

        private static void AddTrigger(EventTrigger et, EventTriggerType type, System.Action callback)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => callback());
            et.triggers.Add(entry);
        }
    }
}
