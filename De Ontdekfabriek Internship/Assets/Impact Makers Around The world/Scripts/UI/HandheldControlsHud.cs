using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KenyaScooter.Core;
using KenyaScooter.Controls;

namespace KenyaScooter.UI
{
    /// <summary>
    /// On-screen GAS (right) and BRAKE (left) pedals for handheld play, asked for at the 25 June 2026 oplevering:
    /// when the tablet is out of its holder a player has no idea where to press for gas and brake. This draws a
    /// clear pedal in each bottom corner — GAS bottom-right, REM bottom-left — matching the existing
    /// touch layout (right half = gas, left half = brake in <see cref="TouchZoneProvider"/> and DesktopProvider).
    ///
    /// It is a HINT, not a new input path. The whole right/left half of the screen is already the live touch zone,
    /// read straight off the raw Touchscreen device by TouchZoneProvider (which the EventSystem never intercepts),
    /// so pressing the pedal already registers as gas/brake. This component only shows WHERE to press and lights
    /// the pedal up from <see cref="ScooterInputRouter.Gas"/>/<see cref="ScooterInputRouter.Brake"/>, so it stays
    /// non-interactive (it can never eat a touch) and there is no risk of double-counting input.
    ///
    /// Visibility is driven by <see cref="HandheldDetector"/>: the pedals appear only while the tablet is handheld
    /// (or the facilitator forced them on) AND a turn is actually being driven. Docked in the holder, the screen
    /// stays clean. Self-bootstraps after the scene loads, so it needs no scene wiring, and is a silent no-op if
    /// nothing is driving (no router / not Playing).
    ///
    /// Look = design round 1, option 1b ("Opus-concept, op het merk gebracht", 2026-07-05): each pedal is a thin
    /// circular ring with the glyph inside and the label underneath — clay-red ring + solid stop-octagon for REM
    /// (#E0683C), brand-orange ring + double speed chevrons for GAS (#F19141; the second chevron is baked lighter
    /// in the sprite). Pressing fills the circle with the pedal colour, turns the glyph cream and adds a soft
    /// coloured glow; nothing scales, per the design. The mock sits on a light card but the game shows the pedals
    /// over the moving world, so a faint dark scrim (restScrimAlpha) backs the ring at rest for legibility — set
    /// it to 0 for the exact design look.
    ///
    /// Icon resolution order per pedal:
    ///   1. the Inspector fields below (hand-placed instance only),
    ///   2. Resources/UI/PedalIcons/gas.png + brake.png (works with the auto-bootstrap — the chosen 1b pair,
    ///      icon_gas_chevrons + icon_brake_octagon from the icon pack, ships there),
    ///   3. the original procedural triangle (so nothing breaks when no sprites exist).
    /// The pack's sprites are pure white; the tint logic colours them (pedal colour idle, cream pressed).
    /// </summary>
    public sealed class HandheldControlsHud : MonoBehaviour
    {
        [SerializeField] private float circleDiameter = 240f;  // the mock's 144px ring at HUD scale (2048-wide ref)
        [SerializeField] private float ringThickness = 5f;     // mock: 3px on a 144 circle
        [SerializeField] private float sideInset = 70f;
        [SerializeField] private float bottomInset = 70f;
        [SerializeField] private float fadeSpeed = 5f;
        [Range(0f, 1f)]
        [SerializeField] private float restScrimAlpha = 0.32f; // dark backing at rest; 0 = the exact 1b design

        [Header("Icon sprites (optional — else Resources/UI/PedalIcons/{gas,brake}, else procedural triangle)")]
        [SerializeField] private Sprite gasIconSprite;
        [SerializeField] private Sprite brakeIconSprite;

        private const string GasIconResource = "UI/PedalIcons/gas";
        private const string BrakeIconResource = "UI/PedalIcons/brake";

        private readonly Color gasColour   = Hex("#F19141");      // brand orange — 1b moves gas off the green
        private readonly Color brakeColour = Hex("#E0683C");      // warm clay-red (distinct from the alert danger red)
        private readonly Color faceColour  = UiKit.SurfaceBase;   // the HUD panel's dark face #1A1310 (rest scrim)
        private readonly Color ink         = UiKit.Ink;           // #FFF6EC

        private CanvasGroup group;
        private Image gasDisc, brakeDisc, gasIcon, brakeIcon, gasGlow, brakeGlow;
        private float gasLit, brakeLit;

