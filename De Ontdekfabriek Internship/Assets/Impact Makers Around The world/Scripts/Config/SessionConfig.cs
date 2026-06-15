using UnityEngine;

namespace KenyaScooter.Config
{
    /// <summary>
    /// Session flow timing (M26, Req §1, §9). The timer ending the session is a
    /// narrative device: the journey is over, the student did not fail (MDA A4).
    /// </summary>
    [CreateAssetMenu(menuName = "Kenya Scooter/Session Config", fileName = "SessionConfig")]
    public sealed class SessionConfig : ScriptableObject
    {
        [Header("Timer (M26)")]
        public float sessionSeconds = 120f;

        [Header("Checkpoint (Req §9.2)")]
        [Tooltip("How far ahead the charge station spawns when the timer expires.")]
        public float checkpointDistance = 110f;
        [Tooltip("Braking deceleration toward the checkpoint; the brake-start distance is computed from v²/2a.")]
        public float checkpointBrakeRate = 9f;
        [Tooltip("The checkpoint counts as reached below this speed (m/s).")]
        public float checkpointStopSpeed = 0.2f;

        [Header("Between turns (relay, M27)")]
        [Tooltip("Checkpoint screen advances to the next student automatically after this many seconds.")]
        public float checkpointAutoAdvanceSeconds = 25f;
        [Tooltip("Game-over / finish screens advance automatically after this many seconds.")]
        public float endScreenAutoAdvanceSeconds = 20f;

        [Header("Speeding (Req §7.3)")]
        [Tooltip("Seconds over the limit before tier 2/3 deductions start.")]
        public float speedingGraceSeconds = 3f;
    }
}
