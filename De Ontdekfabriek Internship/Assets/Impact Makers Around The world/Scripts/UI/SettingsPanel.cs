using TMPro;
using UnityEngine;
using KenyaScooter.Cameras;
using KenyaScooter.Controls;

namespace KenyaScooter.UI
{
    /// <summary>
    /// The gyro settings panel (Req §3.4): toggle gyro steering, toggle Real Rider
    /// Mode, re-calibrate the zero point mid-session, with a live tilt readout.
    /// Buttons are wired in the Inspector to the public methods. The readout
    /// refreshes at 10 Hz, not per frame — it is a debug-ish display, not gameplay.
    /// </summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text tiltReadout;
        [SerializeField] private TMP_Text gyroStateLabel;
        [SerializeField] private TMP_Text realRiderStateLabel;
        [SerializeField] private RealRiderMode realRider;

        private float nextReadoutUpdate;

        private void Update()
        {
            if (panel == null || !panel.activeSelf || Time.unscaledTime < nextReadoutUpdate)
                return;

            nextReadoutUpdate = Time.unscaledTime + 0.1f;
            ScooterInputRouter input = ScooterInputRouter.Instance;
            if (tiltReadout != null && input != null)
                tiltReadout.text = $"{input.TiltDegrees:0.0}°  ({(input.GyroActive ? "GYRO" : "TOETSEN")})";
        }

        public void TogglePanel()
        {
            if (panel != null)
            {
                panel.SetActive(!panel.activeSelf);
                RefreshLabels();
            }
        }

        public void ToggleGyro()
        {
            ScooterInputRouter input = ScooterInputRouter.Instance;
            if (input != null)
                input.GyroEnabled = !input.GyroEnabled;
            RefreshLabels();
        }

        public void ToggleRealRider()
        {
            if (realRider != null)
                realRider.RealRiderEnabled = !realRider.RealRiderEnabled;
            RefreshLabels();
        }

        /// <summary>Re-zero the gyro to the current grip (M8, Req §3.4).</summary>
        public void Calibrate()
        {
            if (ScooterInputRouter.Instance != null)
                ScooterInputRouter.Instance.Calibrate();
        }

        private void RefreshLabels()
        {
            if (gyroStateLabel != null && ScooterInputRouter.Instance != null)
                gyroStateLabel.text = ScooterInputRouter.Instance.GyroEnabled ? "GYRO: AAN" : "GYRO: UIT";
            if (realRiderStateLabel != null && realRider != null)
                realRiderStateLabel.text = realRider.RealRiderEnabled ? "REAL RIDER: AAN" : "REAL RIDER: UIT";
        }
    }
}
