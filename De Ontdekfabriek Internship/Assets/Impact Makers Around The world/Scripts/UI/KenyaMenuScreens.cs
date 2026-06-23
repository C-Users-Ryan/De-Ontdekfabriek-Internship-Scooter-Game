using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KenyaScooter.Core;
using KenyaScooter.Scoring;

namespace KenyaScooter.UI
{
    /// <summary>
    /// Builds and drives the framing screens (Title, Relay hand-off, Journey complete,
    /// Game over) in the warm Ugani landscape style, generated in code so they need no
    /// hand assembly. Call <see cref="Build"/> (or Tools → Kenya Scooter → Build Menu Screens).
    ///
    /// Flow: Ready → Title.  The 2-minute timer expiring (Finished) OR reaching a charge-station
    /// checkpoint → the RELAY screen (this turn's score + class total) with two choices:
    ///   "Volgende speler" hands off to the next student (back to Title), and
    ///   "Laatste speler · Eindstand" opens the JOURNEY-COMPLETE standings, which has its own
    ///   "Nieuwe groep" button. Game over has its own screen. Every button calls the real
    ///   GameManager / GroupScoreManager / LeaderboardManager.
    /// Set <see cref="teamName"/> from the team-setup flow when there is one.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KenyaMenuScreens : MonoBehaviour
    {
        [Header("Team")]
        public string teamName = "TEAM SIMBA";

        [Header("Palette")]
        [SerializeField] private Color ink      = Hex("#F9E9D6");
        [SerializeField] private Color kicker   = Hex("#F2A468");
        [SerializeField] private Color accent   = Hex("#F2A055");
        [SerializeField] private Color cream    = Hex("#FFF6EC");
        [SerializeField] private Color success  = Hex("#9BE07C");
        [SerializeField] private Color cardFill = new Color(0.10f, 0.05f, 0.03f, 0.55f);

        private bool built;
        private GameObject titleRoot, setupRoot, relayRoot, journeyRoot, gameOverRoot;
        private TMP_Text relayScore, relayTotal, relayStat;
        private TMP_Text journeyTotal, journeyRank, journeyStat;
        private TMP_Text goScore, goStat;
        private TMP_Text relayKicker, journeyKicker, goKicker;
        private int cachedScore;
        private bool committedThisGroup;
        private Coroutine fade;
        private readonly List<TMP_Text> boardName = new();
        private readonly List<TMP_Text> boardScore = new();

        // ---- lifecycle --------------------------------------------------------------
        private void Awake() { if (!built) Build(); }

        private void OnEnable()
        {
            GameEvents.StateChanged += OnStateChanged;
            GameEvents.CheckpointReached += OnCheckpoint;
            GameEvents.ScoreChanged += OnScore;
        }

        private void OnDisable()
        {
            GameEvents.StateChanged -= OnStateChanged;
            GameEvents.CheckpointReached -= OnCheckpoint;
            GameEvents.ScoreChanged -= OnScore;
        }

        private void OnScore(int total, int delta) => cachedScore = total;
        private void OnStateChanged(GameState from, GameState to) => ShowFor(to);

        // The relay/charge-station hand-off — same screen whether reached by the timer or a checkpoint.
        private void OnCheckpoint() { PopulateRelay(); Show(relayRoot); }

        private void ShowFor(GameState s)
        {
            switch (s)
            {
                case GameState.Ready:
                    if (FreshGroup()) { PopulateChips(); Show(setupRoot); } else Show(titleRoot); // name a fresh group first
                    break;
                case GameState.Finished: PopulateRelay(); Show(relayRoot); break;
                case GameState.GameOver: PopulateGameOver(); Show(gameOverRoot); break;
                default:                 Show(null); break; // Playing / Rewinding / AtCheckpoint → HUD only
            }
        }

        private void Show(GameObject only)
        {
            SetActive(titleRoot, only == titleRoot);
            SetActive(setupRoot, only == setupRoot);
            SetActive(relayRoot, only == relayRoot);
            SetActive(journeyRoot, only == journeyRoot);
            SetActive(gameOverRoot, only == gameOverRoot);
            if (only != null) FadeIn(only);
        }

        // Soften the jump from gameplay into a screen with a short fade-in.
        private void FadeIn(GameObject go)
        {
            var cg = go.GetComponent<CanvasGroup>();
            if (cg == null) return;
            if (!Application.isPlaying) { cg.alpha = 1f; return; }
            if (fade != null) StopCoroutine(fade);
            fade = StartCoroutine(FadeRoutine(cg));
        }

        private System.Collections.IEnumerator FadeRoutine(CanvasGroup cg)
        {
            cg.alpha = 0f;
            for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
            {
                cg.alpha = Mathf.Clamp01(t / 0.35f);
                yield return null;
            }
            cg.alpha = 1f;
            fade = null;
        }

        private static void SetActive(GameObject go, bool on) { if (go != null && go.activeSelf != on) go.SetActive(on); }

        // ---- data -------------------------------------------------------------------
        private int TurnScore() => GameManager.Instance != null ? GameManager.Instance.Stats.FinalScore : cachedScore;
        private int Overtakes() => GameManager.Instance != null ? GameManager.Instance.Stats.Overtakes : 0;
        private int GroupTotal() => GroupScoreManager.Instance != null ? GroupScoreManager.Instance.GroupTotal : TurnScore();
        private int TurnCount() => GroupScoreManager.Instance != null ? Mathf.Max(1, GroupScoreManager.Instance.TurnCount) : 1;

        private void PopulateRelay()
        {
            if (relayKicker != null) relayKicker.text = "BEURT KLAAR  ·  " + teamName;
            if (relayScore != null) relayScore.text = Group(TurnScore());
            if (relayTotal != null) relayTotal.text = "KLAS TOTAAL  ·  " + Group(GroupTotal());
            if (relayStat != null)  relayStat.text  = Overtakes() + " SCHONE INHAALACTIES";
        }

        private void PopulateJourney()
        {
            int total = GroupTotal();
            int rank = LeaderboardManager.Instance != null ? LeaderboardManager.Instance.RankOf(total) : 1;
            if (journeyKicker != null) journeyKicker.text = "JOURNEY COMPLETE  ·  " + teamName;
            if (journeyTotal != null) journeyTotal.text = Group(total);
            if (journeyRank != null)  journeyRank.text  = "PLEK " + rank + " VAN DE KLAS";
            if (journeyStat != null)  journeyStat.text  = TurnCount() + " SPELERS  ·  SAMEN GEREDEN";

            // The saved local leaderboard (this group was just committed into it), with this group highlighted.
            var entries = LeaderboardManager.Instance != null ? LeaderboardManager.Instance.Entries : null;
            var last = LeaderboardManager.Instance != null ? LeaderboardManager.Instance.LastCommitted : null;
            for (int i = 0; i < boardName.Count; i++)
            {
                if (entries != null && i < entries.Count)
                {
                    var e = entries[i];
                    boardName[i].text = (i + 1) + "  ·  " + e.label.ToUpper();
                    boardScore[i].text = Group(e.score);
                    Color c = e == last ? accent : ink;
                    boardName[i].color = c; boardScore[i].color = c;
                }
                else { boardName[i].text = ""; boardScore[i].text = ""; }
            }
        }

        private void PopulateGameOver()
        {
            if (goKicker != null) goKicker.text = "OEPS  ·  " + teamName;
            if (goScore != null) goScore.text = Group(TurnScore());
            if (goStat != null)  goStat.text  = Overtakes() + " SCHONE INHAALACTIES";
        }

        // ---- button actions ---------------------------------------------------------
        private void StartGame()  { if (GameManager.Instance != null) GameManager.Instance.StartSession(); }
        // Hand off straight into the next teammate's turn — no trip back through the title/setup. The group
        // total survives SessionReset, so the score and multiplier carry over (that's the teamwork point).
        private void NextPlayer() { if (GameManager.Instance != null) GameManager.Instance.StartSession(); }
        private void ShowStandings() { CommitCurrentGroup(); PopulateJourney(); Show(journeyRoot); }

        // Save the finished group to the local leaderboard NOW (when the standings are reached), so the score
        // persists across play sessions even if nobody taps "Nieuwe groep" and the iPad is closed. Guarded so a
        // group is never committed twice.
        private void CommitCurrentGroup()
        {
            if (committedThisGroup) return;
            if (LeaderboardManager.Instance != null && GroupScoreManager.Instance != null)
            {
                LeaderboardManager.Instance.CommitGroup(GroupScoreManager.Instance.GroupTotal, teamName);
                committedThisGroup = true;
            }
        }

        private void NewGroup()
        {
            CommitCurrentGroup(); // belt-and-suspenders if standings was somehow bypassed
            if (GroupScoreManager.Instance != null) GroupScoreManager.Instance.ResetGroup();
            committedThisGroup = false;
            if (GameManager.Instance != null) GameManager.Instance.PrepareNextTurn();
        }

        private void PickTeam(string name) { teamName = name; StartGame(); }
        private bool FreshGroup() => GroupScoreManager.Instance == null || GroupScoreManager.Instance.TurnCount == 0;

        private RectTransform chipRow;
        private const int ChipsShown = 6;
        // Swahili animal names. Easily extend for more uniqueness; pick from those not yet on the local board.
        private static readonly string[] TeamNamePool =
        {
            "SIMBA","TWIGA","CHUI","TEMBO","FARU","KIBOKO","NYATI","DUMA","SWALA","NGIRI",
            "FISI","KOBE","TAI","KORONGO","POPO","NYANI","NGAMIA","SUNGURA","MAMBA","NYOKA"
        };

        // Fills the setup screen with a random subset of names NOT already on this iPad's local leaderboard, so two
        // groups never share a name (until the pool runs out). Re-run each time a fresh group reaches the setup screen.
        private void PopulateChips()
        {
            if (chipRow == null) return;
            for (int i = chipRow.childCount - 1; i >= 0; i--)
            {
                var c = chipRow.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
            }

            var used = new HashSet<string>();
            var entries = LeaderboardManager.Instance != null ? LeaderboardManager.Instance.Entries : null;
            if (entries != null)
                for (int i = 0; i < entries.Count; i++)
                    if (!string.IsNullOrEmpty(entries[i].label)) used.Add(entries[i].label.ToUpper());

            var pool = new List<string>();
            for (int i = 0; i < TeamNamePool.Length; i++)
                if (!used.Contains(TeamNamePool[i])) pool.Add(TeamNamePool[i]);
            if (pool.Count == 0) pool.AddRange(TeamNamePool); // every name taken — reuse rather than show nothing

            for (int i = pool.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (pool[i], pool[j]) = (pool[j], pool[i]); }

            int n = Mathf.Min(ChipsShown, pool.Count);
            float x = 0f, w = 168f;
            for (int i = 0; i < n; i++)
            {
                string pick = pool[i];
                RectTransform b = NewRect(chipRow, "Chip");
                b.anchorMin = new Vector2(0f, 0.5f); b.anchorMax = new Vector2(0f, 0.5f); b.pivot = new Vector2(0f, 0.5f);
                b.sizeDelta = new Vector2(w, 64f); b.anchoredPosition = new Vector2(x, 0f);
                Image img = b.gameObject.AddComponent<Image>(); img.sprite = Rounded(28); img.type = Image.Type.Sliced; img.color = cream;
                var btn = b.gameObject.AddComponent<Button>(); btn.targetGraphic = img; btn.onClick.AddListener(() => PickTeam(pick));
                TMP_Text t = AddText(b, "Label", pick, 22, Hex("#9c3a12"), TextAlignmentOptions.Center); t.fontStyle = FontStyles.Bold; Stretch(t.rectTransform);
                x += w + 16f;
            }
        }

        // ---- build ------------------------------------------------------------------
        [ContextMenu("Rebuild now")]
        public void Build()
        {
            ClearGenerated();
            boardName.Clear(); boardScore.Clear();
            Stretch((RectTransform)transform);

            titleRoot    = BuildTitle();
            setupRoot    = BuildSetup();
            relayRoot    = BuildRelay();
            journeyRoot  = BuildJourney();
            gameOverRoot = BuildGameOver();
            built = true;

            ShowFor(GameManager.State);
        }

        private GameObject BuildTitle()
        {
            RectTransform root = MakeScreen("Title", Hex("#a9421a"), Hex("#f6a949"));
            BeginColumn(root, 250f);
            KickerRow("IMPACT MAKERS AROUND THE WORLD");
            Hero("Hero", "KENYA", 130f);
            Spaced(Line("Tag", "RIJD DE A109 · NAIROBI → MOMBASA", 26f, kicker), 0.18f);
            Gap(16f);
            ButtonRow("ANZA!  ·  TIK OM TE STARTEN", StartGame);
            return root.gameObject;
        }

        private GameObject BuildSetup()
        {
            RectTransform root = MakeScreen("TeamSetup", Hex("#a9421a"), Hex("#f6a949"));
            BeginColumn(root, 235f);
            KickerRow("IMPACT MAKERS AROUND THE WORLD");
            Hero("Hero", "KIES JE TEAM", 92f);
            Spaced(Line("Sub", "TIK OP EEN NAAM OM TE STARTEN", 24f, kicker), 0.18f);
            Gap(18f);
            chipRow = Place("Chips", 64f, 0f); // filled per show by PopulateChips with random, unused names
            return root.gameObject;
        }

        private GameObject BuildRelay()
        {
            RectTransform root = MakeScreen("Relay", Hex("#5a2412"), Hex("#f3a046"));
            BeginColumn(root, 225f);
            relayKicker = KickerRow("BEURT KLAAR  ·  " + teamName);
            relayScore = Hero("Score", "3 503", 118f);
            relayTotal = Line("Total", "KLAS TOTAAL  ·  12 480", 28f, ink);
            Gap(6f);
            relayStat = Line("Stat", "0 SCHONE INHAALACTIES", 22f, success);
            Gap(18f);
            ButtonRow("VOLGENDE SPELER  →", NextPlayer,
                      "LAATSTE SPELER · EINDSTAND", ShowStandings);
            return root.gameObject;
        }

        private GameObject BuildJourney()
        {
            RectTransform root = MakeScreen("Journey", Hex("#5e2614"), Hex("#f6a949"));
            BeginColumn(root, 235f);
            journeyKicker = KickerRow("JOURNEY COMPLETE  ·  " + teamName);
            journeyTotal = Hero("Total", "12 480", 118f);
            journeyRank  = Line("Rank", "PLEK 1 VAN DE KLAS", 28f, ink);
            Gap(6f);
            journeyStat  = Line("Stat", "1 SPELERS · SAMEN GEREDEN", 22f, success);
            Gap(18f);
            ButtonRow("NIEUWE GROEP", NewGroup);
            BuildBoard(root);
            return root.gameObject;
        }

        // The class scoreboard: stored group totals plus this group, ranked, on the right of the Journey screen.
        private void BuildBoard(RectTransform root)
        {
            RectTransform card = NewRect(root, "Scoreboard");
            Anchor(card, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(760f, 660f), new Vector2(-430f, 0f));
            Image bg = card.gameObject.AddComponent<Image>(); bg.sprite = Rounded(24); bg.type = Image.Type.Sliced; bg.color = cardFill; bg.raycastTarget = false;

            TMP_Text head = AddText(card, "Head", "KLASSEMENT", 24, kicker, TextAlignmentOptions.Left);
            head.rectTransform.anchorMin = new Vector2(0f, 1f); head.rectTransform.anchorMax = new Vector2(0f, 1f); head.rectTransform.pivot = new Vector2(0f, 1f);
            head.rectTransform.sizeDelta = new Vector2(400f, 30f); head.rectTransform.anchoredPosition = new Vector2(48f, -44f); Spaced(head, 0.2f);

            for (int i = 0; i < 6; i++)
            {
                float y = -110f - i * 84f;
                TMP_Text n = AddText(card, "n" + i, "", 28, ink, TextAlignmentOptions.Left);
                n.rectTransform.anchorMin = new Vector2(0f, 1f); n.rectTransform.anchorMax = new Vector2(0f, 1f); n.rectTransform.pivot = new Vector2(0f, 1f);
                n.rectTransform.sizeDelta = new Vector2(480f, 36f); n.rectTransform.anchoredPosition = new Vector2(48f, y);
                TMP_Text s = AddText(card, "s" + i, "", 28, ink, TextAlignmentOptions.Right); s.fontStyle = FontStyles.Bold;
                s.rectTransform.anchorMin = new Vector2(1f, 1f); s.rectTransform.anchorMax = new Vector2(1f, 1f); s.rectTransform.pivot = new Vector2(1f, 1f);
                s.rectTransform.sizeDelta = new Vector2(220f, 36f); s.rectTransform.anchoredPosition = new Vector2(-48f, y);
                boardName.Add(n); boardScore.Add(s);
            }
        }

        private GameObject BuildGameOver()
        {
            RectTransform root = MakeScreen("GameOver", Hex("#4a2414"), Hex("#d98a44"));
            BeginColumn(root, 248f);
            goKicker = KickerRow("OEPS  ·  " + teamName);
            goScore = Hero("Score", "9 240", 118f);
            Line("Line", "Zelfs de beste chauffeurs hebben een off-dag.", 24f, Hex("#F3BE92"));
            Gap(6f);
            goStat = Line("Stat", "0 SCHONE INHAALACTIES", 22f, success);
            Gap(18f);
            ButtonRow("NOG EEN KEER", StartGame);
            return root.gameObject;
        }

        // ---- deterministic top-down layout ------------------------------------------
        private RectTransform column;
        private float cursor;

        private void BeginColumn(RectTransform root, float topY)
        {
            column = NewRect(root, "Content");
            column.anchorMin = new Vector2(0f, 0.5f); column.anchorMax = new Vector2(0f, 0.5f); column.pivot = new Vector2(0f, 1f);
            column.sizeDelta = new Vector2(1180f, 760f); column.anchoredPosition = new Vector2(140f, topY);
            cursor = 0f;
        }

        private RectTransform Place(string name, float height, float gapAfter)
        {
            RectTransform rt = NewRect(column, name);
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(1180f, height); rt.anchoredPosition = new Vector2(0f, -cursor);
            cursor += height + gapAfter;
            return rt;
        }

        private void Gap(float px) => cursor += px;

        private TMP_Text KickerRow(string text)
        {
            RectTransform row = Place("Kicker", 34f, 18f);
            Image ring = AddImage(row, "Ring", accent, RingThin());
            ring.rectTransform.anchorMin = new Vector2(0f, 0.5f); ring.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            ring.rectTransform.pivot = new Vector2(0f, 0.5f); ring.rectTransform.sizeDelta = new Vector2(30f, 30f); ring.rectTransform.anchoredPosition = Vector2.zero;
            TMP_Text badge = AddText(row, "tatoe", "tatoe", 11, cream, TextAlignmentOptions.Center);
            badge.rectTransform.anchorMin = new Vector2(0f, 0.5f); badge.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            badge.rectTransform.pivot = new Vector2(0f, 0.5f); badge.rectTransform.sizeDelta = new Vector2(30f, 16f); badge.rectTransform.anchoredPosition = Vector2.zero;
            TMP_Text k = AddText(row, "Text", text, 22, kicker, TextAlignmentOptions.Left);
            k.rectTransform.anchorMin = new Vector2(0f, 0.5f); k.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            k.rectTransform.pivot = new Vector2(0f, 0.5f); k.rectTransform.sizeDelta = new Vector2(1000f, 28f); k.rectTransform.anchoredPosition = new Vector2(46f, 0f);
            Spaced(k, 0.16f);
            return k;
        }

        private TMP_Text Hero(string name, string text, float size)
        {
            RectTransform rt = Place(name, size * 1.05f, 10f);
            TMP_Text t = AddText(rt, "T", text, size, cream, TextAlignmentOptions.Left);
            t.fontStyle = FontStyles.Bold; Stretch(t.rectTransform);
            return t;
        }

        private TMP_Text Line(string name, string text, float size, Color col)
        {
            RectTransform rt = Place(name, size * 1.45f, 20f);
            TMP_Text t = AddText(rt, "T", text, size, col, TextAlignmentOptions.Left);
            Stretch(t.rectTransform);
            return t;
        }

        private void ButtonRow(string label, UnityEngine.Events.UnityAction action)
        {
            RectTransform row = Place("Buttons", 64f, 0f);
            AddButton(row, 0f, label, action, true);
        }

        private void ButtonRow(string l1, UnityEngine.Events.UnityAction a1, string l2, UnityEngine.Events.UnityAction a2)
        {
            RectTransform row = Place("Buttons", 64f, 0f);
            float w1 = AddButton(row, 0f, l1, a1, true);
            AddButton(row, w1 + 18f, l2, a2, false);
        }

        private float AddButton(RectTransform row, float x, string label, UnityEngine.Events.UnityAction action, bool primary)
        {
            float w = label.Length * 13.5f + 56f;
            RectTransform b = NewRect(row, "Button");
            b.anchorMin = new Vector2(0f, 0.5f); b.anchorMax = new Vector2(0f, 0.5f); b.pivot = new Vector2(0f, 0.5f);
            b.sizeDelta = new Vector2(w, 64f); b.anchoredPosition = new Vector2(x, 0f);
            Image img = b.gameObject.AddComponent<Image>(); img.sprite = Rounded(28); img.type = Image.Type.Sliced; img.color = primary ? cream : cardFill;
            var btn = b.gameObject.AddComponent<Button>(); btn.targetGraphic = img; if (action != null) btn.onClick.AddListener(action);
            TMP_Text t = AddText(b, "Label", label, 18, primary ? Hex("#9c3a12") : cream, TextAlignmentOptions.Center);
            t.fontStyle = FontStyles.Bold; Stretch(t.rectTransform);
            return w;
        }

        private RectTransform MakeScreen(string name, Color top, Color bottom)
        {
            RectTransform root = NewRect((RectTransform)transform, name);
            Stretch(root);
            root.gameObject.AddComponent<CanvasGroup>(); // drives the fade-in
            Image bg = root.gameObject.AddComponent<Image>(); bg.sprite = GradientSprite(top, bottom); bg.color = Color.white; bg.raycastTarget = true;
            Image ring = AddImage(root, "FocusRing", new Color(1f, 0.96f, 0.92f, 0.14f), Ring(0.9f));
            Anchor(ring.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(900f, 900f), new Vector2(120f, 0f));
            return root;
        }

        // ---- generic UI + sprites ----------------------------------------------------
        private static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

        private RectTransform NewRect(RectTransform parent, string name)
        { var go = new GameObject(name, typeof(RectTransform)); go.layer = parent.gameObject.layer; var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false); return rt; }

