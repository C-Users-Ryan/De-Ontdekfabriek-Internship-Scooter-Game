using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KenyaScooter.UI
{
    /// <summary>
    /// Shared, additive design tokens for the whole procedural UI (the warm TATOE · Ugani &amp; Jump Energy
    /// house style). Before this, every screen carried its own slightly-different palette and a grab-bag of
    /// magic radii, type sizes and one-off hex strings ("#9c3a12" pasted in five files, accent as both
    /// #F19141 and #F2A055). This centralises the colour roles, a type scale, a corner-radius rhythm and a
    /// spacing rhythm so the restyle is one coherent system instead of scattered numbers, and adds the two
    /// things procedural sprites lacked for depth: a soft DROP SHADOW and a top INNER-HIGHLIGHT, both drawn
    /// procedurally so the no-art-pipeline rule still holds.
    ///
    /// It is deliberately a plain static helper, not a ScriptableObject or MonoBehaviour: no scene wiring, no
    /// serialized reference, nothing to break the existing one-click builders. A screen opts in by reading a
    /// token or calling <see cref="DropShadow"/> / <see cref="InnerHighlight"/>; nothing is forced on it.
    ///
    /// The PALETTE now reads the active <see cref="UITheme"/> live (see the properties below): assign a theme
    /// asset and every screen that reads a UiKit colour reskins with no edits; assign none and the baked
    /// Ugani fallback keeps today's look. This makes the theme asset the single source of truth for colour,
    /// so adjusting the whole UI for a future group is one asset swap. These fallback colours (and a UITheme's
    /// matching colour defaults) are the documented Huisstijl analysis (surface/base #1A1310, accent #f19141,
    /// ink/primary #FFF6EC, the warm ramp, and the functional success/caution/danger set). The type scale,
    /// radius/spacing rhythm and shadow constants stay compile-time here (they are structural, not brand) — a
    /// UITheme does not carry them (its few sizing fields are the themed end screen's own, not a global ramp).
    /// </summary>
    public static class UiKit
    {
        // ---- palette — the active UITheme is the single source of truth --------------
        // Screens keep writing UiKit.Accent, UiKit.Ink, etc. exactly as before; the value now comes LIVE from
        // UITheme.Active when a theme asset is loaded, and otherwise from the canonical Ugani fallback baked in
        // below (from the Huisstijl analysis). So a re-skin for a future group is one asset swap that reaches
        // every screen with no per-screen edits, and with no change to today's look when no theme is assigned.
        private static UITheme Th => UITheme.Active;

        // Brand / chrome
        public static Color Accent      => Th != null ? Th.accent       : _accent;      // signature "Jump Energy" orange
        public static Color AccentSoft  => Th != null ? Th.accentSoft   : _accentSoft;  // a lifted accent for gradients / glows
        public static Color Gold        => Th != null ? Th.gold         : _gold;
        public static Color Burnt       => Th != null ? Th.burnt        : _burnt;
        public static Color Rust        => Th != null ? Th.rust         : _rust;
        public static Color Oxblood     => Th != null ? Th.oxblood      : _oxblood;

        // Warm near-black surfaces (raised cards read lighter than the base, which gives the cluster depth)
        public static Color SurfaceBase => Th != null ? Th.surfaceBase  : _surfaceBase;
        public static Color SurfaceRaise=> Th != null ? Th.surfaceRaise : _surfaceRaise;
        public static Color SurfaceSunk => Th != null ? Th.surfaceSunk  : _surfaceSunk; // recessed wells (the odometer window)

        // Ink
        public static Color Ink         => Th != null ? Th.inkPrimary   : _ink;         // primary warm white
        public static Color InkMuted    => Th != null ? Th.inkMuted     : _inkMuted;    // captions / secondary
        public static Color InkOnLight  => Th != null ? Th.inkOnLight   : _inkOnLight;  // dark warm-brown text on a cream button
        public static Color InkOnAccent => Th != null ? Th.inkOnAccent  : _inkOnAccent; // near-black warm on an orange fill

        // Functional state (always paired with an icon / shape / position, never colour alone)
        public static Color Success     => Th != null ? Th.success      : _success;
        public static Color Caution     => Th != null ? Th.caution      : _caution;
        public static Color Danger      => Th != null ? Th.danger       : _danger;

        // Canonical Ugani fallback — the exact values used before theming existed, kept so "no theme asset"
        // renders identically to today. These are the numbers a UITheme's field defaults also carry.
        static readonly Color _accent      = Hex("#F19141");
        static readonly Color _accentSoft  = Hex("#F6A949");
        static readonly Color _gold        = Hex("#D0B85B");
        static readonly Color _burnt       = Hex("#C45416");
        static readonly Color _rust        = Hex("#AE5A1C");
        static readonly Color _oxblood     = Hex("#771613");
        static readonly Color _surfaceBase = Hex("#1A1310");
        static readonly Color _surfaceRaise= Hex("#2A1E18");
        static readonly Color _surfaceSunk = Hex("#0E0A08");
        static readonly Color _ink         = Hex("#FFF6EC");
        static readonly Color _inkMuted    = Hex("#C9B7A6");
        static readonly Color _inkOnLight  = Hex("#7A2E10");
        static readonly Color _inkOnAccent = Hex("#3A1408");
        static readonly Color _success     = Hex("#67B44E");
        static readonly Color _caution     = Hex("#F5B43C");
        static readonly Color _danger      = Hex("#E5352B");

        // ---- type scale (px, 2048x1536 tablet reference) -----------------------------
        public const float DisplayXl = 120f; // title, final score numeral
        public const float DisplayL  = 64f;  // screen headers, live score
        public const float Heading   = 36f;  // streak ×, day label, section heads
        public const float Body      = 24f;  // labels, rows
        public const float Caption   = 18f;  // secondary captions
        public const float Micro     = 15f;  // tiny instrument sub-labels

        /// <summary>Tracking (letter-spacing) for ALL-CAPS labels, the house style for kickers and chips.</summary>
        public const float CapsTracking = 0.14f;

        // ---- radius rhythm (a small ladder, not ad-hoc values) -----------------------
        public const int RadiusXl = 30; // big panels / cards
        public const int RadiusL  = 24; // standard panel
        public const int RadiusM  = 18; // rows, chips
        public const int RadiusS  = 12; // small chips, inset displays

        // ---- spacing rhythm (4px base) -----------------------------------------------
        public const float Space1 = 8f;
        public const float Space2 = 16f;
        public const float Space3 = 24f;
        public const float Space4 = 32f;

        // ---- elevation / shadow ------------------------------------------------------
        /// <summary>The soft shadow's downward offset and how far it bleeds, used by callers placing a shadow.</summary>
        public const float ShadowDrop = 10f;
        public const float ShadowGrow = 26f;
        public static Color ShadowColour(float strength) => new Color(0f, 0f, 0f, Mathf.Clamp01(strength));

        static UiKit() { }

        public static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

        /// <summary>A colour with a replaced alpha (cheap, allocation-free).</summary>
        public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        // ---- procedural sprites (cached) ---------------------------------------------
        private static readonly Dictionary<int, Sprite> _rounded = new();
        private static readonly Dictionary<int, Sprite> _shadow = new();
        private static Sprite _topSheen;

        /// <summary>A 9-sliced rounded-rect, identical maths to the per-file ones but shared and cached.</summary>
        public static Sprite Rounded(int radius)
        {
            if (_rounded.TryGetValue(radius, out var c)) return c;
            int s = radius * 2 + 4;
            var tex = NewTex(s, s);
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
            {
                float dx = Mathf.Max(radius - x, x - (s - radius), 0f);
                float dy = Mathf.Max(radius - y, y - (s - radius), 0f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(radius - d + 0.5f)));
            }
            tex.Apply();
            var sp = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            _rounded[radius] = sp; return sp;
        }

        /// <summary>
        /// A soft, rounded drop-shadow sprite: opaque in the middle, feathering to transparent over a margin,
        /// 9-sliced so it stretches under any panel. Place it behind a panel, a little larger and nudged down,
        /// tinted with <see cref="ShadowColour"/>, to lift the panel off the background.
        /// </summary>
        public static Sprite SoftShadow(int radius)
        {
            if (_shadow.TryGetValue(radius, out var c)) return c;
            int feather = 18;
            int s = (radius + feather) * 2 + 4;
            var tex = NewTex(s, s);
            int inner = radius + feather; // centre stays solid up to the rounded inner box
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
            {
                float dx = Mathf.Max(radius + feather - x, x - (s - radius - feather), 0f);
                float dy = Mathf.Max(radius + feather - y, y - (s - radius - feather), 0f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                // d=0 fully solid, fading to 0 across the feather band
                float a = 1f - Mathf.Clamp01(d / feather);
                a = a * a; // ease so the falloff looks soft, not linear
                tex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
            tex.Apply();
            var sp = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(inner, inner, inner, inner));
            _shadow[radius] = sp; return sp;
        }

        /// <summary>
        /// A top-down sheen: a soft white gradient strongest at the top edge, fading to nothing by mid-height.
        /// Laid over a panel at a low alpha it reads as a subtle lit top edge (a gentle bevel), the cheap trick
        /// that makes a flat fill look like a moulded surface. Vertical gradient, stretched to the panel width.
        /// </summary>
        public static Sprite TopSheen()
        {
            if (_topSheen != null) return _topSheen;
            int h = 64;
            var tex = new Texture2D(2, h, TextureFormat.RGBA32, false)
            { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < h; y++)
            {
                float t = y / (float)(h - 1);          // 0 bottom → 1 top
                float a = Mathf.SmoothStep(0f, 1f, t);  // strong at the top, gone at the bottom
                a *= a;
                var col = new Color(1, 1, 1, a);
                tex.SetPixel(0, y, col); tex.SetPixel(1, y, col);
            }
            tex.Apply();
            _topSheen = Sprite.Create(tex, new Rect(0, 0, 2, h), new Vector2(0.5f, 0.5f), 100f);
            return _topSheen;
        }

        /// <summary>Convenience: drop a soft shadow image as the first child behind a panel rect.</summary>
        public static Image AddDropShadow(RectTransform panel, int radius, float strength = 0.35f,
                                          float grow = ShadowGrow, float drop = ShadowDrop)
        {
            var go = new GameObject("Shadow", typeof(RectTransform));
            go.layer = panel.gameObject.layer;
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(panel.parent, false);
            rt.SetSiblingIndex(panel.GetSiblingIndex()); // sits directly behind the panel
            rt.anchorMin = panel.anchorMin; rt.anchorMax = panel.anchorMax; rt.pivot = panel.pivot;
            rt.sizeDelta = panel.sizeDelta + new Vector2(grow, grow);
            rt.anchoredPosition = panel.anchoredPosition + new Vector2(0f, -drop);
            var img = go.AddComponent<Image>();
            img.sprite = SoftShadow(radius); img.type = Image.Type.Sliced;
            img.color = ShadowColour(strength); img.raycastTarget = false;
            return img;
        }

        private static Texture2D NewTex(int w, int h) =>
            new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };

        /// <summary>Apply the house caps-tracking to a label in one call.</summary>
        public static void Caps(TMP_Text t, float tracking = CapsTracking) => t.characterSpacing = tracking * 100f;
    }
}
