using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace OvertakeGame
{
    /// <summary>
    /// A small in-game settings overlay for the gyroscope feature.
    /// Shows the current tilt angle, a toggle button, and a calibrate button.
    ///
    /// SETUP:
    ///   Add to a Canvas child. Wire the references below.
    ///   Place it somewhere accessible during play (corner of screen).
    ///
    /// Canvas structure:
    ///   GyroSettingsPanel
    ///     ├── TiltReadout (TMP_Text)   — shows live tilt degrees
    ///     ├── ToggleButton (Button)    — enable/disable gyro
    ///     ├── ToggleLabel (TMP_Text)   — button label
    ///     └── CalibrateButton (Button) — zero current angle
    /// </summary>
    public class GyroSettingsUI : MonoBehaviour
    {
        [Header("References")]
        public GyroscopeSteering gyroSteering;

        [Header("UI Elements")]
        public TMP_Text tiltReadout;
        public Button   toggleButton;
        public TMP_Text toggleLabel;
        public Button   calibrateButton;
        public GameObject settingsPanel;

        [Header("Settings Panel Toggle")]
        [Tooltip("Key to show/hide the settings panel during testing.")]
        public KeyCode settingsPanelKey = KeyCode.G;

        void Start()
        {
            toggleButton?.onClick.AddListener(OnTogglePressed);
            calibrateButton?.onClick.AddListener(OnCalibratePressed);
            UpdateToggleLabel();
        }

        void Update()
        {
            // Live tilt readout
            if (tiltReadout != null && gyroSteering != null)
            {
                float tilt  = gyroSteering.CurrentTiltDegrees;
                float input = gyroSteering.CurrentSteeringInput;
                string arrow = input < -0.1f ? "◄" : (input > 0.1f ? "►" : "—");
                tiltReadout.text = $"Tilt: {tilt:F1}°  {arrow}  ({input:F2})";
            }

            // Toggle panel visibility with keyboard shortcut (for PC testing)
            if (Input.GetKeyDown(settingsPanelKey))
                settingsPanel?.SetActive(!settingsPanel.activeSelf);
        }

        private void OnTogglePressed()
        {
            if (gyroSteering == null) return;
            gyroSteering.SetEnabled(!gyroSteering.enableGyroSteering);
            UpdateToggleLabel();
        }

        private void OnCalibratePressed()
        {
            gyroSteering?.CalibrateToCurrentAngle();
        }

        private void UpdateToggleLabel()
        {
            if (toggleLabel == null || gyroSteering == null) return;
            toggleLabel.text = gyroSteering.enableGyroSteering
                ? "Gyro: ON"
                : "Gyro: OFF";
        }
    }
}
