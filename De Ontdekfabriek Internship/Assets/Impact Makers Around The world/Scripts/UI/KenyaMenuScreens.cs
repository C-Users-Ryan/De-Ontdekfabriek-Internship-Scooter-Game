using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using KenyaScooter.Core;
using KenyaScooter.Config;
using KenyaScooter.Scoring;
using KenyaScooter.Settings;

namespace KenyaScooter.UI
{
    /// <summary>
    /// Builds and drives the framing screens (Title, Team select, Relay hand-off, Journey complete,
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
    // NOTE: this class is split across two files for readability (2026-06-27, no behaviour change):
    //   KenyaMenuScreens.cs       — runtime: lifecycle, screen flow, data population, button actions.
    //   KenyaMenuScreens.Build.cs — construction: Build() and the procedural layout/sprite helpers.
    // UI v2 (2026-07-05, per "Kenya Game UI — Improved" / huisstijl spec): the Title is centred with
    // one accent-filled action, team select is select-then-confirm cards, the relay turn reads as a
    // gain joining the class total, the eindstand board carries rank medallions with a full accent
    // row for this group, and game over hands the tablet on. Install BOTH v2 halves together.
    [DisallowMultipleComponent]
    public sealed partial class KenyaMenuScreens : MonoBehaviour
    {
        [Header("Team")]
        public string teamName = "TEAM SIMBA";

        // Palette: canonical Ugani tokens from UiKit, so every framing screen, the HUD and the settings menu
        // share one source of truth instead of three near-but-not-equal hex sets. The token-derived roles are
        // live properties (not serialized fields) reading UiKit, so a UITheme swap now recolours these screens
        // too — the same fix SettingsMenu already carries — completing the keystone → screens path. Only the two
        // genuinely-local colours (a subtitle lift with no UiKit token, and a translucent card fill) are kept
        // baked, as static readonly, so the palette has one consistent story with no half-serialized values.
        private static Color ink        => UiKit.Ink;                          // warm white #FFF6EC
        private static readonly Color kicker = Hex("#F2A468");                 // lighter accent for the subtitle (local lift, no token)
        private static Color accent     => UiKit.Accent;                       // signature #F19141
        private static Color cream      => UiKit.Ink;                          // cream button face
        private static Color success    => UiKit.Success;                      // #67B44E (in-system green)
        private static Color muted      => UiKit.InkMuted;                     // captions / card subtitles #C9B7A6
        private static readonly Color cardFill = new Color(0.07f, 0.04f, 0.03f, 0.62f); // translucent dark card (local)
        private static Color inkOnLight => UiKit.InkOnLight;                   // dark warm-brown text on a cream button

        private bool built;
        private GameObject titleRoot, setupRoot, relayRoot, journeyRoot, gameOverRoot;
        private TMP_Text relayScore, relayTotal, relayStat, relayJoin;
        // ✓ pip discs ahead of the clean-overtake counts (sprite check marks — the font has no ✓ glyph).
        private Image[] relayPips, goPips;
        private TMP_Text journeyTotal, journeyRank, journeyStat;
        private TMP_Text goScore, goStat;
        private TMP_Text relayKicker, journeyKicker, goKicker;
        private Button goSecondButton;   // GameOver's second action — relabelled/rewired per GameMode in PopulateGameOver
        private TMP_Text goSecondLabel;
        private int cachedScore;
        private bool committedThisGroup;
        private Coroutine fade;
        // Eindstand scoreboard rows (built once in BuildBoard, recoloured per show in PopulateJourney):
        // name/score texts, the rank medallion (numeral + disc) and the full accent highlight for this group.
        private readonly List<TMP_Text> boardName = new();
        private readonly List<TMP_Text> boardScore = new();
        private readonly List<TMP_Text> boardRank = new();
        private readonly List<Image> boardRankBg = new();
        private readonly List<Image> boardRowBg = new();
        private readonly List<Image> boardStripe = new(); // zebra strips, hidden for empty rows (null on odd rows)
        // The front-of-house Kenya/Dutch drive-side toggle (v2: one segmented pill, Title screen only).
        private readonly List<(Image bg, TMP_Text label, bool leftMode)> modeButtons = new();

