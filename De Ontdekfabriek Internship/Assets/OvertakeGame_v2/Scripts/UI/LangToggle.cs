using UnityEngine;
using TMPro;

namespace OvertakeGame
{
    /// <summary>
    /// Language toggle button — switches all UI between English and Swahili.
    /// Attach to the EN | SW toggle button on the start screen.
    ///
    /// SETUP:
    ///   1. Add a Button to your start screen Canvas named LangToggle.
    ///   2. Add a TMP_Text child showing "EN | SW".
    ///   3. Attach this script and assign the text references below.
    ///   4. Wire the button's OnClick event to this script's Toggle() method.
    ///   5. Wire all other UI TMP_Text fields to refresh via OnLanguageChanged
    ///      or call RefreshAll() after toggling.
    /// </summary>
    public class LangToggle : MonoBehaviour
    {
        [Header("Start Screen Labels")]
        public TMP_Text startButtonLabel;
        public TMP_Text taglineLabel;
        public TMP_Text toggleButtonLabel;

        [Header("Optional — other labels to refresh")]
        public TMP_Text scoreLabel;
        public TMP_Text distanceLabel;
        public TMP_Text bestLabel;

        void Start() => RefreshAll();

        public void Toggle()
        {
            SwahiliUI.I?.Toggle();
            RefreshAll();
        }

        private void RefreshAll()
        {
            if (SwahiliUI.I == null) return;

            if (startButtonLabel  != null) startButtonLabel.text  = SwahiliUI.I.Get("start");
            if (taglineLabel      != null) taglineLabel.text      = SwahiliUI.I.Get("tagline");
            if (scoreLabel        != null) scoreLabel.text        = SwahiliUI.I.Get("score");
            if (distanceLabel     != null) distanceLabel.text     = SwahiliUI.I.Get("distance");
            if (bestLabel         != null) bestLabel.text         = SwahiliUI.I.Get("best");

            // Update the toggle button label to show active language
            if (toggleButtonLabel != null)
                toggleButtonLabel.text = SwahiliUI.I.useSwahili ? "SW | EN" : "EN | SW";
        }
    }
}