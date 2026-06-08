using UnityEngine;
using UnityEngine.UI;

namespace OvertakeGame
{
    /// <summary>
    /// Procedurally builds the Nairobi skyline silhouette on the start screen
    /// from UI Image primitives — no external sprite asset needed.
    ///
    /// Building data matches real Nairobi skyline profile left-to-right:
    ///   KICC dome, Times Tower, UAP Tower, Britam Tower,
    ///   Teleposta Towers, low-rise fill blocks.
    ///
    /// SETUP:
    ///   1. Create an empty RectTransform in your start screen Canvas named "Skyline".
    ///      Anchor to bottom-stretch. Height ~250px. This is the parent.
    ///   2. Attach this script to the Skyline GameObject.
    ///   3. Hit Play — buildings are generated automatically.
    ///      Or call BuildSkyline() from the Editor via context menu.
    ///
    /// COLOUR:
    ///   Default silhouette colour is near-black with a slight warm tint
    ///   (#1A1208) so it reads against the sunset/morning sky.
    ///   Change skylineColour in the Inspector to match your sky.
    /// </summary>
    public class NairobiSkylineBuilder : MonoBehaviour
    {
        [Header("Appearance")]
        [Tooltip("Silhouette colour. Dark warm tone recommended.")]
        public Color skylineColour = new Color(0.10f, 0.07f, 0.03f, 1f);

        [Tooltip("Y offset from bottom of parent. Adjusts where buildings sit.")]
        public float groundY = 0f;

        [Header("Scale")]
        [Tooltip("Multiply all widths and heights by this for different screen sizes.")]
        public float scale = 1f;

        // ── Building definitions — real Nairobi skyline profile ────────────────
        // (name, widthPx, heightPx, topStyle)
        // topStyle: 0=flat, 1=dome/circle, 2=stepped pyramid, 3=tapered, 4=angled
        private static readonly (string name, float w, float h, int top)[] Buildings =
        {
            ("LowRise_1",         60f,  60f, 0),
            ("LowRise_2",         45f,  75f, 0),
            ("KICC_Dome",         40f, 180f, 1),   // Kenyatta International Convention Centre
            ("LowRise_3",         55f,  90f, 0),
            ("Times_Tower",       35f, 220f, 2),   // Tallest in Nairobi — stepped pyramid
            ("LowRise_4",         40f,  85f, 0),
            ("UAP_Tower",         30f, 160f, 3),   // Tapered cylinder
            ("LowRise_5",         50f,  70f, 0),
            ("Britam_Tower",      28f, 150f, 4),   // Slim, slightly angled top
            ("LowRise_6",         45f,  80f, 0),
            ("Teleposta_Left",    20f, 130f, 0),   // Twin block — left
            ("Teleposta_Right",   18f, 130f, 0),   // Twin block — right (gap between)
            ("LowRise_7",         60f,  55f, 0),
            ("LowRise_8",         40f,  65f, 0),
        };

        [ContextMenu("Build Skyline")]
        public void BuildSkyline()
        {
            // Remove any existing children
            for (int i = transform.childCount - 1; i >= 0; i--)
                DestroyImmediate(transform.GetChild(i).gameObject);

            RectTransform parent = GetComponent<RectTransform>();
            float totalWidth = 0f;
            foreach (var b in Buildings) totalWidth += b.w * scale + 2f;

            float x = -totalWidth * 0.5f;

            foreach (var b in Buildings)
            {
                float w = b.w * scale;
                float h = b.h * scale;

                // Main building block
                var go  = new GameObject(b.name, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
                var rt  = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0f);
                rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot     = new Vector2(0.5f, 0f);
                rt.sizeDelta = new Vector2(w, h);
                rt.anchoredPosition = new Vector2(x + w * 0.5f, groundY);
                go.GetComponent<Image>().color = skylineColour;

                // Top decoration based on style
                switch (b.top)
                {
                    case 1: AddDome(go.transform,   w, h, skylineColour); break;
                    case 2: AddSteppedPyramid(go.transform, w, h, skylineColour); break;
                    case 3: AddTaperedTop(go.transform, w, h, skylineColour); break;
                    case 4: AddAngledTop(go.transform, w, h, skylineColour); break;
                }

                x += w + 2f; // 2px gap between buildings
            }
        }

        void Start() => BuildSkyline();

        // ── Top style helpers ──────────────────────────────────────────────────

        private void AddDome(Transform parent, float w, float h, Color c)
        {
            // Circle on top of the tower
            var go  = new GameObject("Dome", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt  = go.GetComponent<RectTransform>();
            float r = w * 0.6f;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot     = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(r, r * 0.5f);
            rt.anchoredPosition = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = c;
            // Unity UI Image doesn't have a built-in circle primitive,
            // but a very rounded Image with sprite type Filled approximates it.
            // For a cleaner circle, assign a circle sprite to img.sprite.
        }

        private void AddSteppedPyramid(Transform parent, float w, float h, Color c)
        {
            // Two narrowing steps above the building
            float[] widths = { w * 0.65f, w * 0.35f };
            float[] heights = { h * 0.08f, h * 0.06f };
            float yOffset = 0f;
            for (int i = 0; i < widths.Length; i++)
            {
                var go = new GameObject($"Step_{i}", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(parent, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot     = new Vector2(0.5f, 0f);
                rt.sizeDelta = new Vector2(widths[i], heights[i]);
                rt.anchoredPosition = new Vector2(0f, yOffset);
                go.GetComponent<Image>().color = c;
                yOffset += heights[i];
            }
        }

        private void AddTaperedTop(Transform parent, float w, float h, Color c)
        {
            var go = new GameObject("Taper", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot     = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(w * 0.4f, h * 0.12f);
            rt.anchoredPosition = Vector2.zero;
            go.GetComponent<Image>().color = c;
        }

        private void AddAngledTop(Transform parent, float w, float h, Color c)
        {
            var go = new GameObject("Angled", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot     = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(w * 0.5f, h * 0.08f);
            rt.anchoredPosition = new Vector2(-w * 0.25f, 0f);
            go.GetComponent<Image>().color = c;
        }
    }
}
