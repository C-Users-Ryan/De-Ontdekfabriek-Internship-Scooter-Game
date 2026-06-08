using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace OvertakeGame
{
    /// <summary>
    /// In-game settings panel for the gyroscope feature.
    /// Shows live tilt readout, toggle button, and calibrate button.
    /// Press G on keyboard to show/hide during testing.
    /// </summary>
    public class GyroSettingsUI : MonoBehaviour
    {
        [Header("References")]
        public GyroscopeSteering gyroSteering;

        [Header("UI Elements")]
        public TMP_Text   tiltReadout;
        public Button     toggleButton;
        public TMP_Text   toggleLabel;
        public Button     calibrateButton;
        public GameObject settingsPanel;

        [Header("Settings Panel Toggle Key")]
        public KeyCode settingsPanelKey = KeyCode.G;

        void Start()
        {
            toggleButton   ?.onClick.AddListener(OnTogglePressed);
            calibrateButton?.onClick.AddListener(OnCalibratePressed);
            UpdateToggleLabel();
        }

        void Update()
        {
            if (tiltReadout != null && gyroSteering != null)
            {
                float tilt  = gyroSteering.CurrentTiltDegrees;
                float input = gyroSteering.CurrentSteeringInput;
                string arrow = input < -0.1f ? "◄" : input > 0.1f ? "►" : "—";
                tiltReadout.text = $"Tilt: {tilt:F1}°  {arrow}  ({input:F2})";
            }
            if (Input.GetKeyDown(settingsPanelKey))
                settingsPanel?.SetActive(!settingsPanel.activeSelf);
        }

        private void OnTogglePressed()
        {
            if (gyroSteering == null) return;
            gyroSteering.SetEnabled(!gyroSteering.enableGyroSteering);
            UpdateToggleLabel();
        }

        private void OnCalibratePressed() => gyroSteering?.CalibrateToCurrentAngle();

        private void UpdateToggleLabel()
        {
            if (toggleLabel == null || gyroSteering == null) return;
            toggleLabel.text = gyroSteering.enableGyroSteering ? "Gyro: ON" : "Gyro: OFF";
        }
    }
}
