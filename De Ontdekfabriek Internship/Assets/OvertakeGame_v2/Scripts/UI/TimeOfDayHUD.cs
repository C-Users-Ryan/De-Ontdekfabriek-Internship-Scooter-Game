using UnityEngine;
using TMPro;

namespace OvertakeGame
{
    /// <summary>
    /// Displays the Swahili time-of-day label in a corner of the HUD.
    /// ASUBUHI (Morning) · MCHANA (Midday) · ALASIRI (Late afternoon) · JIONI (Sunset)
    ///
    /// SETUP:
    ///   1. Add a TMP_Text element to your HUD Canvas in a corner.
    ///   2. Attach this script and assign the text reference.
    ///   3. Optionally assign the three colour values for each time.
    /// </summary>
    public class TimeOfDayHUD : MonoBehaviour
    {
        [Header("UI Reference")]
        public TMP_Text timeLabel;

        [Header("Label Colours per Time")]
        public Color morningColour = new Color(1.00f, 0.82f, 0.54f); // warm gold
        public Color middayColour  = new Color(0.95f, 0.98f, 1.00f); // cool white
        public Color sunsetColour  = new Color(1.00f, 0.50f, 0.20f); // orange

        void Start()  => Refresh();
        void Update() => Refresh();

        private void Refresh()
        {
            if (timeLabel == null) return;

            string label = DayCycleManager.CurrentLabel;
            timeLabel.text  = label;
            timeLabel.color = label switch
            {
                "ASUBUHI" => morningColour,
                "MCHANA"  => middayColour,
                "ALASIRI" => sunsetColour,
                "JIONI"   => sunsetColour,
                _         => Color.white
            };
        }
    }
}