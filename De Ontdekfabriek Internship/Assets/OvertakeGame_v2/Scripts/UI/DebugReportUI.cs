using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace OvertakeGame
{
    /// <summary>
    /// Debug / facilitator panel for generating and viewing the analytics report.
    /// Accessible via a hidden button or shake gesture during a session.
    ///
    /// SETUP:
    ///   1. Add a hidden panel to your Canvas (Debug/FacilitatorPanel).
    ///   2. Inside: a TMP_Text for the report output, a Generate button,
    ///      a Reset Analytics button, a Reset High Score button,
    ///      a Reset Tutorial button, and a Close button.
    ///   3. Attach this script and wire all references.
    ///   4. Access during a session by pressing Tab on keyboard (or
    ///      the hidden button — e.g. tap the score label 5 times).
    /// </summary>
    public class DebugReportUI : MonoBehaviour
    {
        [Header("Panel")]
        public GameObject panel;

        [Header("UI Elements")]
        public TMP_Text   reportText;
        public Button     generateButton;
        public Button     resetAnalyticsButton;
        public Button     resetHighScoreButton;
        public Button     resetTutorialButton;
        public Button     closeButton;

        [Header("Keyboard Shortcut (Desktop / Testing)")]
        public KeyCode toggleKey = KeyCode.Tab;

        void Start()
        {
            panel?.SetActive(false);
            generateButton?.onClick.AddListener(GenerateReport);
            resetAnalyticsButton?.onClick.AddListener(ResetAnalytics);
            resetHighScoreButton?.onClick.AddListener(ResetHighScore);
            resetTutorialButton?.onClick.AddListener(ResetTutorial);
            closeButton?.onClick.AddListener(() => panel?.SetActive(false));
        }

        void Update()
        {
            if (Input.GetKeyDown(toggleKey))
                panel?.SetActive(!panel.activeSelf);
        }

        private void GenerateReport()
        {
            if (AnalyticsManager.I == null) return;
            string report = AnalyticsManager.I.GenerateReport();
            if (reportText != null)
                reportText.text = report;
            Debug.Log(report);
        }

        private void ResetAnalytics()
        {
            AnalyticsManager.I?.ResetAll();
            if (reportText != null) reportText.text = "Analytics reset.";
        }

        private void ResetHighScore()
        {
            HighScoreManager.I?.ResetAll();
            if (reportText != null) reportText.text = "High scores reset.";
        }

        private void ResetTutorial()
        {
            TutorialHint.ResetForTesting();
            if (reportText != null) reportText.text = "Tutorial flag reset. Will show on next play.";
        }
    }
}
