using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems; // press-and-hold detection for the access-code recovery
using KenyaScooter.UI; // shared UiKit design tokens (palette, radii, shadows, sheen)

namespace KenyaScooter.Settings
{
    /// <summary>
    /// The facilitator settings menu, generated in code in the warm Ugani house style (same approach as
    /// KenyaMenuScreens / DiegeticHud, authored on the 2048x1536 iPad 4:3 reference canvas). It is built
    /// entirely from <see cref="SettingsCatalog"/>: a category list down the left, and one row per setting
    /// on the right with the right widget (slider / on-off / +- stepper), a live value chip, a one-line
    /// description and a per-row reset. Nothing about any individual setting is hardcoded here — adding a
    /// setting to the catalog makes a new row appear automatically.
    ///
    /// Opening it pauses the game (Time.timeScale = 0) so the facilitator can adjust mid-session; closing
    /// it restores play. Every edit writes through <see cref="GameSettings"/>, which saves the override to
    /// PlayerPrefs and applies it to the live config immediately, so changes are visible the moment the
    /// menu closes and persist across restarts.
    ///
    /// Self-creates its own canvas if none exists (GetOrCreate), so the FacilitatorGate can summon it with
    /// no scene wiring; or build it ahead of time via Tools > Kenya Scooter > UI Builders > Build Settings Menu.
    /// </summary>
    // NOTE: this class is split across two files for readability (2026-06-30, no behaviour change):
    //   SettingsMenu.cs       — state/lifecycle: open/close, the access-code (PIN) flow, Update.
    //   SettingsMenu.Build.cs — construction: Build() and the procedural header/sidebar/rows/overview/profiles UI.
    [DisallowMultipleComponent]
    public sealed partial class SettingsMenu : MonoBehaviour
    {
        // House palette (Ugani / Impact Makers Around The World), now sourced from the shared UiKit tokens so the facilitator menu matches
        // the framing screens and the HUD exactly (the rationale doc flagged the old #F2A055 accent / #F9E9D6 ink
        // as "snap to the canonical brand hexes at final art lock" — this is that snap). The warm Maroon panel is
        // kept deliberately: the rationale documents it as the deep warm backing that makes the orange and the
        // white text pop.
        private static readonly Color Ink      = UiKit.Hex("#F9E9D6"); // a hair warmer than pure cream for big body areas
        // The token-derived roles are live properties (not cached fields) so a UITheme swap reskins this menu too.
        private static Color Muted      => UiKit.InkMuted;              // #C9B7A6 (exact token)
        private static readonly Color Kicker    = Hex("#F2A468");
        private static Color Accent     => UiKit.Accent;               // canonical #F19141
        private static Color Cream      => UiKit.Ink;                  // canonical #FFF6EC
        private static readonly Color Maroon     = Hex("#2A1410");
        private static Color InkOnLight => UiKit.InkOnLight;           // dark warm-brown text on a cream/orange face
        private static readonly Color CardFill  = new Color(0.10f, 0.05f, 0.03f, 0.85f);
        private static readonly Color RowFill   = new Color(1f, 0.96f, 0.92f, 0.06f);
        private static readonly Color TrackFill = new Color(1f, 0.96f, 0.92f, 0.16f);

        private bool built;
        private GameObject root;
        private RectTransform rowHost;       // where setting rows are placed
        private RectTransform categoryHost;  // the left sidebar
        private TMP_Text headerSub;
        private SettingCategory current = SettingCategory.Profiles;

        private readonly List<Button> categoryButtons = new();
        private readonly List<SettingCategory> categoryOrder = new();
        private readonly List<TMP_Text> categoryBadges = new(); // per-category "how many settings changed" count
        private bool showAdvanced; // whether the current category's "Meer opties" rows are revealed