        private Image AddImage(RectTransform parent, string name, Color colour, Sprite sprite)
        { var rt = NewRect(parent, name); var img = rt.gameObject.AddComponent<Image>(); img.color = colour; img.sprite = sprite; img.type = (sprite != null && sprite.border != Vector4.zero) ? Image.Type.Sliced : Image.Type.Simple; img.raycastTarget = false; return img; }

        private TMP_Text AddText(RectTransform parent, string name, string text, float size, Color colour, TextAlignmentOptions align)
        { var rt = NewRect(parent, name); var t = rt.gameObject.AddComponent<TextMeshProUGUI>(); t.text = text; t.fontSize = size; t.color = colour; t.alignment = align; t.raycastTarget = false; return t; }

        private void Spaced(TMP_Text t, float em) => t.characterSpacing = em * 100f;

        private static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f); rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero; }
        private static void Anchor(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 size, Vector2 pos) { rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = size; rt.anchoredPosition = pos; }

        private void ClearGenerated()
        { var rt = (RectTransform)transform; for (int i = rt.childCount - 1; i >= 0; i--) { var c = rt.GetChild(i).gameObject; if (Application.isPlaying) Destroy(c); else DestroyImmediate(c); } }

        private static string Group(int n)
        {
            string s = Mathf.Abs(n).ToString();
            var sb = new System.Text.StringBuilder();
            int c = 0;
            for (int i = s.Length - 1; i >= 0; i--) { sb.Insert(0, s[i]); if (++c % 3 == 0 && i > 0) sb.Insert(0, ' '); }
            return (n < 0 ? "-" : "") + sb.ToString();
        }

        private readonly System.Collections.Generic.Dictionary<int, Sprite> _rounded = new();
        private readonly System.Collections.Generic.Dictionary<float, Sprite> _ring = new();
        private Sprite _ringThin;

        private Sprite GradientSprite(Color top, Color bottom)
        {
            int h = 256; var tex = new Texture2D(2, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < h; y++) { Color c = Color.Lerp(bottom, top, y / (float)(h - 1)); tex.SetPixel(0, y, c); tex.SetPixel(1, y, c); }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 2, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(0, 4, 0, 4));
        }

        private Sprite Rounded(int radius)
        {
            if (_rounded.TryGetValue(radius, out var c)) return c;
            int s = radius * 2 + 4; var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
            { float dx = Mathf.Max(radius - x, x - (s - radius), 0f); float dy = Mathf.Max(radius - y, y - (s - radius), 0f); tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f))); }
            tex.Apply(); var sp = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius)); _rounded[radius] = sp; return sp;
        }

        private Sprite Ring(float innerFrac)
        {
            if (_ring.TryGetValue(innerFrac, out var c)) return c;
            int s = 256; var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            float r = s * 0.5f, cx = r, cy = r, inner = r * innerFrac;
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
            { float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)); tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(r - d) * Mathf.Clamp01(d - inner))); }
            tex.Apply(); var sp = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f); _ring[innerFrac] = sp; return sp;
        }

        private Sprite RingThin() { if (_ringThin == null) _ringThin = Ring(0.82f); return _ringThin; }
    }
}
