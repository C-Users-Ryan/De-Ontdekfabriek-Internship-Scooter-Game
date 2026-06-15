using TMPro;
using UnityEngine;
using KenyaScooter.Core;

namespace KenyaScooter.UI
{
    /// <summary>
    /// Numeric + needle speedometer (Req §12.1). The needle sweeps between the two
    /// configured angles over 0..maxKmh; the readout tints amber/red with the
    /// speeding tier (Req §7.3) so the limit feedback lives where the player's eye
    /// already is. Text updates only when the rounded km/h changes.
    /// </summary>
    public sealed class Speedometer : MonoBehaviour
    {
        [SerializeField] private TMP_Text kmhText;
        [SerializeField] private RectTransform needle;
        [SerializeField] private float maxKmh = 110f;
        [SerializeField] private float needleMinAngle = 120f;
        [SerializeField] private float needleMaxAngle = -120f;
        [SerializeField] private Color normalColour = Color.white;
        [SerializeField] private Color warnColour = new Color(1f, 0.8f, 0.3f);
        [SerializeField] private Color dangerColour = new Color(1f, 0.35f, 0.3f);

        private int lastKmh = -1;

        private void OnEnable() => GameEvents.SpeedingTierChanged += HandleTierChanged;
        private void OnDisable() => GameEvents.SpeedingTierChanged -= HandleTierChanged;

        private void Update()
        {
            float kmh = WorldSpeed.Instance.CurrentKmh;

            if (needle != null)
            {
                float angle = Mathf.Lerp(needleMinAngle, needleMaxAngle, Mathf.Clamp01(kmh / maxKmh));
                needle.localRotation = Quaternion.Euler(0f, 0f, angle);
            }

            int rounded = Mathf.RoundToInt(kmh);
            if (rounded != lastKmh && kmhText != null)
            {
                lastKmh = rounded;
                kmhText.text = rounded.ToString();
            }
        }

        private void HandleTierChanged(int tier)
        {
            if (kmhText != null)
                kmhText.color = tier >= 3 ? dangerColour : tier >= 1 ? warnColour : normalColour;
        }
    }
}