        // Master detail level. Basis = each category opens with just its essentials (the finer tuning behind
        // "Meer opties"); Expert = every category opens fully expanded. Persisted, so a venue keeps its choice.
        private const string ExpertKey = "ksg.ui.expert";
        private bool ExpertMode
        {
            get => PlayerPrefs.GetInt(ExpertKey, 0) == 1;
            set { PlayerPrefs.SetInt(ExpertKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        // ---- access code (PIN) state --------------------------------------------------
        // The keypad screen has three jobs: unlock the menu, set a new code (twice), confirm it. One small
        // state machine keeps the same on-screen keypad doing all three, so there is only one widget to build.
        private enum LockMode { Unlock, SetNew, ConfirmNew }
        private GameObject lockRoot;          // the dim + keypad sheet, drawn above the settings sheet
        private RectTransform lockPanel;
        private TMP_Text lockTitle, lockSub, lockStatus, lockHint;
        // v2 entry dots: a filled accent disc per typed digit, a hollow ring for the rest (real components,
        // not ●/○ typography). lockDotHole is the dark punch that turns a disc into a ring when enabled.
        private Image[] lockDotFill;
        private Image[] lockDotHole;
        private LockMode lockMode = LockMode.Unlock;
        private string entered = "";
        private string firstNewPin = "";      // remembered between the two "set a new code" steps
        private float recoveryHeldSince = -1f; // unscaled time the padlock was first held, for lock-out recovery
        private Coroutine shake;               // handle to the running shake, so a re-trigger can stop it (nameof can't)

        // ---- lifecycle / access -------------------------------------------------------

        /// <summary>Finds the menu in the scene, or creates a canvas + menu on demand (used by the gate).</summary>
        public static SettingsMenu GetOrCreate()
        {
            var existing = FindObjectOfType<SettingsMenu>();
            if (existing != null) return existing;
            if (!Application.isPlaying) return null;

            var canvasGO = new GameObject("Settings Menu Canvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            DontDestroyOnLoad(canvasGO);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // above HUD and framing screens
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(2048f, 1536f);
            scaler.matchWidthOrHeight = 0.5f;

            EnsureEventSystem();

            var go = new GameObject("Settings Menu", typeof(RectTransform));
            go.transform.SetParent(canvasGO.transform, false);
            var menu = go.AddComponent<SettingsMenu>();
            menu.Build();
            return menu;
        }

        private void Awake() { if (!built) Build(); }

        // ---- open / close -------------------------------------------------------------

        /// <summary>The gated entry the FacilitatorGate uses: shows the access-code keypad first when a code is
        /// required, and only opens the settings once it is entered. Pauses the game either way so a child cannot
        /// keep playing while the keypad is up.</summary>
        public void RequestOpen()
        {
            if (!built) Build();
            Time.timeScale = 0f; // pause; all the menu's own animations use unscaled time
            AudioListener.pause = true; // freeze the soundscape with the world — engines droning over a frozen game reads as broken
            if (FacilitatorLock.Enabled)
                ShowLock(LockMode.Unlock);
            else
                Open();
        }

        /// <summary>Opens the settings sheet directly (already unlocked, or no code required).</summary>
        public void Open()
        {
            if (!built) Build();
            if (lockRoot != null) lockRoot.SetActive(false);
            root.SetActive(true);
            RefreshHeader();
            ShowCategory(current);
            Time.timeScale = 0f;
            AudioListener.pause = true;
        }

        public void Close()
        {
            if (root != null) root.SetActive(false);
            if (lockRoot != null) lockRoot.SetActive(false);
            entered = ""; firstNewPin = ""; recoveryHeldSince = -1f;
            Time.timeScale = 1f;
            AudioListener.pause = false; // resume the soundscape with the world
        }

        private void RefreshHeader()
        {
            if (headerSub != null)
                headerSub.text = "INSTELLINGEN VOOR BEGELEIDERS  ·  WIJZIGINGEN BLIJVEN BEWAARD";
        }

        // ---- access code (PIN) flow ----------------------------------------------------

        /// <summary>Called from the "Toegangscode wijzigen" action: pops the keypad in set-a-new-code mode over the
        /// already-open settings, so staff can change the code without a hardware keyboard.</summary>
        public static void RequestChangePin()
        {
            var menu = FindObjectOfType<SettingsMenu>();
            if (menu != null) menu.BeginChangePin();
        }

        private void BeginChangePin()
        {
            if (!built) Build();
            ShowLock(LockMode.SetNew);
        }

        private void ShowLock(LockMode mode)
        {
            lockMode = mode;
            entered = "";
            firstNewPin = "";
            recoveryHeldSince = -1f;
            if (root != null) root.SetActive(mode != LockMode.Unlock); // change-PIN keeps the settings visible behind
            if (lockRoot != null) lockRoot.SetActive(true);
            PaintLock("");
        }

        private void PaintLock(string status)
        {
            if (lockTitle != null)
                lockTitle.text = lockMode == LockMode.Unlock ? "TOEGANGSCODE" : "NIEUWE CODE";
            if (lockSub != null)
            {
                switch (lockMode)
                {
                    case LockMode.SetNew:     lockSub.text = "Kies een nieuwe code van " + FacilitatorLock.PinLength + " cijfers."; break;
                    case LockMode.ConfirmNew: lockSub.text = "Tik de nieuwe code nog een keer ter bevestiging."; break;
                    default:                  lockSub.text = "Voer de begeleiderscode in om de instellingen te openen."; break;
                }
            }
            if (lockStatus != null) lockStatus.text = status;
            // The padlock recovery hint only applies where the padlock hold works (Unlock mode).
            if (lockHint != null) lockHint.gameObject.SetActive(lockMode == LockMode.Unlock);
            UpdateDots();
        }

        private void UpdateDots()
        {
            if (lockDotFill == null) return;
            for (int i = 0; i < lockDotFill.Length; i++)
            {
                bool filled = i < entered.Length;
                if (lockDotFill[i] != null) lockDotFill[i].color = filled ? Accent : new Color(Cream.r, Cream.g, Cream.b, 0.35f);
                if (lockDotHole[i] != null) lockDotHole[i].enabled = !filled; // punch out the centre → hollow ring
            }
        }

        private void PressDigit(int d)
        {
            if (entered.Length >= FacilitatorLock.PinLength) return;
            entered += (char)('0' + d);
            UpdateDots();
            if (entered.Length >= FacilitatorLock.PinLength)
                Evaluate();
        }

        private void PressBackspace()
        {
            if (entered.Length > 0) entered = entered.Substring(0, entered.Length - 1);
            UpdateDots();
        }

        private void Evaluate()
        {
            switch (lockMode)
            {
                case LockMode.Unlock:
                    if (FacilitatorLock.Verify(entered)) { Open(); }
                    else { entered = ""; PaintLock("Onjuiste code. Probeer opnieuw."); ShakeLock(); }
                    break;

                case LockMode.SetNew:
                    firstNewPin = entered; entered = "";
                    lockMode = LockMode.ConfirmNew; PaintLock("");
                    break;

                case LockMode.ConfirmNew:
                    if (entered == firstNewPin && FacilitatorLock.SetPin(entered))
                    {
                        entered = ""; PaintLock("Code gewijzigd ✓");
                        StartCoroutine(BackToSettingsRealtime(0.9f)); // brief confirmation, then back to the settings
                    }
                    else
                    {
                        entered = ""; firstNewPin = ""; lockMode = LockMode.SetNew;
                        PaintLock("Codes komen niet overeen. Begin opnieuw."); ShakeLock();
                    }
                    break;
            }
        }

        private System.Collections.IEnumerator BackToSettingsRealtime(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            Open();
        }

        private void ShakeLock()
        {
            if (lockPanel == null) return;
            if (shake != null) StopCoroutine(shake); // stop by handle — StopCoroutine(nameof(...)) can't cancel an IEnumerator-started routine
            shake = StartCoroutine(ShakeRoutine());
        }

        private System.Collections.IEnumerator ShakeRoutine()
        {
            Vector2 home = Vector2.zero; // the panel's resting position (centred); never drift off it
            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.unscaledDeltaTime;
                float damp = 1f - (t / 0.35f);
                lockPanel.anchoredPosition = home + new Vector2(Mathf.Sin(t * 60f) * 22f * damp, 0f);
                yield return null;
            }
            lockPanel.anchoredPosition = home;
        }

        // The padlock can be press-held to recover from a forgotten code (staff-only, documented). Runs in Update
        // because it must work while the game is paused (Time.timeScale == 0) and across the keypad's own state.
        private void Update()
        {
            if (lockRoot == null || !lockRoot.activeSelf || lockMode != LockMode.Unlock) { recoveryHeldSince = -1f; return; }
            if (recoveryHeldSince < 0f) return;
            if (Time.unscaledTime - recoveryHeldSince >= FacilitatorLock.RecoveryHoldSeconds)
            {
                recoveryHeldSince = -1f;
                FacilitatorLock.ResetToDefault();
                entered = "";
                PaintLock("Code teruggezet naar standaard.");
            }
        }

    }
}
