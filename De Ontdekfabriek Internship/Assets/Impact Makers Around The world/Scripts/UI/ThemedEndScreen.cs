using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KenyaScooter.Core;

namespace KenyaScooter.UI
{
    /// <summary>
    /// Builds the journey-complete / game-over screen at runtime, entirely from a UITheme — there is no
    /// hand-placed prefab to keep in sync, the whole look comes from one asset. It shows when the session
    /// ends (Finished or GameOver), fills in the turn's stats as a mirror (not a judgement), and the button
    /// hands over to the next student. A live, themed reference for the GUI spec's §6.7 / §6.8.
    ///
    /// Drop this on an empty GameObject, assign a UITheme, and play. Tick Preview On Start to see it without
    /// finishing a run. The Swahili strings below are serialized so you can match your verified SwahiliUI copy.
    /// </summary>
    public sealed class ThemedEndScreen : MonoBehaviour
    {
        [SerializeField] private UITheme theme;

        [Header("Copy (set to your verified SwahiliUI strings)")]
        [SerializeField] private string headerFinished = "MWISHO WA SAFARI";
        [SerializeField] private string headerGameOver = "MWISHO";
        [SerializeField] private string subtitle = "journey complete";
        [SerializeField] private string nextLabel = "NEXT PLAYER";

        [Tooltip("Show the screen on Start with sample numbers, to preview the design without finishing a run.")]
        [SerializeField] private bool previewOnStart = false;

        private CanvasGroup group;
        private TMP_Text headerText;
        private TMP_Text scoreText;
        private TMP_Text breakdownText;

        private void Awake()
        {
            if (theme != null)
                UITheme.Active = theme;
            Build();
            SetVisible(false);
        }

        private void OnEnable() => GameEvents.StateChanged += HandleStateChanged;
        private void OnDisable() => GameEvents.StateChanged -= HandleStateChanged;

        private void Start()
        {
            if (previewOnStart)
            {
                headerText.text = headerFinished;
                Populate(3420, 12, 1.4f, 3);
                SetVisible(true);
            }
        }

        private void HandleStateChanged(GameState from, GameState to)
        {
            if (to == GameState.Finished || to == GameState.GameOver)
            {
                var stats = GameManager.Instance != null ? GameManager.Instance.Stats : null;
                headerText.text = to == GameState.GameOver ? headerGameOver : headerFinished;
                Populate(
                    stats != null ? stats.FinalScore : 0,
                    stats != null ? stats.Overtakes : 0,
                    stats != null ? stats.DistanceMetres / 1000f : 0f,
                    stats != null ? stats.BestStreak : 0);
                SetVisible(true);
            }
            else if (to == GameState.Ready || to == GameState.Playing)
            {
                SetVisible(false);
            }
        }

        private void Populate(int score, int overtakes, float km, int streak)
        {
            scoreText.text = score.ToString("N0");
            string ok = theme != null ? ColorUtility.ToHtmlStringRGB(theme.success) : "67B44E";
            breakdownText.text = $"<color=#{ok}>INGEHAALD ×{overtakes}</color>    ·    {km:0.0} km    ·    streak ×{streak}";
        }

        private void OnNext()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.PrepareNextTurn();
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            if (group == null)
                return;
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        // ---- Runtime construction (every value comes from the theme) ------------------

        private void Build()
        {
            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null)
                canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;

            if (GetComponent<CanvasScaler>() == null)
            {
                CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1200f);
                scaler.matchWidthOrHeight = 0.5f;
            }
            if (GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            group = GetComponent<CanvasGroup>();
            if (group == null)
                group = gameObject.AddComponent<CanvasGroup>();

            Color surface = theme != null ? theme.surfaceBase : new Color(0.10f, 0.075f, 0.062f);
            Color ink = theme != null ? theme.inkPrimary : Color.white;
            Color muted = theme != null ? theme.inkMuted : new Color(0.79f, 0.72f, 0.65f);
            Color accent = theme != null ? theme.accent : new Color(0.95f, 0.57f, 0.25f);
            float dim = theme != null ? theme.overlayDim : 0.55f;

            Image dimImage = NewImage(transform, new Color(0f, 0f, 0f, dim), rounded: false);
            Stretch(dimImage.rectTransform);

            Image panel = NewImage(transform, surface, rounded: true);
            RectTransform panelRt = panel.rectTransform;
            panelRt.anchorMin = panelRt.anchorMax = panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(1000f, 640f);
            panelRt.anchoredPosition = Vector2.zero;

            const float w = 900f;
            headerText = NewText(panelRt, headerFinished, theme != null ? theme.displayL : 64f, accent, display: true);
            PlaceTop(headerText.rectTransform, 56f, w, 84f);

            TMP_Text sub = NewText(panelRt, subtitle, theme != null ? theme.caption : 17f, muted, display: false);
            PlaceTop(sub.rectTransform, 150f, w, 28f);

            scoreText = NewText(panelRt, "0", theme != null ? theme.displayXl : 104f, accent, display: true);
            PlaceTop(scoreText.rectTransform, 196f, w, 150f);

            breakdownText = NewText(panelRt, string.Empty, theme != null ? theme.body : 24f, ink, display: false);
            PlaceTop(breakdownText.rectTransform, 372f, w, 40f);

            Image button = NewImage(panelRt, accent, rounded: true);
            RectTransform buttonRt = button.rectTransform;
            buttonRt.anchorMin = buttonRt.anchorMax = new Vector2(0.5f, 0f);
            buttonRt.pivot = new Vector2(0.5f, 0f);
            buttonRt.sizeDelta = new Vector2(360f, theme != null ? theme.primaryButtonHeight : 56f);
            buttonRt.anchoredPosition = new Vector2(0f, 56f);
            button.gameObject.AddComponent<Button>().onClick.AddListener(OnNext);

            TMP_Text buttonLabel = NewText(buttonRt, nextLabel, theme != null ? theme.body : 24f, surface, display: false);
            Stretch(buttonLabel.rectTransform);
        }

        private Image NewImage(Transform parent, Color color, bool rounded)
        {
            var go = new GameObject("Image", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Image image = go.AddComponent<Image>();
            image.color = color;
            if (rounded)
            {
                // The built-in UI sprite gives the standard rounded panel with no custom asset needed.
                image.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Background.psd");
                image.type = Image.Type.Sliced;
            }
            return image;
        }

        private TMP_Text NewText(Transform parent, string content, float size, Color color, bool display)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.fontStyle = display ? FontStyles.Bold : FontStyles.Normal;
            TMP_FontAsset font = theme != null ? (display ? theme.DisplayFontOrDefault : theme.BodyFontOrDefault) : null;
            if (font != null)
                text.font = font;
            return text;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void PlaceTop(RectTransform rt, float topOffset, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(0f, -topOffset);
        }
    }
}
