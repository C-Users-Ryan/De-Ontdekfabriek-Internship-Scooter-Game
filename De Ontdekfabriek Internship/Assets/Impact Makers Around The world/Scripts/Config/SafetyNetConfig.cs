using UnityEngine;

namespace KenyaScooter.Config
{
    /// <summary>
    /// Tuning for the three safety systems: how collisions are graded, the grace charges, and the
    /// rewind (M15, M16, M17, Req §7.4/§8). Together they back the workshop promise — a session keeps
    /// going after any collision unless game-over is turned on explicitly.
    /// </summary>
    [CreateAssetMenu(menuName = "Kenya Scooter/Safety Net Config", fileName = "SafetyNetConfig")]
    public sealed class SafetyNetConfig : ScriptableObject
    {
        [Header("Collision classification (M15)")]
        [Tooltip("Below this relative speed a hit is Light — grace eligible, never fatal.")]
        public float lightHitMaxKmh = 35f;
        [Tooltip("At or above this relative speed a hit is Hard — always rewind / game over.")]
        public float hardHitMinKmh = 60f;
        [Tooltip("MDA M15 calls hits between the thresholds 'context-dependent'. Interpretation (D17): they consume grace when available, otherwise escalate to Hard.")]
        public bool mediumHitsUseGrace = true;
        [Tooltip("Seconds between collision triggers — prevents double-firing on one contact (Req §7.4).")]
        public float collisionCooldown = 0.6f;
        [Tooltip("Seconds of invulnerability after a rewind or fallback recovery.")]
        public float postRecoveryInvulnerability = 1.5f;

        [Header("Grace (M16)")]
        public int graceCharges = 1;
        [Tooltip("Seconds of clean driving (no collisions, hazards or violations) before a charge regenerates.")]
        public float graceRechargeSeconds = 8f;

        [Header("Rewind (M17)")]
        [Tooltip("Seconds of play recorded in the ring buffer.")]
        public float rewindWindowSeconds = 3.5f;
        public float rewindSnapshotInterval = 0.15f;
        [Tooltip("Duration of the reverse playback.")]
        public float rewindPlaybackSeconds = 1.3f;
        [Tooltip("Duration of the B&W ramp in and out.")]
        public float rewindFadeSeconds = 0.25f;
        [Tooltip("Per-turn cap (M17 default 2). After this, a hard crash falls back to recovery or game over.")]
        public int rewindsPerTurn = 2;
        [Tooltip("Snapshot capacity: player + traffic + hazards + tiles. Sized once at startup.")]
        public int maxRewindParticipants = 160;
    }
}
