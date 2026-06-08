using UnityEngine;
using TMPro;

namespace OvertakeGame
{
    /// <summary>
    /// Singleton that tracks best score, best distance, and total runs.
    /// Persists across sessions via PlayerPrefs.
    /// Attach to a DontDestroyOnLoad GameObject.
    ///
    /// USAGE — call at the end of each run:
    ///
    ///   bool isNewBest = HighScoreManager.I.TrySubmit(currentScore, currentDistance);
    ///   bestScoreLabel.text = HighScoreManager.I.BestScoreDisplay;
    ///   bestDistLabel.text  = HighScoreManager.I.BestDistanceDisplay;
    ///   if (isNewBest)
    ///       newBestBanner.SetActive(true); // flash "REKODI MPYA!" — Swahili: New Record!
    /// </summary>
    public class HighScoreManager : MonoBehaviour
    {
        public static HighScoreManager I { get; private set; }

        private const string KEY_SCORE    = "roads_best_score";
        private const string KEY_DISTANCE = "roads_best_distance";
        private const string KEY_RUNS     = "roads_total_runs";

        public int   BestScore    { get; private set; }
        public float BestDistance { get; private set; }
        public int   TotalRuns   { get; private set; }

        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
            DontDestroyOnLoad(gameObject);
            Load();
        }

        private void Load()
        {
            BestScore    = PlayerPrefs.GetInt  (KEY_SCORE,    0);
            BestDistance = PlayerPrefs.GetFloat(KEY_DISTANCE, 0f);
            TotalRuns    = PlayerPrefs.GetInt  (KEY_RUNS,     0);
        }

        /// <summary>
        /// Call at the end of every run.
        /// Returns true if the run set a new score or distance record.
        /// Use the return value to trigger a "REKODI MPYA!" (New Record) flash.
        /// </summary>
        public bool TrySubmit(int score, float distance)
        {
            TotalRuns++;
            PlayerPrefs.SetInt(KEY_RUNS, TotalRuns);

            bool newBest = false;

            if (score > BestScore)
            {
                BestScore = score;
                PlayerPrefs.SetInt(KEY_SCORE, BestScore);
                newBest = true;
            }
            if (distance > BestDistance)
            {
                BestDistance = distance;
                PlayerPrefs.SetFloat(KEY_DISTANCE, BestDistance);
                newBest = true;
            }

            PlayerPrefs.Save();
            return newBest;
        }

        /// <summary>
        /// Wipe all records. Call after showing a confirmation dialog.
        /// Resets in memory and in PlayerPrefs.
        /// </summary>
        public void ResetAll()
        {
            PlayerPrefs.DeleteKey(KEY_SCORE);
            PlayerPrefs.DeleteKey(KEY_DISTANCE);
            PlayerPrefs.DeleteKey(KEY_RUNS);
            Load();
            Debug.Log("[HighScoreManager] All records wiped.");
        }

        // ── Formatted display strings ─────────────────────────────────────────
        /// <summary>"000000" or "------" if no score yet.</summary>
        public string BestScoreDisplay =>
            BestScore > 0 ? BestScore.ToString("D6") : "------";

        /// <summary>"0123 M" or "---- M" if no distance yet.</summary>
        public string BestDistanceDisplay =>
            BestDistance > 0
                ? $"{Mathf.FloorToInt(BestDistance):D4} M"
                : "---- M";
    }
}
