using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KenyaScooter.Core;
using KenyaScooter.Controls;

namespace KenyaScooter.UI
{
    /// <summary>
    /// A one-time "how to play" card, shown on the very first turn ever played on this tablet. From the
    /// 2026-06-23 play test a new player did not realise the tablet is TILTED to steer; the same
    /// controls-not-discoverable theme covers the gas/brake the client later asked to surface. This card
    /// teaches all three controls at once — KANTEL = STUREN, RECHTS = GAS, LINKS = REM — for about five
    /// seconds, then fades. It dismisses early the moment the player actually drives, so it never gets in the
    /// way of someone who already understands.
    ///
    /// It complements, and does not duplicate, <see cref="TiltSteerHint"/>: the tilt hint is a light per-turn
    /// nudge for every new player in the relay, while this fuller card appears once to introduce the controls.
    /// On that single first turn the tilt hint stands down (it checks <see cref="PendingFirstRun"/>) so the two
    /// never stack. A facilitator can replay this for a new group via the settings menu (<see cref="ResetFirstRun"/>).
    ///
    /// Self-bootstraps after the scene loads, needs no scene wiring, and is a no-op if nothing raises the
    /// session events.
    /// </summary>
    public sealed class HowToPlayOverlay : MonoBehaviour
    {
        private const string SeenKey = "ksg.howto.seen";

        [SerializeField] private float showSeconds = 5f;
        [SerializeField] private float fadeSpeed = 3f;

        // Set the first time the card is shown this app run, so a second turn in the same run never re-triggers it
        // even before the PlayerPrefs flag is persisted (which happens a frame later, see Update).
        private static bool consumedThisProcess;

        /// <summary>
        /// True while the first-run card is still pending (not yet seen on this tablet). Read by TiltSteerHint so
        /// it can stand down for the one turn this card owns. Deliberately reads ONLY the persisted flag, not the
        /// process flag: during the first SessionStarted dispatch the flag is still unset for every listener
        /// regardless of event order, so the tilt hint reliably suppresses; this card then persists the flag on
        /// the next frame.
        /// </summary>
        public static bool PendingFirstRun => PlayerPrefs.GetInt(SeenKey, 0) == 0;

        /// <summary>Whether the onboarding (this card AND the per-turn TiltSteerHint) is shown at all. A facilitator
        /// can switch it ON for a new group via the "Uitleg tonen" setting (or just "Uitleg opnieuw tonen"). Default
        /// OFF: the build runs as a facilitated relay and most groups are walked through it, so the overlays stay out
        /// of the way unless asked for. Stored in PlayerPrefs like every other override, so it survives a restart.</summary>
        public const string TutorialPrefKey = "ksg.tutorial";
        /// <summary>Default for <see cref="TutorialPrefKey"/> (1 = on, 0 = off). One source of truth so the setting
        /// in SettingsCatalog and this gate can never disagree on the shipped default.</summary>
        public const int TutorialDefault = 0;
        public static bool TutorialEnabled => PlayerPrefs.GetInt(TutorialPrefKey, TutorialDefault) == 1;

