using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KenyaScooter.Core;

namespace KenyaScooter.UI
{
    /// <summary>
    /// The lightweight ENDLESS-mode HUD overlay: a row of life pips and a distance readout, shown ONLY during a
    /// Vrij-rijden run (GameManager.Mode == Endless while Playing). The main diegetic HUD keeps showing score and
    /// speed; this adds the two things endless needs that the timed relay HUD does not — lives left and how far
    /// you got. It reads GameManager.LivesRemaining via the GameEvents.LivesChanged event and the world distance
    /// from WorldSpeed.
    ///
    /// Self-bootstraps and builds its own screen-space overlay canvas (the same pattern as AttractMode), so it
    /// needs no scene wiring and is completely inert in the normal group-relay mode — it simply never shows.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EndlessHud : MonoBehaviour
    {
        private const int MaxPips = 6;                                          // never draw more than this many life pips
        private static readonly Color Alive = new Color(1f, 0.34f, 0.28f, 1f);  // a warm "heart" red for a life left
        private static readonly Color Lost  = new Color(1f, 1f, 1f, 0.16f);     // a spent life: a faint ghost

        private GameObject panel;
        private CanvasGroup group;
        private RectTransform pipHost;
        private readonly List<Image> pips = new List<Image>(MaxPips);
        private TMP_Text distanceText;
        private int pipCount;             // how many pips are currently built (the run's starting life count, capped)
        private int lastLives = -1;       // last value seen from LivesChanged (may arrive before the UI is built)
        private int lastDistanceShown = -1;
        private Sprite discSprite;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // DISABLED 2026-07-13 — this separate top-centre overlay collided with the diegetic HUD's top strip and
            // read off-brand. Endless lives + distance are now shown INSIDE the diegetic cluster (see DiegeticHud:
            // the battery gauge becomes LEVENS and the route bar fills by distance). So this no longer self-spawns.
            // (Kept as a component in case a future design wants a standalone endless overlay again.)
        }

        private void OnEnable() => GameEvents.LivesChanged += OnLivesChanged;
        private void OnDisable() => GameEvents.LivesChanged -= OnLivesChanged;

        private void OnLivesChanged(int lives)
        {
            lastLives = lives;
            if (panel == null) { EnsureBuilt(); return; } // EnsureBuilt applies lastLives itself
            // The reset raises the FULL life count first; size the pip row to it (capped), then just dim as lives fall.
            int target = Mathf.Clamp(Mathf.Max(lives, pipCount), 0, MaxPips);
            if (target != pipCount) BuildPips(target);
            RefreshPips();
        }

        private void Update()
        {
            bool show = GameManager.Mode == GameMode.Endless && GameManager.State == GameState.Playing;
            if (panel == null)
            {
                if (!show) return;
                EnsureBuilt();
            }
            if (group != null) group.alpha = show ? 1f : 0f;
            if (!show) return;

            // Distance travelled (metres of world), refreshed only when the whole-metre value changes.
            float d = WorldSpeed.Instance != null ? WorldSpeed.Instance.DistanceTravelled : 0f;
            int m = Mathf.FloorToInt(d);
            if (m != lastDistanceShown && distanceText != null)
            {
                lastDistanceShown = m;
                distanceText.text = m < 1000 ? m + " m" : (m / 1000f).ToString("0.0") + " km";
            }
        }

        private void EnsureBuilt()
        {
            if (panel != null) return;
            BuildOverlay();
            if (lastLives >= 0) { BuildPips(Mathf.Clamp(lastLives, 0, MaxPips)); RefreshPips(); }
        }

