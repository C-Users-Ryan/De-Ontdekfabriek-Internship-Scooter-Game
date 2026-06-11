using UnityEngine;
using System;
using System.Collections;

namespace OvertakeGame
{
    /// <summary>
    /// Subway-Surfers-style collision grace system. Absorbs light hits so
    /// the player does not immediately get a game over on every small mistake.
    ///
    /// HOW IT WORKS:
    ///   Every traffic collision now carries a relativeSpeedKmh value calculated
    ///   by OvertakeCollisionHandler (player speed ± vehicle speed, depending on direction).
    ///
    ///   LIGHT HIT  (relative speed < lightHitMaxKmh AND graceLives > 0)
    ///     → Grace life consumed. Points deducted proportional to impact severity.
    ///       Visual flash + warning. No game over. Grace regenerates after regenDelaySec.
    ///
    ///   HARD HIT   (relative speed ≥ hardHitMinKmh, OR no grace lives left)
    ///     → TryAbsorb returns false → GameManager triggers game over as before.
    ///
    ///   IN BETWEEN (lightHitMaxKmh ≤ speed < hardHitMinKmh AND no grace)
    ///     → Fatal. No grace was available to soften it.
    ///
    /// SETUP:
    ///   Add to the same GameObject as GameManager.
    ///   Wire graceSystem reference in GameManager Inspector.
    ///   Wire scoreManager reference in Inspector.
    ///   Optionally wire OnGraceAbsorbed / OnGraceRestored to a UI shield icon animator.
    ///
    /// TUNING:
    ///   maxGraceLives = 1  → one free hit, then game over on next collision (default)
    ///   regenDelaySec = 8  → after 8 seconds clean driving the shield comes back
    ///   lightHitMaxKmh = 35 → same-direction graze ≈ 30–40 km/h relative speed → grace
    ///   hardHitMinKmh  = 60 → head-on at any reasonable speed = always fatal
    /// </summary>
    public class GraceSystem : MonoBehaviour
    {
        public static GraceSystem Instance { get; private set; }

        [Header("Grace Lives")]
        [Tooltip("Number of light hits that can be absorbed before a hit becomes fatal.")]
        public int maxGraceLives = 1;

        [Tooltip("Seconds of clean driving (no collisions) before one grace life regenerates.")]
        public float regenDelaySec = 8f;

        [Header("Impact Classification (km/h)")]
        [Tooltip("Hits with relative speed BELOW this are light — grace can absorb them.")]
        public float lightHitMaxKmh = 35f;

        [Tooltip("Hits with relative speed AT OR ABOVE this are always fatal, regardless of grace lives.")]
        public float hardHitMinKmh = 60f;

        [Header("Point Cost of a Grace Hit")]
        [Tooltip("Maximum points deducted when a grace life is spent on a heavy-light hit (at lightHitMaxKmh). "
               + "Scales linearly: a 10 km/h graze costs ~28% of this value.")]
        public int lightHitMaxDeduction = 50;

        [Header("References")]
        public ScoreManager scoreManager;

        // ── State ──────────────────────────────────────────────────────────────
        public int  GraceLives { get; private set; }
        public bool HasGrace   => GraceLives > 0;

        // ── Events (wire to UI for shield flash / restore icon) ────────────────
        /// <summary>Fired when a grace life is spent. int = lives remaining.</summary>
        public event Action<int> OnGraceLivesChanged;
        /// <summary>Fired when a hit is absorbed — trigger shield flash VFX/SFX.</summary>
        public event Action      OnGraceAbsorbed;
        /// <summary>Fired when a grace life regenerates — show "+SCHILD" popup.</summary>
        public event Action      OnGraceRestored;

        private Coroutine _regenCoroutine;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance   = this;
            GraceLives = maxGraceLives;
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Called by GameManager when a traffic collision is registered.
        /// Returns TRUE  → hit absorbed, do NOT trigger game over.
        /// Returns FALSE → hit is fatal, GameManager should call TriggerGameOver().
        /// </summary>
        public bool TryAbsorb(float relativeSpeedKmh)
        {
            // Hard hit — always fatal, no grace can save this
            if (relativeSpeedKmh >= hardHitMinKmh) return false;

            // No grace lives left — fatal
            if (GraceLives <= 0) return false;

            // ── Absorb ──
            GraceLives--;
            OnGraceLivesChanged?.Invoke(GraceLives);
            OnGraceAbsorbed?.Invoke();

            // Deduct points proportional to severity
            float severity  = Mathf.InverseLerp(0f, lightHitMaxKmh, relativeSpeedKmh);
            int   deduction = Mathf.RoundToInt(lightHitMaxDeduction * Mathf.Lerp(0.15f, 1f, severity));
            if (deduction > 0)
                scoreManager?.AddPoints(-deduction, "KLAP!");

            // Start regen timer — resets on every absorbed hit
            if (_regenCoroutine != null) StopCoroutine(_regenCoroutine);
            if (GraceLives < maxGraceLives)
                _regenCoroutine = StartCoroutine(RegenAfterDelay());

            return true;
        }

        /// <summary>Call at session start / restart.</summary>
        public void ResetGrace()
        {
            if (_regenCoroutine != null) { StopCoroutine(_regenCoroutine); _regenCoroutine = null; }
            GraceLives = maxGraceLives;
            OnGraceLivesChanged?.Invoke(GraceLives);
        }

        // ── Private ────────────────────────────────────────────────────────────

        private IEnumerator RegenAfterDelay()
        {
            yield return new WaitForSeconds(regenDelaySec);
            if (GraceLives < maxGraceLives)
            {
                GraceLives++;
                OnGraceLivesChanged?.Invoke(GraceLives);
                OnGraceRestored?.Invoke();
                // Show popup without a point change — label-only event
                scoreManager?.SignalEvent(0, "+SCHILD");
            }
            _regenCoroutine = null;
        }
    }
}
