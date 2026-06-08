using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace OvertakeGame
{
    /// <summary>
    /// On-screen touch controls for phone and tablet.
    /// Auto-builds four touch zones at runtime — no manual button setup needed.
    /// Hides itself on desktop unless forceShowOnDesktop is true.
    ///
    /// Layout (landscape):
    ///   Bottom-left 40%  → steer left
    ///   Bottom-right 40% → steer right
    ///   Top-left         → brake
    ///   Top-right        → gas
    ///
    /// SETUP:
    ///   1. Add an empty GameObject as child of your Canvas. Name it MobileControls.
    ///   2. Add this component. Assign playerController.
    ///   3. Done — zones are built at runtime.
    /// </summary>
    public class MobileInputUI : MonoBehaviour
    {
        [Header("References")]
        public PlayerController playerController;

        [Header("Settings")]
        public bool  forceShowOnDesktop = false;
        [Range(0f,1f)]
        public float overlayAlpha       = 0.15f;

        private bool _leftHeld, _rightHeld, _gasHeld, _brakeHeld;

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
            CreateZone("SteerLeft",  new Vector2(0f,  0f),  new Vector2(0.4f, 0.7f),
                new Color(0f, 0.5f, 1f, overlayAlpha), "◄",
                () => _leftHeld  = true, () => _leftHeld  = false);
            CreateZone("SteerRight", new Vector2(0.6f,0f),  new Vector2(1f,   0.7f),
                new Color(0f, 0.5f, 1f, overlayAlpha), "►",
                () => _rightHeld = true, () => _rightHeld = false);
            CreateZone("Gas",        new Vector2(0.6f,0.7f),new Vector2(1f,   1f),
                new Color(0f, 1f, 0.2f, overlayAlpha), "⬆",
                () => _gasHeld   = true, () => _gasHeld   = false);
            CreateZone("Brake",      new Vector2(0f,  0.7f),new Vector2(0.4f, 1f),
                new Color(1f, 0.3f, 0.1f, overlayAlpha), "⬇",
                () => _brakeHeld = true, () => _brakeHeld = false);
        }

        private void CreateZone(string zoneName, Vector2 anchorMin, Vector2 anchorMax,
            Color color, string label, System.Action onDown, System.Action onUp)
        {
            var go = new GameObject(zoneName, typeof(RectTransform), typeof(Image), typeof(EventTrigger));
            go.transform.SetParent(transform, false);
            var rt       = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img      = go.GetComponent<Image>();
            img.color    = color; img.raycastTarget = true;

            var labelGo  = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var labelRT  = labelGo.GetComponent<RectTransform>();
            labelRT.anchorMin = Vector2.zero; labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = labelRT.offsetMax = Vector2.zero;
            var text     = labelGo.AddComponent<Text>();
            text.text    = label; text.alignment = TextAnchor.MiddleCenter;
            text.fontSize= 28; text.color = new Color(1f,1f,1f,0.7f);
            text.font    = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var et = go.GetComponent<EventTrigger>();
            var dn = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            dn.callback.AddListener(_ => onDown());
            et.triggers.Add(dn);
            var up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            up.callback.AddListener(_ => onUp());
            et.triggers.Add(up);
            var ex = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            ex.callback.AddListener(_ => onUp());
            et.triggers.Add(ex);
        }
    }
}