        private void BuildOverlay()
        {
            var canvasGO = new GameObject("EndlessHudCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90; // above the HUD, below the attract banner (95) and the facilitator menu (100)
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(2048f, 1536f);

            var panelRT = new GameObject("Panel", typeof(RectTransform)).GetComponent<RectTransform>();
            panelRT.SetParent(canvasGO.transform, false);
            panelRT.anchorMin = new Vector2(0.5f, 1f); panelRT.anchorMax = new Vector2(0.5f, 1f); panelRT.pivot = new Vector2(0.5f, 1f);
            panelRT.sizeDelta = new Vector2(620f, 130f); panelRT.anchoredPosition = new Vector2(0f, -28f);
            panel = panelRT.gameObject;
            group = panel.AddComponent<CanvasGroup>();
            group.interactable = false; group.blocksRaycasts = false; // never eats a tap
            group.alpha = 0f;

            TMP_Text cap = MakeText(panelRT, "Caption", "LEVENS", 26f, new Color(1f, 0.96f, 0.92f, 0.9f));
            cap.rectTransform.anchorMin = new Vector2(0.5f, 1f); cap.rectTransform.anchorMax = new Vector2(0.5f, 1f); cap.rectTransform.pivot = new Vector2(0.5f, 1f);
            cap.rectTransform.sizeDelta = new Vector2(620f, 30f); cap.rectTransform.anchoredPosition = new Vector2(0f, 0f);
            cap.fontStyle = FontStyles.Bold; cap.characterSpacing = 12f;

            pipHost = new GameObject("Pips", typeof(RectTransform)).GetComponent<RectTransform>();
            pipHost.SetParent(panelRT, false);
            pipHost.anchorMin = new Vector2(0.5f, 1f); pipHost.anchorMax = new Vector2(0.5f, 1f); pipHost.pivot = new Vector2(0.5f, 1f);
            pipHost.sizeDelta = new Vector2(620f, 48f); pipHost.anchoredPosition = new Vector2(0f, -36f);

            distanceText = MakeText(panelRT, "Distance", "0 m", 30f, new Color(1f, 0.85f, 0.6f, 1f));
            distanceText.rectTransform.anchorMin = new Vector2(0.5f, 1f); distanceText.rectTransform.anchorMax = new Vector2(0.5f, 1f); distanceText.rectTransform.pivot = new Vector2(0.5f, 1f);
            distanceText.rectTransform.sizeDelta = new Vector2(620f, 36f); distanceText.rectTransform.anchoredPosition = new Vector2(0f, -90f);
            distanceText.fontStyle = FontStyles.Bold;
        }

        private void BuildPips(int count)
        {
            if (pipHost == null) return;
            for (int i = pipHost.childCount - 1; i >= 0; i--) Destroy(pipHost.GetChild(i).gameObject);
            pips.Clear();
            pipCount = Mathf.Clamp(count, 0, MaxPips);
            const float size = 40f, gap = 14f;
            float total = pipCount * size + Mathf.Max(0, pipCount - 1) * gap;
            float x0 = -total * 0.5f + size * 0.5f;
            for (int i = 0; i < pipCount; i++)
            {
                var pipRT = new GameObject("Pip" + i, typeof(RectTransform)).GetComponent<RectTransform>();
                pipRT.SetParent(pipHost, false);
                pipRT.anchorMin = new Vector2(0.5f, 0.5f); pipRT.anchorMax = new Vector2(0.5f, 0.5f); pipRT.pivot = new Vector2(0.5f, 0.5f);
                pipRT.sizeDelta = new Vector2(size, size); pipRT.anchoredPosition = new Vector2(x0 + i * (size + gap), 0f);
                var img = pipRT.gameObject.AddComponent<Image>(); img.sprite = DiscSprite(); img.raycastTarget = false;
                pips.Add(img);
            }
        }

        private void RefreshPips()
        {
            int lives = Mathf.Clamp(lastLives, 0, pips.Count);
            for (int i = 0; i < pips.Count; i++)
                if (pips[i] != null) pips[i].color = i < lives ? Alive : Lost;
        }

        private TMP_Text MakeText(RectTransform parent, string name, string text, float size, Color colour)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.color = colour; t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
            return t;
        }

        // A soft-edged filled disc (life pip), drawn once — no art asset. Same trick the menu screens use for
        // their pip discs, kept local so this overlay stays a single self-contained file.
        private Sprite DiscSprite()
        {
            if (discSprite != null) return discSprite;
            int s = 64; var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            float c = (s - 1) * 0.5f, r = c - 1f;
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(r - d + 0.5f)));
            }
            tex.Apply();
            discSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
            return discSprite;
        }
    }
}
