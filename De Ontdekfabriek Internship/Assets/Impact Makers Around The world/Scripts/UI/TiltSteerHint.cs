using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KenyaScooter.Core;
using KenyaScooter.Controls;

namespace KenyaScooter.UI
{
    /// <summary>
    /// First-run steering hint. From the 2026-06-23 play test: a new player did not realise the tablet is
    /// TILTED to steer (controls-not-discoverable, the same theme as the earlier missing gas cue). This shows a
    /// brief "KANTEL OM TE STUREN" cue when a turn begins and clears it the moment the player actually tilts (or
    /// after a few seconds) — so every new player in the relay gets the nudge, but it never nags someone who
    /// already steers. It builds its own overlay and self-bootstraps, so it needs no scene wiring and is a no-op
    /// if nothing raises the session events.
    /// </summary>
    public sealed class TiltSteerHint : MonoBehaviour
    {
        [SerializeField] private string message = "KANTEL OM TE STUREN";
        [Tooltip("Steer input magnitude (0..1) that counts as the player having tilted.")]
        [SerializeField] private float tiltThreshold = 0.3f;
        [Tooltip("Seconds of sustained tilt before the hint clears (so a stray jitter does not dismiss it).")]
        [SerializeField] private float tiltHoldToDismiss = 0.35f;
        [Tooltip("Clear the hint after this long even if the player never tilts.")]
        [SerializeField] private float maxShowSeconds = 6f;
        [SerializeField] private float fadeSpeed = 4f;

        private CanvasGroup group;
        private GameObject hint;
        private float shownTime;
        private float tiltTime;
        private bool showing;

        // Self-bootstrap: spawn one after the scene loads if none was placed by hand, so the hint works with no
        // scene wiring (same approach as the scoring managers and the haptics).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<TiltSteerHint>() != null)
                return;
            var go = new GameObject("TiltSteerHint (auto)");
            go.AddComponent<TiltSteerHint>();
            DontDestroyOnLoad(go);
        }

        private void Awake() => BuildUI();

        private void OnEnable()
        {
            GameEvents.SessionStarted += Show;
            GameEvents.SessionReset += BeginHide;
        }

        private void OnDisable()
        {
            GameEvents.SessionStarted -= Show;
            GameEvents.SessionReset -= BeginHide;
        }

        private void Show()
        {
            // Off entirely when the facilitator switched the tutorial off ("Uitleg tonen"). Otherwise, on the very
            // first turn ever the fuller HowToPlayOverlay card teaches steering (and gas/brake), so this lighter
            // per-turn nudge stands down for that one turn to avoid two overlapping hints.
            if (!HowToPlayOverlay.TutorialEnabled || HowToPlayOverlay.PendingFirstRun)
                return;
            showing = true;
            shownTime = 0f;
            tiltTime = 0f;
            if (hint != null) hint.SetActive(true);
        }

        private void BeginHide() => showing = false;

        private void Update()
        {
            if (group == null)
                return;

            if (showing)
            {
                shownTime += Time.unscaledDeltaTime;
                float lat = ScooterInputRouter.Instance != null ? Mathf.Abs(ScooterInputRouter.Instance.Lateral) : 0f;
                tiltTime = lat > tiltThreshold ? tiltTime + Time.unscaledDeltaTime : 0f;
                if (tiltTime >= tiltHoldToDismiss || shownTime >= maxShowSeconds)
                    showing = false; // they tilted (or timed out) — they have it now
            }

            float target = showing ? 1f : 0f;
            group.alpha = Mathf.MoveTowards(group.alpha, target, fadeSpeed * Time.unscaledDeltaTime);
            if (!showing && group.alpha <= 0.01f && hint != null && hint.activeSelf)
                hint.SetActive(false);
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("TiltHintCanvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500; // above the HUD
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(2048f, 1536f); // iPad 4:3
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            hint = new GameObject("Hint", typeof(RectTransform));
            hint.transform.SetParent(canvasGO.transform, false);
            group = hint.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false; // never eats the player's touches
            var prt = (RectTransform)hint.transform; // created with a RectTransform above (a bare GameObject + CanvasGroup has none)
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.62f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(1000f, 200f);

            // Soft shadow so the nudge floats above the road as a rounded capsule, not a flat bar.
            var shadow = new GameObject("Shadow", typeof(RectTransform));
            shadow.transform.SetParent(hint.transform, false);
            var shImg = shadow.AddComponent<Image>();
            shImg.sprite = UiKit.SoftShadow(UiKit.RadiusXl); shImg.type = Image.Type.Sliced;
            shImg.color = UiKit.ShadowColour(0.40f); shImg.raycastTarget = false;
            var shrt = (RectTransform)shadow.transform;
            shrt.anchorMin = Vector2.zero; shrt.anchorMax = Vector2.one; shrt.offsetMin = new Vector2(-22f, -30f); shrt.offsetMax = new Vector2(22f, 10f);

            var bg = new GameObject("BG", typeof(RectTransform));
            bg.transform.SetParent(hint.transform, false);
            var bgImg = bg.AddComponent<Image>();
            bgImg.sprite = UiKit.Rounded(UiKit.RadiusXl); bgImg.type = Image.Type.Sliced;
            bgImg.color = new Color(0.08f, 0.06f, 0.05f, 0.80f);
            bgImg.raycastTarget = false;
            var brt = bg.GetComponent<RectTransform>();
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;

            var labelGO = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(hint.transform, false);
            var tmp = labelGO.AddComponent<TextMeshProUGUI>();
            tmp.text = "<color=#F19141>◄</color>    " + message + "    <color=#F19141>►</color>"; // accent arrows around the message
            tmp.fontSize = 70f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.characterSpacing = 4f;
            tmp.color = UiKit.Ink;
            tmp.raycastTarget = false;
            var lrt = labelGO.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;

            group.alpha = 0f;
            hint.SetActive(false);
        }
    }
}
