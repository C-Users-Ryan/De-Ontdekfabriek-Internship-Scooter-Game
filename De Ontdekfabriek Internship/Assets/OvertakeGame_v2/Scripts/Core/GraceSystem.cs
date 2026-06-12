using UnityEngine;
using System;
using System.Collections;

namespace OvertakeGame
{
    /// <summary>
    /// Collision grace shield — absorbs light hits so the player is not immediately penalised
    /// for every small mistake (especially important for new players in a relay context).
    ///
    /// LIGHT HIT  (relative speed below lightHitMaxKmh AND charges > 0)
    ///   → Charge consumed. Points deducted proportional to impact speed.
    ///     Warning + camera shake. No game over.
    ///
    /// HARD HIT   (relative speed at or above hardHitMinKmh, OR no charges left)
    ///   → TryAbsorb returns false → GameManager triggers game over or rewind.
    ///
    /// Charges regenerate automatically after regenDelaySec of clean driving.
    /// ResetGrace() must be called at session start to restore full charges.
    /// </summary>
    public class GraceSystem : MonoBehaviour
    {
        public static GraceSystem Instance { get; private set; }

        [Header("Charges")]
        [Tooltip("How many light hits can be absorbed before the next hit is fatal.")]
        public int   maxGraceLives    = 1;
        [Tooltip("Seconds of clean driving required to regenerate one charge.")]
        public float regenDelaySec    = 8f;

        [Header("Impact Classification (km/h)")]
        [Tooltip("Hits with relative speed BELOW this value can be absorbed by grace.")]
        public float lightHitMaxKmh   = 35f;
        [Tooltip("Hits at or above this speed are always fatal regardless of grace charges.")]
        public float hardHitMinKmh    = 60f;

        [Header("Point Cost")]
        [Tooltip("Maximum points deducted when a grace charge absorbs a heavy-light hit. " +
                 "Scales with impact speed — a gentle graze costs ~15% of this value.")]
        public int   lightHitMaxDeduction = 50;

        [Header("References")]
        public ScoreManager scoreManager;

        // ── State ──────────────────────────────────────────────────────────────
        public int  GraceLives { get; private set; }
        public bool HasGrace   => GraceLives > 0;

        // ── Events (wire to a shield icon animator on the HUD) ─────────────────
        public event Action<int> OnGraceLivesChanged;
        public event Action      OnGraceAbsorbed;
        public event Action      OnGraceRestored;

        private Coroutine _regenCoroutine;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance   = this;
            GraceLives = maxGraceLives;
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Call when a traffic collision occurs.
        /// Returns true  → hit absorbed, do NOT trigger game over.
        /// Returns false → hit is fatal, GameManager should proceed to game over / rewind.
        /// </summary>
        public bool TryAbsorb(float relativeSpeedKmh)
        {
            if (relativeSpeedKmh >= hardHitMinKmh) return false;
            if (GraceLives <= 0)                   return false;

            GraceLives--;
            OnGraceLivesChanged?.Invoke(GraceLives);
            OnGraceAbsorbed?.Invoke();

            float severity  = Mathf.InverseLerp(0f, lightHitMaxKmh, relativeSpeedKmh);
            int   deduction = Mathf.RoundToInt(lightHitMaxDeduction * Mathf.Lerp(0.15f, 1f, severity));
            if (deduction > 0)
                scoreManager?.AddPoints(-deduction, "KLAP!");

            if (_regenCoroutine != null) StopCoroutine(_regenCoroutine);
            if (GraceLives < maxGraceLives)
                _regenCoroutine = StartCoroutine(RegenAfterDelay());

            return true;
        }

        /// <summary>Call at session start to restore full grace charges.</summary>
        public void ResetGrace()
        {
            if (_regenCoroutine != null) { StopCoroutine(_regenCoroutine); _regenCoroutine = null; }
            GraceLives = maxGraceLives;
            OnGraceLivesChanged?.Invoke(GraceLives);
        }

        // ── Internal ───────────────────────────────────────────────────────────

        private IEnumerator RegenAfterDelay()
        {
            yield return new WaitForSeconds(regenDelaySec);
            if (GraceLives < maxGraceLives)
            {
                GraceLives++;
                OnGraceLivesChanged?.Invoke(GraceLives);
                OnGraceRestored?.Invoke();
                scoreManager?.SignalEvent(0, "+SCHILD");
            }
            _regenCoroutine = null;
        }
    }
}