        // Self-bootstrap: spawn one after the scene loads if none was placed by hand (same approach as
        // TiltSteerHint, the scoring managers and the haptics), so it works with zero scene setup.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<HandheldControlsHud>() != null)
                return;
            var go = new GameObject("HandheldControlsHud (auto)");
            go.AddComponent<HandheldControlsHud>();
            DontDestroyOnLoad(go);
        }

        private void Awake() => BuildUI();

        private void Update()
        {
            if (group == null)
                return;

            HandheldDetector.Tick();

            var router = ScooterInputRouter.Instance;
            float gas = router != null ? router.Gas : 0f;
            float brake = router != null ? router.Brake : 0f;

            bool show = HandheldDetector.ShowControls && GameManager.State == GameState.Playing;
            group.alpha = Mathf.MoveTowards(group.alpha, show ? 1f : 0f, fadeSpeed * Time.unscaledDeltaTime);

            // Press feedback (1b): the circle fills with the pedal colour, the glyph goes cream, a glow blooms.
            gasLit = Mathf.MoveTowards(gasLit, gas > 0.5f ? 1f : 0f, 9f * Time.deltaTime);
            brakeLit = Mathf.MoveTowards(brakeLit, brake > 0.5f ? 1f : 0f, 9f * Time.deltaTime);
            ApplyPressed(gasDisc, gasIcon, gasGlow, gasColour, gasLit);
            ApplyPressed(brakeDisc, brakeIcon, brakeGlow, brakeColour, brakeLit);
        }

