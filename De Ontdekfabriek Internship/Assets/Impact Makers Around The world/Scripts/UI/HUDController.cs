using TMPro;
using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;
using KenyaScooter.Session;

namespace KenyaScooter.UI
{
    /// <summary>
    /// The play HUD (Req §12.1): score, streak multiplier, MM:SS timer, the Swahili
    /// day label with per-phase colour (M25), the grace icon and the tap-to-start
    /// overlay. Fully event-driven — text only updates when a value changes, and the
    /// timer string is rebuilt at most once per second.
    /// </summary>
    public sealed class HUDController : MonoBehaviour
    {
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text scoreCaption;
        [SerializeField] private TMP_Text multiplierText;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text dayLabel;
        [SerializeField] private TMP_Text startPrompt;
        [SerializeField] private GameObject startOverlay;
        [SerializeField] private GameObject graceIcon;
        [Tooltip("For the per-phase label colour — same asset DayCycleManager uses.")]
        [SerializeField] private DayCycleConfig dayConfig;

        private int lastSeconds = -1;

        private void OnEnable()
        {
            GameEvents.ScoreChanged += HandleScoreChanged;
            GameEvents.StreakChanged += HandleStreakChanged;
            GameEvents.DayPhaseChanged += HandleDayPhaseChanged;
            GameEvents.StateChanged += HandleStateChanged;
            GameEvents.GraceAbsorbed += HandleGraceAbsorbed;
            GameEvents.GraceRecharged += HandleGraceRecharged;
            GameEvents.SessionReset += HandleSessionReset;
            SwahiliUI.LanguageChanged += ApplyLanguage;
            ApplyLanguage();
        }

        private void OnDisable()
        {
            GameEvents.ScoreChanged -= HandleScoreChanged;
            GameEvents.StreakChanged -= HandleStreakChanged;
            GameEvents.DayPhaseChanged -= HandleDayPhaseChanged;
            GameEvents.StateChanged -= HandleStateChanged;
            GameEvents.GraceAbsorbed -= HandleGraceAbsorbed;
            GameEvents.GraceRecharged -= HandleGraceRecharged;
            GameEvents.SessionReset -= HandleSessionReset;
            SwahiliUI.LanguageChanged -= ApplyLanguage;
        }

        private void Update()
        {
            if (TimerManager.Instance == null || timerText == null)
                return;

            int seconds = Mathf.CeilToInt(TimerManager.Instance.Remaining);
            if (seconds == lastSeconds)
                return;
            lastSeconds = seconds;
            timerText.text = $"{seconds / 60}:{seconds % 60:00}";
        }

        private void HandleScoreChanged(int total, int delta)
        {
            if (scoreText != null)
                scoreText.text = total.ToString();
        }

        private void HandleStreakChanged(int streak, float multiplier)
        {
            if (multiplierText == null)
                return;
            bool show = multiplier > 1f;
            multiplierText.gameObject.SetActive(show);
            if (show)
                multiplierText.text = $"×{multiplier:0.#}";
        }

        private void HandleDayPhaseChanged(int index, string label)
        {
            if (dayLabel == null)
                return;
            dayLabel.text = label;
            if (dayConfig != null && index < dayConfig.phases.Length)
                dayLabel.color = dayConfig.phases[index].labelColour;
        }

        private void HandleStateChanged(GameState from, GameState to)
        {
            if (startOverlay != null)
                startOverlay.SetActive(to == GameState.Ready);
        }

        private void HandleGraceAbsorbed() => SetGraceIcon(false);
        private void HandleGraceRecharged() => SetGraceIcon(true);
        private void HandleSessionReset() => SetGraceIcon(true);

        private void SetGraceIcon(bool available)
        {
            if (graceIcon != null)
                graceIcon.SetActive(available);
        }

        private void ApplyLanguage()
        {
            if (scoreCaption != null)
                scoreCaption.text = SwahiliUI.Get("HUD_SCORE");
            if (startPrompt != null)
                startPrompt.text = SwahiliUI.Get("START_PROMPT");
        }
    }
}