        private CanvasGroup group;
        private GameObject card;
        private bool showing;
        private bool persistPending;
        private float shownTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<HowToPlayOverlay>() != null)
                return;
            var go = new GameObject("HowToPlayOverlay (auto)");
            go.AddComponent<HowToPlayOverlay>();
            DontDestroyOnLoad(go);
        }

        /// <summary>One-shot request from "Uitleg opnieuw tonen": show the card at the next turn ONCE, without
        /// permanently switching the tutorial on. (The old behaviour set "Uitleg tonen" to AAN as a side effect,
        /// which silently turned the per-turn tilt hint on for every following turn — play-test 2026-07-05.)</summary>
        private const string OnceKey = "ksg.howto.once";
        private static bool OneShotRequested => PlayerPrefs.GetInt(OnceKey, 0) == 1;

        /// <summary>Facilitator action: show the how-to again at the start of the next turn (for a new group).</summary>
        public static void ResetFirstRun()
        {
            consumedThisProcess = false;
            PlayerPrefs.SetInt(OnceKey, 1);  // one showing, next turn — the "Uitleg tonen" toggle itself is untouched
            PlayerPrefs.DeleteKey(SeenKey);
            PlayerPrefs.Save();
        }

        private void Awake() => BuildUI();

        private void OnEnable()
        {
            GameEvents.SessionStarted += OnSessionStarted;
            GameEvents.SessionReset += BeginHide;
        }

        private void OnDisable()
        {
            GameEvents.SessionStarted -= OnSessionStarted;
            GameEvents.SessionReset -= BeginHide;
        }

        private void OnSessionStarted()
        {
            bool oneShot = OneShotRequested;
            if (consumedThisProcess || !PendingFirstRun || (!TutorialEnabled && !oneShot))
                return;
            if (oneShot) { PlayerPrefs.DeleteKey(OnceKey); PlayerPrefs.Save(); } // consume the single showing
            consumedThisProcess = true;
            persistPending = true;   // write the "seen" flag next frame, after every SessionStarted listener has run
            showing = true;
            shownTime = 0f;
            if (card != null) card.SetActive(true);
        }

        private void BeginHide() => showing = false;

        private void Update()
        {
            if (group == null)
                return;

            if (persistPending)
            {
                persistPending = false;
                PlayerPrefs.SetInt(SeenKey, 1);
                PlayerPrefs.Save();
            }

            if (showing)
            {
                shownTime += Time.unscaledDeltaTime;
                if (shownTime >= showSeconds || PlayerActed())
                    showing = false;
            }

            float target = showing ? 1f : 0f;
            group.alpha = Mathf.MoveTowards(group.alpha, target, fadeSpeed * Time.unscaledDeltaTime);
            if (!showing && group.alpha <= 0.01f && card != null && card.activeSelf)
                card.SetActive(false);
        }

        // They have started driving (tilted enough, or pressed gas/brake) — they clearly get it, so clear the card.
        private bool PlayerActed()
        {
            var r = ScooterInputRouter.Instance;
            if (r == null) return false;
            return Mathf.Abs(r.Lateral) > 0.4f || r.Gas > 0.5f || r.Brake > 0.5f;
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("HowToCanvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 550; // above the HUD, the pedals and the tilt hint
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(2048f, 1536f); // iPad 4:3
            scaler.matchWidthOrHeight = 0.5f;
            // No GraphicRaycaster: the card must never intercept the very touches that drive the scooter.

            // A soft dim scrim behind the card so the lesson reads against any scene, without blocking touches.
            var scrim = new GameObject("Scrim", typeof(RectTransform));
            scrim.transform.SetParent(canvasGO.transform, false);
            var scrimImg = scrim.AddComponent<Image>();
            scrimImg.color = new Color(0f, 0f, 0f, 0.42f); scrimImg.raycastTarget = false;
            var srt = (RectTransform)scrim.transform; srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one; srt.sizeDelta = Vector2.zero;

            card = new GameObject("Card", typeof(RectTransform));
            card.transform.SetParent(canvasGO.transform, false);
            group = card.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            group.alpha = 0f;
            var crt = (RectTransform)card.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(1320f, 575f); // v2: wider card, three tiles side by side

            // Soft drop shadow lifts the card off the scene. It is the FIRST child so it draws behind the body
            // (a parent's own graphic would draw under its children, so the fill lives on a child too, below).
            Image shadow = AddImage(crt, "CardShadow", UiKit.ShadowColour(0.45f), UiKit.SoftShadow(UiKit.RadiusXl));
            shadow.rectTransform.anchorMin = Vector2.zero; shadow.rectTransform.anchorMax = Vector2.one;
            shadow.rectTransform.offsetMin = new Vector2(-26f, -38f); shadow.rectTransform.offsetMax = new Vector2(26f, 14f);

            // Card body per the spec panel: rust hairline with the 95% surface fill 3px inside it.
            Image hair = AddImage(crt, "Hairline", UiKit.WithAlpha(UiKit.Rust, 0.55f), RoundedSprite(UiKit.RadiusXl));
            Stretch(hair.rectTransform);
            Image bg = AddImage(crt, "Body", new Color(0.102f, 0.075f, 0.063f, 0.95f), RoundedSprite(UiKit.RadiusXl));
            bg.rectTransform.anchorMin = Vector2.zero; bg.rectTransform.anchorMax = Vector2.one;
            bg.rectTransform.offsetMin = new Vector2(3f, 3f); bg.rectTransform.offsetMax = new Vector2(-3f, -3f);

            // Lit top edge + the accent tab hugging the card's top edge (v2 header tab).
            Image sheen = AddImage(crt, "Sheen", new Color(1f, 0.95f, 0.88f, 0.06f), UiKit.TopSheen());
            sheen.rectTransform.anchorMin = new Vector2(0f, 1f); sheen.rectTransform.anchorMax = new Vector2(1f, 1f);
            sheen.rectTransform.pivot = new Vector2(0.5f, 1f);
            sheen.rectTransform.offsetMin = new Vector2(12f, -200f); sheen.rectTransform.offsetMax = new Vector2(-12f, -10f);
            Image lip = AddImage(crt, "AccentTab", UiKit.Accent, RoundedSprite(4));
            Anchor(lip.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(320f, 10f), new Vector2(0f, -5f));

            TMP_Text kick = AddText(crt, "Kicker", "TIK · KANTEL · RIJD", 22, Hex("#F2A468"), TextAlignmentOptions.Center);
            Anchor(kick.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(1080f, 30f), new Vector2(0f, -44f));
            UiKit.Caps(kick, 0.20f);

            // v2: the title drops the orange (warm white per the improved design); the tiles carry the colour.
            TMP_Text title = AddText(crt, "Title", "ZO SPEEL JE", 52, UiKit.Ink, TextAlignmentOptions.Center);
            Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(1080f, 66f), new Vector2(0f, -94f));
            UiKit.Caps(title, 0.05f);

            // Three pictorial tiles, readable in any order and mapped like the real touch zones: KANTEL in the
            // middle-left of attention, GAS on the RIGHT tile, REM on the LEFT tile — same sides as the thumbs.
            // Gas/brake keep the HandheldControlsHud pedal colours; "ingedrukt houden" is spelled out.
            Tile(crt, -430f, Hex("#E0683C"), "REM · LINKS",  "Duim links\ningedrukt houden",  2);
            Tile(crt, 0f,    UiKit.Gold,     "KANTEL",       "Kantel de tablet\nom te sturen", 0);
            Tile(crt, 430f,  Hex("#F19141"), "GAS · RECHTS", "Duim rechts\ningedrukt houden", 1);

            // The card says it steps aside on its own — no dismiss button needed.
            TMP_Text foot = AddText(crt, "Footer", "VERDWIJNT ZODRA JE GAAT RIJDEN", 19, Hex("#9A8676"), TextAlignmentOptions.Center);
            Anchor(foot.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(1080f, 26f), new Vector2(0f, -530f));
            UiKit.Caps(foot, 0.14f);
        }

        // One pictorial tile: colour ring + tinted fill, a drawn icon (no font glyph gambles), the control name
        // in its colour, and a two-line plain-language description. glyph: 0 = tilt tablet, 1 = gas ▲▲, 2 = rem ▼▼.
        private void Tile(RectTransform parent, float x, Color colour, string titleText, string desc, int glyph)
        {
            RectTransform tile = NewRect(parent, "Tile");
            Anchor(tile, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(400f, 330f), new Vector2(x, -315f));

            Image ring = AddImage(tile, "Ring", UiKit.WithAlpha(colour, 0.50f), RoundedSprite(UiKit.RadiusL));
            Stretch(ring.rectTransform);
            Color fillCol = Color.Lerp(new Color(0.10f, 0.075f, 0.06f, 1f), colour, 0.10f);
            Image fill = AddImage(tile, "Fill", fillCol, RoundedSprite(UiKit.RadiusL));
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = new Vector2(3f, 3f); fill.rectTransform.offsetMax = new Vector2(-3f, -3f);

            if (glyph == 0)
            {
                // A tilted tablet outline with rocking arrows — the steering gesture as a picture.
                RectTransform icon = NewRect(tile, "Icon");
                Anchor(icon, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(190f, 126f), new Vector2(0f, -92f));
                icon.localRotation = Quaternion.Euler(0f, 0f, 10f);
                Image frame = AddImage(icon, "Frame", colour, RoundedSprite(16));
                Stretch(frame.rectTransform);
                Image inner = AddImage(icon, "Inner", fillCol, RoundedSprite(12));
                inner.rectTransform.anchorMin = Vector2.zero; inner.rectTransform.anchorMax = Vector2.one;
                inner.rectTransform.offsetMin = new Vector2(9f, 9f); inner.rectTransform.offsetMax = new Vector2(-9f, -9f);
                Image la = AddImage(icon, "RockL", colour, TriangleSprite(true));
                Anchor(la.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(32f, 32f), new Vector2(-40f, 0f));
                la.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f);   // points left
                Image ra = AddImage(icon, "RockR", colour, TriangleSprite(true));
                Anchor(ra.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(32f, 32f), new Vector2(40f, 0f));
                ra.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -90f);  // points right
            }
            else
            {
                bool up = glyph == 1;
                // Ring roundel with the pedal's double arrows — solid + faded, so "houden" reads as repetition.
                Image discRing = AddImage(tile, "IconRing", colour, RoundedSprite(62));
                Anchor(discRing.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(124f, 124f), new Vector2(0f, -92f));
                Image discFill = AddImage(tile, "IconFill", Color.Lerp(fillCol, colour, 0.14f), RoundedSprite(56));
                Anchor(discFill.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(112f, 112f), new Vector2(0f, -92f));
                Image a1 = AddImage(tile, "Arrow1", colour, TriangleSprite(up));
                Anchor(a1.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(42f, 38f), new Vector2(0f, up ? -76f : -108f));
                Image a2 = AddImage(tile, "Arrow2", UiKit.WithAlpha(colour, 0.45f), TriangleSprite(up));
                Anchor(a2.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(42f, 38f), new Vector2(0f, up ? -108f : -76f));
            }

            TMP_Text tt = AddText(tile, "Title", titleText, 30, colour, TextAlignmentOptions.Center);
            Anchor(tt.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(370f, 40f), new Vector2(0f, -196f));
            UiKit.Caps(tt, 0.08f);

            TMP_Text d = AddText(tile, "Desc", desc, 21, UiKit.InkMuted, TextAlignmentOptions.Center);
            d.fontStyle = FontStyles.Normal;
            Anchor(d.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(350f, 70f), new Vector2(0f, -258f));
        }

        // ---- helpers -----------------------------------------------------------------
        private static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

        private RectTransform NewRect(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        private Image AddImage(RectTransform parent, string name, Color colour, Sprite sprite)
        {
            var rt = NewRect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = colour; img.sprite = sprite; img.raycastTarget = false;
            if (sprite != null && sprite.border != Vector4.zero) img.type = Image.Type.Sliced;
            return img;
        }

        private TMP_Text AddText(RectTransform parent, string name, string text, float size, Color colour, TextAlignmentOptions align)
        {
            var rt = NewRect(parent, name);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.color = colour; t.alignment = align;
            t.raycastTarget = false; t.fontStyle = FontStyles.Bold;
            return t;
        }

        private static void Stretch(RectTransform rt)
        { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f); rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero; }

        private static void Anchor(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 size, Vector2 pos)
        { rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = size; rt.anchoredPosition = pos; }

        // Solid triangle sprite, same maths as the HandheldControlsHud pedal fallback, so the tile arrows
        // match the pedals exactly and no font glyph coverage is gambled on.
        private Sprite _triUp, _triDown;
        private Sprite TriangleSprite(bool up)
        {
            if (up && _triUp != null) return _triUp;
            if (!up && _triDown != null) return _triDown;
            int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            Vector2 apex = up ? new Vector2(0.5f, 0.88f) : new Vector2(0.5f, 0.12f);
            Vector2 b1 = up ? new Vector2(0.14f, 0.16f) : new Vector2(0.14f, 0.84f);
            Vector2 b2 = up ? new Vector2(0.86f, 0.16f) : new Vector2(0.86f, 0.84f);
            Vector2[] pts = { apex, b1, b2 };
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
            {
                bool inside = PointInPoly(new Vector2((float)x / s, (float)y / s), pts);
                tex.SetPixel(x, y, new Color(1, 1, 1, inside ? 1f : 0f));
            }
            tex.Apply();
            var sp = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
            if (up) _triUp = sp; else _triDown = sp;
            return sp;
        }

        private static bool PointInPoly(Vector2 p, Vector2[] v)
        { bool c = false; for (int i = 0, j = v.Length - 1; i < v.Length; j = i++) if (((v[i].y > p.y) != (v[j].y > p.y)) && (p.x < (v[j].x - v[i].x) * (p.y - v[i].y) / (v[j].y - v[i].y) + v[i].x)) c = !c; return c; }

        private readonly Dictionary<int, Sprite> _rounded = new();
        private Sprite RoundedSprite(int radius)
        {
            if (_rounded.TryGetValue(radius, out var cached)) return cached;
            int s = radius * 2 + 4;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
            {
                float dx = Mathf.Max(radius - x, x - (s - radius), 0f);
                float dy = Mathf.Max(radius - y, y - (s - radius), 0f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(radius - d + 0.5f)));
            }
            tex.Apply();
            var sp = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            _rounded[radius] = sp; return sp;
        }
    }
}