        private void ApplyPressed(Image disc, Image icon, Image glow, Color colour, float lit)
        {
            if (disc == null) return;
            Color rest = faceColour; rest.a = restScrimAlpha;
            disc.color = Color.Lerp(rest, colour, lit);   // scrim at rest → solid colour fill when pressed
            icon.color = Color.Lerp(colour, ink, lit);    // glyph: pedal colour → cream
            Color halo = colour; halo.a = 0.6f * lit;
            glow.color = halo;
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("HandheldControlsCanvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50; // above the HUD cluster, below the framing screens (which only show when not Playing)
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(2048f, 1536f); // iPad 4:3 reference, like the rest of the UI
            scaler.matchWidthOrHeight = 0.5f;
            // No GraphicRaycaster: the pedals are a hint, never an input target — the raw touch zones do the input.

            var rootGO = new GameObject("Pedals", typeof(RectTransform));
            rootGO.transform.SetParent(canvasGO.transform, false);
            group = rootGO.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false; // never eats the player's touches
            group.alpha = 0f;
            var rootRT = (RectTransform)rootGO.transform;
            Stretch(rootRT);

            // Resolve the icon sprites once — Inspector field first, then Resources, then the old triangle.
            Sprite brakeSprite = ResolveIconSprite(brakeIconSprite, BrakeIconResource, false);
            Sprite gasSprite   = ResolveIconSprite(gasIconSprite,   GasIconResource,   true);

            // Brake — bottom-left, clay-red ring with the solid stop-octagon (mock glyph: 60px on the 144 circle).
            BuildPedal(rootRT, "BrakePedal", "REM", brakeColour, brakeSprite, 100f,
                new Vector2(0f, 0f), new Vector2(sideInset, bottomInset), out brakeDisc, out brakeIcon, out brakeGlow);

            // Gas — bottom-right, brand-orange ring with the double speed chevrons (mock glyph: 64px).
            BuildPedal(rootRT, "GasPedal", "GAS", gasColour, gasSprite, 107f,
                new Vector2(1f, 0f), new Vector2(-sideInset, bottomInset), out gasDisc, out gasIcon, out gasGlow);
        }

        // Inspector sprite → Resources (Assets/Resources/UI/PedalIcons/gas.png + brake.png) → triangle.
        private Sprite ResolveIconSprite(Sprite assigned, string resourcesPath, bool triangleUp)
        {
            if (assigned != null) return assigned;
            Sprite loaded = Resources.Load<Sprite>(resourcesPath);
            return loaded != null ? loaded : TriangleSprite(triangleUp);
        }

        private void BuildPedal(RectTransform parent, string name, string label, Color colour, Sprite iconSprite,
                                float iconSize, Vector2 corner, Vector2 inset,
                                out Image disc, out Image icon, out Image glow)
        {
            const float labelGap = 22f;    // mock: 13px under the 144 circle
            const float labelHeight = 44f;

            RectTransform pedal = NewRect(parent, name);
            pedal.anchorMin = pedal.anchorMax = corner;
            pedal.pivot = corner;
            pedal.sizeDelta = new Vector2(circleDiameter, circleDiameter + labelGap + labelHeight);
            pedal.anchoredPosition = inset;

            // The ring circle at the top of the pedal; the label hangs underneath, like the mock's uitlegkaart.
            RectTransform circle = NewRect(pedal, "Circle");
            Anchor(circle, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(circleDiameter, circleDiameter), new Vector2(0f, -circleDiameter * 0.5f));

            // Press glow behind everything (clear at rest), nudged down like the mock's coloured drop glow.
            glow = AddImage(circle, "Glow", Color.clear, RadialGlowSprite());
            Anchor(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(circleDiameter + 96f, circleDiameter + 96f), new Vector2(0f, -10f));

            // The fill disc: faint dark scrim at rest (readability over the world), pedal colour when pressed.
            Color rest = faceColour; rest.a = restScrimAlpha;
            disc = AddImage(circle, "Disc", rest, CircleSprite());
            Stretch(disc.rectTransform);

            Image rim = AddImage(circle, "Ring", colour, RingSprite());
            Stretch(rim.rectTransform);

            icon = AddImage(circle, "Icon", colour, iconSprite);
            icon.preserveAspect = true; // pack glyphs are square but safe for any ratio
            Anchor(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(iconSize, iconSize), Vector2.zero);

            // Label in the pedal colour, wide-tracked, under the circle (mock: Oswald 19px, 0.3em letterspacing).
            TMP_Text t = AddText(pedal, "Label", label, 32, colour, TextAlignmentOptions.Center);
            Anchor(t.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(circleDiameter + 40f, labelHeight), new Vector2(0f, labelHeight * 0.5f));
            t.characterSpacing = 10f;
        }

        // ---- UI helpers (kept local so the component is self-contained, matching DiegeticHud's style) ----
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

        // ---- procedural sprites (instance-level, like DiegeticHud: a static Sprite cache can dangle after an
        // editor domain reload) ------------------------------------------------------
        private Sprite _triUp, _triDown, _circle, _ring, _radialGlow;

        // A filled, antialiased white disc — the pedal's fill (scrim at rest, solid colour when pressed).
        private Sprite CircleSprite()
        {
            if (_circle != null) return _circle;
            int s = 128; var tex = NewTex(s, s);
            float r = s * 0.5f - 1.5f;
            Vector2 c = new Vector2((s - 1) * 0.5f, (s - 1) * 0.5f);
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c);
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(r - d + 0.5f)));
            }
            tex.Apply();
            _circle = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
            return _circle;
        }

        // The thin circular outline; on-screen thickness follows ringThickness/circleDiameter.
        private Sprite RingSprite()
        {
            if (_ring != null) return _ring;
            int s = 256; var tex = NewTex(s, s);
            float outer = s * 0.5f - 1.5f;
            float inner = outer - Mathf.Max(2f, s * ringThickness / Mathf.Max(1f, circleDiameter));
            Vector2 c = new Vector2((s - 1) * 0.5f, (s - 1) * 0.5f);
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c);
                float a = Mathf.Clamp01(outer - d + 0.5f) * Mathf.Clamp01(d - inner + 0.5f);
                tex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
            tex.Apply();
            _ring = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
            return _ring;
        }

        // A soft radial falloff for the pressed glow (the mock's coloured drop shadow).
        private Sprite RadialGlowSprite()
        {
            if (_radialGlow != null) return _radialGlow;
            int s = 96; var tex = NewTex(s, s);
            float r = s * 0.5f - 1f;
            Vector2 c = new Vector2((s - 1) * 0.5f, (s - 1) * 0.5f);
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c);
                float a = Mathf.Clamp01(1f - d / r); a *= a;
                tex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
            tex.Apply();
            _radialGlow = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
            return _radialGlow;
        }

        private Sprite TriangleSprite(bool up)
        {
            if (up && _triUp != null) return _triUp;
            if (!up && _triDown != null) return _triDown;
            int s = 64; var tex = NewTex(s, s);
            // Up: apex top-centre, base along the bottom. Down: mirror vertically.
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

        private static Texture2D NewTex(int w, int h)
        { return new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp }; }
    }
}
