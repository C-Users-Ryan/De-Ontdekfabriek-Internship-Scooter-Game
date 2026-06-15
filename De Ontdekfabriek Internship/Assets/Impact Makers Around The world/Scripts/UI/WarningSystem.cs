using TMPro;
using UnityEngine;
using KenyaScooter.Core;

namespace KenyaScooter.UI
{
    /// <summary>
    /// The warning panel (Req §12.1): one-shot warnings for collisions and hazard
    /// hits, persistent warnings while the wrong-lane or speeding condition holds.
    /// One-shots take priority over persistent state for their short lifetime, then
    /// the panel falls back. Text is rebuilt only when the shown key changes.
    /// </summary>
    public sealed class WarningSystem : MonoBehaviour
    {
        [SerializeField] private CanvasGroup panel;
        [SerializeField] private TMP_Text label;
        [SerializeField] private float oneShotSeconds = 1.6f;
        [SerializeField] private float fadeSpeed = 6f;

        private string oneShotKey;
        private float oneShotUntil;
        private bool wrongLaneActive;
        private bool speedingActive;
        private string shownKey;

        private void OnEnable()
        {
            GameEvents.CollisionOccurred += HandleCollision;
            GameEvents.HazardHit += HandleHazard;
            GameEvents.WrongLaneChanged += HandleWrongLane;
            GameEvents.SpeedingTierChanged += HandleSpeedingTier;
            GameEvents.SessionReset += HandleSessionReset;
        }

        private void OnDisable()
        {
            GameEvents.CollisionOccurred -= HandleCollision;
            GameEvents.HazardHit -= HandleHazard;
            GameEvents.WrongLaneChanged -= HandleWrongLane;
            GameEvents.SpeedingTierChanged -= HandleSpeedingTier;
            GameEvents.SessionReset -= HandleSessionReset;
        }

        private void Update()
        {
            string targetKey = Time.time < oneShotUntil ? oneShotKey
                : wrongLaneActive ? "WARN_WRONGLANE"
                : speedingActive ? "WARN_SPEEDING"
                : null;

            if (targetKey != shownKey)
            {
                shownKey = targetKey;
                if (targetKey != null && label != null)
                    label.text = SwahiliUI.Get(targetKey);
            }

            if (panel != null)
                panel.alpha = Mathf.MoveTowards(panel.alpha, targetKey != null ? 1f : 0f, fadeSpeed * Time.deltaTime);
        }

        private void ShowOneShot(string key)
        {
            oneShotKey = key;
            oneShotUntil = Time.time + oneShotSeconds;
        }

        private void HandleCollision(CollisionSeverity severity, float relativeKmh, Vector3 position, bool absorbed)
        {
            if (!absorbed)
                ShowOneShot("WARN_COLLISION");
        }

        private void HandleHazard(HazardKind kind, float playerKmh, Vector3 position)
        {
            switch (kind)
            {
                case HazardKind.Pothole: ShowOneShot("WARN_POTHOLE"); break;
                case HazardKind.Rock: ShowOneShot("WARN_ROCK"); break;
                default: ShowOneShot("WARN_BUMP"); break;
            }
        }

        private void HandleWrongLane(bool inWrongLane) => wrongLaneActive = inWrongLane;
        private void HandleSpeedingTier(int tier) => speedingActive = tier >= 1;

        private void HandleSessionReset()
        {
            oneShotKey = null;
            oneShotUntil = 0f;
            wrongLaneActive = false;
            speedingActive = false;
            shownKey = null;
            if (panel != null)
                panel.alpha = 0f;
        }
    }
}
