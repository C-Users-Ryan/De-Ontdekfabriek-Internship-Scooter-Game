using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

namespace OvertakeGame
{
    public class WarningSystem : MonoBehaviour
    {
        public enum WarningType { Collision, WrongLane, Speeding, Pothole }

        [Header("Warning Messages")]
        public string collisionWarningText = "⚠ COLLISION!";
        public string wrongLaneWarningText  = "⚠ WRONG LANE!";
        public string speedingWarningText   = "⚠ SPEEDING!";
        public string potholeWarningText    = "⚠ POTHOLE!";

        [Header("Display Settings")]
        public float warningDisplayDuration  = 2f;
        public float warningCooldownPerType  = 1.5f;

        [Header("UI References")]
        public GameObject warningPanel;
        public TMP_Text   warningText;

        private readonly Dictionary<WarningType, float> _lastShownTime = new();
        private Coroutine _hideCoroutine;

        void Awake() => warningPanel?.SetActive(false);

        public void ShowWarning(WarningType type)
        {
            if (_lastShownTime.TryGetValue(type, out float last) &&
                Time.time - last < warningCooldownPerType) return;

            _lastShownTime[type] = Time.time;

            if (warningText != null)
                warningText.text = type switch
                {
                    WarningType.Collision => collisionWarningText,
                    WarningType.WrongLane => wrongLaneWarningText,
                    WarningType.Speeding  => speedingWarningText,
                    WarningType.Pothole   => potholeWarningText,
                    _                     => "⚠ WARNING"
                };

            warningPanel?.SetActive(true);
            if (_hideCoroutine != null) StopCoroutine(_hideCoroutine);
            _hideCoroutine = StartCoroutine(HideAfterDelay(warningDisplayDuration));
        }

        public void HideWarning() => warningPanel?.SetActive(false);

        private IEnumerator HideAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            HideWarning();
        }
    }
}
