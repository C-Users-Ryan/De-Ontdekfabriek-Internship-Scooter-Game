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

        [Header("Endless mode (Vrij rijden)")]
        [Tooltip("Lives in the solo Endless mode: a hard crash costs one life and at zero the run ends. The timed " +
                 "group relay ignores this. NOTE: this default only seeds a brand-new asset — set it explicitly on " +
                 "the live SessionConfig.asset (an existing asset keeps whatever value it was serialized with).")]
        public int endlessLives = 3;

        [Header("Checkpoint (Req §9.2)")]
        [Tooltip("Seconds before the timer runs out that the charge station is woven into the road — placed at the " +
                 "far draw horizon (in the haze) with no pop, so the player watches it emerge and grow as they ride " +
                 "up to it instead of it appearing at the last second. Keep well below the session length; too high " +
                 "and a very fast player could reach it before the time is actually up.")]
        public float checkpointLeadSeconds = 10f;
        [Tooltip("(No longer used for spawning — the station now weaves in at the draw horizon, so the approach " +
                 "distance follows RoadSequencer.spawnHorizon. Kept for compatibility.)")]
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
        [Tooltip("How far onto the shoulder the bike pulls to charge (metres from lane centre; the side follows the " +
                 "driving side). 0 (default) = the bike just stops in its lane at the stop point, no sideways pull; " +
                 ">0 = it eases that many metres onto the near shoulder into a bay.")]
        public float bayLateral = 0f;
        [Tooltip("How far the bike angles toward the bay while parked (degrees toward the near shoulder). 0 (default) " +
                 "= it stays straight. Pair with bayLateral > 0 if you want a visible pull-in to a roadside bay.")]
        public float bayYaw = 0f;
        [Tooltip("How fast the world ramps back up to cruising speed as the bike pulls out (m/s^2).")]
        public float pullOutAccel = 7f;

        [Header("Speeding (Req §7.3)")]
        [Tooltip("Seconds over the limit before tier 2/3 deductions start.")]
        public float speedingGraceSeconds = 3f;
    }
}
