using UnityEngine;

namespace KenyaScooter.Scoring
{
    /// <summary>
    /// Best individual score, persisted locally (Req §12.2 — "REKODI MPYA!" on the
    /// game-over/finish screens). PlayerPrefs only; the game runs fully offline.
    /// </summary>
    public static class HighScoreManager
    {
        private const string Key = "ksg.highscore";

        public static int Best => PlayerPrefs.GetInt(Key, 0);

        /// <summary>Returns true when the submitted score is a new record.</summary>
        public static bool TrySubmit(int score)
        {
            if (score <= Best)
                return false;
            PlayerPrefs.SetInt(Key, score);
            PlayerPrefs.Save();
            return true;
        }

        public static void ResetHighScore()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
