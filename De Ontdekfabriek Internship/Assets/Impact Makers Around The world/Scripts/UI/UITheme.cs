using UnityEngine;
using TMPro;

namespace KenyaScooter.UI
{
    /// <summary>
    /// The game's whole look in one asset (TATOE · Ugani & Jump Energy). Every screen reads its colours,
    /// text sizes and panel tokens from here instead of hardcoding them, so the UI stays on-brand and a
    /// re-skin (a new location, SC4) is a single asset swap. The defaults are the Ugani palette straight
    /// from the Huisstijl analysis.
    /// </summary>
    [CreateAssetMenu(menuName = "Kenya Scooter/UI Theme", fileName = "UITheme_Ugani")]
    public sealed class UITheme : ScriptableObject
    {
        /// <summary>Set once at startup so any UI script can read the active theme without a serialized reference.</summary>
        public static UITheme Active { get; set; }

        [Header("Brand / chrome")]
        public Color accent = new Color32(0xF1, 0x91, 0x41, 0xFF);      // #f19141 signature orange
        public Color gold = new Color32(0xD0, 0xB8, 0x5B, 0xFF);        // #d0b85b
        public Color burnt = new Color32(0xC4, 0x54, 0x16, 0xFF);       // #c45416
        public Color rust = new Color32(0xAE, 0x5A, 0x1C, 0xFF);        // #ae5a1c (panel hairline)
        public Color oxblood = new Color32(0x77, 0x16, 0x13, 0xFF);     // #771613
        public Color surfaceBase = new Color32(0x1A, 0x13, 0x10, 0xFF); // #1A1310 panel fill
        public Color surfaceRaise = new Color32(0x2A, 0x1E, 0x18, 0xFF);// #2A1E18 raised cards
        public Color inkPrimary = new Color32(0xFF, 0xF6, 0xEC, 0xFF);  // #FFF6EC warm white
        public Color inkMuted = new Color32(0xC9, 0xB7, 0xA6, 0xFF);    // #C9B7A6 captions

        [Header("Functional state (always paired with an icon/shape, never colour alone)")]
        public Color success = new Color32(0x67, 0xB4, 0x4E, 0xFF);     // #67B44E
        public Color caution = new Color32(0xF5, 0xB4, 0x3C, 0xFF);     // #F5B43C
        public Color danger = new Color32(0xE5, 0x35, 0x2B, 0xFF);      // #E5352B

        [Header("Type scale (px, tablet landscape)")]
        public float displayXl = 104f; // title, final score
        public float displayL = 64f;   // screen headers, live score
        public float heading = 36f;    // streak ×, day label
        public float body = 24f;       // labels, rows
        public float caption = 17f;    // secondary captions

        [Header("Fonts (leave empty to fall back to the TMP default)")]
        [Tooltip("Display / leaders — BC Barell, or an open stand-in like Anton or Oswald.")]
        public TMP_FontAsset displayFont;
        [Tooltip("Body / UI — Myriad Pro, or the open stand-in Source Sans 3.")]
        public TMP_FontAsset bodyFont;

        [Header("Component tokens")]
        public float panelRadius = 24f;
        [Range(0f, 1f)] public float panelOpacity = 0.92f;
        public float hairlineWidth = 1f;
        public float primaryButtonHeight = 56f;
        public float buttonRadius = 28f;
        [Tooltip("How far the game behind an overlay screen is dimmed toward black.")]
        [Range(0f, 1f)] public float overlayDim = 0.55f;

        public TMP_FontAsset DisplayFontOrDefault => displayFont != null ? displayFont : TMP_Settings.defaultFontAsset;
        public TMP_FontAsset BodyFontOrDefault => bodyFont != null ? bodyFont : TMP_Settings.defaultFontAsset;
    }
}
