using UnityEngine;

namespace KenyaScooter.Config
{
    /// <summary>
    /// Timing for the session flow (M26, Req §1, §9). The timer running out is a story beat, not a
    /// loss — the journey is simply over and the student made it (MDA A4).
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

        [Header("Charge-station relay choreography")]
        [Tooltip("ON: reaching the charge station plays the diegetic pull-in, a short charge, then a pull-out when the next player starts. OFF: the proven instant relay (stop, then the score screen). Toggleable so the liked build is never at risk.")]
        public bool cinematicRelay = true;
        [Tooltip("Seconds for the bike to ease off the road into the charging bay.")]
        public float pullInSeconds = 1.0f;
        [Tooltip("Seconds the bike visibly charges (battery refills) before the hand-off screen appears. Kept short and positive on purpose (green top-up, not 'the battery died again').")]
        public float chargeSeconds = 2.0f;
        [Tooltip("Seconds for the bike to ease back onto the road when the next player starts.")]
        public float pullOutSeconds = 1.1f;
        [Tooltip("How far onto the shoulder the bike pulls to charge (metres from lane centre; the side follows the driving side, so it pulls to the near shoulder).")]
        public float bayLateral = 3.0f;
        [Tooltip("How far the bike angles toward the bay while parked (degrees toward the near shoulder).")]
        public float bayYaw = 32f;
        [Tooltip("How fast the world ramps back up to cruising speed as the bike pulls out (m/s^2).")]
        public float pullOutAccel = 7f;

        [Header("Speeding (Req §7.3)")]
        [Tooltip("Seconds over the limit before tier 2/3 deductions start.")]
        public float speedingGraceSeconds = 3f;
    }
}
