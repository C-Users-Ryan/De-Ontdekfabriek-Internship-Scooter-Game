using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

namespace OvertakeGame
{
    /// <summary>
    /// Displays contextual warnings on screen. Each type has a display duration and a cooldown
    /// so repeated triggers don't spam the panel every frame.
    /// </summary>
    public class WarningSystem : MonoBehaviour
    {
        public enum WarningType { Collision, WrongLane, Speeding, Pothole, Rock }

        [Header("Display Duration")]
        public float displayDuration    = 2f;
        public float cooldownPerType    = 1.5f;

        [Header("Warning Labels (Swahili)")]
        public string collisionLabel = "ANGALIA! MGONGANO";
        public string wrongLaneLabel = "NJIA MBAYA!";
        public string speedingLabel  = "POLEPOLE!";
        public string potholeLabel   = "SHIMO!";
        public string rockLabel      = "JIWE!";

        [Header("UI References")]
        public GameObject warningPanel;
        public TMP_Text   warningText;

        private readonly Dictionary<WarningType, float> _lastShownTime = new();
        private Coroutine _hideCoroutine;

        void Awake() => warningPanel?.SetActive(false);

        public void ShowWarning(WarningType type)
        {
            if (_lastShownTime.TryGetValue(type, out float last) &&
                Time.time - last < cooldownPerType) return;

            _lastShownTime[type] = Time.time;

            if (warningText != null)
                warningText.text = type switch
                {
                    WarningType.Collision => collisionLabel,
                    WarningType.WrongLane => wrongLaneLabel,
                    WarningType.Speeding  => speedingLabel,
                    WarningType.Pothole   => potholeLabel,
                    WarningType.Rock      => rockLabel,
                    _                     => "ANGALIA!"
                };

            warningPanel?.SetActive(true);
            if (_hideCoroutine != null) StopCoroutine(_hideCoroutine);
            _hideCoroutine = StartCoroutine(HideAfterDelay(displayDuration));
        }

        public void HideWarning() => warningPanel?.SetActive(false);

        private IEnumerator HideAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            HideWarning();
        }
    }
}
