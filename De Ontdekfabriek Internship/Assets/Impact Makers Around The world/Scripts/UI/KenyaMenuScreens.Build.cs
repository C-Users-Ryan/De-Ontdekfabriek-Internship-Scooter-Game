// Construction half of KenyaMenuScreens — UI v2 (2026-07-04): the improved framing screens.
// Drop-in replacement for Assets/Impact Makers Around The world/Scripts/UI/KenyaMenuScreens.Build.cs.
// Pairs with the v2 KenyaMenuScreens.cs (runtime half) — install BOTH files together.
//
// What changed vs v1 (per "Kenya Game UI — Improved" / huisstijl spec):
//   • Title follows the huisstijl title-page rule: composition CENTRED, one action (logo ring removed 2026-07-06).
//   • Primary buttons are the spec token: accent #F19141 fill, ink-on-accent text (pressed = burnt).
//     Secondary buttons are the spec outline: 2px accent border, warm-white text.
//   • Drive-side toggle is ONE segmented pill, on the Title screen only (removed from Team select).
//   • Team select: chips become 48dp+ team cards with initial roundels + Dutch animal names,
//     a visible selected state and an explicit confirm button (built in the runtime half).
//   • Relay: turn score reads as a gain (+, accent); ✓ pips; class total on a spec panel with a
//     gold "+X ERBIJ" join chip. Coaching report + route strip unchanged.
//   • Journey: score in accent, placement on a gold chip, leaderboard on the spec panel
//     (92% surface, rust hairline) with rank medallions and a full accent row for this group.
//   • Game over: encouraging line in italic, the clean-overtake count on a success card,
//     second action "VOLGENDE SPELER" hands the tablet on without retrying.
//   • All "A109" strings removed.
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
    public sealed partial class KenyaMenuScreens
    {
        // ---- build ------------------------------------------------------------------
        [ContextMenu("Rebuild now")]
        public void Build()
        {
            ClearGenerated();
            boardName.Clear(); boardScore.Clear(); boardRank.Clear(); boardRankBg.Clear(); boardRowBg.Clear(); boardStripe.Clear();
            modeButtons.Clear();
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
            // Centred per the huisstijl title-page rule: the composition is centred; one clear action.
            // The Title is the FRONT DOOR: it always shows first. Only the ANZA! CTA advances it — a stray
            // press anywhere on the screen must NOT start the game (removed the full-screen tap so the screen
            // holds until the button is pressed).
            // v2.2 (play-test): the mock's scenery is back (dark dusk top, sun, ridges, skyline), the
            // stack breathes, the CTA is unmistakably the biggest thing, and the drive-side chooser is
            // quieted to the FOOT of the screen — a facilitator control, not a player one.
            RectTransform root = MakeScreen("Title", Hex("#3A1409"), Hex("#F3AD54"), centerFocus: true, skyline: true);
            BeginColumn(root, 344f, centered: true);
            CenterKicker("IMPACT MAKERS AROUND THE WORLD");
            Gap(6f);
            Hero("Hero", "KENYA", 170f);
            Gap(8f);
            BuildRouteMotif(); // ● NAIROBI – – – – – MOMBASA ⚡ (the journey premise, as a picture)
            Gap(48f);
            BuildTitleCta("ANZA!  ·  TIK OM TE STARTEN");
            BuildModeToggle(root); // pinned to the bottom edge, small — never competing with the CTA
            return root.gameObject;
        }

        // The Nairobi → Mombasa route motif under the title: departure dot, dashed road, destination bolt.
        private void BuildRouteMotif()
        {
            RectTransform row = Place("Route", 42f, 0f);
            RectTransform c = NewRect(row, "Motif");
            Anchor(c, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(640f, 42f), Vector2.zero);

            Color warm = Hex("#FFD9AD");
            Image dot = AddImage(c, "Dot", accent, UiKit.Rounded(8));
            Anchor(dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 16f), new Vector2(12f, 0f));
            TMP_Text a = AddText(c, "Nairobi", "NAIROBI", 27, warm, TextAlignmentOptions.Left);
            a.fontStyle = FontStyles.Bold; UiKit.Caps(a, 0.12f);
            a.rectTransform.anchorMin = new Vector2(0f, 0.5f); a.rectTransform.anchorMax = new Vector2(0f, 0.5f); a.rectTransform.pivot = new Vector2(0f, 0.5f);
            a.rectTransform.sizeDelta = new Vector2(190f, 34f); a.rectTransform.anchoredPosition = new Vector2(30f, 0f);
            for (int i = 0; i < 6; i++)
            {
                Image dash = AddImage(c, "Dash" + i, accent, UiKit.Rounded(2));
                Anchor(dash.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 5f), new Vector2(216f + i * 27f, 0f));
            }
            TMP_Text b = AddText(c, "Mombasa", "MOMBASA", 27, warm, TextAlignmentOptions.Left);
            b.fontStyle = FontStyles.Bold; UiKit.Caps(b, 0.12f);
            b.rectTransform.anchorMin = new Vector2(0f, 0.5f); b.rectTransform.anchorMax = new Vector2(0f, 0.5f); b.rectTransform.pivot = new Vector2(0f, 0.5f);
            b.rectTransform.sizeDelta = new Vector2(210f, 34f); b.rectTransform.anchoredPosition = new Vector2(392f, 0f);
            Image bolt = AddImage(c, "Bolt", UiKit.Gold, BoltSprite());
            Anchor(bolt.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(28f, 28f), new Vector2(614f, 0f));
        }

        // The one hero action: a big flat accent pill with the mock's soft breathing pulse.
        private void BuildTitleCta(string label)
        {
            RectTransform row = Place("Cta", 132f, 0f);
            float w = label.Length * 20f + 180f;
            RectTransform b = NewRect(row, "Button");
            Anchor(b, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(w, 120f), Vector2.zero);
            UiKit.AddDropShadow(b, UiKit.RadiusXl, 0.38f, 34f, 12f);
            Image img = b.gameObject.AddComponent<Image>(); img.sprite = UiKit.Rounded(60); img.type = Image.Type.Sliced; img.color = accent;
            var btn = b.gameObject.AddComponent<Button>(); btn.targetGraphic = img; btn.onClick.AddListener(BeginFromTitle);
            var cb = btn.colors; cb.fadeDuration = 0.08f;
            cb.highlightedColor = new Color(1.04f, 1.04f, 1.04f, 1f);
            cb.pressedColor = new Color(0.82f, 0.58f, 0.35f, 1f); // burnt
            btn.colors = cb;
            TMP_Text t = AddText(b, "Label", label, 34, UiKit.InkOnAccent, TextAlignmentOptions.Center);
            t.fontStyle = FontStyles.Bold; UiKit.Caps(t, 0.06f); Stretch(t.rectTransform);
            b.gameObject.AddComponent<UiPulse>().SetWave(0f, 0.015f, 0.42f); // the mock's slow ctapulse
        }

        private GameObject BuildSetup()
        {
            RectTransform root = MakeScreen("TeamSetup", Hex("#3A1409"), Hex("#F3AD54"), skyline: true);
            BeginColumn(root, 250f);
            KickerRow("NIEUWE GROEP  ·  KIES JE NAAM");
            Hero("Hero", "KIES JE TEAM", 92f);
            // v2.1 (play-test): tapping a card starts the game directly again — the extra confirm step
            // was one tap too many for the relay pace. The loud prompt says exactly that.
            Spaced(Line("Sub", "TIK EEN NAAM OM TE SPELEN", 34f, cream), 0.10f);
            Gap(22f);
            chipRow = Place("Chips", 276f, 0f); // filled per show by PopulateChips (team cards, 3×2)
            return root.gameObject;
        }

        private GameObject BuildRelay()
        {
            // v2.4 (mock parity, strict): EXACTLY the mock's content, nothing else — kicker, giant gain,
            // ring pips, the two-line class panel, two stacked actions. The route strip and the JOUW RIT
            // coaching panel are GONE (play-test verdict: not in the design, added noise).
            RectTransform root = MakeScreen("Relay", Hex("#2A0F08"), Hex("#F0A64C"));
            BeginColumn(root, 400f);
            relayKicker = KickerRow("BEURT KLAAR  ·  " + teamName);
            relayScore = Hero("Score", "+3 503", 240f);
            relayScore.color = accent; // the turn reads as a GAIN in the signature accent
            relayStat = Line("Stat", "0 SCHONE INHAALACTIES", 36f, success);
            relayPips = BuildPips((RectTransform)relayStat.rectTransform.parent, 20f, 0f); // ✓ rings ahead of the count
            Gap(10f);
            BuildClassTotalPanel();    // the hand-off story: your points visibly join the class total
            Gap(22f);
            ButtonRow("VOLGENDE SPELER  →", NextPlayer, true);
            Gap(14f);
            ButtonRow("LAATSTE SPELER · EINDSTAND", ShowStandings, false); // stacked, like the mock
            return root.gameObject;
        }

        // A row of ✓ pips — the mock's style: a green RING with the check drawn inside (the font carries no
        // ✓ glyph, so the mark is a sprite). Built hidden; the runtime shows 0-5 of them and shifts the stat
        // text right to make room.
        private Image[] BuildPips(RectTransform host, float xBase, float y)
        {
            var pips = new Image[5];
            for (int i = 0; i < pips.Length; i++)
            {
                Image disc = AddImage(host, "Pip" + i, success, UiKit.Rounded(17));
                Anchor(disc.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(34f, 34f), new Vector2(xBase + i * 42f, y));
                Image hole = AddImage(disc.rectTransform, "Hole", new Color(0.078f, 0.055f, 0.039f, 0.88f), UiKit.Rounded(14));
                hole.rectTransform.anchorMin = Vector2.zero; hole.rectTransform.anchorMax = Vector2.one;
                hole.rectTransform.offsetMin = new Vector2(4f, 4f); hole.rectTransform.offsetMax = new Vector2(-4f, -4f);
                Image mark = AddImage(disc.rectTransform, "Check", Hex("#A4E088"), CheckSprite());
                Anchor(mark.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(19f, 19f), Vector2.zero);
                disc.gameObject.SetActive(false);
                pips[i] = disc;
            }
            return pips;
        }

        // A drawn check mark: two anti-aliased strokes, the same "no font glyph gambles" rule as the
        // HowToPlayOverlay arrows. Cached; used by the accomplishment pips.
        private Sprite _check;
        private Sprite CheckSprite()
        {
            if (_check != null) return _check;
            int s = 64; var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            Vector2 a = new(0.16f, 0.52f), b = new(0.40f, 0.28f), c = new(0.86f, 0.74f);
            const float half = 0.085f; // stroke half-thickness in UV
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
            {
                var p = new Vector2((x + 0.5f) / s, (y + 0.5f) / s);
                float d = Mathf.Min(DistToSegment(p, a, b), DistToSegment(p, b, c));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((half - d) * s * 0.5f)));
            }
            tex.Apply();
            _check = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
            return _check;
        }

        private static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }

        // The class total on a spec panel (surface @92%, rust hairline) with a gold "joins the total" chip.
        // v2.3: the mock's two-line layout — small caption on top, the BIG number under it, chip on the right.
        private void BuildClassTotalPanel()
        {
            RectTransform row = Place("ClassTotal", 176f, 16f);
            RectTransform panel = NewRect(row, "Panel");
            panel.anchorMin = new Vector2(0f, 0.5f); panel.anchorMax = new Vector2(0f, 0.5f); panel.pivot = new Vector2(0f, 0.5f);
            panel.sizeDelta = new Vector2(820f, 176f); panel.anchoredPosition = Vector2.zero;
            Image hair = panel.gameObject.AddComponent<Image>();
            hair.sprite = UiKit.Rounded(UiKit.RadiusL); hair.type = Image.Type.Sliced;
            hair.color = UiKit.WithAlpha(UiKit.Rust, 0.55f); hair.raycastTarget = false;
            Image fill = AddImage(panel, "Fill", new Color(0.102f, 0.075f, 0.063f, 0.94f), UiKit.Rounded(UiKit.RadiusL));
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = new Vector2(2f, 2f); fill.rectTransform.offsetMax = new Vector2(-2f, -2f);

            TMP_Text cap = AddText(panel, "Caption", "KLAS TOTAAL", 22, muted, TextAlignmentOptions.Left);
            cap.fontStyle = FontStyles.Bold; UiKit.Caps(cap, 0.14f);
            cap.rectTransform.anchorMin = new Vector2(0f, 1f); cap.rectTransform.anchorMax = new Vector2(0.6f, 1f); cap.rectTransform.pivot = new Vector2(0f, 1f);
            cap.rectTransform.sizeDelta = new Vector2(0f, 30f); cap.rectTransform.anchoredPosition = new Vector2(38f, -26f);

            relayTotal = AddText(panel, "Total", "12 480", 80, ink, TextAlignmentOptions.Left);
            relayTotal.fontStyle = FontStyles.Bold;
            relayTotal.rectTransform.anchorMin = new Vector2(0f, 0f); relayTotal.rectTransform.anchorMax = new Vector2(0.6f, 1f); relayTotal.rectTransform.pivot = new Vector2(0f, 0.5f);
            relayTotal.rectTransform.offsetMin = new Vector2(38f, 10f); relayTotal.rectTransform.offsetMax = new Vector2(0f, -52f);

            RectTransform chip = NewRect(panel, "Join");
            chip.anchorMin = new Vector2(1f, 0.5f); chip.anchorMax = new Vector2(1f, 0.5f); chip.pivot = new Vector2(1f, 0.5f);
            chip.sizeDelta = new Vector2(300f, 66f); chip.anchoredPosition = new Vector2(-30f, 0f);
            Image cImg = chip.gameObject.AddComponent<Image>();
            cImg.sprite = UiKit.Rounded(UiKit.RadiusS); cImg.type = Image.Type.Sliced; cImg.color = UiKit.Gold; cImg.raycastTarget = false;
            relayJoin = AddText(chip, "t", "+3 503 ERBIJ", 26, UiKit.InkOnAccent, TextAlignmentOptions.Center);
            relayJoin.fontStyle = FontStyles.Bold; UiKit.Caps(relayJoin, 0.03f); Stretch(relayJoin.rectTransform);
        }

        private GameObject BuildJourney()
        {
            // v2.4 (mock parity, strict): kicker, giant total, gold placement chip, green stat, one button,
            // the board on the right — and nothing else (the route map is gone; not in the design).
            RectTransform root = MakeScreen("Journey", Hex("#241026"), Hex("#EB9A44")); // dusk-purple top per the mock's arrival
            BeginColumn(root, 330f);
            journeyKicker = KickerRow("MWISHO WA SAFARI  ·  " + teamName);
            journeyTotal = Hero("Total", "12 480", 240f);
            journeyTotal.color = accent;
            BuildRankChip();
            journeyStat = Line("Stat", "1 SPELERS · SAMEN GEREDEN", 30f, success);
            Gap(26f);
            ButtonRow("NIEUWE GROEP", NewGroup);
            BuildBoard(root);
            return root.gameObject;
        }

        // Placement on a brand-gold chip ("streak-tier / celebration" colour in the spec) instead of a plain line.
        private void BuildRankChip()
        {
            RectTransform row = Place("Rank", 70f, 16f);
            RectTransform chip = NewRect(row, "Chip");
            chip.anchorMin = new Vector2(0f, 0.5f); chip.anchorMax = new Vector2(0f, 0.5f); chip.pivot = new Vector2(0f, 0.5f);
            chip.sizeDelta = new Vector2(520f, 68f); chip.anchoredPosition = Vector2.zero;
            Image g = chip.gameObject.AddComponent<Image>();
            g.sprite = UiKit.Rounded(UiKit.RadiusS); g.type = Image.Type.Sliced; g.color = UiKit.Gold; g.raycastTarget = false;
            journeyRank = AddText(chip, "t", "PLEK 1 VAN DE KLAS", 30, UiKit.InkOnAccent, TextAlignmentOptions.Center);
            journeyRank.fontStyle = FontStyles.Bold; UiKit.Caps(journeyRank, 0.04f); Stretch(journeyRank.rectTransform);
        }

        // The class scoreboard, v2: spec panel (solid 92% surface + rust hairline), rank medallions,
        // and a full accent highlight row for this group (colours applied in PopulateJourney).
        private void BuildBoard(RectTransform root)
        {
            // v2.5: the mock's ACTUAL measurements (its cqw ≈ 20.5px on this canvas): a compact list of
            // 114px rows — 64px medallions, 40px bold mixed-case names, scores flush right — inside an
            // 850-wide panel. Rows sit tight like a list, not floating pills with air between them.
            const float rowPitch = 114f, rowH = 106f, firstY = -122f;
            RectTransform card = NewRect(root, "Scoreboard");
            Anchor(card, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(850f, 880f), new Vector2(-460f, 0f));
            UiKit.AddDropShadow(card, UiKit.RadiusXl, 0.34f, 30f, 12f);
            Image hair = card.gameObject.AddComponent<Image>();
            hair.sprite = UiKit.Rounded(UiKit.RadiusXl); hair.type = Image.Type.Sliced;
            hair.color = UiKit.WithAlpha(UiKit.Rust, 0.55f); hair.raycastTarget = false;
            Image fill = AddImage(card, "Fill", new Color(0.102f, 0.075f, 0.063f, 0.94f), UiKit.Rounded(UiKit.RadiusXl));
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = new Vector2(2f, 2f); fill.rectTransform.offsetMax = new Vector2(-2f, -2f);
            // (No top sheen on this card — it brightened the first row's stripe, so the zebra looked uneven.)

            TMP_Text head = AddText(card, "Head", "KLASSEMENT", 32, kicker, TextAlignmentOptions.Left);
            head.fontStyle = FontStyles.Bold;
            head.rectTransform.anchorMin = new Vector2(0f, 1f); head.rectTransform.anchorMax = new Vector2(0f, 1f); head.rectTransform.pivot = new Vector2(0f, 1f);
            head.rectTransform.sizeDelta = new Vector2(500f, 38f); head.rectTransform.anchoredPosition = new Vector2(40f, -40f); Spaced(head, 0.2f);

            for (int i = 0; i < 6; i++)
            {
                float y = firstY - i * rowPitch;      // top edge of this row
                float mid = y - rowH * 0.5f;          // vertical centre of this row
                // Quiet zebra strip behind alternate rows (hidden by PopulateJourney when the row is empty,
                // so a young leaderboard doesn't show five ghost rows).
                Image rowStripe = null;
                if (i % 2 == 0)
                {
                    rowStripe = AddImage(card, "Row" + i, UiKit.WithAlpha(Color.white, 0.035f), UiKit.Rounded(24)); // whisper-quiet zebra, like the mock
                    rowStripe.rectTransform.anchorMin = new Vector2(0f, 1f); rowStripe.rectTransform.anchorMax = new Vector2(1f, 1f); rowStripe.rectTransform.pivot = new Vector2(0.5f, 1f);
                    rowStripe.rectTransform.offsetMin = new Vector2(22f, 0f); rowStripe.rectTransform.offsetMax = new Vector2(-22f, 0f);
                    rowStripe.rectTransform.sizeDelta = new Vector2(rowStripe.rectTransform.sizeDelta.x, rowH);
                    rowStripe.rectTransform.anchoredPosition = new Vector2(0f, y);
                }
                boardStripe.Add(rowStripe);
                // Full-width accent highlight for THIS group's row (hidden for every other row).
                Image hl = AddImage(card, "Hl" + i, accent, UiKit.Rounded(26));
                hl.rectTransform.anchorMin = new Vector2(0f, 1f); hl.rectTransform.anchorMax = new Vector2(1f, 1f); hl.rectTransform.pivot = new Vector2(0.5f, 1f);
                hl.rectTransform.offsetMin = new Vector2(18f, 0f); hl.rectTransform.offsetMax = new Vector2(-18f, 0f);
                hl.rectTransform.sizeDelta = new Vector2(hl.rectTransform.sizeDelta.x, rowH + 4f);
                hl.rectTransform.anchoredPosition = new Vector2(0f, y + 2f);
                hl.enabled = false;
                boardRowBg.Add(hl);

                // Rank medallion — the mock's 64px disc, centred on the row.
                Image disc = AddImage(card, "RankBg" + i, UiKit.WithAlpha(ink, 0.14f), UiKit.Rounded(32));
                Anchor(disc.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(64f, 64f), new Vector2(84f, mid));
                boardRankBg.Add(disc);
                TMP_Text rk = AddText(card, "Rank" + i, (i + 1).ToString(), 30, ink, TextAlignmentOptions.Center);
                rk.fontStyle = FontStyles.Bold;
                Anchor(rk.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(64f, 64f), new Vector2(84f, mid));
                boardRank.Add(rk);

                TMP_Text n = AddText(card, "n" + i, "", 40, ink, TextAlignmentOptions.Left);
                n.fontStyle = FontStyles.Bold;
                n.rectTransform.anchorMin = new Vector2(0f, 1f); n.rectTransform.anchorMax = new Vector2(0f, 1f); n.rectTransform.pivot = new Vector2(0f, 0.5f);
                n.rectTransform.sizeDelta = new Vector2(440f, 52f); n.rectTransform.anchoredPosition = new Vector2(134f, mid);
                TMP_Text s = AddText(card, "s" + i, "", 40, ink, TextAlignmentOptions.Right); s.fontStyle = FontStyles.Bold;
                s.rectTransform.anchorMin = new Vector2(1f, 1f); s.rectTransform.anchorMax = new Vector2(1f, 1f); s.rectTransform.pivot = new Vector2(1f, 0.5f);
                s.rectTransform.sizeDelta = new Vector2(260f, 52f); s.rectTransform.anchoredPosition = new Vector2(-42f, mid);
                boardName.Add(n); boardScore.Add(s);
            }
        }

        private GameObject BuildGameOver()
        {
            RectTransform root = MakeScreen("GameOver", Hex("#1F0F0A"), Hex("#CF7E3A"));
            BeginColumn(root, 300f);
            goKicker = KickerRow("OEPS  ·  " + teamName);
            goScore = Hero("Score", "9 240", 200f);
            TMP_Text soft = Line("Line", "Zelfs de beste chauffeurs hebben een off-dag.", 24f, Hex("#F3BE92"));
            soft.fontStyle = FontStyles.Italic; // a soft aside, not a verdict
            BuildAccomplishmentCard();
            Gap(10f);
            // Second action: hand the tablet on without retrying — matches the relay flow.
            ButtonRow("NOG EEN KEER", StartGame, "VOLGENDE SPELER", NextPlayer);
            return root.gameObject;
        }

        // The accomplishment is the visual centre: a success-tinted card, independent of the score.
        // No alarm red anywhere on this screen — it encourages, it doesn't judge.
        private void BuildAccomplishmentCard()
        {
            RectTransform row = Place("Accomplishment", 110f, 16f);
            RectTransform panel = NewRect(row, "Panel");
            panel.anchorMin = new Vector2(0f, 0.5f); panel.anchorMax = new Vector2(0f, 0.5f); panel.pivot = new Vector2(0f, 0.5f);
            panel.sizeDelta = new Vector2(690f, 110f); panel.anchoredPosition = Vector2.zero;
            Image hair = panel.gameObject.AddComponent<Image>();
            hair.sprite = UiKit.Rounded(UiKit.RadiusM); hair.type = Image.Type.Sliced;
            hair.color = UiKit.WithAlpha(success, 0.50f); hair.raycastTarget = false;
            Image fill = AddImage(panel, "Fill", new Color(0.404f, 0.706f, 0.306f, 0.13f), UiKit.Rounded(UiKit.RadiusM));
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = new Vector2(2f, 2f); fill.rectTransform.offsetMax = new Vector2(-2f, -2f);

            goStat = AddText(panel, "Stat", "0 SCHONE INHAALACTIES", 24, Hex("#A4E088"), TextAlignmentOptions.Left);
            goStat.fontStyle = FontStyles.Bold;
            goStat.rectTransform.anchorMin = new Vector2(0f, 1f); goStat.rectTransform.anchorMax = new Vector2(1f, 1f); goStat.rectTransform.pivot = new Vector2(0.5f, 1f);
            goStat.rectTransform.offsetMin = new Vector2(26f, 0f); goStat.rectTransform.offsetMax = new Vector2(-26f, 0f);
            goStat.rectTransform.sizeDelta = new Vector2(goStat.rectTransform.sizeDelta.x, 36f);
            goStat.rectTransform.anchoredPosition = new Vector2(0f, -20f);
            goPips = BuildPips(panel, 40f, 17f); // ✓ discs on the stat line (the panel centre sits 17px above it)

            TMP_Text sub = AddText(panel, "Sub", "Die punten tellen gewoon mee voor de klas.", 19, muted, TextAlignmentOptions.Left);
            sub.rectTransform.anchorMin = new Vector2(0f, 1f); sub.rectTransform.anchorMax = new Vector2(1f, 1f); sub.rectTransform.pivot = new Vector2(0.5f, 1f);
            sub.rectTransform.offsetMin = new Vector2(26f, 0f); sub.rectTransform.offsetMax = new Vector2(-26f, 0f);
            sub.rectTransform.sizeDelta = new Vector2(sub.rectTransform.sizeDelta.x, 28f);
            sub.rectTransform.anchoredPosition = new Vector2(0f, -62f);
        }

        // ---- Kenya / Dutch drive-side toggle (front of house, Title screen only) ------
        // v2.2: one SMALL segmented pill pinned to the bottom edge of the screen (the mock's placement) —
        // a facilitator control that must never outweigh the ANZA CTA. Same modeButtons contract;
        // RefreshModeToggle keeps working unchanged.
        private const string DriveSideKey = "world.driveLeft";

        private void BuildModeToggle(RectTransform root)
        {
            // v2.5 (2026-07-06): this caption floats on the open sunset sky with no card behind it, so the
            // light-orange kicker washed out (orange-on-orange, barely legible). Warm-white ink + bold + a
            // dark outline keeps it readable across the whole gradient — bright sky band to building silhouettes.
            TMP_Text cap = AddText(root, "ModeCap", "RIJMODUS  ·  KIES HET LAND", 18, ink, TextAlignmentOptions.Center);
            cap.fontStyle = FontStyles.Bold;
            Anchor(cap.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(700f, 24f), new Vector2(0f, 162f));
            UiKit.Caps(cap, 0.16f);
            var capOutline = cap.gameObject.AddComponent<Outline>();
            capOutline.effectColor = new Color(0f, 0f, 0f, 0.55f);
            capOutline.effectDistance = new Vector2(1.4f, -1.4f);

            RectTransform pill = NewRect(root, "ModePill");
            Anchor(pill, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(660f, 66f), new Vector2(0f, 106f));
            Image pillBg = pill.gameObject.AddComponent<Image>();
            pillBg.sprite = UiKit.Rounded(33); pillBg.type = Image.Type.Sliced;
            pillBg.color = new Color(0.07f, 0.04f, 0.03f, 0.62f); pillBg.raycastTarget = false;
            AddModeButton(pill, 5f, "KENIA  ·  LINKS", true);
            AddModeButton(pill, 331f, "NEDERLAND  ·  RECHTS", false);
        }

        private void AddModeButton(RectTransform pill, float x, string label, bool leftMode)
        {
            const float w = 324f, h = 56f;
            RectTransform b = NewRect(pill, "ModeButton");
            b.anchorMin = new Vector2(0f, 0.5f); b.anchorMax = new Vector2(0f, 0.5f); b.pivot = new Vector2(0f, 0.5f);
            b.sizeDelta = new Vector2(w, h); b.anchoredPosition = new Vector2(x, 0f);
            Image img = b.gameObject.AddComponent<Image>(); img.sprite = UiKit.Rounded(28); img.type = Image.Type.Sliced; img.color = cardFill;
            var btn = b.gameObject.AddComponent<Button>(); btn.targetGraphic = img; btn.onClick.AddListener(() => SetDriveMode(leftMode));
            var cb = btn.colors; cb.fadeDuration = 0.08f; btn.colors = cb;
            TMP_Text t = AddText(b, "Label", label, 19, ink, TextAlignmentOptions.Center); t.fontStyle = FontStyles.Bold; UiKit.Caps(t, 0.05f); Stretch(t.rectTransform);
            modeButtons.Add((img, t, leftMode));
        }

        private void SetDriveMode(bool driveLeft)
        {
            var def = SettingsCatalog.ById(DriveSideKey);
            if (def != null) GameSettings.Set(def, driveLeft ? 1f : 0f);
            else if (RoadSideConfig.Active != null) RoadSideConfig.Active.driveOnLeft = driveLeft;
            RefreshModeToggle();
        }

        private bool CurrentDriveLeft()
        {
            var def = SettingsCatalog.ById(DriveSideKey);
            if (def != null) return GameSettings.CurrentValue(def) >= 0.5f;
            return RoadSideConfig.Active == null || RoadSideConfig.Active.driveOnLeft;
        }

        // ---- deterministic top-down layout ------------------------------------------
        private RectTransform column;
        private float cursor;
        private bool columnCentered;

        private void BeginColumn(RectTransform root, float topY, bool centered = false)
        {
            columnCentered = centered;
            column = NewRect(root, "Content");
            if (centered)
            {
                column.anchorMin = new Vector2(0.5f, 0.5f); column.anchorMax = new Vector2(0.5f, 0.5f); column.pivot = new Vector2(0.5f, 1f);
                column.sizeDelta = new Vector2(1180f, 760f); column.anchoredPosition = new Vector2(0f, topY);
            }
            else
            {
                column.anchorMin = new Vector2(0f, 0.5f); column.anchorMax = new Vector2(0f, 0.5f); column.pivot = new Vector2(0f, 1f);
                column.sizeDelta = new Vector2(1180f, 760f); column.anchoredPosition = new Vector2(140f, topY);
            }
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
            // v2.3: mock-sized — a roomy, wide-tracked caption. (The brand logo ring that sat before it was
            // removed 2026-07-06 along with the other logo marks; the caption now starts at the column edge.)
            RectTransform row = Place("Kicker", 46f, 18f);
            TMP_Text k = AddText(row, "Text", text, 26, kicker, TextAlignmentOptions.Left);
            k.fontStyle = FontStyles.Bold;
            k.rectTransform.anchorMin = new Vector2(0f, 0.5f); k.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            k.rectTransform.pivot = new Vector2(0f, 0.5f); k.rectTransform.sizeDelta = new Vector2(1000f, 32f); k.rectTransform.anchoredPosition = new Vector2(0f, 0f);
            Spaced(k, 0.16f);
            return k;
        }

        // Centred kicker for the Title: just the caption, centred (huisstijl rule). The brand logo ring that
        // sat above it was removed 2026-07-06 with the other logo marks; the caption carries the title alone.
        private TMP_Text CenterKicker(string text)
        {
            RectTransform row = Place("Kicker", 40f, 14f);
            TMP_Text k = AddText(row, "Text", text, 22, kicker, TextAlignmentOptions.Center);
            k.rectTransform.anchorMin = new Vector2(0f, 0f); k.rectTransform.anchorMax = new Vector2(1f, 0f); k.rectTransform.pivot = new Vector2(0.5f, 0f);
            k.rectTransform.sizeDelta = new Vector2(0f, 28f); k.rectTransform.anchoredPosition = new Vector2(0f, 4f);
            Spaced(k, 0.20f);
            return k;
        }

        private TMP_Text Hero(string name, string text, float size)
        {
            RectTransform rt = Place(name, size * 1.05f, 10f);
            TMP_Text t = AddText(rt, "T", text, size, cream, columnCentered ? TextAlignmentOptions.Center : TextAlignmentOptions.Left);
            t.fontStyle = FontStyles.Bold; Stretch(t.rectTransform);
            return t;
        }

        private TMP_Text Line(string name, string text, float size, Color col)
        {
            RectTransform rt = Place(name, size * 1.45f, 20f);
            TMP_Text t = AddText(rt, "T", text, size, col, columnCentered ? TextAlignmentOptions.Center : TextAlignmentOptions.Left);
            Stretch(t.rectTransform);
            return t;
        }

        private static float ButtonWidth(string label) => label.Length * 18f + 110f;

        private void ButtonRow(string label, UnityEngine.Events.UnityAction action)
        {
            RectTransform row = Place("Buttons", 118f, 0f);
            float w = ButtonWidth(label);
            AddButton(row, columnCentered ? (1180f - w) * 0.5f : 0f, label, action, true);
        }

        // Single stacked row with an explicit primary/secondary role (the mock's relay stacks its actions).
        private void ButtonRow(string label, UnityEngine.Events.UnityAction action, bool primary)
        {
            RectTransform row = Place("Buttons", 118f, 0f);
            float w = ButtonWidth(label);
            AddButton(row, columnCentered ? (1180f - w) * 0.5f : 0f, label, action, primary);
        }

        private void ButtonRow(string l1, UnityEngine.Events.UnityAction a1, string l2, UnityEngine.Events.UnityAction a2)
        {
            RectTransform row = Place("Buttons", 118f, 0f);
            float w1 = ButtonWidth(l1), w2 = ButtonWidth(l2);
            float x0 = columnCentered ? (1180f - (w1 + 20f + w2)) * 0.5f : 0f;
            AddButton(row, x0, l1, a1, true);
            AddButton(row, x0 + w1 + 20f, l2, a2, false);
        }

        // Spec buttons, v2.4: mock-scale full-radius pills. PRIMARY = flat accent fill + ink-on-accent
        // text (pressed dims toward burnt); SECONDARY = outline (accent border on a translucent dark fill).
        private float AddButton(RectTransform row, float x, string label, UnityEngine.Events.UnityAction action, bool primary)
        {
            const float h = 110f;
            float w = ButtonWidth(label);
            RectTransform b = NewRect(row, "Button");
            b.anchorMin = new Vector2(0f, 0.5f); b.anchorMax = new Vector2(0f, 0.5f); b.pivot = new Vector2(0f, 0.5f);
            b.sizeDelta = new Vector2(w, h); b.anchoredPosition = new Vector2(x, 0f);

            UiKit.AddDropShadow(b, UiKit.RadiusXl, primary ? 0.32f : 0.20f, 24f, primary ? 9f : 6f);

            Image img = b.gameObject.AddComponent<Image>(); img.sprite = UiKit.Rounded(54); img.type = Image.Type.Sliced;
            img.color = primary ? accent : UiKit.WithAlpha(accent, 0.9f);
            if (!primary)
            {
                // Outline: the accent pill is the border; a dark fill sits 3px inside it.
                Image inner = AddImage(b, "Fill", new Color(0.10f, 0.05f, 0.03f, 0.80f), UiKit.Rounded(51));
                inner.rectTransform.anchorMin = Vector2.zero; inner.rectTransform.anchorMax = Vector2.one;
                inner.rectTransform.offsetMin = new Vector2(3f, 3f); inner.rectTransform.offsetMax = new Vector2(-3f, -3f);
            }
            var btn = b.gameObject.AddComponent<Button>(); btn.targetGraphic = img; if (action != null) btn.onClick.AddListener(action);
            var cb = btn.colors; cb.fadeDuration = 0.08f;
            cb.highlightedColor = new Color(1.04f, 1.04f, 1.04f, 1f);
            // Primary pressed = burnt (#C45416 ≈ accent × this tint); secondary brightens a touch.
            cb.pressedColor = primary ? new Color(0.82f, 0.58f, 0.35f, 1f) : new Color(1.15f, 1.10f, 1.06f, 1f);
            btn.colors = cb;

            // v2.1 (play-test): NO sheen overlay — the design's primary is a clean flat accent pill; the
            // white gradient read as a smear over the orange.

            TMP_Text t = AddText(b, "Label", label, 30, primary ? UiKit.InkOnAccent : ink, TextAlignmentOptions.Center);
            t.fontStyle = FontStyles.Bold; UiKit.Caps(t, 0.04f); Stretch(t.rectTransform);
            return w;
        }

        // The branded screen bed, v2.2 (play-test: "the detail and flare has been lost"): every framing screen
        // carries the mock's scenery — a dark dusk-to-warm gradient, a glowing sun, two mountain ridge bands
        // and a silhouette strip at the foot (Nairobi skyline on the branded front doors, savanna acacias on
        // the ride screens) — all drawn procedurally, no art assets. Left-column screens also get a dark left
        // scrim so warm-white text stays legible over the scenery.
        private RectTransform MakeScreen(string name, Color top, Color bottom, bool centerFocus = false, bool skyline = false)
        {
            RectTransform root = NewRect((RectTransform)transform, name);
            Stretch(root);
            root.gameObject.AddComponent<CanvasGroup>();
            Image bg = root.gameObject.AddComponent<Image>(); bg.sprite = GradientSprite(top, bottom); bg.color = Color.white; bg.raycastTarget = true;

            // Low sun on the horizon: a soft radial glow. Centred behind the title stack on the front door;
            // low-right on the left-column screens so it backs the scenery, not the text.
            Image sun = AddImage(root, "Sun", new Color(1f, 0.88f, 0.58f, 0.85f), RadialGlowSprite());
            if (centerFocus) Anchor(sun.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(980f, 980f), new Vector2(0f, 120f));
            else Anchor(sun.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(820f, 820f), new Vector2(-330f, 120f));

            // Two mountain ridge bands, far above near, like the mock's clip-path ranges.
            Image ridgeFar = AddImage(root, "RidgeFar", new Color(0.486f, 0.247f, 0.125f, 0.50f), RidgeSprite(0));
            ridgeFar.rectTransform.anchorMin = new Vector2(0f, 0.46f); ridgeFar.rectTransform.anchorMax = new Vector2(1f, 0.46f);
            ridgeFar.rectTransform.pivot = new Vector2(0.5f, 1f);
            ridgeFar.rectTransform.sizeDelta = new Vector2(0f, 200f); ridgeFar.rectTransform.anchoredPosition = Vector2.zero;
            Image ridgeNear = AddImage(root, "RidgeNear", new Color(0.427f, 0.204f, 0.09f, 0.68f), RidgeSprite(1));
            ridgeNear.rectTransform.anchorMin = new Vector2(0f, 0.40f); ridgeNear.rectTransform.anchorMax = new Vector2(1f, 0.40f);
            ridgeNear.rectTransform.pivot = new Vector2(0.5f, 1f);
            ridgeNear.rectTransform.sizeDelta = new Vector2(0f, 170f); ridgeNear.rectTransform.anchoredPosition = Vector2.zero;

            // The silhouette strip at the foot — Nairobi skyline on the branded front doors ONLY. The savanna
            // strip (acacias + animals) was CUT in v2.5: stretched to full screen width the pixel shapes read
            // as low-res, not charming (play-test verdict: "remove the animals, they look bad").
            if (skyline)
            {
                Image sil = AddImage(root, "Silhouette", new Color(0.137f, 0.075f, 0.04f, 0.96f), SkylineSprite());
                sil.rectTransform.anchorMin = new Vector2(0f, 0f); sil.rectTransform.anchorMax = new Vector2(1f, 0f);
                sil.rectTransform.pivot = new Vector2(0.5f, 0f);
                sil.rectTransform.sizeDelta = new Vector2(0f, 190f); sil.rectTransform.anchoredPosition = Vector2.zero;
            }

            // (The brand focus-circle motif — FocusRing / FocusRingInner — was removed 2026-07-06 along with
            // the rest of the logo marks, so no brand ring or circle appears anywhere in the UI now.)

            // Legibility scrim behind the left column (mock: linear-gradient(96deg, dark → transparent)).
            if (!centerFocus)
            {
                Image scrim = AddImage(root, "LeftScrim", new Color(0.047f, 0.027f, 0.012f, 0.80f), HorizontalScrimSprite());
                Stretch(scrim.rectTransform);
            }

            Image floor = AddImage(root, "Floor", new Color(0f, 0f, 0f, 0.24f), UiKit.TopSheen());
            floor.rectTransform.anchorMin = new Vector2(0f, 0f); floor.rectTransform.anchorMax = new Vector2(1f, 0f);
            floor.rectTransform.pivot = new Vector2(0.5f, 0f);
            floor.rectTransform.sizeDelta = new Vector2(0f, 520f); floor.rectTransform.anchoredPosition = Vector2.zero;
            floor.rectTransform.localScale = new Vector3(1f, -1f, 1f);
            return root;
        }

        // (The A109 route strip was removed in v2.4 — not in the design, and the km/town read didn't map to
        // anything the player actually rode. JourneyProgress itself is untouched for other systems.)

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

        // Thousands grouping with a THIN gap: the default font's space glyph is huge at hero sizes (the
        // mock's Oswald space is narrow), so the group gap is a fixed 0.28em TMP space tag instead.
        private static string Group(int n)
        {
            string s = Mathf.Abs(n).ToString();
            var sb = new System.Text.StringBuilder();
            int c = 0;
            for (int i = s.Length - 1; i >= 0; i--) { sb.Insert(0, s[i]); if (++c % 3 == 0 && i > 0) sb.Insert(0, "<space=0.28em>"); }
            return (n < 0 ? "-" : "") + sb.ToString();
        }

        private readonly System.Collections.Generic.Dictionary<int, Sprite> _rounded = new();

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

        // ---- scenery sprites (v2.2 backdrop) — all deterministic, no art assets ------

        // Soft radial glow for the low sun (alpha eases to nothing at the rim).
        private Sprite _radialGlow;
        private Sprite RadialGlowSprite()
        {
            if (_radialGlow != null) return _radialGlow;
            int s = 128; var tex = NewTex(s, s);
            float c = (s - 1) * 0.5f;
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = Mathf.Clamp01(1f - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
            tex.Apply(); _radialGlow = ToSprite(tex); return _radialGlow;
        }

        // A jagged mountain ridge band: opaque below a zigzag line built from two triangle waves (seeded by
        // variant so the far and near ranges never align), anti-aliased along the crest.
        private readonly System.Collections.Generic.Dictionary<int, Sprite> _ridges = new();
        private Sprite RidgeSprite(int variant)
        {
            if (_ridges.TryGetValue(variant, out var cached)) return cached;
            int w = 512, h = 64; var tex = NewTex(w, h);
            float p1 = 0.13f + variant * 0.37f, p2 = 0.52f + variant * 0.21f;
            for (int x = 0; x < w; x++)
            {
                float t = x / (float)w;
                float crest = 16f + 22f * TriWave(t * 5.1f + p1) + 16f * TriWave(t * 11.3f + p2);
                for (int y = 0; y < h; y++)
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(crest - y + 0.5f)));
            }
            tex.Apply(); var sp = ToSprite(tex); _ridges[variant] = sp; return sp;
        }

        private static float TriWave(float t) { t = Mathf.Repeat(t, 1f); return 1f - Mathf.Abs(t * 2f - 1f); }

        // The Nairobi skyline silhouette: a run of flat-roofed blocks with the occasional antenna tower,
        // laid out by a fixed little LCG so the same skyline is rebuilt every time.
        private Sprite _skyline;
        private Sprite SkylineSprite()
        {
            if (_skyline != null) return _skyline;
            int w = 1024, h = 128; var tex = NewTex(w, h);
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) tex.SetPixel(x, y, new Color(1f, 1f, 1f, 0f));
            uint seed = 90210;
            float Rand() { seed = seed * 1664525u + 1013904223u; return ((seed >> 16) & 0x7FFF) / 32768f; }
            int cx = 0;
            while (cx < w - 8)
            {
                int bw = 26 + (int)(Rand() * 42f);
                int bh = 26 + (int)(Rand() * 88f);
                for (int x = cx; x < Mathf.Min(cx + bw, w); x++)
                    for (int y = 0; y < bh; y++)
                        tex.SetPixel(x, y, Color.white);
                if (Rand() < 0.22f) // antenna / tower spike
                {
                    int ax = cx + bw / 2;
                    for (int y = bh; y < Mathf.Min(bh + 22, h); y++)
                        for (int x = ax - 2; x <= ax + 2; x++)
                            if (x >= 0 && x < w) tex.SetPixel(x, y, Color.white);
                }
                cx += bw + 2 + (int)(Rand() * 8f);
            }
            tex.Apply(); _skyline = ToSprite(tex); return _skyline;
        }

        // (SavannaSprite — the acacia/animal strip — was deleted in v2.5; see the note in MakeScreen.)

        // Horizontal legibility scrim: solid at the left edge, gone by ~70% across.
        private Sprite _hScrim;
        private Sprite HorizontalScrimSprite()
        {
            if (_hScrim != null) return _hScrim;
            int w = 256; var tex = new Texture2D(w, 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int x = 0; x < w; x++)
            {
                float t = x / (float)(w - 1);
                float a = 1f - Mathf.SmoothStep(0.02f, 0.70f, t);
                tex.SetPixel(x, 0, new Color(1f, 1f, 1f, a));
                tex.SetPixel(x, 1, new Color(1f, 1f, 1f, a));
            }
            tex.Apply(); _hScrim = ToSprite(tex); return _hScrim;
        }

        // The charge bolt (same polygon as the HUD's route end-cap).
        private Sprite _boltSprite;
        private Sprite BoltSprite()
        {
            if (_boltSprite != null) return _boltSprite;
            int s = 64; var tex = NewTex(s, s);
            Vector2[] pts = { new(0.55f, 0.95f), new(0.30f, 0.50f), new(0.48f, 0.50f), new(0.42f, 0.05f), new(0.70f, 0.55f), new(0.52f, 0.55f) };
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
            {
                bool inside = PointInPolygon(new Vector2((float)x / s, (float)y / s), pts);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, inside ? 1f : 0f));
            }
            tex.Apply(); _boltSprite = ToSprite(tex); return _boltSprite;
        }

        private static bool PointInPolygon(Vector2 p, Vector2[] v)
        { bool c = false; for (int i = 0, j = v.Length - 1; i < v.Length; j = i++) if (((v[i].y > p.y) != (v[j].y > p.y)) && (p.x < (v[j].x - v[i].x) * (p.y - v[i].y) / (v[j].y - v[i].y) + v[i].x)) c = !c; return c; }

        private static Texture2D NewTex(int w, int h) =>
            new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };

        private static Sprite ToSprite(Texture2D tex) =>
            Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
    }
}