        // ---- lifecycle --------------------------------------------------------------
        private void Awake()
        {
            if (Application.isPlaying) EnsureTouchUIEventSystem();
            if (!built) Build();
        }

        // Make sure UI taps actually register on the tablet. With the New Input System an EventSystem must carry an
        // InputSystemUIInputModule, or touches never reach the buttons/chips (a legacy StandaloneInputModule is
        // mouse/keyboard only — which is exactly how a tablet build ends up where only the raw tap-to-start fires
        // and the team chips seem dead). If the scene's EventSystem is missing or legacy-configured, fix it here.
        private static void EnsureTouchUIEventSystem()
        {
            var es = Object.FindObjectOfType<EventSystem>();
            if (es == null)
            {
                var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                DontDestroyOnLoad(go);
                return;
            }
            if (es.GetComponent<InputSystemUIInputModule>() == null)
            {
                foreach (var m in es.GetComponents<BaseInputModule>())
                    m.enabled = false; // silence any legacy (Standalone) module so the two do not fight
                es.gameObject.AddComponent<InputSystemUIInputModule>();
            }
        }

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
                case GameState.Ready:    Show(titleRoot); break; // the Title is the front door — always first
                case GameState.Finished: PopulateRelay(); Show(relayRoot); break;
                case GameState.GameOver: PopulateGameOver(); Show(gameOverRoot); break;
                default:                 Show(null); break; // Playing / Rewinding / AtCheckpoint → HUD only
            }
        }

        // The Title's ANZA button: a fresh group goes to the team select first;
        // a running group heads straight into the next turn.
        private void BeginFromTitle()
        {
            // The game mode is a FACILITATOR choice now (settings menu / a Profiel, behind the access code) — NOT a
            // child-facing button on the title. If "Vrij rijden (Endless)" is switched on, ANZA drops straight into
            // a solo lives run; otherwise it's the normal timed class relay (team select for a fresh group, or
            // straight into the next teammate's turn).
            if (GameManager.EndlessSelected)
            {
                StartEndless();
                return;
            }
            GameManager.Mode = GameMode.GroupRelay;
            if (FreshGroup()) { PopulateChips(); Show(setupRoot); }
            else StartGame();
        }

        // Endless (Vrij rijden): skip the team-select screen entirely and drop straight into a solo run with
        // lives. The mode flag makes GameManager use lives instead of the timer and commit nothing to the class.
        private void StartEndless()
        {
            GameManager.Mode = GameMode.Endless;
            teamName = "SPELER"; // endless has no team; a non-empty name keeps the game-over kicker/label code happy
            StartGame();
        }

        // Endless "STOPPEN": abandon the run and go back to the title. Commits nothing (endless never touches the
        // group total or leaderboard).
        private void BackToTitle() { if (GameManager.Instance != null) GameManager.Instance.ForceReset(); }

        private void Show(GameObject only)
        {
            // The global "tap anywhere to start" stays off while ANY framing screen is up: the Title only
            // advances via its ANZA! CTA (which routes a fresh group through the team select), and on the team
            // select a stray tap must never start the game with the default name.
            GameManager.AllowTapToStart = only == null;

            SetActive(titleRoot, only == titleRoot);
            SetActive(setupRoot, only == setupRoot);
            SetActive(relayRoot, only == relayRoot);
            SetActive(journeyRoot, only == journeyRoot);
            SetActive(gameOverRoot, only == gameOverRoot);
            SetActive(nameRoot, false); // the name-entry modal only lives over the setup screen; drop it on any transition
            // Keep the drive-side toggle in step with the live config (it may have been changed in the facilitator menu).
            if (only == titleRoot) RefreshModeToggle();
            if (only != null) FadeIn(only);

            // Tell the audio layer whether a framing screen is now covering the world: it cues the UI open sound and
            // ducks the game-world beds (ambience, soundscape) so nothing from the game drones behind the menu. This
            // is the single source of truth for menu visibility — it also fires for the relay hand-off reached via a
            // checkpoint, where the GameState is still AtCheckpoint but a screen is up. (Edit-time Build() calls Show
            // too; skip the signal there — there is no audio to drive and no listeners in the editor.)
            if (Application.isPlaying) GameEvents.RaiseMenuScreenChanged(only != null);
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

        // ✓ pip discs ahead of the clean-overtake count — shape + colour, never colour alone (capped at 5
        // so the line can't overflow on a big run; the number still tells the exact story). The pips are
        // sprite check marks built hidden by the Build half; this shows N and shifts the text to make room.
        private static void SetPips(Image[] pips, TMP_Text stat, float baseLeft, int n)
        {
            if (pips == null || stat == null) return;
            int shown = Mathf.Clamp(n, 0, pips.Length);
            for (int i = 0; i < pips.Length; i++)
                if (pips[i] != null) pips[i].gameObject.SetActive(i < shown);
            var rt = stat.rectTransform;
            rt.offsetMin = new Vector2(baseLeft + (shown > 0 ? shown * 42f + 10f : 0f), rt.offsetMin.y); // 42 = the pip ring pitch
        }

        // "SIMBA" → "TEAM SIMBA" for the kickers, and "Team Simba" for the board — the mock's voice.
        private string TeamLabel()
        {
            string s = (teamName ?? "").Trim();
            return s.StartsWith("TEAM", System.StringComparison.OrdinalIgnoreCase) ? s.ToUpperInvariant() : "TEAM " + s.ToUpperInvariant();
        }

        private static string BoardLabel(string raw)
        {
            string s = (raw ?? "").Trim();
            if (s.Length == 0) return "";
            if (s.StartsWith("TEAM", System.StringComparison.OrdinalIgnoreCase)) s = s.Substring(4).Trim();
            s = s.ToLowerInvariant();
            return "Team " + char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        private void PopulateRelay()
        {
            // Commit the turn BEFORE reading its score and the class total. On the checkpoint-relay path this
            // screen and GameManager.CommitTurn both fire on CheckpointReached, and the event does not guarantee
            // GameManager runs first — without this the relay reads FinalScore/group total before they are written
            // and shows "+0 · KLAS TOTAAL 0" on a turn that actually earned points. CommitTurn is idempotent.
            if (GameManager.Instance != null) GameManager.Instance.CommitTurn();
            int turn = TurnScore();
            int o = Overtakes();
            if (relayKicker != null) relayKicker.text = "BEURT KLAAR  ·  " + TeamLabel();
            if (relayScore != null) relayScore.text = "+" + Group(turn); // the turn reads as a GAIN
            if (relayTotal != null) relayTotal.text = Group(GroupTotal()); // the caption lives in the panel build (two-line layout)
            if (relayJoin != null)  relayJoin.text  = "+" + Group(turn) + " ERBIJ";
            if (relayStat != null)  relayStat.text  = o + " SCHONE INHAALACTIES";
            SetPips(relayPips, relayStat, 0f, o);
        }

        private void PopulateJourney()
        {
            int total = GroupTotal();
            int rank = LeaderboardManager.Instance != null ? LeaderboardManager.Instance.RankOf(total) : 1;
            if (journeyKicker != null) journeyKicker.text = "MWISHO WA SAFARI  ·  " + TeamLabel(); // Swahili accent in the kicker
            if (journeyTotal != null) journeyTotal.text = Group(total);
            if (journeyRank != null)  journeyRank.text  = "PLEK " + rank + " VAN DE KLAS";
            if (journeyStat != null)  journeyStat.text  = TurnCount() + (TurnCount() == 1 ? " SPELER" : " SPELERS") + "  ·  SAMEN GEREDEN";

            // The saved local leaderboard (this group was just committed into it). This group's row gets the
            // full accent highlight + a dark rank medallion, so it reads across the classroom; the others sit
            // on the quiet zebra with a translucent medallion.
            var entries = LeaderboardManager.Instance != null ? LeaderboardManager.Instance.Entries : null;
            var last = LeaderboardManager.Instance != null ? LeaderboardManager.Instance.LastCommitted : null;
            for (int i = 0; i < boardName.Count; i++)
            {
                bool has = entries != null && i < entries.Count;
                bool mine = false;
                if (has)
                {
                    var e = entries[i];
                    // "Mine" by reference when the commit happened this session, by NAME otherwise — after an
                    // app restart the entries are re-read from disk and the reference match alone comes up
                    // empty, which left the board with no accent row at all (play-test screenshot).
                    mine = e == last || string.Equals(e.label, teamName, System.StringComparison.OrdinalIgnoreCase);
                    boardName[i].text = BoardLabel(e.label); // "Team Simba", the mock's voice
                    boardScore[i].text = Group(e.score);
                    Color c = mine ? UiKit.InkOnAccent : ink;
                    boardName[i].color = c; boardScore[i].color = c;
                }
                else { boardName[i].text = ""; boardScore[i].text = ""; }

                if (i < boardRowBg.Count && boardRowBg[i] != null) boardRowBg[i].enabled = mine;
                if (i < boardStripe.Count && boardStripe[i] != null) boardStripe[i].enabled = has; // no ghost rows on a young board
                if (i < boardRankBg.Count && boardRankBg[i] != null)
                {
                    boardRankBg[i].enabled = has;
                    boardRankBg[i].color = mine ? UiKit.InkOnAccent : UiKit.WithAlpha(ink, 0.14f);
                }
                if (i < boardRank.Count && boardRank[i] != null)
                {
                    boardRank[i].text = has ? (i + 1).ToString() : "";
                    boardRank[i].color = mine ? UiKit.AccentSoft : ink;
                }
            }
        }

        private void PopulateGameOver()
        {
            if (GameManager.Instance != null) GameManager.Instance.CommitTurn(); // idempotent; keep the score read-after-commit like the relay screen
            int o = Overtakes();
            bool endless = GameManager.Mode == GameMode.Endless;
            if (goKicker != null) goKicker.text = endless ? "SPEL VOORBIJ" : "OEPS  ·  " + TeamLabel();
            if (goScore != null) goScore.text = Group(TurnScore());
            if (goStat != null)  goStat.text  = o + " SCHONE INHAALACTIES";
            SetPips(goPips, goStat, 26f, o);

            // The second action swaps by mode: group relay hands the tablet to the next teammate; endless has no
            // relay, so it just STOPS back to the title. (The first button, NOG EEN KEER → StartGame, works for
            // both — in endless it restarts a fresh endless run because Mode is still Endless.)
            if (goSecondButton != null)
            {
                goSecondButton.onClick.RemoveAllListeners();
                if (endless) goSecondButton.onClick.AddListener(BackToTitle);
                else         goSecondButton.onClick.AddListener(NextPlayer);
            }
            if (goSecondLabel != null) goSecondLabel.text = endless ? "STOPPEN" : "VOLGENDE SPELER";
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

        private bool FreshGroup() => GroupScoreManager.Instance == null || GroupScoreManager.Instance.TurnCount == 0;

        // ---- drive-side toggle state (segments built in KenyaMenuScreens.Build.cs) ----
        // Active segment = cream fill + warm-brown ink; inactive = no fill (the pill behind shows through),
        // warm-white ink — the segmented-pill pattern the settings menu's BASIS|EXPERT switch established.
        private void RefreshModeToggle()
        {
            bool left = CurrentDriveLeft();
            foreach (var m in modeButtons)
            {
                bool active = m.leftMode == left;
                m.bg.color = active ? cream : new Color(1f, 1f, 1f, 0f); // alpha 0 stays tappable
                m.label.color = active ? inkOnLight : ink;
            }
        }

        // ---- team select ----------------------------------------------------------------
        // v2.1 (play-test): tapping a card STARTS THE GAME with that name — the confirm step was one tap
        // too many for the relay pace. The cards keep the v2 look (roundel + Dutch animal) and got their
        // "tap me" pulse wave back.
        private RectTransform chipRow;
        private const int ChipsShown = 6;
        // Swahili animal names. Easily extend for more uniqueness; pick from those not yet on the local board.
        private static readonly string[] TeamNamePool =
        {
            "SIMBA","TWIGA","CHUI","TEMBO","FARU","KIBOKO","NYATI","DUMA","SWALA","NGIRI",
            "FISI","KOBE","TAI","KORONGO","POPO","NYANI","NGAMIA","SUNGURA","MAMBA","NYOKA"
        };

        // The Dutch animal under each Swahili name — the design's "Swahili taught in passing".
        private static readonly Dictionary<string, string> DutchAnimal = new()
        {
            { "SIMBA", "Leeuw" },     { "TWIGA", "Giraffe" },     { "CHUI", "Luipaard" },
            { "TEMBO", "Olifant" },   { "FARU", "Neushoorn" },    { "KIBOKO", "Nijlpaard" },
            { "NYATI", "Buffel" },    { "DUMA", "Jachtluipaard" },{ "SWALA", "Gazelle" },
            { "NGIRI", "Wrattenzwijn" }, { "FISI", "Hyena" },     { "KOBE", "Schildpad" },
            { "TAI", "Arend" },       { "KORONGO", "Kraanvogel" },{ "POPO", "Vleermuis" },
            { "NYANI", "Baviaan" },   { "NGAMIA", "Kameel" },     { "SUNGURA", "Haas" },
            { "MAMBA", "Krokodil" },  { "NYOKA", "Slang" }
        };

        // Card + selected-state colours (from the improved design): a translucent dark card with a faint
        // hairline at rest; accent-tinted fill, accent border and an accent roundel once selected.
        private static readonly Color CardRestFill = new Color(0.102f, 0.075f, 0.063f, 0.62f); // rgba(26,19,16,.62)

        // Fills the setup screen with a random subset of names NOT already on this iPad's local leaderboard, so two
        // groups never share a name (until the pool runs out). Re-run each time a fresh group reaches the setup screen.
        private void PopulateChips()
        {
            if (chipRow == null) return;
            ClearChildren(chipRow);

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

            // 3×2 grid of big team cards (initial roundel + name + Dutch animal). Wider than the text column
            // on purpose (play-test: the right of the screen sat empty), and each card pulses in a gentle
            // wave — the "tap me" invitation the play-tests liked.
            // The typed-name option is a facilitator opt-in (settings → Speelduur → "Eigen teamnaam toestaan"),
            // OFF by default so children don't get a free-text keyboard unless a supervisor turned it on.
            bool allowCustom = PlayerPrefs.GetInt("ksg.customName", 0) == 1;
            int n = Mathf.Min(allowCustom ? ChipsShown - 1 : ChipsShown, pool.Count); // last slot reserved for "EIGEN NAAM" only when allowed
            const float w = 500f, h = 130f, gapX = 20f, gapY = 16f;
            for (int i = 0; i < n; i++)
            {
                string pick = pool[i];
                int col = i % 3, rowIdx = i / 3;
                RectTransform b = NewRect(chipRow, "Card");
                b.anchorMin = new Vector2(0f, 1f); b.anchorMax = new Vector2(0f, 1f); b.pivot = new Vector2(0f, 1f);
                b.sizeDelta = new Vector2(w, h);
                b.anchoredPosition = new Vector2(col * (w + gapX), -rowIdx * (h + gapY));

                // Hairline border with the translucent dark fill 3px inside it.
                Image border = b.gameObject.AddComponent<Image>();
                border.sprite = UiKit.Rounded(UiKit.RadiusM); border.type = Image.Type.Sliced;
                border.color = UiKit.WithAlpha(ink, 0.12f);
                Image fill = AddImage(b, "Fill", CardRestFill, UiKit.Rounded(UiKit.RadiusM));
                fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = Vector2.one;
                fill.rectTransform.offsetMin = new Vector2(3f, 3f); fill.rectTransform.offsetMax = new Vector2(-3f, -3f);

                var btn = b.gameObject.AddComponent<Button>(); btn.targetGraphic = border;
                btn.onClick.AddListener(() => PickTeam(pick));
                var cb = btn.colors; cb.fadeDuration = 0.08f; cb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f); btn.colors = cb;

                Image roundel = AddImage(b, "Roundel", UiKit.WithAlpha(ink, 0.14f), UiKit.Rounded(32));
                Anchor(roundel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(64f, 64f), new Vector2(58f, 0f));
                TMP_Text initial = AddText(b, "Initial", pick.Substring(0, 1), 32, ink, TextAlignmentOptions.Center);
                initial.fontStyle = FontStyles.Bold;
                Anchor(initial.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(64f, 64f), new Vector2(58f, 0f));

                TMP_Text name = AddText(b, "Name", pick, 31, ink, TextAlignmentOptions.Left);
                name.fontStyle = FontStyles.Bold; UiKit.Caps(name, 0.03f);
                name.rectTransform.anchorMin = new Vector2(0f, 0.5f); name.rectTransform.anchorMax = new Vector2(1f, 0.5f); name.rectTransform.pivot = new Vector2(0f, 0.5f);
                name.rectTransform.offsetMin = new Vector2(108f, 4f); name.rectTransform.offsetMax = new Vector2(-16f, 46f);
                TMP_Text animal = AddText(b, "Animal", DutchAnimal.TryGetValue(pick, out var nl) ? nl : "", 19, muted, TextAlignmentOptions.Left);
                animal.rectTransform.anchorMin = new Vector2(0f, 0.5f); animal.rectTransform.anchorMax = new Vector2(1f, 0.5f); animal.rectTransform.pivot = new Vector2(0f, 0.5f);
                animal.rectTransform.offsetMin = new Vector2(108f, -38f); animal.rectTransform.offsetMax = new Vector2(-16f, -6f);

                // Gentle "tap me" wave (play-test: kids unsure what to tap) — a soft breath, not a bounce:
                // the default amplitude read as aggressive on cards this size.
                b.gameObject.AddComponent<UiPulse>().SetWave(i * 0.18f, 0.012f, 1.05f);
            }

            // One extra card in the same 3×2 grid (the last slot): type your OWN team name. Opens the on-screen
            // keyboard (KenyaMenuScreens.NameEntry.cs); the typed name routes through the SAME PickTeam(...) the
            // animal cards use, so it flows everywhere a team name already does. Accent-tinted so it reads as the
            // "or make your own" option next to the Swahili animals. Only shown when the facilitator allowed it.
            if (allowCustom)
            {
                int col = n % 3, rowIdx = n / 3;
                RectTransform b = NewRect(chipRow, "CardCustom");
                b.anchorMin = new Vector2(0f, 1f); b.anchorMax = new Vector2(0f, 1f); b.pivot = new Vector2(0f, 1f);
                b.sizeDelta = new Vector2(w, h);
                b.anchoredPosition = new Vector2(col * (w + gapX), -rowIdx * (h + gapY));

                Image border = b.gameObject.AddComponent<Image>();
                border.sprite = UiKit.Rounded(UiKit.RadiusM); border.type = Image.Type.Sliced;
                border.color = UiKit.WithAlpha(accent, 0.65f);
                Image fill = AddImage(b, "Fill", UiKit.WithAlpha(accent, 0.14f), UiKit.Rounded(UiKit.RadiusM));
                fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = Vector2.one;
                fill.rectTransform.offsetMin = new Vector2(3f, 3f); fill.rectTransform.offsetMax = new Vector2(-3f, -3f);

                var btn = b.gameObject.AddComponent<Button>(); btn.targetGraphic = border;
                btn.onClick.AddListener(OpenNameEntry);
                var cb = btn.colors; cb.fadeDuration = 0.08f; cb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f); btn.colors = cb;

                TMP_Text label = AddText(b, "Name", "EIGEN NAAM", 31, ink, TextAlignmentOptions.Center);
                label.fontStyle = FontStyles.Bold; UiKit.Caps(label, 0.03f);
                label.rectTransform.anchorMin = new Vector2(0f, 1f); label.rectTransform.anchorMax = new Vector2(1f, 1f); label.rectTransform.pivot = new Vector2(0.5f, 1f);
                label.rectTransform.offsetMin = new Vector2(16f, -78f); label.rectTransform.offsetMax = new Vector2(-16f, -34f);
                TMP_Text sub = AddText(b, "Sub", "typ je eigen teamnaam", 19, muted, TextAlignmentOptions.Center);
                sub.rectTransform.anchorMin = new Vector2(0f, 0f); sub.rectTransform.anchorMax = new Vector2(1f, 0f); sub.rectTransform.pivot = new Vector2(0.5f, 0f);
                sub.rectTransform.offsetMin = new Vector2(16f, 28f); sub.rectTransform.offsetMax = new Vector2(-16f, 62f);

                b.gameObject.AddComponent<UiPulse>().SetWave(n * 0.18f, 0.012f, 1.05f);
            }
        }

        private void PickTeam(string name) { teamName = name; StartGame(); }

        private void ClearChildren(RectTransform rt)
        {
            if (rt == null) return;
            for (int i = rt.childCount - 1; i >= 0; i--)
            {
                var c = rt.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
            }
        }
    }
}
