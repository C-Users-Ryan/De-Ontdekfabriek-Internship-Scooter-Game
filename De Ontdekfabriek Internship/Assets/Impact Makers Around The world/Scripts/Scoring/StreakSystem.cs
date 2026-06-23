using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;

namespace KenyaScooter.Scoring
{
    /// <summary>
    /// The clean-overtake streak (M20) — the SC3 mechanic. Clean overtakes climb the
    /// multiplier tiers; ANY violation resets to 1×. The game never says "drive
    /// responsibly": careful driving simply compounds value and reckless driving
    /// destroys it (MDA Chain 2). Only ScoreManager calls Register/Reset, so the
    /// read-multiplier-then-increment order is deterministic — the new tier applies
    /// to the NEXT overtake, exactly as M20 specifies.
    /// </summary>
    public sealed class StreakSystem : MonoBehaviour
    {
        [SerializeField] private ScoreConfig config;

        public int Streak { get; private set; }
        public int BestStreak { get; private set; }
        public float Multiplier => MultiplierFor(Streak);

        private void OnEnable()
        {
            GameEvents.SessionReset += HandleSessionReset;
            GameEvents.GroupReset += HandleGroupReset;
        }

        private void OnDisable()
        {
            GameEvents.SessionReset -= HandleSessionReset;
            GameEvents.GroupReset -= HandleGroupReset;
        }

        public void RegisterCleanOvertake()
        {
            Streak++;
            if (Streak > BestStreak)
                BestStreak = Streak;
            GameEvents.RaiseStreakChanged(Streak, Multiplier);
        }

        public void ResetStreak()
        {
            if (Streak == 0)
                return;
            Streak = 0;
            GameEvents.RaiseStreakChanged(0, MultiplierFor(0));
        }

        private float MultiplierFor(int streak)
        {
            ScoreConfig.StreakTier[] tiers = config != null ? config.streakTiers : null;
            if (tiers == null || tiers.Length == 0)
            {
                // Default ladder for an auto-created StreakSystem with no ScoreConfig wired.
                if (streak >= 8) return 3f;
                if (streak >= 6) return 2.5f;
                if (streak >= 4) return 2f;
                if (streak >= 2) return 1.5f;
                return 1f;
            }
            float multiplier = 1f;
            for (int i = 0; i < tiers.Length; i++)
                if (streak >= tiers[i].fromStreak)
                    multiplier = tiers[i].multiplier;
            return multiplier;
        }

        // The shared streak carries across players WITHIN a group — so one player's clean driving keeps the
        // team's multiplier alive and one player's mistake costs everyone. A new turn just refreshes the UI.
        private void HandleSessionReset() => GameEvents.RaiseStreakChanged(Streak, Multiplier);

        // A new class group wipes the shared streak.
        private void HandleGroupReset()
        {
            Streak = 0;
            BestStreak = 0;
            GameEvents.RaiseStreakChanged(0, MultiplierFor(0));
        }
    }
}
