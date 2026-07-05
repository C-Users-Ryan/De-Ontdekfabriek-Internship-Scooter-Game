using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KenyaScooter.Core;
using KenyaScooter.Config;
using KenyaScooter.Traffic;

namespace KenyaScooter.UI
{
    /// <summary>
    /// Per-turn OBJECTIVE cue. The game taught players HOW to drive (HowToPlayOverlay / TiltSteerHint) but never
    /// WHAT to do — the goal (overtake cleanly, on the legal side) was only revealed reactively, after a player
    /// happened to pass their first car and saw the "+150 INGEHAALD!" popup. A cautious kid who just stays in lane
    /// never discovers there is a goal at all. This shows a short "HAAL VEILIG IN!" banner at the start of EVERY
    /// turn, stating the objective the instant the player takes control, then fades. It clears early the moment the
    /// player completes their first clean overtake — once they've done it, the goal is self-evident.
    ///
    /// Unlike <see cref="TiltSteerHint"/>, this does NOT stand down on the very first turn: the HowToPlayOverlay
    /// card that owns that turn teaches the controls but not the objective, so the two are complementary, not
    /// duplicate — and they sit in different screen regions (this banner up top, the card centred).
    ///
    /// The subline names the legal overtaking side from the same source of truth the corrective banner uses
    /// (<see cref="RoadSideConfig"/>), refreshed each turn so the Kenya/Netherlands drive-side toggle is honoured.
    /// It builds its own overlay and self-bootstraps, so it needs no scene wiring and is a no-op if nothing raises
    /// the session events.
    /// </summary>
    public sealed class ObjectiveCue : MonoBehaviour
    {
        [SerializeField] private string headline = "HAAL VEILIG IN!";
        [Tooltip("Clear the cue after this long even if the player never overtakes.")]
        [SerializeField] private float maxShowSeconds = 2.8f;
        [SerializeField] private float fadeSpeed = 4f;

        private CanvasGroup group;
        private GameObject banner;
        private TMP_Text head;
        private TMP_Text sub;
        private float shownTime;
        private bool showing;

        // Self-bootstrap: spawn one after the scene loads if none was placed by hand (same approach as
        // TiltSteerHint, the scoring managers and the haptics), so it works with zero scene wiring.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<ObjectiveCue>() != null)
                return;
            var go = new GameObject("ObjectiveCue (auto)");
            go.AddComponent<ObjectiveCue>();
            DontDestroyOnLoad(go);
        }

        private void Awake() => BuildUI();

        private void OnEnable()
        {
            GameEvents.SessionStarted += Show;
            GameEvents.SessionReset += BeginHide;
            GameEvents.OvertakeCompleted += OnOvertake;
        }

        private void OnDisable()
        {
            GameEvents.SessionStarted -= Show;
            GameEvents.SessionReset -= BeginHide;
            GameEvents.OvertakeCompleted -= OnOvertake;
        }

        private void Show()
        {
            // Name the legal overtaking side fresh each turn (the drive-side toggle may have changed it). Kenya
            // drives left → overtake right; the Netherlands drives right → overtake left — toward the oncoming lane,
            // matching DiegeticHud's corrective banner so the two never disagree.
            bool driveLeft = RoadSideConfig.Active == null || RoadSideConfig.Active.driveOnLeft;
            if (sub != null) sub.text = "INHALEN DOET U " + (driveLeft ? "RECHTS" : "LINKS");

            // Point the chevrons TOWARD the legal overtaking side so they guide the player, not just decorate.
            // Kenya drives left → overtake right (►►); the Netherlands drives right → overtake left (◄◄).
            if (head != null)
            {
                string chev = driveLeft ? "►" : "◄"; // both glyphs proven to render (see TiltSteerHint)
                head.text = "<color=#F19141>" + chev + "</color>  " + headline + "  <color=#F19141>" + chev + "</color>";
            }

            showing = true;
            shownTime = 0f;
            if (banner != null) banner.SetActive(true);
        }

        private void BeginHide() => showing = false;

        // First clean overtake of the turn — they clearly get the goal now, so retire the cue.
        private void OnOvertake(TrafficVehicle _) { if (showing) showing = false; }

