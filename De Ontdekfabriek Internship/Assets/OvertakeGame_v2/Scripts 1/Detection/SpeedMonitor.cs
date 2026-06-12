using UnityEngine;

namespace OvertakeGame
{
    /// <summary>
    /// Monitors player speed against the current posted limit with a three-tier response.
    /// Reads the live limit from SpeedZoneManager — threshold adjusts automatically when
    /// entering town zones, construction stretches, etc.
    ///
    /// TIER 0 — within limit (≤ 100% of limit)      : no action
    /// TIER 1 — light overspeed (100–125%)           : warning after grace period, no deduction
    /// TIER 2 — hard overspeed (125–175%)            : warning + deduction after grace period
    /// TIER 3 — extreme overspeed (> 175%)           : warning + deduction × hardSpeedingRateMultiplier
    ///
    /// The grace period allows brief speed bursts for overtaking without triggering penalties.
    /// </summary>
    public class SpeedMonitor : MonoBehaviour
    {
        [Header("Speed Tiers (fraction of current speed limit)")]
        [Range(1f, 1.5f)]
        [Tooltip("Below this = within limit. No action.")]
        public float lightSpeedingFrac   = 1.00f;

        [Range(1.1f, 1.5f)]
        [Tooltip("Above this = hard speeding. Deduction begins after grace period.")]
        public float hardSpeedingFrac    = 1.25f;

        [Range(1.3f, 2.0f)]
        [Tooltip("Above this = extreme speeding. Deduction rate is multiplied.")]
        public float extremeSpeedingFrac = 1.75f;

        [Tooltip("Multiplier applied to the deduction rate in the extreme tier.")]
        public float hardSpeedingRateMultiplier = 2f;

        [Header("Grace Period")]
        [Tooltip("Seconds above the threshold before consequences begin. " +
                 "Allows brief overtake speed bursts.")]
        public float speedingGracePeriod = 2.5f;

        // ── State (read by other scripts) ─────────────────────────────────────────

        public bool  IsSpeeding      { get; private set; }
        public float CurrentTierFrac { get; private set; }

        private float _speedingTimer;
        private int   _currentTier;
        private float _warnCooldown;

        // ── Update ─────────────────────────────────────────────────────────────────

        void Update()
        {
            if (GameManager.Instance?.CurrentState != GameManager.GameState.Playing) return;
            if (WorldSpeed.Instance == null) return;

            float kmh   = WorldSpeed.Instance.CurrentKmh;
            float limit = SpeedZoneManager.Instance != null
                        ? SpeedZoneManager.Instance.CurrentLimit
                        : 80f;

            float frac = kmh / limit;

            int tier;
            if      (frac > extremeSpeedingFrac) tier = 3;
            else if (frac > hardSpeedingFrac)    tier = 2;
            else if (frac > lightSpeedingFrac)   tier = 1;
            else                                 tier = 0;

            _currentTier    = tier;
            CurrentTierFrac = frac;

            if (tier == 0)
            {
                _speedingTimer = 0f;
                _warnCooldown  = 0f;
                IsSpeeding     = false;
                return;
            }

            _speedingTimer += Time.deltaTime;
            _warnCooldown  -= Time.deltaTime;

            if (_speedingTimer < speedingGracePeriod) return;

            IsSpeeding = true;

            if (tier == 1)
            {
                if (_warnCooldown <= 0f)
                {
                    _warnCooldown = 1.0f;
                    GameManager.Instance?.OnPlayerLightSpeeding();
                }
                return;
            }

            float rateMulti = tier == 3 ? hardSpeedingRateMultiplier : 1f;
            if (_warnCooldown <= 0f) _warnCooldown = 1.0f;
            GameManager.Instance?.OnPlayerSpeeding(rateMulti);
        }
    }
}
