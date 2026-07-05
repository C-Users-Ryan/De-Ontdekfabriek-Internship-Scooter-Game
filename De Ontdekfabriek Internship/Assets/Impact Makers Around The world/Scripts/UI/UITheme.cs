using UnityEngine;
using TMPro;

namespace KenyaScooter.UI
{
    /// <summary>
    /// The UI's COLOUR palette in ONE asset (TATOE · Ugani & Jump Energy) and the single source of truth for it.
    /// Screens don't read this directly — they read <see cref="UiKit"/>, which reads the active theme's colours
    /// live — so recolouring for a future group (a new location, SC4) is a single asset swap that reaches every
    /// screen with no code edits. The colour defaults are the Ugani palette straight from the Huisstijl analysis,
    /// so with no theme assigned the game looks exactly as it does today.
    ///
    /// What a theme asset actually controls today, so nobody expects more than it does:
    ///   • the full colour palette, EVERYWHERE, via UiKit (every framing screen, the HUD, the settings menu);
    ///   • on the <see cref="ThemedEndScreen"/> only: the display/body fonts, the overlay dim and the primary
    ///     button height, plus its own type sizes (that one screen builds itself straight from this asset).
    /// The type scale, corner radii and spacing rhythm for every OTHER screen are structural compile-time
    /// constants in <see cref="UiKit"/> by design (they are layout, not brand) — editing this asset does not
    /// move them. So the few sizing fields here are the end screen's private scale, not a global type ramp.
    ///
    /// To reskin: Assets &gt; Create &gt; Kenya Scooter &gt; UI Theme, recolour it, and drop it in any folder
    /// named "Resources" as "UITheme". It is picked up automatically on startup — the same drop-in convention
    /// <see cref="SwahiliUI"/> uses for <see cref="UIStringsConfig"/>. (Two assets, zero code, recolours the game.)
    /// </summary>
    [CreateAssetMenu(menuName = "Kenya Scooter/UI Theme", fileName = "UITheme_Ugani")]
    public sealed class UITheme : ScriptableObject
    {
        /// <summary>The active skin every UI script reads (via UiKit). Set from a Resources asset at startup,
        /// or assigned in code; null means UiKit falls back to the canonical Ugani values baked into it.</summary>
        public static UITheme Active { get; set; }

        /// <summary>
        /// Auto-load a theme named "UITheme" from any Resources folder before the first scene, mirroring how
        /// SwahiliUI loads UIStringsConfig. A future group drops in one asset and the whole UI reskins.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoLoad()
        {
            var found = Resources.Load<UITheme>("UITheme");
            if (found != null)
                Active = found;
        }

        [Header("Brand / chrome")]
        public Color accent = new Color32(0xF1, 0x91, 0x41, 0xFF);      // #f19141 signature orange
        public Color accentSoft = new Color32(0xF6, 0xA9, 0x49, 0xFF);  // #F6A949 lifted accent for gradients / glows
        public Color gold = new Color32(0xD0, 0xB8, 0x5B, 0xFF);        // #d0b85b
        public Color burnt = new Color32(0xC4, 0x54, 0x16, 0xFF);       // #c45416
        public Color rust = new Color32(0xAE, 0x5A, 0x1C, 0xFF);        // #ae5a1c (panel hairline)
        public Color oxblood = new Color32(0x77, 0x16, 0x13, 0xFF);     // #771613
        public Color surfaceBase = new Color32(0x1A, 0x13, 0x10, 0xFF); // #1A1310 panel fill
        public Color surfaceRaise = new Color32(0x2A, 0x1E, 0x18, 0xFF);// #2A1E18 raised cards
        public Color surfaceSunk = new Color32(0x0E, 0x0A, 0x08, 0xFF); // #0E0A08 recessed wells (odometer window)
        public Color inkPrimary = new Color32(0xFF, 0xF6, 0xEC, 0xFF);  // #FFF6EC warm white
        public Color inkMuted = new Color32(0xC9, 0xB7, 0xA6, 0xFF);    // #C9B7A6 captions
        public Color inkOnLight = new Color32(0x7A, 0x2E, 0x10, 0xFF);  // #7A2E10 dark warm-brown text on a cream face
        public Color inkOnAccent = new Color32(0x3A, 0x14, 0x08, 0xFF); // #3A1408 near-black warm on an orange fill

        [Header("Functional state (always paired with an icon/shape, never colour alone)")]
        public Color success = new Color32(0x67, 0xB4, 0x4E, 0xFF);     // #67B44E
        public Color caution = new Color32(0xF5, 0xB4, 0x3C, 0xFF);     // #F5B43C
        public Color danger = new Color32(0xE5, 0x35, 0x2B, 0xFF);      // #E5352B

        [Header("Type scale used by the themed end screen (px, tablet landscape)")]
        public float displayXl = 104f; // final score numeral
        public float displayL = 64f;   // header
        public float body = 24f;       // breakdown line, button label
        public float caption = 17f;    // subtitle

        [Header("Fonts (leave empty to fall back to the TMP default)")]
        [Tooltip("Display / leaders — BC Barell, or an open stand-in like Anton or Oswald.")]
        public TMP_FontAsset displayFont;
        [Tooltip("Body / UI — Myriad Pro, or the open stand-in Source Sans 3.")]
        public TMP_FontAsset bodyFont;

        [Header("Component tokens used by the themed end screen")]
        public float primaryButtonHeight = 56f;
        [Tooltip("How far the game behind an overlay screen is dimmed toward black.")]
        [Range(0f, 1f)] public float overlayDim = 0.55f;

        public TMP_FontAsset DisplayFontOrDefault => displayFont != null ? displayFont : TMP_Settings.defaultFontAsset;
        public TMP_FontAsset BodyFontOrDefault => bodyFont != null ? bodyFont : TMP_Settings.defaultFontAsset;
    }
}
