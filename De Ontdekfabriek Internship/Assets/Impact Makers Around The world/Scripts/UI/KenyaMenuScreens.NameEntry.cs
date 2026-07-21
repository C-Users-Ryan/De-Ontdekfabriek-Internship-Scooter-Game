// Name-entry ("Eigen naam") half of KenyaMenuScreens — a self-contained ON-SCREEN KEYBOARD so a group can
// TYPE a team name on a locked kiosk tablet (Unity's TouchScreenKeyboard is unreliable in a fullscreen,
// screen-pinned build, so we draw our own QWERTY). The typed, cleaned name routes through the SAME
// PickTeam(...) the animal cards use, so it flows everywhere a team name already does: the relay / journey /
// game-over kickers, the persisted leaderboard row, and the "this is us" board highlight. Pairs with the
// "EIGEN NAAM" card added in PopulateChips (KenyaMenuScreens.cs). No new prefab or scene wiring — it reuses
// the same procedural UI helpers (NewRect / AddText / AddImage / Anchor / Stretch / UiKit) as the rest.
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KenyaScooter.Scoring;

namespace KenyaScooter.UI
{
    public sealed partial class KenyaMenuScreens
    {
        private GameObject nameRoot;
        private TMP_Text nameDisplay;
        private string nameBuffer = "";
        private const int NameMaxLen = 12; // the board name column is finite; keep names short so they never clip
        private const int NameMinLen = 2;

        // A deliberately SMALL blocklist — enough to stop the obvious, not a bulletproof filter. This is a
        // FACILITATED exhibition (an adult is present), so "good enough" is the right bar; extend as needed.
        private static readonly HashSet<string> NameBlocklist = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
        {
            "KUT", "LUL", "KKR", "HOER", "NEUK", "TERING", "KANKER", "FUCK", "SHIT", "DICK", "COCK", "CUNT", "BITCH", "SEX", "NAZI"
        };

        // ---- open / close / edit ------------------------------------------------------

        private void OpenNameEntry()
        {
            if (nameRoot == null) BuildNameEntry();
            nameBuffer = "";
            UpdateNameDisplay();
            nameRoot.SetActive(true);
            nameRoot.transform.SetAsLastSibling(); // draw above the setup screen
        }

        private void CloseNameEntry() { if (nameRoot != null) nameRoot.SetActive(false); }

        private void NameType(char c)
        {
            if (nameBuffer.Length >= NameMaxLen) return;
            nameBuffer += c;
            UpdateNameDisplay();
        }

        private void NameBackspace()
        {
            if (nameBuffer.Length > 0) nameBuffer = nameBuffer.Substring(0, nameBuffer.Length - 1);
            UpdateNameDisplay();
        }

        private void UpdateNameDisplay()
        {
            if (nameDisplay == null) return;
            nameDisplay.text = string.IsNullOrEmpty(nameBuffer) ? "<alpha=#55>TYP EEN NAAM" : nameBuffer.ToUpperInvariant();
        }

        private void NameConfirm()
        {
            string cleaned = CleanName(nameBuffer);
            if (cleaned.Length < NameMinLen)
            {
                nameBuffer = "";
                if (nameDisplay != null) nameDisplay.text = "<alpha=#55>MINSTENS 2 LETTERS";
                return; // stay on the keyboard so they can try again
            }
            cleaned = MakeNameUnique(cleaned);
            CloseNameEntry();
            PickTeam(cleaned); // sets teamName + starts the game — the exact path the animal cards use
        }

        // Uppercase, letters + single spaces only, trimmed and length-clamped, obvious profanity rejected.
        private string CleanName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            var sb = new System.Text.StringBuilder(raw.Length);
            bool lastSpace = false;
            foreach (char ch in raw.ToUpperInvariant())
            {
                bool letter = ch >= 'A' && ch <= 'Z';
                bool space = ch == ' ';
                if (letter) { sb.Append(ch); lastSpace = false; }
                else if (space && !lastSpace && sb.Length > 0) { sb.Append(' '); lastSpace = true; }
            }
            string s = sb.ToString().Trim();
            if (s.Length > NameMaxLen) s = s.Substring(0, NameMaxLen).Trim();
            string compact = s.Replace(" ", "");
            foreach (string bad in NameBlocklist)
                if (compact.Contains(bad)) return ""; // reject → treated as "too short", re-prompt
            return s;
        }

        // If the typed name already sits on this iPad's local leaderboard, append a number so the journey-board
        // "this is us" highlight (which matches by name after an app restart) can't latch onto the wrong row.
        private string MakeNameUnique(string name)
        {
            var used = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            var entries = LeaderboardManager.Instance != null ? LeaderboardManager.Instance.Entries : null;
            if (entries != null)
                for (int i = 0; i < entries.Count; i++)
                    if (!string.IsNullOrEmpty(entries[i].label)) used.Add(entries[i].label.Trim());

            if (!used.Contains(name)) return name;
            string stem = name.Length > NameMaxLen - 3 ? name.Substring(0, NameMaxLen - 3).Trim() : name;
            for (int n = 2; n <= 99; n++)
            {
                string candidate = stem + " " + n;
                if (!used.Contains(candidate)) return candidate;
            }
            return name;
        }

        // ---- construction -------------------------------------------------------------

