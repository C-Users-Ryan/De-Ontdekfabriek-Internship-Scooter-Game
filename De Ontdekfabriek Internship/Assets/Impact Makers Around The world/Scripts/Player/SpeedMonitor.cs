using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;
using KenyaScooter.Session;

namespace KenyaScooter.Player
{
    /// <summary>
    /// Speeding tiers against the active zone's limit (Req §7.3): tier 1 (up to 25%
    /// over) warns only, tier 2 (25%+) deducts per second, tier 3 (75%+) deducts at
    /// double rate. A grace period runs before tier 2/3 penalties start, so briefly
    /// cresting the limit during an overtake is not punished. The limit comes from
    /// SpeedZoneManager (per RoadSequence); 0 means unrestricted.
    /// </summary>
    public sealed class SpeedMonitor : MonoBehaviour
    {
        [SerializeField] private SessionConfig config;

        public int CurrentTier { get; private set; }

        private float overLimitTime;
        private float tickAccumulator;

        private void OnEnable() => GameEvents.SessionReset += HandleSessionReset;
        private void OnDisable() => GameEvents.SessionReset -= HandleSessionReset;

        private void Update()
        {
            if (GameManager.State != GameState.Playing)
                return;

            float limit = SpeedZoneManager.Instance != null ? SpeedZoneManager.Instance.CurrentLimitKmh : 0f;
            int tier = 0;
            if (limit > 0f)
            {
                float ratio = WorldSpeed.Instance.CurrentKmh / limit;
                tier = ratio >= 1.75f ? 3 : ratio >= 1.25f ? 2 : ratio > 1f ? 1 : 0;
            }

            if (tier != CurrentTier)
            {
                CurrentTier = tier;
                GameEvents.RaiseSpeedingTierChanged(tier);
                if (tier < 2)
                {
                    overLimitTime = 0f;
                    tickAccumulator = 0f;
                }
            }

            if (tier < 2)
                return;

            overLimitTime += Time.deltaTime;
            if (overLimitTime < config.speedingGraceSeconds)
                return;

            tickAccumulator += Time.deltaTime;
            if (tickAccumulator >= 1f)
            {
                tickAccumulator -= 1f;
                GameEvents.RaiseSpeedingTick(CurrentTier);
            }
        }

        private void HandleSessionReset()
        {
            if (CurrentTier != 0)
                GameEvents.RaiseSpeedingTierChanged(0);
            CurrentTier = 0;
            overLimitTime = 0f;
            tickAccumulator = 0f;
        }
    }
}
