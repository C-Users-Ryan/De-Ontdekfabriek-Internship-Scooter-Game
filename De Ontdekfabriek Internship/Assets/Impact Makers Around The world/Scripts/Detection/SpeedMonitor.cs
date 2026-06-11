using UnityEngine;

namespace OvertakeGame
{
    /// <summary>
    /// Monitors player speed against the current posted limit and applies tiered
    /// consequences. Reads the live limit from SpeedZoneManager so the threshold
    /// automatically adjusts when entering town zones, construction, etc.
    ///
    /// CHANGES FROM PREVIOUS VERSION:
    ///   - Removed own speedLimitKmh field → reads SpeedZoneManager.CurrentLimit.
    ///   - THREE-TIER response replaces the binary speeding flag:
    ///
    ///     TIER 0 — Within limit (≤ 100% of limit)
    ///       No action. SpeedZoneManager.OnLimitChanged already updated the HUD.
    ///
    ///     TIER 1 — Light overspeed (100%–125% of limit)
    ///       Grace period applies. After speedingGracePeriod seconds: WarningSystem
    ///       shows the warning, but NO point deduction. Yellow-light behaviour.
    ///       This feels fair — a brief burst above 80 in an 80 zone doesn't punish.
    ///
    ///     TIER 2 — Hard overspeed (> 125% of limit, e.g. 100+ in an 80 zone)
    ///       After grace period: WarningSystem warning AND point deduction at the
    ///       full speedingDeductionPerSecond rate. SpeedMonitor sets IsSpeeding = true
    ///       which GameManager.OnPlayerSpeeding() reads.
    ///
    ///     TIER 3 — Extreme overspeed (> hardSpeedingFrac of limit, default 175%)
    ///       Same as tier 2 but deduction rate scales up (hardSpeedingRateMultiplier).
    ///       E.g. 140 km/h in an 80 zone → 2x the deduction rate.
    ///       Intended: driving flat-out in a town zone should feel meaningfully punishing.
    ///
    /// WHY THREE TIERS:
    ///   Real road safety distinguishes between "going a little fast" and "reckless
    ///   driving". A career exploration game should model the same distinction.
    ///   Research (WHO 2023 road safety) links speed tier to crash severity — this
    ///   maps that into the consequence model.
    ///
    /// SETUP:
    ///   Same GameObject as SpeedZoneManager or assign reference in Inspector.
    ///   Wire gameManager reference (reads deductOnSpeeding, warnOnSpeeding).
    /// </summary>
    public class SpeedMonitor : MonoBehaviour
    {
        [Header("Speed Tiers (fraction of current speed limit)")]
        [Tooltip("Below this fraction of the limit = in range. No action.")]
        [Range(1f, 1.5f)]
        public float lightSpeedingFrac = 1.00f;   // > limit → tier 1 (grace only)

        [Tooltip("Above this fraction = hard speeding. Deduction begins after grace period.")]
        [Range(1.1f, 1.5f)]
        public float hardSpeedingFrac  = 1.25f;   // > 125% of limit → tier 2

        [Tooltip("Above this fraction = extreme speeding. Deduction rate is multiplied.")]
        [Range(1.3f, 2.0f)]
        public float extremeSpeedingFrac = 1.75f; // > 175% of limit → tier 3

        [Tooltip("Multiplier applied to the deduction rate in tier 3 (extreme speeding).")]
        public float hardSpeedingRateMultiplier = 2f;

        [Header("Grace Period")]
        [Tooltip("Seconds above the threshold before consequences begin. Allows brief overtake speed bursts.")]
        public float speedingGracePeriod = 2.5f;

        // ── State (readable by other scripts) ──────────────────────────────────
        public bool  IsSpeeding     { get; private set; }
        public float CurrentTierFrac { get; private set; }  // 0=ok, 1=light, 2=hard, 3=extreme

        private float _speedingTimer;
        private int   _currentTier;    // 0, 1, 2, 3
        private float _warnCooldown;   // prevents ShowWarning being called every frame

        // ── Update ─────────────────────────────────────────────────────────────

        void Update()
        {
            if (GameManager.Instance?.CurrentState != GameManager.GameState.Playing) return;
            if (WorldSpeed.Instance == null) return;

            float kmh   = WorldSpeed.Instance.CurrentKmh;
            float limit = SpeedZoneManager.Instance != null
                        ? SpeedZoneManager.Instance.CurrentLimit
                        : 80f; // fallback if SpeedZoneManager missing

            float frac = kmh / limit; // how many × the speed limit we're doing

            // Classify tier
            int tier;
            if      (frac > extremeSpeedingFrac) tier = 3;
            else if (frac > hardSpeedingFrac)    tier = 2;
            else if (frac > lightSpeedingFrac)   tier = 1;
            else                                 tier = 0;

            _currentTier    = tier;
            CurrentTierFrac = frac;

            if (tier == 0)
            {
                // Under the limit — reset everything
                _speedingTimer = 0f;
                _warnCooldown  = 0f;
                IsSpeeding     = false;
                return;
            }

            // ── Over the limit ──
            _speedingTimer += Time.deltaTime;
            _warnCooldown  -= Time.deltaTime;

            if (_speedingTimer < speedingGracePeriod) return; // still in grace window

            // Grace expired
            IsSpeeding = true;

            // Tier 1: warn only (throttled so ShowWarning isn't called every frame)
            if (tier == 1)
            {
                if (_warnCooldown <= 0f)
                {
                    _warnCooldown = 1.0f;
                    GameManager.Instance?.OnPlayerLightSpeeding();
                }
                return;
            }

            // Tier 2 / 3: warn (throttled) + deduct (per-frame accumulation is fine)
            float rateMulti = tier == 3 ? hardSpeedingRateMultiplier : 1f;
            if (_warnCooldown <= 0f)
            {
                _warnCooldown = 1.0f;
            }
            GameManager.Instance?.OnPlayerSpeeding(rateMulti);
        }
    }
}