        private void Update()
        {
            if (group == null)
                return;

            if (showing)
            {
                shownTime += Time.unscaledDeltaTime;
                if (shownTime >= maxShowSeconds)
                    showing = false;
            }

            float target = showing ? 1f : 0f;
            group.alpha = Mathf.MoveTowards(group.alpha, target, fadeSpeed * Time.unscaledDeltaTime);
            if (!showing && group.alpha <= 0.01f && banner != null && banner.activeSelf)
                banner.SetActive(false);
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("ObjectiveCueCanvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 520; // above the HUD and the tilt hint, below the first-run how-to card (550)
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(2048f, 1536f); // iPad 4:3
            scaler.matchWidthOrHeight = 0.5f;
            // No GraphicRaycaster: the banner must never intercept the touches that drive the scooter.

            banner = new GameObject("Banner", typeof(RectTransform));
            banner.transform.SetParent(canvasGO.transform, false);
            group = banner.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            var prt = (RectTransform)banner.transform;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.85f); // top-centre, clear of the centred how-to card / tilt hint
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(1180f, 220f);

            // Soft shadow so the cue floats above the road as a rounded capsule, not a flat bar.
            var shadow = new GameObject("Shadow", typeof(RectTransform));
            shadow.transform.SetParent(banner.transform, false);
            var shImg = shadow.AddComponent<Image>();
            shImg.sprite = UiKit.SoftShadow(UiKit.RadiusXl); shImg.type = Image.Type.Sliced;
            shImg.color = UiKit.ShadowColour(0.40f); shImg.raycastTarget = false;
            var shrt = (RectTransform)shadow.transform;
            shrt.anchorMin = Vector2.zero; shrt.anchorMax = Vector2.one; shrt.offsetMin = new Vector2(-22f, -30f); shrt.offsetMax = new Vector2(22f, 10f);

            var bg = new GameObject("BG", typeof(RectTransform));
            bg.transform.SetParent(banner.transform, false);
            var bgImg = bg.AddComponent<Image>();
            bgImg.sprite = UiKit.Rounded(UiKit.RadiusXl); bgImg.type = Image.Type.Sliced;
            bgImg.color = new Color(0.08f, 0.06f, 0.05f, 0.80f);
            bgImg.raycastTarget = false;
            var brt = bg.GetComponent<RectTransform>();
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;

            // Headline: the objective, with accent chevrons that read as forward passing/overtaking.
            var headGO = new GameObject("Headline", typeof(RectTransform));
            headGO.transform.SetParent(banner.transform, false);
            head = headGO.AddComponent<TextMeshProUGUI>();
            // Initial text; Show() re-points the chevrons toward the legal overtaking side each turn.
            head.text = "<color=#F19141>►</color>  " + headline + "  <color=#F19141>►</color>";
            head.fontSize = 76f;
            head.fontStyle = FontStyles.Bold;
            head.alignment = TextAlignmentOptions.Center;
            head.characterSpacing = 4f;
            head.color = UiKit.Ink;
            head.raycastTarget = false;
            var hrt = headGO.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0f, 0f); hrt.anchorMax = new Vector2(1f, 1f);
            hrt.offsetMin = new Vector2(20f, 56f); hrt.offsetMax = new Vector2(-20f, -16f);

            // Subline: the legal overtaking side (set per turn in Show()).
            var subGO = new GameObject("Subline", typeof(RectTransform));
            subGO.transform.SetParent(banner.transform, false);
            sub = subGO.AddComponent<TextMeshProUGUI>();
            sub.text = "INHALEN DOET U RECHTS";
            sub.fontSize = 36f;
            sub.fontStyle = FontStyles.Bold;
            sub.alignment = TextAlignmentOptions.Center;
            sub.characterSpacing = 6f;
            sub.color = UiKit.Accent;
            sub.raycastTarget = false;
            var srt = subGO.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0f, 0f); srt.anchorMax = new Vector2(1f, 0f);
            srt.pivot = new Vector2(0.5f, 0f);
            srt.sizeDelta = new Vector2(-40f, 52f); srt.anchoredPosition = new Vector2(0f, 24f);

            group.alpha = 0f;
            banner.SetActive(false);
        }
    }
}
