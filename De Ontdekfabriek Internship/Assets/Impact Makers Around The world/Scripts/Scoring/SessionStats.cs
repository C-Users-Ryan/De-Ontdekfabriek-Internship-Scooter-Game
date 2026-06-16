namespace KenyaScooter.Scoring
{
    /// <summary>
    /// Everything one student's turn produced — the end-screen breakdown is a mirror,
    /// not a judgement (MDA A6). Owned and updated by GameManager via GameEvents;
    /// read by the checkpoint/game-over/finish screens, GroupScoreManager and
    /// AnalyticsManager.
    /// </summary>
    public sealed class SessionStats
    {
        public int Overtakes;
        public int NearMisses;
        public int WrongLaneTicks;
        public int SpeedingTicks;
        public int PotholeHits;
        public int RockHits;
        public int SpeedBumpHits;
        public int LightCollisions;
        public int HardCollisions;
        public int GracesUsed;
        public int RewindsUsed;
        public int BestStreak;
        public int FinalScore;
        public float DistanceMetres;

        public void Reset()
        {
            Overtakes = 0;
            NearMisses = 0;
            WrongLaneTicks = 0;
            SpeedingTicks = 0;
            PotholeHits = 0;
            RockHits = 0;
            SpeedBumpHits = 0;
            LightCollisions = 0;
            HardCollisions = 0;
            GracesUsed = 0;
            RewindsUsed = 0;
            BestStreak = 0;
            FinalScore = 0;
            DistanceMetres = 0f;
        }
    }
}
