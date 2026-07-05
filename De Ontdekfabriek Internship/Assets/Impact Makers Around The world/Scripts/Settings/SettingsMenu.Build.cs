// Construction half of SettingsMenu — split out 2026-06-30 for readability (no behaviour change).
// The state/lifecycle half (open/close, the access-code PIN flow, Update) lives in SettingsMenu.cs.
// This file holds Build() and the procedural header / sidebar / rows / overview / profiles UI.
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using KenyaScooter.UI;

namespace KenyaScooter.Settings
{
    public sealed partial class SettingsMenu
    {
        // ---- build --------------------------------------------------------------------

        [ContextMenu("Rebuild now")]
        public void Build()
        {
            ClearGenerated();
            categoryButtons.Clear();
            categoryOrder.Clear();
            categoryBadges.Clear();
            Stretch((RectTransform)transform);

            // Dim backdrop that also blocks taps reaching the game behind it.
            root = NewRect((RectTransform)transform, "Root").gameObject;
            Stretch((RectTransform)root.transform);
            var dim = root.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.78f);
            dim.raycastTarget = true;

            // Centre panel on the spec tokens (v2.7, mock parity): rust hairline, near-solid warm surface,
            // and the accent bar running along the panel's top edge like the mock's operator desk.
            RectTransform panel = NewRect((RectTransform)root.transform, "Panel");
            Anchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1904f, 1456f), Vector2.zero);
            UiKit.AddDropShadow(panel, UiKit.RadiusXl, 0.45f, 40f, 16f);
            var panelImg = panel.gameObject.AddComponent<Image>();
            panelImg.sprite = UiKit.Rounded(UiKit.RadiusXl); panelImg.type = Image.Type.Sliced;
            panelImg.color = UiKit.WithAlpha(UiKit.Rust, 0.55f);
            var panelFill = AddImage(panel, "Fill", new Color(0.125f, 0.071f, 0.047f, 0.98f), UiKit.Rounded(UiKit.RadiusXl));
            panelFill.rectTransform.anchorMin = Vector2.zero; panelFill.rectTransform.anchorMax = Vector2.one;
            panelFill.rectTransform.offsetMin = new Vector2(2f, 2f); panelFill.rectTransform.offsetMax = new Vector2(-2f, -2f);
            var topBar = AddImage(panel, "AccentBar", Accent, Rounded(4));
            topBar.rectTransform.anchorMin = new Vector2(0f, 1f); topBar.rectTransform.anchorMax = new Vector2(1f, 1f);
            topBar.rectTransform.pivot = new Vector2(0.5f, 1f);
            topBar.rectTransform.offsetMin = new Vector2(90f, 0f); topBar.rectTransform.offsetMax = new Vector2(-90f, 0f);
            topBar.rectTransform.sizeDelta = new Vector2(topBar.rectTransform.sizeDelta.x, 8f);
            topBar.rectTransform.anchoredPosition = new Vector2(0f, -4f);

            BuildHeader(panel);
            BuildSidebar(panel);
            BuildRowHost(panel);
            BuildFooter(panel);

            // The access-code keypad, built as a sibling AFTER the settings sheet so it draws on top of it
            // (it can appear over the open settings when changing the code).
            BuildLockOverlay();

            built = true;
            root.SetActive(false);
            if (lockRoot != null) lockRoot.SetActive(false);

            // Default to the first category (Profielen — the easy way in).
            if (categoryOrder.Count > 0) current = categoryOrder[0];
        }

        private void BuildHeader(RectTransform panel)
        {
            // v2.8 (mock parity, at measured scale): a tall header band — big tatoe ring, 64px wordmark,
            // caps subtitle, and a roomy detail toggle + close on the right. A hairline under it separates
            // the header from the body like the mock.
            RectTransform head = NewRect(panel, "Header");
            Anchor(head, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 168f), new Vector2(0f, -96f));
            head.offsetMin = new Vector2(64f, head.offsetMin.y);
            head.offsetMax = new Vector2(-64f, head.offsetMax.y);

            var ring = AddImage(head, "Ring", Accent, RingSprite());
            Anchor(ring.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(92f, 92f), new Vector2(46f, 2f));
            TMP_Text ringT = AddText(head, "t", "t", 32, Cream, TextAlignmentOptions.Center); ringT.fontStyle = FontStyles.Bold;
            Anchor(ringT.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(92f, 44f), new Vector2(46f, 2f));

            TMP_Text title = AddText(head, "Title", "Spelinstellingen", 66, Cream, TextAlignmentOptions.Left);
            title.fontStyle = FontStyles.Bold;
            title.rectTransform.anchorMin = new Vector2(0f, 0.5f); title.rectTransform.anchorMax = new Vector2(0.7f, 0.5f);
            title.rectTransform.pivot = new Vector2(0f, 0.5f);
            title.rectTransform.offsetMin = new Vector2(122f, -20f); title.rectTransform.offsetMax = new Vector2(0f, 62f);

            headerSub = AddText(head, "Sub", "", 21, Kicker, TextAlignmentOptions.Left);
            headerSub.fontStyle = FontStyles.Bold;
            headerSub.rectTransform.anchorMin = new Vector2(0f, 0.5f); headerSub.rectTransform.anchorMax = new Vector2(0.85f, 0.5f);
            headerSub.rectTransform.pivot = new Vector2(0f, 0.5f);
            headerSub.rectTransform.sizeDelta = new Vector2(0f, 28f); headerSub.rectTransform.anchoredPosition = new Vector2(122f, -42f);
            Spaced(headerSub, 0.10f);

            // Close (big, top-right, easy target) — the X is DRAWN (the font has no ✕ glyph; it rendered as a box).
            RectTransform close = NewRect(head, "Close");
            Anchor(close, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(88f, 88f), new Vector2(-8f, 2f));
            var ci = close.gameObject.AddComponent<Image>(); ci.sprite = Rounded(44); ci.type = Image.Type.Sliced; ci.color = TrackFill;
            var cb = close.gameObject.AddComponent<Button>(); cb.targetGraphic = ci; cb.onClick.AddListener(Close);
            var cbc = cb.colors; cbc.fadeDuration = 0.08f; cbc.pressedColor = new Color(1.3f, 1.3f, 1.3f, 1f); cb.colors = cbc;
            var cx = AddImage(close, "x", Cream, XSprite());
            Anchor(cx.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(34f, 34f), Vector2.zero);

            BuildDetailLevelControl(head);

            // Header hairline.
            var rule = AddImage(panel, "HeaderRule", UiKit.WithAlpha(UiKit.Rust, 0.4f), Rounded(2));
            rule.rectTransform.anchorMin = new Vector2(0f, 1f); rule.rectTransform.anchorMax = new Vector2(1f, 1f); rule.rectTransform.pivot = new Vector2(0.5f, 1f);
            rule.rectTransform.offsetMin = new Vector2(64f, 0f); rule.rectTransform.offsetMax = new Vector2(-64f, 0f);
            rule.rectTransform.sizeDelta = new Vector2(rule.rectTransform.sizeDelta.x, 2f);
            rule.rectTransform.anchoredPosition = new Vector2(0f, -192f);
        }

        // A drawn ✕ (two anti-aliased strokes) and a thin ring — the font lacks both glyph shapes.
        private Sprite _x;
        private Sprite XSprite()
        {
            if (_x != null) return _x;
            int s = 64; var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            Vector2 a = new(0.18f, 0.18f), b = new(0.82f, 0.82f), c = new(0.18f, 0.82f), d = new(0.82f, 0.18f);
            const float half = 0.085f;
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
            {
                var p = new Vector2((x + 0.5f) / s, (y + 0.5f) / s);
                float dist = Mathf.Min(DistSeg(p, a, b), DistSeg(p, c, d));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((half - dist) * s * 0.5f)));
            }
            tex.Apply();
            _x = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
            return _x;
        }

        private static float DistSeg(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }

        private Sprite _ringSprite;
        private Sprite RingSprite()
        {
            if (_ringSprite != null) return _ringSprite;
            int s = 128; var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            float r = s * 0.5f, inner = r * 0.80f;
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
            {
                float dd = Mathf.Sqrt((x - r) * (x - r) + (y - r) * (y - r));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(r - dd) * Mathf.Clamp01(dd - inner)));
            }
            tex.Apply();
            _ringSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
            return _ringSprite;
        }

        // BASIS | EXPERT master switch in the header. BASIS keeps every category to its essentials (finer tuning
        // behind "Meer opties"); EXPERT opens them fully. Persisted, and re-renders the current page on change.
        private void BuildDetailLevelControl(RectTransform head)
        {
            RectTransform seg = NewRect(head, "DetailLevel");
            seg.anchorMin = new Vector2(1f, 0.5f); seg.anchorMax = new Vector2(1f, 0.5f); seg.pivot = new Vector2(1f, 0.5f);
            seg.sizeDelta = new Vector2(360f, 68f); seg.anchoredPosition = new Vector2(-128f, 2f);
            var segBg = seg.gameObject.AddComponent<Image>(); segBg.sprite = Rounded(34); segBg.type = Image.Type.Sliced; segBg.color = TrackFill;

            Image basImg = null, expImg = null; TMP_Text basT = null, expT = null;
            System.Action paint = () =>
            {
                bool e = ExpertMode;
                var clear = new Color(0f, 0f, 0f, 0f);
                basImg.color = e ? clear : Accent; basT.color = e ? Ink : InkOnLight;
                expImg.color = e ? Accent : clear; expT.color = e ? InkOnLight : Ink;
            };

            RectTransform a = NewRect(seg, "Basis");
            a.anchorMin = new Vector2(0f, 0f); a.anchorMax = new Vector2(0.5f, 1f); a.offsetMin = new Vector2(5f, 5f); a.offsetMax = new Vector2(-2.5f, -5f);
            basImg = a.gameObject.AddComponent<Image>(); basImg.sprite = Rounded(29); basImg.type = Image.Type.Sliced;
            var ab = a.gameObject.AddComponent<Button>(); ab.targetGraphic = basImg;
            basT = AddText(a, "t", "BASIS", 24, Ink, TextAlignmentOptions.Center); basT.fontStyle = FontStyles.Bold; Stretch(basT.rectTransform);
            ab.onClick.AddListener(() => { ExpertMode = false; showAdvanced = false; paint(); ShowCategory(current); });

            RectTransform b = NewRect(seg, "Expert");
            b.anchorMin = new Vector2(0.5f, 0f); b.anchorMax = new Vector2(1f, 1f); b.offsetMin = new Vector2(2.5f, 5f); b.offsetMax = new Vector2(-5f, -5f);
            expImg = b.gameObject.AddComponent<Image>(); expImg.sprite = Rounded(29); expImg.type = Image.Type.Sliced;
            var bb = b.gameObject.AddComponent<Button>(); bb.targetGraphic = expImg;
            expT = AddText(b, "t", "EXPERT", 24, Ink, TextAlignmentOptions.Center); expT.fontStyle = FontStyles.Bold; Stretch(expT.rectTransform);
            bb.onClick.AddListener(() => { ExpertMode = true; showAdvanced = true; paint(); ShowCategory(current); });

            paint();
        }

        private void BuildSidebar(RectTransform panel)
        {
            // v2.8: wider column (500), clear of the header (top −210) and footer (bottom 150).
            categoryHost = NewRect(panel, "Categories");
            Anchor(categoryHost, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(500f, 0f), Vector2.zero);
            categoryHost.offsetMin = new Vector2(56f, 156f);
            categoryHost.offsetMax = new Vector2(56f + 500f, -210f);

            float y = 0f;
            foreach (SettingCategory cat in System.Enum.GetValues(typeof(SettingCategory)))
            {
                // Only show categories that actually have settings, actions or (for Profielen) presets.
                bool any = false;
                foreach (var _ in SettingsCatalog.InCategory(cat)) { any = true; break; }
                if (!any) foreach (var _ in SettingsCatalog.ActionsInCategory(cat)) { any = true; break; }
                if (!any && cat == SettingCategory.Profiles && SettingsCatalog.Presets.Count > 0) any = true;
                if (!any && cat == SettingCategory.Overview) any = true; // a read-only view, always available
                if (!any) continue;

                categoryOrder.Add(cat);
                SettingCategory captured = cat;

                // v2.8 (mock): quiet rows — no grey boxes; only the ACTIVE category wears the accent pill,
                // labels read mixed-case. Pitch (84) is sized so all ~12 categories fit the column height
                // without a scroll (12 × 84 = 1008 < the ~1090 available).
                RectTransform b = NewRect(categoryHost, "Cat_" + cat);
                b.anchorMin = new Vector2(0f, 1f); b.anchorMax = new Vector2(1f, 1f); b.pivot = new Vector2(0.5f, 1f);
                b.sizeDelta = new Vector2(0f, 76f); b.anchoredPosition = new Vector2(0f, -y);
                var img = b.gameObject.AddComponent<Image>(); img.sprite = Rounded(24); img.type = Image.Type.Sliced; img.color = new Color(0f, 0f, 0f, 0f);
                var btn = b.gameObject.AddComponent<Button>(); btn.targetGraphic = img;
                btn.onClick.AddListener(() => ShowCategory(captured));
                TMP_Text t = AddText(b, "Label", SidebarLabel(cat), 30, Ink, TextAlignmentOptions.Left);
                t.fontStyle = FontStyles.Bold;
                t.rectTransform.anchorMin = new Vector2(0f, 0f); t.rectTransform.anchorMax = new Vector2(1f, 1f);
                t.rectTransform.offsetMin = new Vector2(32f, 0f); t.rectTransform.offsetMax = new Vector2(-62f, 0f);

                // Small round badge on the right showing how many settings in this category differ from default,
                // so the facilitator can see at a glance where they have changed things. Hidden when zero.
                // Brand GOLD, not accent — "what did I change" is information, not an alarm or a call to action.
                RectTransform badge = NewRect(b, "Badge");
                badge.anchorMin = new Vector2(1f, 0.5f); badge.anchorMax = new Vector2(1f, 0.5f); badge.pivot = new Vector2(1f, 0.5f);
                badge.sizeDelta = new Vector2(40f, 40f); badge.anchoredPosition = new Vector2(-16f, 0f);
                var badgeImg = badge.gameObject.AddComponent<Image>(); badgeImg.sprite = Rounded(20); badgeImg.type = Image.Type.Sliced; badgeImg.color = UiKit.Gold; badgeImg.raycastTarget = false;
                TMP_Text badgeT = AddText(badge, "n", "", 21, UiKit.InkOnAccent, TextAlignmentOptions.Center); badgeT.fontStyle = FontStyles.Bold; Stretch(badgeT.rectTransform);
                categoryBadges.Add(badgeT);

                categoryButtons.Add(btn);
                y += 84f;
            }

            RefreshSidebarCounts();
        }

        /// <summary>Updates each category's change-count badge (how many of its settings differ from the shipped
        /// default). Called after every edit/reset so the badges always reflect the live state.</summary>
        private void RefreshSidebarCounts()
        {
            for (int i = 0; i < categoryOrder.Count && i < categoryBadges.Count; i++)
            {
                int n = 0;
                foreach (var def in SettingsCatalog.InCategory(categoryOrder[i]))
                    if (!GameSettings.IsAtDefault(def)) n++;
                var badgeT = categoryBadges[i];
                if (badgeT == null) continue;
                badgeT.text = n > 0 ? n.ToString() : "";
                var img = badgeT.transform.parent != null ? badgeT.transform.parent.GetComponent<Image>() : null;
                if (img != null) img.enabled = n > 0;
            }
        }

        private ScrollRect scroll;
        private RectTransform content;

        private void BuildRowHost(RectTransform panel)
        {
            // A scroll view for the rows (there can be more rows than fit). v2.8: starts clear of the wider
            // sidebar (56 + 500 + 56 = 612) and below the header. A thin divider separates it from the sidebar.
            RectTransform view = NewRect(panel, "Rows");
            Anchor(view, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            view.offsetMin = new Vector2(612f, 156f);
            view.offsetMax = new Vector2(-56f, -210f);
            var mask = view.gameObject.AddComponent<RectMask2D>();

            var divider = AddImage(panel, "SidebarDivider", UiKit.WithAlpha(UiKit.Rust, 0.32f), Rounded(2));
            divider.rectTransform.anchorMin = new Vector2(0f, 0f); divider.rectTransform.anchorMax = new Vector2(0f, 1f);
            divider.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            divider.rectTransform.sizeDelta = new Vector2(2f, 0f);
            divider.rectTransform.offsetMin = new Vector2(586f, 156f); divider.rectTransform.offsetMax = new Vector2(588f, -210f);

            scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true; scroll.scrollSensitivity = 28f;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            content = NewRect(view, "Content");
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f); content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(0f, 0f); content.offsetMax = new Vector2(0f, 0f);
            scroll.content = content;

            rowHost = content;
        }

        private void BuildFooter(RectTransform panel)
        {
            // v2.8: taller footer band with a hairline above it and mock-scale buttons.
            RectTransform foot = NewRect(panel, "Footer");
            Anchor(foot, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 96f), new Vector2(0f, 64f));
            foot.offsetMin = new Vector2(64f, foot.offsetMin.y);
            foot.offsetMax = new Vector2(-64f, foot.offsetMax.y);

            var rule = AddImage(panel, "FooterRule", UiKit.WithAlpha(UiKit.Rust, 0.4f), Rounded(2));
            rule.rectTransform.anchorMin = new Vector2(0f, 0f); rule.rectTransform.anchorMax = new Vector2(1f, 0f); rule.rectTransform.pivot = new Vector2(0.5f, 0f);
            rule.rectTransform.offsetMin = new Vector2(64f, 0f); rule.rectTransform.offsetMax = new Vector2(-64f, 0f);
            rule.rectTransform.sizeDelta = new Vector2(rule.rectTransform.sizeDelta.x, 2f);
            rule.rectTransform.anchoredPosition = new Vector2(0f, 128f);

            // Reset-all (left, with a confirm tap-twice guard). The spec SECONDARY — a 2px accent outline
            // on a translucent dark fill — so it no longer competes with KLAAR for "the important button".
            RectTransform reset = NewRect(foot, "ResetAll");
            reset.anchorMin = new Vector2(0f, 0.5f); reset.anchorMax = new Vector2(0f, 0.5f); reset.pivot = new Vector2(0f, 0.5f);
            reset.sizeDelta = new Vector2(440f, 80f); reset.anchoredPosition = Vector2.zero;
            var ri = reset.gameObject.AddComponent<Image>(); ri.sprite = Rounded(40); ri.type = Image.Type.Sliced; ri.color = UiKit.WithAlpha(Accent, 0.9f);
            var riFill = AddImage(reset, "Fill", new Color(0.10f, 0.05f, 0.03f, 0.80f), Rounded(37));
            riFill.rectTransform.anchorMin = Vector2.zero; riFill.rectTransform.anchorMax = Vector2.one;
            riFill.rectTransform.offsetMin = new Vector2(3f, 3f); riFill.rectTransform.offsetMax = new Vector2(-3f, -3f);
            var rb = reset.gameObject.AddComponent<Button>(); rb.targetGraphic = ri;
            TMP_Text rt = AddText(reset, "Label", "ALLES TERUG NAAR STANDAARD", 22, Ink, TextAlignmentOptions.Center); rt.fontStyle = FontStyles.Bold; Stretch(rt.rectTransform);
            bool armed = false;
            rb.onClick.AddListener(() =>
            {
                if (!armed) { armed = true; rt.text = "NOG EEN KEER TIKKEN OM TE BEVESTIGEN"; rt.color = Accent; return; }
                GameSettings.ResetAll(); armed = false; rt.text = "ALLES TERUG NAAR STANDAARD"; rt.color = Ink; ShowCategory(current);
            });

            // Done (right). The spec PRIMARY — the one accent-filled button on the sheet (pressed = burnt).
            RectTransform done = NewRect(foot, "Done");
            done.anchorMin = new Vector2(1f, 0.5f); done.anchorMax = new Vector2(1f, 0.5f); done.pivot = new Vector2(1f, 0.5f);
            done.sizeDelta = new Vector2(320f, 80f); done.anchoredPosition = Vector2.zero;
            UiKit.AddDropShadow(done, UiKit.RadiusXl, 0.30f, 20f, 7f);
            var di = done.gameObject.AddComponent<Image>(); di.sprite = Rounded(40); di.type = Image.Type.Sliced; di.color = Accent;
            var db = done.gameObject.AddComponent<Button>(); db.targetGraphic = di; db.onClick.AddListener(Close);
            var dbc = db.colors; dbc.fadeDuration = 0.08f; dbc.pressedColor = new Color(0.82f, 0.58f, 0.35f, 1f); db.colors = dbc;
            TMP_Text dt = AddText(done, "Label", "KLAAR", 26, UiKit.InkOnAccent, TextAlignmentOptions.Center); dt.fontStyle = FontStyles.Bold; UiKit.Caps(dt, 0.05f); Stretch(dt.rectTransform);
        }

        // ---- access-code keypad UI ----------------------------------------------------

        private void BuildLockOverlay()
        {
            lockRoot = NewRect((RectTransform)transform, "Lock Root").gameObject;
            Stretch((RectTransform)lockRoot.transform);
            var dim = lockRoot.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.9f); // a touch darker than the settings dim — fully blocks the game
            dim.raycastTarget = true;

            // v2 (screen 10): the keypad sheet moves onto the spec panel — 97% surface inside a rust hairline,
            // with the accent tab hugging the top edge, matching every other spec panel in the game.
            lockPanel = NewRect((RectTransform)lockRoot.transform, "Lock Panel");
            Anchor(lockPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(760f, 980f), Vector2.zero);
            UiKit.AddDropShadow(lockPanel, UiKit.RadiusXl, 0.5f, 40f, 16f);
            var pImg = lockPanel.gameObject.AddComponent<Image>();
            pImg.sprite = UiKit.Rounded(UiKit.RadiusXl); pImg.type = Image.Type.Sliced; pImg.color = UiKit.WithAlpha(UiKit.Rust, 0.55f);
            var pFill = AddImage(lockPanel, "Fill", new Color(0.102f, 0.075f, 0.063f, 0.97f), UiKit.Rounded(UiKit.RadiusXl));
            pFill.rectTransform.anchorMin = Vector2.zero; pFill.rectTransform.anchorMax = Vector2.one;
            pFill.rectTransform.offsetMin = new Vector2(2f, 2f); pFill.rectTransform.offsetMax = new Vector2(-2f, -2f);
            var pTab = AddImage(lockPanel, "AccentTab", Accent, Rounded(5));
            Anchor(pTab.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(190f, 10f), new Vector2(0f, -5f));

            // Padlock badge (also the staff-only press-and-hold recovery hotspot). v2: an accent RING (the
            // tatoe focus-circle motif) instead of a filled disc.
            RectTransform badge = NewRect(lockPanel, "Padlock");
            badge.anchorMin = new Vector2(0.5f, 1f); badge.anchorMax = new Vector2(0.5f, 1f); badge.pivot = new Vector2(0.5f, 1f);
            badge.sizeDelta = new Vector2(120f, 120f); badge.anchoredPosition = new Vector2(0f, -56f);
            var bImg = badge.gameObject.AddComponent<Image>(); bImg.sprite = Rounded(60); bImg.type = Image.Type.Sliced; bImg.color = Accent; bImg.raycastTarget = true;
            var bInner = AddImage(badge, "Inner", new Color(0.102f, 0.075f, 0.063f, 1f), Rounded(52));
            bInner.rectTransform.anchorMin = Vector2.zero; bInner.rectTransform.anchorMax = Vector2.one;
            bInner.rectTransform.offsetMin = new Vector2(9f, 9f); bInner.rectTransform.offsetMax = new Vector2(-9f, -9f);
            // "PIN" rather than a padlock emoji: the project's TMP font reliably renders plain text, while emoji
            // need a separate fallback atlas that may not be present — a missing-glyph box would look broken.
            TMP_Text bGlyph = AddText(badge, "g", "PIN", 34, Cream, TextAlignmentOptions.Center); bGlyph.fontStyle = FontStyles.Bold; Spaced(bGlyph, 0.08f); Stretch(bGlyph.rectTransform);
            AddHoldToRecover(badge);

            lockTitle = AddText(lockPanel, "Title", "TOEGANGSCODE", 46, Cream, TextAlignmentOptions.Center); lockTitle.fontStyle = FontStyles.Bold; Spaced(lockTitle, 0.04f);
            lockTitle.rectTransform.anchorMin = new Vector2(0f, 1f); lockTitle.rectTransform.anchorMax = new Vector2(1f, 1f); lockTitle.rectTransform.pivot = new Vector2(0.5f, 1f);
            lockTitle.rectTransform.sizeDelta = new Vector2(0f, 56f); lockTitle.rectTransform.anchoredPosition = new Vector2(0f, -180f);

            lockSub = AddText(lockPanel, "Sub", "", 22, Muted, TextAlignmentOptions.Center);
            lockSub.rectTransform.anchorMin = new Vector2(0f, 1f); lockSub.rectTransform.anchorMax = new Vector2(1f, 1f); lockSub.rectTransform.pivot = new Vector2(0.5f, 1f);
            lockSub.rectTransform.offsetMin = new Vector2(48f, 0f); lockSub.rectTransform.offsetMax = new Vector2(-48f, 0f);
            lockSub.rectTransform.sizeDelta = new Vector2(lockSub.rectTransform.sizeDelta.x, 60f); lockSub.rectTransform.anchoredPosition = new Vector2(0f, -238f);
            lockSub.enableWordWrapping = true;

            // v2: the entry dots are real components — a filled accent disc per typed digit, a hollow ring for
            // the rest — instead of ●/○ typography, so the fill state reads at a glance from across the desk.
            RectTransform dotsRow = NewRect(lockPanel, "Dots");
            dotsRow.anchorMin = new Vector2(0.5f, 1f); dotsRow.anchorMax = new Vector2(0.5f, 1f); dotsRow.pivot = new Vector2(0.5f, 1f);
            dotsRow.sizeDelta = new Vector2(600f, 48f); dotsRow.anchoredPosition = new Vector2(0f, -318f);
            lockDotFill = new Image[FacilitatorLock.PinLength];
            lockDotHole = new Image[FacilitatorLock.PinLength];
            const float dotSize = 40f, dotGap = 34f;
            float dotsX0 = -(FacilitatorLock.PinLength - 1) * (dotSize + dotGap) * 0.5f;
            for (int i = 0; i < FacilitatorLock.PinLength; i++)
            {
                Image dot = AddImage(dotsRow, "Dot" + i, UiKit.WithAlpha(Cream, 0.35f), Rounded(20));
                Anchor(dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(dotSize, dotSize), new Vector2(dotsX0 + i * (dotSize + dotGap), 0f));
                Image hole = AddImage(dot.rectTransform, "Hole", new Color(0.102f, 0.075f, 0.063f, 1f), Rounded(15));
                hole.rectTransform.anchorMin = Vector2.zero; hole.rectTransform.anchorMax = Vector2.one;
                hole.rectTransform.offsetMin = new Vector2(6f, 6f); hole.rectTransform.offsetMax = new Vector2(-6f, -6f);
                lockDotFill[i] = dot; lockDotHole[i] = hole;
            }

            lockStatus = AddText(lockPanel, "Status", "", 20, Kicker, TextAlignmentOptions.Center);
            lockStatus.rectTransform.anchorMin = new Vector2(0f, 1f); lockStatus.rectTransform.anchorMax = new Vector2(1f, 1f); lockStatus.rectTransform.pivot = new Vector2(0.5f, 1f);
            lockStatus.rectTransform.sizeDelta = new Vector2(0f, 32f); lockStatus.rectTransform.anchoredPosition = new Vector2(0f, -372f);

            // Keypad: 1-9, then backspace / 0 / cancel. Sized to leave a comfortable bottom margin on the 980-tall
            // panel so it stays clear of the edge on the 16:10 deployment aspect, not just the 4:3 reference canvas.
            RectTransform kp = NewRect(lockPanel, "Keypad");
            kp.anchorMin = new Vector2(0.5f, 1f); kp.anchorMax = new Vector2(0.5f, 1f); kp.pivot = new Vector2(0.5f, 1f);
            kp.sizeDelta = new Vector2(600f, 500f); kp.anchoredPosition = new Vector2(0f, -408f);

            for (int n = 1; n <= 9; n++)
            {
                int digit = n;
                MakeKeyButton(kp, digit.ToString(), (n - 1) % 3, (n - 1) / 3, TrackFill, () => PressDigit(digit));
            }
            // "WIS" instead of ← and a DRAWN ✕: the font carries neither arrow nor cross glyph (both
            // rendered as boxes), and "WIS" is clearer for staff anyway.
            MakeKeyButton(kp, "WIS", 0, 3, CardFill, PressBackspace);
            MakeKeyButton(kp, "0", 1, 3, TrackFill, () => PressDigit(0));
            MakeKeyButton(kp, "X", 2, 3, CardFill, () =>
            {
                // Cancel: abort opening (Unlock) or drop the change and go back to the settings.
                if (lockMode == LockMode.Unlock) Close(); else Open();
            }, drawnX: true);

            // v2: the padlock press-hold recovery becomes discoverable microcopy instead of tribal knowledge.
            // Only shown in Unlock mode (PaintLock drives that), and the wording follows the real hold time.
            lockHint = AddText(lockPanel, "Hint",
                "Code kwijt? Houd het slotje " + Mathf.RoundToInt(FacilitatorLock.RecoveryHoldSeconds) + " seconden ingedrukt.",
                18, Muted, TextAlignmentOptions.Center);
            lockHint.rectTransform.anchorMin = new Vector2(0f, 1f); lockHint.rectTransform.anchorMax = new Vector2(1f, 1f);
            lockHint.rectTransform.pivot = new Vector2(0.5f, 1f);
            lockHint.rectTransform.sizeDelta = new Vector2(0f, 30f); lockHint.rectTransform.anchoredPosition = new Vector2(0f, -922f);
        }

        private void MakeKeyButton(RectTransform grid, string glyph, int col, int row, Color fill, System.Action onClick, bool drawnX = false)
        {
            const float cellW = 200f, cellH = 122f;
            RectTransform b = NewRect(grid, "Key_" + glyph);
            b.anchorMin = new Vector2(0f, 1f); b.anchorMax = new Vector2(0f, 1f); b.pivot = new Vector2(0.5f, 0.5f);
            b.sizeDelta = new Vector2(cellW - 18f, cellH - 18f);
            b.anchoredPosition = new Vector2(col * cellW + cellW * 0.5f, -(row * cellH + cellH * 0.5f));
            UiKit.AddDropShadow(b, UiKit.RadiusL, 0.22f, 12f, 4f);
            var img = b.gameObject.AddComponent<Image>(); img.sprite = UiKit.Rounded(UiKit.RadiusL); img.type = Image.Type.Sliced; img.color = fill;
            var btn = b.gameObject.AddComponent<Button>(); btn.targetGraphic = img;
            var bc = btn.colors; bc.fadeDuration = 0.06f; bc.pressedColor = new Color(1f, 1f, 1f, 0.85f); bc.highlightedColor = Color.white; btn.colors = bc;
            Graphic face; // TMP label or the drawn ✕ — both recolour through Graphic.color
            if (drawnX)
            {
                var xi = AddImage(b, "g", Cream, XSprite());
                Anchor(xi.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(30f, 30f), Vector2.zero);
                face = xi;
            }
            else
            {
                TMP_Text t = AddText(b, "g", glyph, glyph.Length > 1 ? 28 : 40, Cream, TextAlignmentOptions.Center);
                t.fontStyle = FontStyles.Bold; if (glyph.Length > 1) UiKit.Caps(t, 0.06f);
                Stretch(t.rectTransform);
                face = t;
            }
            // v2: a visible pressed state — the key flashes accent-filled with dark ink for a beat, so staff
            // can see each digit land (the Button tint alone can't recolour the glyph).
            btn.onClick.AddListener(() => { StartCoroutine(KeyFlashRoutine(img, face, fill)); onClick(); });
        }

        private System.Collections.IEnumerator KeyFlashRoutine(Image img, Graphic face, Color restFill)
        {
            img.color = Accent; if (face != null) face.color = InkOnLight;
            yield return new WaitForSecondsRealtime(0.14f); // realtime: the menu runs with the game paused
            if (img != null) img.color = restFill;
            if (face != null) face.color = Cream;
        }

        // Attaches press-and-hold tracking to the padlock so a forgotten code can be reset to the default in the
        // field (staff-only, documented in the deployment notes; never shown to students).
        private void AddHoldToRecover(RectTransform target)
        {
            var trigger = target.gameObject.AddComponent<EventTrigger>();
            var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            down.callback.AddListener(_ => { if (lockMode == LockMode.Unlock) recoveryHeldSince = Time.unscaledTime; });
            var up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            up.callback.AddListener(_ => recoveryHeldSince = -1f);
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => recoveryHeldSince = -1f);
            trigger.triggers.Add(down); trigger.triggers.Add(up); trigger.triggers.Add(exit);
        }

        // ---- category switching + rows ------------------------------------------------

        private void ShowCategory(SettingCategory cat)
        {
            if (cat != current) showAdvanced = ExpertMode; // a fresh category follows the master detail level
            current = cat;
            HighlightCategory(cat);
            ClearChildren(rowHost);

            if (cat == SettingCategory.Profiles)
            {
                ShowProfiles();
                RefreshSidebarCounts();
                return;
            }

            if (cat == SettingCategory.Overview)
            {
                ShowOverview();
                RefreshSidebarCounts();
                return;
            }

            float y = 4f;
            const float rowH = 158f, gap = 16f, headerH = 112f, moreH = 72f;

            int basicCount = 0, advancedCount = 0;
            foreach (var def in SettingsCatalog.InCategory(cat))
                if (SettingsCatalog.IsAdvanced(def.Key)) advancedCount++; else basicCount++;

            // Header: the category's plain-language blurb + a "reset just this category" button. Shown whenever the
            // category has settings (it sits between the per-row reset and the footer's reset-all).
            if (basicCount + advancedCount > 0)
            {
                BuildCategoryHeader(cat, y, headerH);
                y += headerH + gap;
            }

            // The essentials first, so a category never opens as a wall of knobs.
            foreach (var def in SettingsCatalog.InCategory(cat))
            {
                if (SettingsCatalog.IsAdvanced(def.Key)) continue;
                BuildRow(def, y, rowH);
                y += rowH + gap;
            }

            // The finer tuning is revealed on demand behind one clear button.
            if (advancedCount > 0)
            {
                BuildAdvancedToggle(cat, advancedCount, y, moreH);
                y += moreH + gap;
                if (showAdvanced)
                {
                    foreach (var def in SettingsCatalog.InCategory(cat))
                    {
                        if (!SettingsCatalog.IsAdvanced(def.Key)) continue;
                        BuildRow(def, y, rowH);
                        y += rowH + gap;
                    }
                }
            }

            foreach (var action in SettingsCatalog.ActionsInCategory(cat))
            {
                BuildActionRow(action, y, rowH);
                y += rowH + gap;
            }

            // size content for scrolling
            rowHost.sizeDelta = new Vector2(rowHost.sizeDelta.x, y);
            if (scroll != null) scroll.verticalNormalizedPosition = 1f;
            RefreshSidebarCounts();
        }

        private void BuildCategoryHeader(SettingCategory cat, float y, float headerH)
        {
            RectTransform head = NewRect(rowHost, "CatHeader");
            head.anchorMin = new Vector2(0f, 1f); head.anchorMax = new Vector2(1f, 1f); head.pivot = new Vector2(0.5f, 1f);
            head.offsetMin = new Vector2(16f, 0f); head.offsetMax = new Vector2(-16f, 0f);
            head.sizeDelta = new Vector2(head.sizeDelta.x, headerH); head.anchoredPosition = new Vector2(0f, -y);

            // Plain-language "what does this control" line (the understandability win), with the category name above it.
            TMP_Text name = AddText(head, "Name", SettingsCatalog.CategoryLabel(cat), 24, Kicker, TextAlignmentOptions.TopLeft); name.fontStyle = FontStyles.Bold; UiKit.Caps(name, 0.06f);
            name.rectTransform.anchorMin = new Vector2(0f, 1f); name.rectTransform.anchorMax = new Vector2(0.62f, 1f); name.rectTransform.pivot = new Vector2(0f, 1f);
            name.rectTransform.sizeDelta = new Vector2(0f, 32f); name.rectTransform.anchoredPosition = new Vector2(4f, -14f);

            TMP_Text blurb = AddText(head, "Blurb", SettingsCatalog.CategoryBlurb(cat), 22, Muted, TextAlignmentOptions.TopLeft);
            blurb.rectTransform.anchorMin = new Vector2(0f, 1f); blurb.rectTransform.anchorMax = new Vector2(0.62f, 1f); blurb.rectTransform.pivot = new Vector2(0f, 1f);
            blurb.rectTransform.sizeDelta = new Vector2(0f, 52f); blurb.rectTransform.anchoredPosition = new Vector2(4f, -50f);
            blurb.enableWordWrapping = true;

            RectTransform b = NewRect(head, "ResetCat");
            b.anchorMin = new Vector2(1f, 0.5f); b.anchorMax = new Vector2(1f, 0.5f); b.pivot = new Vector2(1f, 0.5f);
            b.sizeDelta = new Vector2(380f, 48f); b.anchoredPosition = new Vector2(-26f, 0f);
            var bi = b.gameObject.AddComponent<Image>(); bi.sprite = Rounded(24); bi.type = Image.Type.Sliced; bi.color = CardFill;
            var btn = b.gameObject.AddComponent<Button>(); btn.targetGraphic = bi;
            TMP_Text bt = AddText(b, "Label", "DEZE CATEGORIE TERUGZETTEN", 16, Ink, TextAlignmentOptions.Center); bt.fontStyle = FontStyles.Bold; Stretch(bt.rectTransform);

            bool armed = false;
            btn.onClick.AddListener(() =>
            {
                if (!armed) { armed = true; bt.text = "NOG EEN KEER TIKKEN"; bt.color = Accent; return; }
                foreach (var def in SettingsCatalog.InCategory(cat))
                    GameSettings.ResetOne(def);
                ShowCategory(cat); // rebuild so every control snaps to default
            });
        }

        // The "Meer opties" reveal: a full-width button that shows/hides the category's finer-tuning rows, so each
        // page leads with just the essentials. Keeps every option reachable without overwhelming the page.
        private void BuildAdvancedToggle(SettingCategory cat, int count, float y, float h)
        {
            RectTransform b = NewRect(rowHost, "MoreOptions");
            b.anchorMin = new Vector2(0f, 1f); b.anchorMax = new Vector2(1f, 1f); b.pivot = new Vector2(0.5f, 1f);
            b.offsetMin = new Vector2(16f, 0f); b.offsetMax = new Vector2(-16f, 0f);
            b.sizeDelta = new Vector2(b.sizeDelta.x, h); b.anchoredPosition = new Vector2(0f, -y);
            var bi = b.gameObject.AddComponent<Image>(); bi.sprite = Rounded(22); bi.type = Image.Type.Sliced; bi.color = showAdvanced ? TrackFill : RowFill;
            var btn = b.gameObject.AddComponent<Button>(); btn.targetGraphic = bi;
            string text = showAdvanced ? "MINDER OPTIES" : "MEER OPTIES  (" + count + ")";
            TMP_Text t = AddText(b, "Label", text, 20, Ink, TextAlignmentOptions.Center); t.fontStyle = FontStyles.Bold; UiKit.Caps(t, 0.04f); Stretch(t.rectTransform);
            btn.onClick.AddListener(() => { showAdvanced = !showAdvanced; ShowCategory(cat); });
        }

        // ---- overview (read-only "what is different from default") ---------------------

        private void ShowOverview()
        {
            float y = 0f;
            const float gap = 8f;

            RectTransform intro = NewRect(rowHost, "OverviewIntro");
            intro.anchorMin = new Vector2(0f, 1f); intro.anchorMax = new Vector2(1f, 1f); intro.pivot = new Vector2(0.5f, 1f);
            intro.offsetMin = new Vector2(16f, 0f); intro.offsetMax = new Vector2(-16f, 0f);
            intro.sizeDelta = new Vector2(intro.sizeDelta.x, 92f); intro.anchoredPosition = new Vector2(0f, -y);
            TMP_Text it = AddText(intro, "t",
                "Dit is alles wat nu anders staat dan de standaard. Wat hier niet staat, staat gewoon op de standaardwaarde. Onderaan kun je met ALLES TERUG NAAR STANDAARD in één keer terug.",
                20, Muted, TextAlignmentOptions.TopLeft);
            it.rectTransform.anchorMin = new Vector2(0f, 1f); it.rectTransform.anchorMax = new Vector2(1f, 1f); it.rectTransform.pivot = new Vector2(0.5f, 1f);
            it.rectTransform.offsetMin = new Vector2(12f, 0f); it.rectTransform.offsetMax = new Vector2(-12f, -6f);
            it.enableWordWrapping = true;
            y += 92f + gap;

            int changedTotal = 0;
            foreach (SettingCategory c in System.Enum.GetValues(typeof(SettingCategory)))
            {
                if (c == SettingCategory.Profiles || c == SettingCategory.Overview) continue;

                var changed = new List<SettingDefinition>();
                foreach (var def in SettingsCatalog.InCategory(c))
                    if (!GameSettings.IsAtDefault(def)) changed.Add(def);
                if (changed.Count == 0) continue;

                BuildOverviewHeader(SettingsCatalog.CategoryLabel(c), y); y += 44f + 4f;
                foreach (var def in changed)
                {
                    BuildOverviewLine(def.Label, def.Display(GameSettings.CurrentValue(def)), y);
                    y += 50f + 4f; changedTotal++;
                }
                y += 10f;
            }

            if (changedTotal == 0)
            {
                BuildOverviewNote("Alles staat op de standaardinstelling — het spel speelt zoals bedoeld.", y);
                y += 60f + gap;
            }

            // The access-code status is always worth showing, even at default, so staff can confirm the lock is on.
            BuildOverviewHeader("TOEGANG", y); y += 44f + 4f;
            BuildOverviewLine("Toegangscode vereist", FacilitatorLock.Enabled ? "AAN" : "UIT", y);
            y += 50f + gap;

            rowHost.sizeDelta = new Vector2(rowHost.sizeDelta.x, y);
            if (scroll != null) scroll.verticalNormalizedPosition = 1f;
        }

        private void BuildOverviewHeader(string text, float y)
        {
            RectTransform r = NewRect(rowHost, "OvHeader_" + text);
            r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(0.5f, 1f);
            r.offsetMin = new Vector2(16f, 0f); r.offsetMax = new Vector2(-16f, 0f);
            r.sizeDelta = new Vector2(r.sizeDelta.x, 44f); r.anchoredPosition = new Vector2(0f, -y);
            TMP_Text t = AddText(r, "t", text, 20, Kicker, TextAlignmentOptions.Left); t.fontStyle = FontStyles.Bold; UiKit.Caps(t, 0.06f);
            t.rectTransform.anchorMin = new Vector2(0f, 0f); t.rectTransform.anchorMax = new Vector2(1f, 1f);
            t.rectTransform.offsetMin = new Vector2(28f, 0f); t.rectTransform.offsetMax = new Vector2(-12f, 0f);
        }

        private void BuildOverviewLine(string label, string value, float y)
        {
            RectTransform r = NewRect(rowHost, "OvLine_" + label);
            r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(0.5f, 1f);
            r.offsetMin = new Vector2(16f, 0f); r.offsetMax = new Vector2(-16f, 0f);
            r.sizeDelta = new Vector2(r.sizeDelta.x, 50f); r.anchoredPosition = new Vector2(0f, -y);
            var bg = r.gameObject.AddComponent<Image>(); bg.sprite = Rounded(14); bg.type = Image.Type.Sliced; bg.color = RowFill;

            TMP_Text l = AddText(r, "L", label, 22, Ink, TextAlignmentOptions.Left);
            l.rectTransform.anchorMin = new Vector2(0f, 0f); l.rectTransform.anchorMax = new Vector2(0.7f, 1f);
            l.rectTransform.offsetMin = new Vector2(28f, 0f); l.rectTransform.offsetMax = new Vector2(0f, 0f);

            TMP_Text v = AddText(r, "V", value, 22, Accent, TextAlignmentOptions.Right); v.fontStyle = FontStyles.Bold;
            v.rectTransform.anchorMin = new Vector2(0.7f, 0f); v.rectTransform.anchorMax = new Vector2(1f, 1f);
            v.rectTransform.offsetMin = new Vector2(0f, 0f); v.rectTransform.offsetMax = new Vector2(-28f, 0f);
        }

        private void BuildOverviewNote(string text, float y)
        {
            RectTransform r = NewRect(rowHost, "OvNote");
            r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(0.5f, 1f);
            r.offsetMin = new Vector2(16f, 0f); r.offsetMax = new Vector2(-16f, 0f);
            r.sizeDelta = new Vector2(r.sizeDelta.x, 60f); r.anchoredPosition = new Vector2(0f, -y);
            var bg = r.gameObject.AddComponent<Image>(); bg.sprite = Rounded(14); bg.type = Image.Type.Sliced; bg.color = RowFill;
            TMP_Text t = AddText(r, "t", text, 21, Ink, TextAlignmentOptions.Center); Stretch(t.rectTransform);
            t.rectTransform.offsetMin = new Vector2(24f, 0f); t.rectTransform.offsetMax = new Vector2(-24f, 0f);
        }

        // The Profielen page, v2.8 (mock parity, at measured scale): a big heading, a 2-wide grid of roomy
        // tappable preset cards (the whole card is the button — no separate "kies dit profiel"), then the
        // "OF PAS ZELF AAN" divider with the two most-used quick settings, and the own-slots below.
        private void ShowProfiles()
        {
            float y = 4f;

            RectTransform intro = NewRect(rowHost, "ProfilesIntro");
            intro.anchorMin = new Vector2(0f, 1f); intro.anchorMax = new Vector2(1f, 1f); intro.pivot = new Vector2(0.5f, 1f);
            intro.offsetMin = new Vector2(20f, 0f); intro.offsetMax = new Vector2(-20f, 0f);
            intro.sizeDelta = new Vector2(intro.sizeDelta.x, 110f); intro.anchoredPosition = new Vector2(0f, -y);
            TMP_Text head = AddText(intro, "Head", "Kies een profiel om snel te starten", 40, Cream, TextAlignmentOptions.TopLeft);
            head.fontStyle = FontStyles.Bold;
            head.rectTransform.anchorMin = new Vector2(0f, 1f); head.rectTransform.anchorMax = new Vector2(1f, 1f); head.rectTransform.pivot = new Vector2(0.5f, 1f);
            head.rectTransform.offsetMin = new Vector2(4f, -52f); head.rectTransform.offsetMax = new Vector2(-4f, -2f);
            TMP_Text introT = AddText(intro, "t",
                "Eén tik zet alles goed. Daarna kun je links nog alles zelf bijstellen.",
                24, Muted, TextAlignmentOptions.TopLeft);
            introT.rectTransform.anchorMin = new Vector2(0f, 1f); introT.rectTransform.anchorMax = new Vector2(1f, 1f); introT.rectTransform.pivot = new Vector2(0.5f, 1f);
            introT.rectTransform.offsetMin = new Vector2(4f, -100f); introT.rectTransform.offsetMax = new Vector2(-4f, -58f);
            y += 110f + 18f;

            // The 2-wide preset grid (content area is ~1236 wide → cards ~588). Tall enough that even a
            // two-line title plus a three-line description never overruns the card (auto-layout inside).
            const float cardH = 236f, gap = 22f, cardW = 588f;
            int i = 0, presetRows = 0;
            foreach (var preset in SettingsCatalog.Presets)
            {
                int col = i % 2, gridRow = i / 2;
                BuildPresetCard(preset, 20f + col * (cardW + gap), y + gridRow * (cardH + gap), cardW, cardH);
                presetRows = gridRow + 1;
                i++;
            }
            y += presetRows * (cardH + gap) + 12f;

            // "— OF PAS ZELF AAN —"
            BuildProfilesDivider(y);
            y += 42f + 16f;

            // The two settings a facilitator reaches for most, right on the front page (they write the same
            // definitions the full categories edit, so the two surfaces can never disagree).
            y += QuickSettingRow(SettingsCatalog.ById("world.driveLeft"), y,
                "Rijmodus · land", "Kenia rijdt links, Nederland rechts.") + 8f;
            y += QuickSettingRow(SettingsCatalog.ById("env.dust"), y,
                "Stof en haze", "Rode-stof sfeer en hitte-waas in beeld.") + 16f;

            // Facilitator's OWN profiles: save the current setup into a slot and re-apply it any day.
            y += 10f;
            RectTransform sub = NewRect(rowHost, "OwnHeader");
            sub.anchorMin = new Vector2(0f, 1f); sub.anchorMax = new Vector2(1f, 1f); sub.pivot = new Vector2(0.5f, 1f);
            sub.offsetMin = new Vector2(20f, 0f); sub.offsetMax = new Vector2(-20f, 0f);
            sub.sizeDelta = new Vector2(sub.sizeDelta.x, 76f); sub.anchoredPosition = new Vector2(0f, -y);
            TMP_Text subT = AddText(sub, "t", "EIGEN PROFIELEN", 28, Kicker, TextAlignmentOptions.TopLeft); subT.fontStyle = FontStyles.Bold; UiKit.Caps(subT, 0.06f);
            subT.rectTransform.anchorMin = new Vector2(0f, 1f); subT.rectTransform.anchorMax = new Vector2(1f, 1f); subT.rectTransform.pivot = new Vector2(0.5f, 1f);
            subT.rectTransform.offsetMin = new Vector2(4f, -40f); subT.rectTransform.offsetMax = new Vector2(-4f, -4f);
            TMP_Text subD = AddText(sub, "d", "Sla je huidige instellingen op in een slot en gebruik ze later opnieuw.", 21, Muted, TextAlignmentOptions.TopLeft);
            subD.rectTransform.anchorMin = new Vector2(0f, 1f); subD.rectTransform.anchorMax = new Vector2(1f, 1f); subD.rectTransform.pivot = new Vector2(0.5f, 1f);
            subD.rectTransform.offsetMin = new Vector2(4f, -74f); subD.rectTransform.offsetMax = new Vector2(-4f, -42f);
            y += 76f + gap;

            for (int s = 1; s <= CustomPresets.SlotCount; s++)
            {
                BuildSlotCard(s, y, 172f);
                y += 172f + gap;
            }

            rowHost.sizeDelta = new Vector2(rowHost.sizeDelta.x, y);
            if (scroll != null) scroll.verticalNormalizedPosition = 1f;
        }

        // One facilitator save-slot: shows its state and the right buttons (save when empty; apply / overwrite /
        // clear when filled). Destructive/overwrite buttons confirm on a second tap, like the rest of the menu.
        private void BuildSlotCard(int slot, float y, float cardH)
        {
            bool filled = CustomPresets.IsFilled(slot);

            RectTransform card = NewRect(rowHost, "Slot_" + slot);
            card.anchorMin = new Vector2(0f, 1f); card.anchorMax = new Vector2(1f, 1f); card.pivot = new Vector2(0.5f, 1f);
            card.offsetMin = new Vector2(16f, 0f); card.offsetMax = new Vector2(-16f, 0f);
            card.sizeDelta = new Vector2(card.sizeDelta.x, cardH); card.anchoredPosition = new Vector2(0f, -y);
            var bg = card.gameObject.AddComponent<Image>(); bg.sprite = Rounded(18); bg.type = Image.Type.Sliced; bg.color = RowFill;

            TMP_Text label = AddText(card, "Label", CustomPresets.Label(slot), 26, Cream, TextAlignmentOptions.TopLeft); label.fontStyle = FontStyles.Bold;
            label.rectTransform.anchorMin = new Vector2(0f, 1f); label.rectTransform.anchorMax = new Vector2(0.5f, 1f); label.rectTransform.pivot = new Vector2(0f, 1f);
            label.rectTransform.sizeDelta = new Vector2(0f, 36f); label.rectTransform.anchoredPosition = new Vector2(28f, -28f);

            TMP_Text status = AddText(card, "Status", filled ? "Opgeslagen — klaar om toe te passen" : "Nog leeg", 18, filled ? Kicker : Muted, TextAlignmentOptions.TopLeft);
            status.rectTransform.anchorMin = new Vector2(0f, 1f); status.rectTransform.anchorMax = new Vector2(0.5f, 1f); status.rectTransform.pivot = new Vector2(0f, 1f);
            status.rectTransform.sizeDelta = new Vector2(0f, 28f); status.rectTransform.anchoredPosition = new Vector2(28f, -72f);

            if (!filled)
            {
                // Empty: a single big "save current" button, centred. Refresh goes through ShowCategory so the
                // page is CLEARED first (a bare ShowProfiles() stacked a duplicate page on top).
                MakeSlotButton(card, "HUIDIGE OPSLAAN", 0f, Accent, InkOnLight, false, () =>
                {
                    CustomPresets.Save(slot);
                    ShowCategory(SettingCategory.Profiles);
                });
            }
            else
            {
                // Filled: apply / overwrite / clear, stacked.
                MakeSlotButton(card, "TOEPASSEN", 46f, Accent, InkOnLight, false, () =>
                {
                    CustomPresets.Apply(slot);
                    RefreshSidebarCounts();
                });
                MakeSlotButton(card, "OPSLAAN", 0f, TrackFill, Cream, true, () =>
                {
                    CustomPresets.Save(slot);
                    ShowCategory(SettingCategory.Profiles);
                });
                MakeSlotButton(card, "WISSEN", -46f, Hex("#8c2f17"), Cream, true, () =>
                {
                    CustomPresets.Clear(slot);
                    ShowCategory(SettingCategory.Profiles);
                });
            }
        }

        // A button on a slot card (column on the right). `yOffset` stacks it; `confirm` asks for a second tap first.
        private void MakeSlotButton(RectTransform card, string text, float yOffset, Color fill, Color ink, bool confirm, System.Action onClick)
        {
            const float bw = 300f, bh = 40f;
            RectTransform b = NewRect(card, "SlotBtn_" + text);
            b.anchorMin = new Vector2(1f, 0.5f); b.anchorMax = new Vector2(1f, 0.5f); b.pivot = new Vector2(1f, 0.5f);
            b.sizeDelta = new Vector2(bw, bh);
            b.anchoredPosition = new Vector2(-26f, yOffset);
            var bi = b.gameObject.AddComponent<Image>(); bi.sprite = Rounded(20); bi.type = Image.Type.Sliced; bi.color = fill;
            var btn = b.gameObject.AddComponent<Button>(); btn.targetGraphic = bi;
            var bc = btn.colors; bc.fadeDuration = 0.08f; btn.colors = bc;
            TMP_Text bt = AddText(b, "Label", text, 18, ink, TextAlignmentOptions.Center); bt.fontStyle = FontStyles.Bold; UiKit.Caps(bt, 0.03f); Stretch(bt.rectTransform);

            bool armed = false;
            btn.onClick.AddListener(() =>
            {
                if (confirm && !armed) { armed = true; bt.text = "ZEKER?"; return; }
                try { onClick(); } catch (System.Exception e) { Debug.LogException(e); }
            });
        }

        // The most recently applied preset, so its card can wear the ACTIEF chip. Presets are additive and a
        // facilitator can still hand-tune afterwards, so this deliberately tracks "last applied", not "still exact".
        private const string LastPresetKey = "ksg.preset.last";

        // One compact grid card (mock): the WHOLE card is the button; the active one wears the accent ring
        // and the ACTIEF chip. Applying repaints the page — the chip moving IS the confirmation.
        private void BuildPresetCard(SettingsPreset preset, float x, float y, float w, float h)
        {
            bool active = PlayerPrefs.GetString(LastPresetKey, "") == preset.Key;

            RectTransform card = NewRect(rowHost, "Preset_" + preset.Key);
            card.anchorMin = new Vector2(0f, 1f); card.anchorMax = new Vector2(0f, 1f); card.pivot = new Vector2(0f, 1f);
            card.sizeDelta = new Vector2(w, h); card.anchoredPosition = new Vector2(x, -y);
            var bg = card.gameObject.AddComponent<Image>(); bg.sprite = Rounded(28); bg.type = Image.Type.Sliced;
            bg.color = active ? UiKit.WithAlpha(Accent, 0.9f) : RowFill;
            var btn = card.gameObject.AddComponent<Button>(); btn.targetGraphic = bg;
            var bc = btn.colors; bc.fadeDuration = 0.08f; bc.pressedColor = new Color(1.6f, 1.6f, 1.6f, 1f); btn.colors = bc;
            btn.onClick.AddListener(() =>
            {
                try { preset.Apply(); } catch (System.Exception e) { Debug.LogException(e); }
                PlayerPrefs.SetString(LastPresetKey, preset.Key); PlayerPrefs.Save();
                RefreshSidebarCounts();
                ShowCategory(SettingCategory.Profiles); // clean repaint — the ACTIEF chip moves to this card
            });

            if (active)
            {
                // Active card: accent ring with the tinted fill inside it (state = shape + colour, not colour alone).
                var inner = AddImage(card, "Fill", new Color(0.16f, 0.10f, 0.07f, 0.97f), Rounded(24));
                inner.rectTransform.anchorMin = Vector2.zero; inner.rectTransform.anchorMax = Vector2.one;
                inner.rectTransform.offsetMin = new Vector2(5f, 5f); inner.rectTransform.offsetMax = new Vector2(-5f, -5f);
                var chipR = NewRect(card, "ActiveChip");
                chipR.anchorMin = new Vector2(1f, 1f); chipR.anchorMax = new Vector2(1f, 1f); chipR.pivot = new Vector2(1f, 1f);
                chipR.sizeDelta = new Vector2(150f, 46f); chipR.anchoredPosition = new Vector2(-24f, -22f);
                var chipImg = chipR.gameObject.AddComponent<Image>(); chipImg.sprite = Rounded(23); chipImg.type = Image.Type.Sliced; chipImg.color = Accent; chipImg.raycastTarget = false;
                TMP_Text chipT = AddText(chipR, "t", "ACTIEF", 21, UiKit.InkOnAccent, TextAlignmentOptions.Center); // no ✓ glyph in the font
                chipT.fontStyle = FontStyles.Bold; UiKit.Caps(chipT, 0.05f); Stretch(chipT.rectTransform);
            }

            // Title + description in an auto-layout column, so the description ALWAYS flows below the real
            // title height — a long title that wraps to two lines ("Uitdagend — oudere kinderen") pushes the
            // description down instead of colliding with it (the fixed-position version overlapped them).
            RectTransform col = NewRect(card, "Col");
            col.anchorMin = Vector2.zero; col.anchorMax = Vector2.one; col.pivot = new Vector2(0.5f, 0.5f);
            col.offsetMin = new Vector2(34f, 24f); col.offsetMax = new Vector2(active ? -168f : -34f, -24f);
            var vlg = col.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.spacing = 10f;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

            TMP_Text label = AddText(col, "Label", preset.Label, 34, Cream, TextAlignmentOptions.Left); label.fontStyle = FontStyles.Bold;
            label.enableWordWrapping = true;
            TMP_Text desc = AddText(col, "Desc", preset.Description, 22, Muted, TextAlignmentOptions.Left);
            desc.enableWordWrapping = true;
        }

        // "— OF PAS ZELF AAN —": rule, caps caption, rule (the mock's section divider).
        private void BuildProfilesDivider(float y)
        {
            RectTransform r = NewRect(rowHost, "Divider");
            r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(0.5f, 1f);
            r.offsetMin = new Vector2(20f, 0f); r.offsetMax = new Vector2(-20f, 0f);
            r.sizeDelta = new Vector2(r.sizeDelta.x, 42f); r.anchoredPosition = new Vector2(0f, -y);

            Image left = AddImage(r, "RuleL", new Color(1f, 0.78f, 0.59f, 0.14f), Rounded(2));
            left.rectTransform.anchorMin = new Vector2(0f, 0.5f); left.rectTransform.anchorMax = new Vector2(0.5f, 0.5f); left.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            left.rectTransform.offsetMin = new Vector2(12f, -1.5f); left.rectTransform.offsetMax = new Vector2(-200f, 1.5f);
            Image right = AddImage(r, "RuleR", new Color(1f, 0.78f, 0.59f, 0.14f), Rounded(2));
            right.rectTransform.anchorMin = new Vector2(0.5f, 0.5f); right.rectTransform.anchorMax = new Vector2(1f, 0.5f); right.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            right.rectTransform.offsetMin = new Vector2(200f, -1.5f); right.rectTransform.offsetMax = new Vector2(-12f, 1.5f);
            TMP_Text t = AddText(r, "t", "OF PAS ZELF AAN", 22, Kicker, TextAlignmentOptions.Center);
            t.fontStyle = FontStyles.Bold; UiKit.Caps(t, 0.14f); Stretch(t.rectTransform);
        }

        // A quick-access row on the Profielen page for one catalog setting: label + one-liner on the left,
        // the real control on the right (segmented pill for the drive side, the switch for toggles). Writes
        // through GameSettings, so it IS the same setting as in the full category.
        private float QuickSettingRow(SettingDefinition def, float y, string labelText, string descText)
        {
            const float rowH = 132f;
            if (def == null) return 0f; // catalog changed — skip quietly rather than crash the page

            RectTransform row = NewRect(rowHost, "Quick_" + def.Key);
            row.anchorMin = new Vector2(0f, 1f); row.anchorMax = new Vector2(1f, 1f); row.pivot = new Vector2(0.5f, 1f);
            row.offsetMin = new Vector2(20f, 0f); row.offsetMax = new Vector2(-20f, 0f);
            row.sizeDelta = new Vector2(row.sizeDelta.x, rowH); row.anchoredPosition = new Vector2(0f, -y);
            var rowBg = row.gameObject.AddComponent<Image>(); rowBg.sprite = Rounded(22); rowBg.type = Image.Type.Sliced; rowBg.color = RowFill;

            TMP_Text label = AddText(row, "Label", labelText, 32, Cream, TextAlignmentOptions.TopLeft); label.fontStyle = FontStyles.Bold;
            label.rectTransform.anchorMin = new Vector2(0f, 1f); label.rectTransform.anchorMax = new Vector2(0.55f, 1f); label.rectTransform.pivot = new Vector2(0f, 1f);
            label.rectTransform.sizeDelta = new Vector2(0f, 40f); label.rectTransform.anchoredPosition = new Vector2(30f, -22f);

            TMP_Text desc = AddText(row, "Desc", descText, 21, Muted, TextAlignmentOptions.TopLeft);
            desc.rectTransform.anchorMin = new Vector2(0f, 1f); desc.rectTransform.anchorMax = new Vector2(0.55f, 1f); desc.rectTransform.pivot = new Vector2(0f, 1f);
            desc.rectTransform.sizeDelta = new Vector2(0f, 56f); desc.rectTransform.anchoredPosition = new Vector2(30f, -66f);
            desc.enableWordWrapping = true;

            System.Action<float> commit = v => { GameSettings.Set(def, v); RefreshSidebarCounts(); };
            if (def.Key == "world.driveLeft")
                BuildDriveSideSegments(row, def, commit);
            else if (def.Widget == SettingWidget.Toggle)
                BuildToggle(row, def, GameSettings.CurrentValue(def) >= 0.5f, on => commit(on ? 1f : 0f), null);
            return rowH;
        }

        // "PROFIELEN" → "Profielen": the sidebar reads mixed-case per the mock; the catalog labels stay as-is.
        private static string SidebarLabel(SettingCategory cat)
        {
            string s = SettingsCatalog.CategoryLabel(cat) ?? "";
            if (s.Length == 0) return s;
            return char.ToUpperInvariant(s[0]) + s.Substring(1).ToLowerInvariant();
        }

        private void HighlightCategory(SettingCategory cat)
        {
            for (int i = 0; i < categoryButtons.Count; i++)
            {
                bool on = categoryOrder[i] == cat;
                var img = categoryButtons[i].targetGraphic as Image;
                if (img != null) img.color = on ? Accent : new Color(0f, 0f, 0f, 0f); // quiet rows: pill only when active
                var label = categoryButtons[i].GetComponentInChildren<TMP_Text>();
                if (label != null) label.color = on ? InkOnLight : Ink;
            }
        }

        private void BuildRow(SettingDefinition def, float y, float rowH)
        {
            RectTransform row = NewRect(rowHost, "Row_" + def.Key);
            row.anchorMin = new Vector2(0f, 1f); row.anchorMax = new Vector2(1f, 1f); row.pivot = new Vector2(0.5f, 1f);
            row.offsetMin = new Vector2(16f, 0f); row.offsetMax = new Vector2(-16f, 0f);
            row.sizeDelta = new Vector2(row.sizeDelta.x, rowH); row.anchoredPosition = new Vector2(0f, -y);
            var bg = row.gameObject.AddComponent<Image>(); bg.sprite = Rounded(18); bg.type = Image.Type.Sliced; bg.color = RowFill;

            // Label + description (left two-thirds).
            TMP_Text label = AddText(row, "Label", def.Label, 29, Cream, TextAlignmentOptions.TopLeft); label.fontStyle = FontStyles.Bold;
            label.rectTransform.anchorMin = new Vector2(0f, 1f); label.rectTransform.anchorMax = new Vector2(0.62f, 1f); label.rectTransform.pivot = new Vector2(0f, 1f);
            label.rectTransform.sizeDelta = new Vector2(0f, 40f); label.rectTransform.anchoredPosition = new Vector2(30f, -24f);

            TMP_Text desc = AddText(row, "Desc", def.Description, 21, Muted, TextAlignmentOptions.TopLeft);
            desc.rectTransform.anchorMin = new Vector2(0f, 1f); desc.rectTransform.anchorMax = new Vector2(0.62f, 1f); desc.rectTransform.pivot = new Vector2(0f, 1f);
            desc.rectTransform.sizeDelta = new Vector2(0f, 62f); desc.rectTransform.anchoredPosition = new Vector2(30f, -68f);
            desc.enableWordWrapping = true;

            // Value chip (right of the control): the live value, in the accent, so the eye finds "what this is set
            // to right now" at a glance (the one job the accent does in this menu, per the design rationale).
            TMP_Text chip = AddText(row, "Chip", "", 26, Accent, TextAlignmentOptions.Right); chip.fontStyle = FontStyles.Bold;
            chip.rectTransform.anchorMin = new Vector2(1f, 1f); chip.rectTransform.anchorMax = new Vector2(1f, 1f); chip.rectTransform.pivot = new Vector2(1f, 1f);
            chip.rectTransform.sizeDelta = new Vector2(220f, 36f); chip.rectTransform.anchoredPosition = new Vector2(-220f, -24f);

            // Per-row reset.
            RectTransform reset = NewRect(row, "Reset");
            reset.anchorMin = new Vector2(1f, 1f); reset.anchorMax = new Vector2(1f, 1f); reset.pivot = new Vector2(1f, 1f);
            reset.sizeDelta = new Vector2(46f, 46f); reset.anchoredPosition = new Vector2(-22f, -20f);
            var rsi = reset.gameObject.AddComponent<Image>(); rsi.sprite = Rounded(23); rsi.type = Image.Type.Sliced; rsi.color = TrackFill;
            var rsb = reset.gameObject.AddComponent<Button>(); rsb.targetGraphic = rsi;
            TMP_Text rsx = AddText(reset, "icon", "↺", 24, Ink, TextAlignmentOptions.Center); Stretch(rsx.rectTransform);

            // A small "this row differs from the shipped default" dot, left of the reset, so the facilitator can
            // spot what they have changed. The reset arrow also brightens when there is something to reset.
            RectTransform dot = NewRect(row, "ChangedDot");
            dot.anchorMin = new Vector2(1f, 1f); dot.anchorMax = new Vector2(1f, 1f); dot.pivot = new Vector2(0.5f, 0.5f);
            dot.sizeDelta = new Vector2(14f, 14f); dot.anchoredPosition = new Vector2(-80f, -43f);
            var dotImg = dot.gameObject.AddComponent<Image>(); dotImg.sprite = Rounded(7); dotImg.type = Image.Type.Sliced; dotImg.color = Accent; dotImg.raycastTarget = false;
            System.Action paintChanged = () =>
            {
                bool changed = !GameSettings.IsAtDefault(def);
                dotImg.enabled = changed;
                rsi.color = changed ? new Color(Accent.r, Accent.g, Accent.b, 0.5f) : TrackFill;
                rsx.color = changed ? Cream : Muted;
            };

            // Two paths, so a continuous drag doesn't hammer the disk:
            //   preview = apply the value to the LIVE config + chip every frame (cheap, instant feedback), no save;
            //   commit  = persist the override (one PlayerPrefs write) + refresh the change indicators.
            // Toggles and steppers are discrete taps, so they commit directly; only the slider previews-then-commits.
            float startValue = GameSettings.CurrentValue(def);
            System.Action<float> preview = v =>
            {
                float c = def.Coerce(v);
                if (def.Set != null) { try { def.Set(c); } catch (System.Exception e) { Debug.LogException(e); } }
                chip.text = def.Display(c);
            };
            System.Action<float> commit = v =>
            {
                GameSettings.Set(def, v);
                chip.text = def.Display(GameSettings.CurrentValue(def));
                paintChanged();
                RefreshSidebarCounts();
            };

            // v2 (screen 11): the drive-side row is the same segmented KENIA·LINKS | NEDERLAND·RECHTS control
            // as the title screen — one pattern for this choice everywhere, instead of an abstract on/off toggle.
            if (def.Key == "world.driveLeft")
            {
                BuildDriveSideSegments(row, def, commit);
            }
            else switch (def.Widget)
            {
                case SettingWidget.Toggle:
                    BuildToggle(row, def, startValue >= 0.5f, on => commit(on ? 1f : 0f), chip);
                    break;
                case SettingWidget.Stepper:
                    BuildStepper(row, def, chip, commit);
                    break;
                default:
                    BuildSlider(row, def, startValue, preview, commit, chip);
                    break;
            }

            chip.text = def.Display(startValue);
            paintChanged();

            rsb.onClick.AddListener(() =>
            {
                GameSettings.ResetOne(def);
                ShowCategory(current); // simplest: rebuild the page so the control snaps to default
            });
        }

        // ---- action rows --------------------------------------------------------------

        private void BuildActionRow(SettingAction action, float y, float rowH)
        {
            RectTransform row = NewRect(rowHost, "Action_" + action.Key);
            row.anchorMin = new Vector2(0f, 1f); row.anchorMax = new Vector2(1f, 1f); row.pivot = new Vector2(0.5f, 1f);
            row.offsetMin = new Vector2(16f, 0f); row.offsetMax = new Vector2(-16f, 0f);
            row.sizeDelta = new Vector2(row.sizeDelta.x, rowH); row.anchoredPosition = new Vector2(0f, -y);
            var bg = row.gameObject.AddComponent<Image>(); bg.sprite = Rounded(18); bg.type = Image.Type.Sliced; bg.color = RowFill;

            TMP_Text label = AddText(row, "Label", action.Label, 26, Cream, TextAlignmentOptions.TopLeft); label.fontStyle = FontStyles.Bold;
            label.rectTransform.anchorMin = new Vector2(0f, 1f); label.rectTransform.anchorMax = new Vector2(0.58f, 1f); label.rectTransform.pivot = new Vector2(0f, 1f);
            label.rectTransform.sizeDelta = new Vector2(0f, 36f); label.rectTransform.anchoredPosition = new Vector2(28f, -22f);

            TMP_Text desc = AddText(row, "Desc", action.Description, 18, Muted, TextAlignmentOptions.TopLeft);
            desc.rectTransform.anchorMin = new Vector2(0f, 1f); desc.rectTransform.anchorMax = new Vector2(0.58f, 1f); desc.rectTransform.pivot = new Vector2(0f, 1f);
            desc.rectTransform.sizeDelta = new Vector2(0f, 84f); desc.rectTransform.anchoredPosition = new Vector2(28f, -62f);
            desc.enableWordWrapping = true;

            // The button, right side. Destructive actions are a deep red and confirm on a second tap.
            RectTransform b = NewRect(row, "Btn");
            b.anchorMin = new Vector2(1f, 0.5f); b.anchorMax = new Vector2(1f, 0.5f); b.pivot = new Vector2(1f, 0.5f);
            b.sizeDelta = new Vector2(300f, 78f); b.anchoredPosition = new Vector2(-26f, 0f);
            var bi = b.gameObject.AddComponent<Image>(); bi.sprite = Rounded(28); bi.type = Image.Type.Sliced;
            Color rest = action.Destructive ? Hex("#8c2f17") : Accent;
            bi.color = rest;
            var btn = b.gameObject.AddComponent<Button>(); btn.targetGraphic = bi;
            var btc = btn.colors; btc.fadeDuration = 0.08f; btn.colors = btc;
            TMP_Text bt = AddText(b, "Label", action.ButtonText, 24, action.Destructive ? Cream : InkOnLight, TextAlignmentOptions.Center);
            bt.fontStyle = FontStyles.Bold; UiKit.Caps(bt, 0.04f); Stretch(bt.rectTransform);

            bool armed = false;
            btn.onClick.AddListener(() =>
            {
                if (action.Destructive && !armed)
                {
                    armed = true; bt.text = "ZEKER?"; bi.color = Accent; return; // arm: a second tap confirms
                }
                try { action.Invoke?.Invoke(); } catch (System.Exception e) { Debug.LogException(e); }
                armed = false; bi.color = rest; bt.text = "GEDAAN ✓";
            });
        }

        // ---- widgets ------------------------------------------------------------------

        private void BuildSlider(RectTransform row, SettingDefinition def, float value,
                                 System.Action<float> onPreview, System.Action<float> onCommit, TMP_Text chip)
        {
            RectTransform area = NewRect(row, "Slider");
            area.anchorMin = new Vector2(0.62f, 0f); area.anchorMax = new Vector2(1f, 0f); area.pivot = new Vector2(0.5f, 0f);
            area.sizeDelta = new Vector2(-40f, 18f); area.anchoredPosition = new Vector2(-10f, 34f);

            var slider = area.gameObject.AddComponent<Slider>();
            var track = NewRect(area, "Track");
            Stretch(track); var ti = track.gameObject.AddComponent<Image>(); ti.color = TrackFill; ti.sprite = Rounded(9); ti.type = Image.Type.Sliced;

            var fillArea = NewRect(area, "FillArea"); Stretch(fillArea); fillArea.offsetMin = new Vector2(9f, 0f); fillArea.offsetMax = new Vector2(-9f, 0f);
            var fill = NewRect(fillArea, "Fill"); fill.anchorMin = new Vector2(0f, 0f); fill.anchorMax = new Vector2(1f, 1f); fill.offsetMin = Vector2.zero; fill.offsetMax = Vector2.zero;
            var fi = fill.gameObject.AddComponent<Image>(); fi.color = Accent; fi.sprite = Rounded(9); fi.type = Image.Type.Sliced;

            var handleArea = NewRect(area, "HandleArea"); Stretch(handleArea); handleArea.offsetMin = new Vector2(14f, 0f); handleArea.offsetMax = new Vector2(-14f, 0f);
            var handle = NewRect(handleArea, "Handle"); handle.sizeDelta = new Vector2(44f, 44f);
            // A soft shadow under the knob (a child added FIRST so it draws behind the knob face, and offset down)
            // makes the handle read as a raised knob the facilitator can grab.
            var hShadow = NewRect(handle, "HandleShadow"); Stretch(hShadow); hShadow.offsetMin = new Vector2(-7f, -9f); hShadow.offsetMax = new Vector2(7f, 5f);
            var hsi = hShadow.gameObject.AddComponent<Image>(); hsi.sprite = UiKit.SoftShadow(22); hsi.type = Image.Type.Sliced; hsi.color = UiKit.ShadowColour(0.32f); hsi.raycastTarget = false;
            var face = NewRect(handle, "Face"); Stretch(face);
            var hi = face.gameObject.AddComponent<Image>(); hi.color = Cream; hi.sprite = Rounded(22); hi.type = Image.Type.Sliced;

            slider.fillRect = fill; slider.handleRect = handle; slider.targetGraphic = hi;
            slider.minValue = def.Min; slider.maxValue = def.Max;
            slider.wholeNumbers = def.Step >= 1f;
            slider.SetValueWithoutNotify(value);

            // Live preview every frame (no disk write); persist once when the finger lifts or the drag ends.
            slider.onValueChanged.AddListener(v => onPreview(v));
            var trigger = slider.gameObject.AddComponent<EventTrigger>();
            var up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            up.callback.AddListener(_ => onCommit(slider.value));
            var end = new EventTrigger.Entry { eventID = EventTriggerType.EndDrag };
            end.callback.AddListener(_ => onCommit(slider.value));
            trigger.triggers.Add(up); trigger.triggers.Add(end);
        }

        // The drive-side segmented pill (v2): the same two-segment pattern as the title screen and the
        // BASIS|EXPERT switch, sitting where a slider would. Left segment = Kenya (value 1), right = NL (0).
        private void BuildDriveSideSegments(RectTransform row, SettingDefinition def, System.Action<float> onCommit)
        {
            RectTransform seg = NewRect(row, "DriveSide");
            seg.anchorMin = new Vector2(0.62f, 0f); seg.anchorMax = new Vector2(1f, 0f); seg.pivot = new Vector2(0.5f, 0f);
            seg.sizeDelta = new Vector2(-44f, 64f); seg.anchoredPosition = new Vector2(-10f, 24f);
            var segBg = seg.gameObject.AddComponent<Image>(); segBg.sprite = Rounded(32); segBg.type = Image.Type.Sliced; segBg.color = TrackFill;

            Image keImg = null, nlImg = null; TMP_Text keT = null, nlT = null;
            System.Action paint = () =>
            {
                bool left = GameSettings.CurrentValue(def) >= 0.5f;
                var clear = new Color(0f, 0f, 0f, 0f);
                keImg.color = left ? Accent : clear; keT.color = left ? UiKit.InkOnAccent : Ink;
                nlImg.color = left ? clear : Accent; nlT.color = left ? Ink : UiKit.InkOnAccent;
            };

            RectTransform a = NewRect(seg, "Kenia");
            a.anchorMin = new Vector2(0f, 0f); a.anchorMax = new Vector2(0.5f, 1f); a.offsetMin = new Vector2(4f, 4f); a.offsetMax = new Vector2(-2f, -4f);
            keImg = a.gameObject.AddComponent<Image>(); keImg.sprite = Rounded(28); keImg.type = Image.Type.Sliced;
            var ab = a.gameObject.AddComponent<Button>(); ab.targetGraphic = keImg;
            keT = AddText(a, "t", "KENIA · LINKS", 18, Ink, TextAlignmentOptions.Center); keT.fontStyle = FontStyles.Bold; Stretch(keT.rectTransform);
            ab.onClick.AddListener(() => { onCommit(1f); paint(); });

            RectTransform b = NewRect(seg, "Nederland");
            b.anchorMin = new Vector2(0.5f, 0f); b.anchorMax = new Vector2(1f, 1f); b.offsetMin = new Vector2(2f, 4f); b.offsetMax = new Vector2(-4f, -4f);
            nlImg = b.gameObject.AddComponent<Image>(); nlImg.sprite = Rounded(28); nlImg.type = Image.Type.Sliced;
            var bb = b.gameObject.AddComponent<Button>(); bb.targetGraphic = nlImg;
            nlT = AddText(b, "t", "NEDERLAND · RECHTS", 18, Ink, TextAlignmentOptions.Center); nlT.fontStyle = FontStyles.Bold; Stretch(nlT.rectTransform);
            bb.onClick.AddListener(() => { onCommit(0f); paint(); });

            paint();
        }

        private void BuildToggle(RectTransform row, SettingDefinition def, bool on,
                                 System.Action<bool> onChange, TMP_Text chip)
        {
            RectTransform area = NewRect(row, "Toggle");
            area.anchorMin = new Vector2(1f, 0f); area.anchorMax = new Vector2(1f, 0f); area.pivot = new Vector2(1f, 0f);
            area.sizeDelta = new Vector2(150f, 64f); area.anchoredPosition = new Vector2(-22f, 30f);
            var bgImg = area.gameObject.AddComponent<Image>(); bgImg.sprite = Rounded(32); bgImg.type = Image.Type.Sliced;
            var btn = area.gameObject.AddComponent<Button>(); btn.targetGraphic = bgImg;

            RectTransform knob = NewRect(area, "Knob"); knob.sizeDelta = new Vector2(52f, 52f);
            knob.anchorMin = new Vector2(0f, 0.5f); knob.anchorMax = new Vector2(0f, 0.5f); knob.pivot = new Vector2(0f, 0.5f);
            var knobShadow = NewRect(knob, "KnobShadow"); Stretch(knobShadow); knobShadow.offsetMin = new Vector2(-7f, -9f); knobShadow.offsetMax = new Vector2(7f, 5f);
            var ksi = knobShadow.gameObject.AddComponent<Image>(); ksi.sprite = UiKit.SoftShadow(26); ksi.type = Image.Type.Sliced; ksi.color = UiKit.ShadowColour(0.32f); ksi.raycastTarget = false;
            var knobFace = NewRect(knob, "Face"); Stretch(knobFace);
            var ki = knobFace.gameObject.AddComponent<Image>(); ki.sprite = Rounded(26); ki.type = Image.Type.Sliced; ki.color = Cream;

            bool state = on;
            System.Action paint = () =>
            {
                bgImg.color = state ? new Color(Accent.r, Accent.g, Accent.b, 0.9f) : TrackFill;
                knob.anchoredPosition = new Vector2(state ? 92f : 6f, 0f);
            };
            paint();
            btn.onClick.AddListener(() => { state = !state; paint(); onChange(state); });
        }

        private void BuildStepper(RectTransform row, SettingDefinition def, TMP_Text chip, System.Action<float> onChange)
        {
            RectTransform area = NewRect(row, "Stepper");
            area.anchorMin = new Vector2(1f, 0f); area.anchorMax = new Vector2(1f, 0f); area.pivot = new Vector2(1f, 0f);
            area.sizeDelta = new Vector2(240f, 64f); area.anchoredPosition = new Vector2(-22f, 30f);

            float[] value = { GameSettings.CurrentValue(def) };

            RectTransform minus = MakeStepButton(area, "-", 0f);
            RectTransform plus = MakeStepButton(area, "+", 184f);
            TMP_Text mid = AddText(area, "Val", def.Display(value[0]), 28, Cream, TextAlignmentOptions.Center); mid.fontStyle = FontStyles.Bold;
            mid.rectTransform.anchorMin = new Vector2(0f, 0f); mid.rectTransform.anchorMax = new Vector2(1f, 1f);
            mid.rectTransform.offsetMin = new Vector2(56f, 0f); mid.rectTransform.offsetMax = new Vector2(-56f, 0f);

            System.Action<float> step = delta =>
            {
                value[0] = def.Coerce(value[0] + delta);
                mid.text = def.Display(value[0]);
                onChange(value[0]);
            };
            minus.GetComponent<Button>().onClick.AddListener(() => step(-Mathf.Max(def.Step, 1f)));
            plus.GetComponent<Button>().onClick.AddListener(() => step(Mathf.Max(def.Step, 1f)));
        }

        private RectTransform MakeStepButton(RectTransform parent, string glyph, float x)
        {
            RectTransform b = NewRect(parent, "Step" + glyph);
            b.anchorMin = new Vector2(0f, 0f); b.anchorMax = new Vector2(0f, 1f); b.pivot = new Vector2(0f, 0.5f);
            b.sizeDelta = new Vector2(56f, 0f); b.anchoredPosition = new Vector2(x, 0f);
            var img = b.gameObject.AddComponent<Image>(); img.sprite = Rounded(28); img.type = Image.Type.Sliced; img.color = TrackFill;
            var btn = b.gameObject.AddComponent<Button>(); btn.targetGraphic = img;
            TMP_Text t = AddText(b, "g", glyph, 34, Cream, TextAlignmentOptions.Center); t.fontStyle = FontStyles.Bold; Stretch(t.rectTransform);
            return b;
        }

        // ---- generic UI + sprites (mirrors KenyaMenuScreens) --------------------------

        private static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

        private RectTransform NewRect(RectTransform parent, string name)
        { var go = new GameObject(name, typeof(RectTransform)); go.layer = parent.gameObject.layer; var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false); return rt; }

        private TMP_Text AddText(RectTransform parent, string name, string text, float size, Color colour, TextAlignmentOptions align)
        { var rt = NewRect(parent, name); var t = rt.gameObject.AddComponent<TextMeshProUGUI>(); t.text = text; t.fontSize = size; t.color = colour; t.alignment = align; t.raycastTarget = false; return t; }

        private Image AddImage(RectTransform parent, string name, Color colour, Sprite sprite)
        { var rt = NewRect(parent, name); var img = rt.gameObject.AddComponent<Image>(); img.color = colour; img.sprite = sprite; img.type = (sprite != null && sprite.border != Vector4.zero) ? Image.Type.Sliced : Image.Type.Simple; img.raycastTarget = false; return img; }

        private void Spaced(TMP_Text t, float em) => t.characterSpacing = em * 100f;

        private static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f); rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero; }
        private static void Anchor(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 size, Vector2 pos) { rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = size; rt.anchoredPosition = pos; }

        private void ClearGenerated()
        { var rt = (RectTransform)transform; for (int i = rt.childCount - 1; i >= 0; i--) { var c = rt.GetChild(i).gameObject; if (Application.isPlaying) Destroy(c); else DestroyImmediate(c); } }

        private void ClearChildren(RectTransform rt)
        { if (rt == null) return; for (int i = rt.childCount - 1; i >= 0; i--) { var c = rt.GetChild(i).gameObject; if (Application.isPlaying) Destroy(c); else DestroyImmediate(c); } }

        private static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null) return;
            var es = new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            DontDestroyOnLoad(es);
        }

        private readonly Dictionary<int, Sprite> _rounded = new();
        private Sprite Rounded(int radius)
        {
            if (_rounded.TryGetValue(radius, out var c)) return c;
            int s = radius * 2 + 4; var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int yy = 0; yy < s; yy++) for (int xx = 0; xx < s; xx++)
            { float dx = Mathf.Max(radius - xx, xx - (s - radius), 0f); float dy = Mathf.Max(radius - yy, yy - (s - radius), 0f); tex.SetPixel(xx, yy, new Color(1, 1, 1, Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f))); }
            tex.Apply(); var sp = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius)); _rounded[radius] = sp; return sp;
        }
    }
}
