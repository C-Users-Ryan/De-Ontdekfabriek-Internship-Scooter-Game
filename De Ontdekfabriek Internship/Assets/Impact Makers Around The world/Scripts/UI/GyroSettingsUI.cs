using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace OvertakeGame
{
    /// <summary>
    /// In-game settings panel for gyroscope controls and Real Rider Mode.
    ///
    /// ── BUTTONS ──────────────────────────────────────────────────────────────────
    ///   Toggle Gyro      — enables / disables gyroscope steering entirely
    ///   Toggle Real Rider — enables / disables horizon lock (Real Rider Mode)
    ///   Calibrate        — sets the current device angle as the steering zero point
    ///
    /// ── KEYBOARD SHORTCUTS ───────────────────────────────────────────────────────
    ///   G → show / hide this panel
    ///   H → toggle Real Rider Mode directly (fast iteration during playtesting)
    ///
    /// ── SETUP ────────────────────────────────────────────────────────────────────
    ///   Assign gyroSteering, then assign each UI element.
    ///   settingsPanel is shown/hidden by G — assign the root panel GameObject.
    ///   All button assignments are optional: missing buttons are silently skipped.
    /// </summary>
    public class GyroSettingsUI : MonoBehaviour
    {
        [Header("References")]
        public GyroscopeSteering gyroSteering;

        [Header("UI Elements")]
        [Tooltip("Live display of tilt angle and steering input. Updated every frame.")]
        public TMP_Text   tiltReadout;

        [Tooltip("Toggles gyroscope steering ON / OFF.")]
        public Button     toggleGyroButton;
        public TMP_Text   toggleGyroLabel;

        [Tooltip("Toggles Real Rider Mode (horizon lock) ON / OFF.\n" +
                 "ON  = road stays flat, scooter mesh leans.\n" +
                 "OFF = no camera compensation (road tilts with device).")]
        public Button     toggleRealRiderButton;
        public TMP_Text   toggleRealRiderLabel;

        [Tooltip("Recalibrates the tilt zero point to the current device angle.")]
        public Button     calibrateButton;

        [Tooltip("Root of the settings panel — shown/hidden by the G key.")]
        public GameObject settingsPanel;

        [Header("Keyboard Shortcuts")]
        public KeyCode settingsPanelKey = KeyCode.G;
        [Tooltip("Quick-toggle for Real Rider Mode — useful during playtesting without opening the panel.")]
        public KeyCode realRiderKey     = KeyCode.H;

        // ── Unity lifecycle ───────────────────────────────────────────────────────

        void Start()
        {
            toggleGyroButton    ?.onClick.AddListener(OnToggleGyroPressed);
            toggleRealRiderButton?.onClick.AddListener(OnToggleRealRiderPressed);
            calibrateButton     ?.onClick.AddListener(OnCalibratePressed);
            UpdateAllLabels();
        }

        void Update()
        {
            // Live tilt readout
            if (tiltReadout != null && gyroSteering != null)
            {
                float  tilt  = gyroSteering.CurrentTiltDegrees;
                float  input = gyroSteering.CurrentSteeringInput;
                string arrow = input < -0.1f ? "◄" : input > 0.1f ? "►" : "—";
                tiltReadout.text = $"Tilt {tilt:F1}°  {arrow}  ({input:F2})";
            }

            // Show / hide the settings panel
            if (Input.GetKeyDown(settingsPanelKey))
                settingsPanel?.SetActive(!settingsPanel.activeSelf);

            // Quick-toggle Real Rider Mode without opening the panel
            if (Input.GetKeyDown(realRiderKey) && gyroSteering != null)
            {
                gyroSteering.SetHorizonLock(!gyroSteering.HorizonLockEnabled);
                UpdateAllLabels();
            }
        }

        // ── Button handlers ───────────────────────────────────────────────────────

        private void OnToggleGyroPressed()
        {
            if (gyroSteering == null) return;
            gyroSteering.SetEnabled(!gyroSteering.enableGyroSteering);
            UpdateAllLabels();
        }

        private void OnToggleRealRiderPressed()
        {
            if (gyroSteering == null) return;
            gyroSteering.SetHorizonLock(!gyroSteering.HorizonLockEnabled);
            UpdateAllLabels();
        }

        private void OnCalibratePressed() => gyroSteering?.CalibrateToCurrentAngle();

        // ── Label refresh ─────────────────────────────────────────────────────────

        private void UpdateAllLabels()
        {
            if (gyroSteering == null) return;

            if (toggleGyroLabel != null)
                toggleGyroLabel.text = gyroSteering.enableGyroSteering
                    ? "Gyro: ON" : "Gyro: OFF";

            if (toggleRealRiderLabel != null)
                toggleRealRiderLabel.text = gyroSteering.HorizonLockEnabled
                    ? "Real Rider: ON" : "Real Rider: OFF";
        }
    }
}
