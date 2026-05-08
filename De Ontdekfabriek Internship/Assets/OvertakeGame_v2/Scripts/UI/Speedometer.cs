using UnityEngine;
using TMPro;

namespace OvertakeGame
{
    public class Speedometer : MonoBehaviour
    {
        [Header("Needle Settings")]
        public RectTransform needleTransform;
        public float needleZeroAngle = -135f;
        public float needleMaxAngle  =  135f;

        [Header("Speed Range")]
        public float maxSpeedKmh = 200f;

        [Header("Text Readout")]
        public TMP_Text speedText;
        public string   textFormat = "{0:0} km/h";

        public void UpdateSpeed(float speedKmh)
        {
            float t     = Mathf.Clamp01(speedKmh / maxSpeedKmh);
            float angle = Mathf.Lerp(needleZeroAngle, needleMaxAngle, t);

            if (needleTransform != null)
                needleTransform.localRotation = Quaternion.Euler(0f, 0f, angle);

            if (speedText != null)
                speedText.text = string.Format(textFormat, Mathf.RoundToInt(speedKmh));
        }
    }
}
