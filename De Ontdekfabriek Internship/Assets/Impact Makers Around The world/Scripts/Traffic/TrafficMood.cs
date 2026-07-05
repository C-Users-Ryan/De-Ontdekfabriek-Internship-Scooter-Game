using UnityEngine;
using KenyaScooter.Core;

namespace KenyaScooter.Traffic
{
    /// <summary>
    /// The road's memory of HOW the player drives — the social feedback loop that makes traffic feel alive
    /// instead of scripted. One 0..1 "respect" scalar falls when the player drives badly (collisions, wrong
    /// lane, speeding, scaring oncoming drivers) and recovers with clean driving and time. Traffic reads it
    /// through four scale factors every tick:
    ///   low respect  → drivers are annoyed and wary of you: they yield LESS, oncoming swerves defensively
    ///                  WIDE, horns fire far more readily, and followers crowd closer (tailgating pressure);
    ///   high respect → courteous roads: readier yielding, calm horns, normal spacing.
    /// So reckless play visibly turns the road hostile and careful play visibly earns cooperation — the
    /// lesson (your behaviour shapes how others treat you) expressed in behaviour, not text (MDA D4).
    ///
    /// Also owns two facilitator dials (SettingsCatalog): <see cref="Enabled"/> (the whole reaction system)
    /// and <see cref="Contrast"/> — how far driver archetypes deviate from "Normal" (1 = authored values,
    /// higher stretches aggressive/cautious/distracted further apart so personalities read unmistakably).
    /// Static + event-driven + lazily ticked (recovery is applied on read), so it costs nothing per frame.
    /// </summary>
    public static class TrafficMood
    {
        /// <summary>Facilitator gate: OFF = all scales return 1 and traffic ignores the player's conduct.</summary>
        public static bool Enabled = true;

        /// <summary>Archetype contrast dial (0.25–2): multiplies how far each personality's traits sit from neutral.</summary>
        public static float Contrast = 1f;

        private const float RecoverPerSecond = 0.02f;   // clean driving slowly wins the road back
        private static float respect = 1f;              // 1 = clean driver, 0 = the road is fed up
        private static float lastRecover;
        private static bool hooked;

        /// <summary>Current 0..1 respect after lazy recovery (public for HUD/analytics use).</summary>
        public static float Respect
        {
            get
            {
                if (GameManager.State == GameState.Playing && Time.time > lastRecover)
                {
                    respect = Mathf.Min(1f, respect + (Time.time - lastRecover) * RecoverPerSecond);
                    lastRecover = Time.time;
                }
                return respect;
            }
        }

        // The four behaviour scales traffic multiplies in per tick. All 1 when the system is off.
        public static float YieldScale  => Enabled ? Mathf.Lerp(0.55f, 1.10f, Respect) : 1f; // annoyed drivers make less room
        public static float SwerveScale => Enabled ? Mathf.Lerp(1.60f, 1.00f, Respect) : 1f; // wary oncoming gives you a wide berth
        public static float HornScale   => Enabled ? Mathf.Lerp(2.60f, 0.90f, Respect) : 1f; // the road tells you off
        public static float FollowScale => Enabled ? Mathf.Lerp(0.80f, 1.05f, Respect) : 1f; // impatient followers crowd you

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Hook()
        {
            if (hooked) return;
            hooked = true;
            GameEvents.SessionReset += () => { respect = 1f; lastRecover = Time.time; }; // every student starts fresh
            GameEvents.CollisionOccurred += (sev, kmh, pos, absorbed) =>
                Hit(sev == CollisionSeverity.Hard ? 0.25f : 0.12f);
            GameEvents.NearMiss += v => Hit(0.05f);          // you scared someone
            GameEvents.WrongLaneTick += () => Hit(0.06f);
            GameEvents.SpeedingTick += tier => Hit(0.03f * Mathf.Max(1, tier));
            GameEvents.OvertakeCompleted += v => { if (v == null || v.PassOnCorrectSide) Reward(0.06f); }; // clean pass earns respect
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { hooked = false; respect = 1f; lastRecover = 0f; Enabled = true; Contrast = 1f; }

        private static void Hit(float amount) { respect = Mathf.Max(0f, Respect - amount); }
        private static void Reward(float amount) { respect = Mathf.Min(1f, Respect + amount); }
    }
}
