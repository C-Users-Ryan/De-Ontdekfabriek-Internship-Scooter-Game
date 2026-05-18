using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace OvertakeGame
{
    /// <summary>
    /// On-screen touch controls for phone and tablet.
    ///
    /// Layout (landscape):
    ///   LEFT HALF of screen  → steer left  (hold)
    ///   RIGHT HALF of screen → steer right (hold)
    ///   GAS button           → accelerate  (hold)
    ///   BRAKE button         → brake       (hold)
    ///
    /// Add this component to a UI GameObject on the Canvas.
    /// It auto-builds the touch zones from code so you don't need to
    /// manually wire up buttons — just attach and it works.
    ///
    /// SETUP:
    ///   1. Add an empty GameObject as a child of your Canvas. Name it "MobileControls".
    ///   2. Add this component to it.
    ///   3. Assign playerController.
    ///   4. The control overlay auto-shows on mobile/tablet and hides on desktop
    ///      (unless forceShowOnDesktop is true for testing).
    /// </summary>
    public class MobileInputUI : MonoBehaviour
    {
        [Header("References")]
        public PlayerController playerController;

        [Header("Settings")]
        [Tooltip("Show mobile controls even in the editor/desktop for testing.")]
        public bool forceShowOnDesktop = false;

        [Tooltip("Opacity of the touch zone overlays (0 = invisible, 1 = fully visible).")]
        [Range(0f, 1f)]
        public float overlayAlpha = 0.15f;

        // ── Built at runtime ──────────────────────────────────────────────────
        private bool _leftHeld;
        private bool _rightHeld;
        private bool _gasHeld;
        private bool _brakeHeld;

        void Start()
        {
            bool isMobile = Application.isMobilePlatform || forceShowOnDesktop;
            if (!isMobile) { gameObject.SetActive(false); return; }

            BuildControls();
        }

        void Update()
        {
            if (playerController == null) return;
            playerController.mobileGas     = _gasHeld;
            playerController.mobileBrake   = _brakeHeld;
            playerController.mobileLateral = _leftHeld ? -1f : (_rightHeld ? 1f : 0f);
        }

        private void BuildControls()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) { Debug.LogError("[MobileInputUI] Must be a child of a Canvas."); return; }

            // ── Steer Left — left 40% of screen ───────────────────────────────
            CreateZone("SteerLeft",
                new Vector2(0f, 0f), new Vector2(0.4f, 0.7f),
                new Color(0f, 0.5f, 1f, overlayAlpha),
                "◄ STEER",
                onDown: () => _leftHeld  = true,
                onUp:   () => _leftHeld  = false);

            // ── Steer Right — right 40% of screen ─────────────────────────────
            CreateZone("SteerRight",
                new Vector2(0.6f, 0f), new Vector2(1f, 0.7f),
                new Color(0f, 0.5f, 1f, overlayAlpha),
                "STEER ►",
                onDown: () => _rightHeld = true,
                onUp:   () => _rightHeld = false);

            // ── Gas — top right ────────────────────────────────────────────────
            CreateZone("Gas",
                new Vector2(0.6f, 0.7f), new Vector2(1f, 1f),
                new Color(0f, 1f, 0.2f, overlayAlpha),
                "⬆ GAS",
                onDown: () => _gasHeld   = true,
                onUp:   () => _gasHeld   = false);

            // ── Brake — top left ───────────────────────────────────────────────
            CreateZone("Brake",
                new Vector2(0f, 0.7f), new Vector2(0.4f, 1f),
                new Color(1f, 0.3f, 0.1f, overlayAlpha),
                "⬇ BRAKE",
                onDown: () => _brakeHeld = true,
                onUp:   () => _brakeHeld = false);
        }

        private void CreateZone(string zoneName,
                                 Vector2 anchorMin, Vector2 anchorMax,
                                 Color color, string label,
                                 System.Action onDown, System.Action onUp)
        {
            // Container
            var go = new GameObject(zoneName, typeof(RectTransform), typeof(Image), typeof(EventTrigger));
            go.transform.SetParent(transform, false);

            var rt         = go.GetComponent<RectTransform>();
            rt.anchorMin   = anchorMin;
            rt.anchorMax   = anchorMax;
            rt.offsetMin   = Vector2.zero;
            rt.offsetMax   = Vector2.zero;

            var img        = go.GetComponent<Image>();
            img.color      = color;
            img.raycastTarget = true;

            // Label
            var labelGo    = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var labelRT    = labelGo.GetComponent<RectTransform>();
            labelRT.anchorMin  = Vector2.zero;
            labelRT.anchorMax  = Vector2.one;
            labelRT.offsetMin  = Vector2.zero;
            labelRT.offsetMax  = Vector2.zero;

            // Use Unity's legacy Text since TMP may not be present in every project
            var text           = labelGo.AddComponent<Text>();
            text.text          = label;
            text.alignment     = TextAnchor.MiddleCenter;
            text.fontSize      = 28;
            text.color         = new Color(1f, 1f, 1f, 0.7f);
            text.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Touch events
            var et = go.GetComponent<EventTrigger>();

            var downEntry  = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            downEntry.callback.AddListener(_ => onDown());
            et.triggers.Add(downEntry);

            var upEntry    = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            upEntry.callback.AddListener(_ => onUp());
            et.triggers.Add(upEntry);

            var exitEntry  = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exitEntry.callback.AddListener(_ => onUp());
            et.triggers.Add(exitEntry);
        }
    }
}