        private void BuildNameEntry()
        {
            nameRoot = NewRect((RectTransform)transform, "NameEntry").gameObject;
            Stretch((RectTransform)nameRoot.transform);
            var dim = nameRoot.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.82f); dim.raycastTarget = true; // block taps to the setup behind

            RectTransform panel = NewRect((RectTransform)nameRoot.transform, "Panel");
            Anchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1560f, 1040f), Vector2.zero);
            Image hair = panel.gameObject.AddComponent<Image>();
            hair.sprite = UiKit.Rounded(UiKit.RadiusXl); hair.type = Image.Type.Sliced; hair.color = UiKit.WithAlpha(UiKit.Rust, 0.55f);
            Image fill = AddImage(panel, "Fill", new Color(0.102f, 0.075f, 0.063f, 0.98f), UiKit.Rounded(UiKit.RadiusXl));
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = new Vector2(2f, 2f); fill.rectTransform.offsetMax = new Vector2(-2f, -2f);

            TMP_Text title = AddText(panel, "Title", "TYP JE TEAMNAAM", 40, cream, TextAlignmentOptions.Center);
            title.fontStyle = FontStyles.Bold; UiKit.Caps(title, 0.05f);
            title.rectTransform.anchorMin = new Vector2(0.5f, 1f); title.rectTransform.anchorMax = new Vector2(0.5f, 1f); title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.sizeDelta = new Vector2(1400f, 52f); title.rectTransform.anchoredPosition = new Vector2(0f, -34f);

            // The typed-so-far display, on a sunken track.
            RectTransform disp = NewRect(panel, "Display");
            disp.anchorMin = new Vector2(0.5f, 1f); disp.anchorMax = new Vector2(0.5f, 1f); disp.pivot = new Vector2(0.5f, 1f);
            disp.sizeDelta = new Vector2(1200f, 108f); disp.anchoredPosition = new Vector2(0f, -104f);
            Image dispBg = disp.gameObject.AddComponent<Image>(); dispBg.sprite = UiKit.Rounded(UiKit.RadiusM); dispBg.type = Image.Type.Sliced; dispBg.color = new Color(1f, 0.96f, 0.92f, 0.10f); dispBg.raycastTarget = false;
            nameDisplay = AddText(disp, "Text", "<alpha=#55>TYP EEN NAAM", 52, cream, TextAlignmentOptions.Center);
            nameDisplay.fontStyle = FontStyles.Bold; Stretch(nameDisplay.rectTransform);

            // Three QWERTY letter rows.
            NameKeyRow(panel, "QWERTYUIOP", -272f);
            NameKeyRow(panel, "ASDFGHJKL", -388f);
            NameKeyRow(panel, "ZXCVBNM", -504f);

            // Bottom action row: cancel · space · backspace · confirm.
            NameKey(panel, "TERUG", -500f, -624f, 220f, 104f, CloseNameEntry);
            NameKey(panel, "SPATIE", -160f, -624f, 420f, 104f, () => NameType(' '));
            NameKey(panel, "WIS", 180f, -624f, 220f, 104f, NameBackspace);
            NameKey(panel, "SPEEL", 460f, -624f, 300f, 104f, NameConfirm, primary: true);
        }

        private void NameKeyRow(RectTransform panel, string letters, float y)
        {
            const float kw = 118f, kh = 104f, gap = 12f;
            int n = letters.Length;
            float total = n * kw + (n - 1) * gap;
            float x0 = -total * 0.5f + kw * 0.5f;
            for (int i = 0; i < n; i++)
            {
                char ch = letters[i]; // loop-local so the closure captures THIS letter
                NameKey(panel, ch.ToString(), x0 + i * (kw + gap), y, kw, kh, () => NameType(ch));
            }
        }

        // One key. Positioned from the panel's top-centre (y is negative, downward). Accent keys (SPEEL) get the
        // primary fill; the rest a quiet translucent face — the same look as the settings keypad.
        private void NameKey(RectTransform panel, string glyph, float x, float y, float w, float h, UnityEngine.Events.UnityAction action, bool primary = false)
        {
            RectTransform b = NewRect(panel, "Key_" + glyph);
            b.anchorMin = new Vector2(0.5f, 1f); b.anchorMax = new Vector2(0.5f, 1f); b.pivot = new Vector2(0.5f, 1f);
            b.sizeDelta = new Vector2(w, h); b.anchoredPosition = new Vector2(x, y);
            Image img = b.gameObject.AddComponent<Image>(); img.sprite = UiKit.Rounded(UiKit.RadiusM); img.type = Image.Type.Sliced;
            img.color = primary ? accent : new Color(1f, 0.96f, 0.92f, 0.10f);
            var btn = b.gameObject.AddComponent<Button>(); btn.targetGraphic = img;
            var cb = btn.colors; cb.fadeDuration = 0.06f; cb.highlightedColor = Color.white; cb.pressedColor = new Color(1f, 1f, 1f, 0.85f); btn.colors = cb;
            if (action != null) btn.onClick.AddListener(action);
            TMP_Text t = AddText(b, "g", glyph, glyph.Length > 1 ? 26 : 40, primary ? UiKit.InkOnAccent : cream, TextAlignmentOptions.Center);
            t.fontStyle = FontStyles.Bold; if (glyph.Length > 1) UiKit.Caps(t, 0.04f); Stretch(t.rectTransform);
        }
    }
}
