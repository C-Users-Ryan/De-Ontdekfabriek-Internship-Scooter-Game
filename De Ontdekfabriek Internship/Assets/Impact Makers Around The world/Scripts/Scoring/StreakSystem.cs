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

        private void OnEnable() => GameEvents.SessionReset += HandleSessionReset;
        private void OnDisable() => GameEvents.SessionReset -= HandleSessionReset;

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
            float multiplier = 1f;
            ScoreConfig.StreakTier[] tiers = config.streakTiers;
            for (int i = 0; i < tiers.Length; i++)
                if (streak >= tiers[i].fromStreak)
                    multiplier = tiers[i].multiplier;
            return multiplier;
        }

        private void HandleSessionReset()
        {
            Streak = 0;
            BestStreak = 0;
            GameEvents.RaiseStreakChanged(0, MultiplierFor(0));
        }
    }
}
