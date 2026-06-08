using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace OvertakeGame
{
    /// <summary>
    /// Lightweight local analytics using PlayerPrefs.
    /// No internet required. All data stays on device — no privacy concerns.
    /// Attach to the same persistent GameObject as HighScoreManager.
    ///
    /// After a playtesting session at De Ontdekfabriek, generate the report
    /// and retrieve the file via Xcode Devices or File Sharing (enable in Info.plist).
    ///
    /// WIRING — call these from existing scripts:
    ///   GameManager (run starts)    → AnalyticsManager.I.OnRunStart()
    ///   GameManager (every frame)   → AnalyticsManager.I.OnDistanceUpdate(distance)
    ///   OvertakeCollisionHandler    → AnalyticsManager.I.OnCrash(tileName, distance)
    ///   GameManager (run ends)      → AnalyticsManager.I.OnRunEnd(finalDistance)
    ///
    /// Each TrafficVehicle or road tile prefab should expose a public string
    /// tileName matching the names in the crash table below so the crash-by-tile
    /// breakdown is populated automatically.
    ///
    /// TRACKED EVENTS:
    ///   Session start    — run count, time of day
    ///   Crash            — tile type, distance, time of day
    ///   Distance milestones — 100m, 500m, 1km, 2km reached
    ///   Session end      — final score, distance, run duration
    /// </summary>
    public class AnalyticsManager : MonoBehaviour
    {
        public static AnalyticsManager I { get; private set; }

        // ── PlayerPrefs keys ──────────────────────────────────────────────────
        private const string K_TOTAL_RUNS   = "rn_runs";
        private const string K_TOTAL_DIST   = "rn_dist";
        private const string K_TOTAL_TIME   = "rn_time";
        private const string K_TOTAL_SCORE  = "rn_score";
        private const string K_CRASH_PRE    = "rn_crash_";    // + tile name
        private const string K_TOD_PRE      = "rn_tod_";      // + time of day name
        private const string K_MILE_PRE     = "rn_mile_";     // + metres

        // ── Session state ─────────────────────────────────────────────────────
        private float   _sessionStart;
        private int     _sessionScore;
        private readonly int[]  _milestones    = { 100, 500, 1000, 2000 };
        private readonly bool[] _milestonesHit = new bool[4];

        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
            DontDestroyOnLoad(gameObject);
        }

        // ── Call at start of every run ────────────────────────────────────────
        public void OnRunStart()
        {
            _sessionStart = Time.realtimeSinceStartup;
            _sessionScore = 0;
            System.Array.Clear(_milestonesHit, 0, _milestonesHit.Length);

            Increment(K_TOTAL_RUNS);

            // Track which time of day gets played most
            // DayCycleManager.Current is set before GameManager.StartGame()
            string tod = DayCycleManager.Current.ToString();
            Increment(K_TOD_PRE + tod);
        }

        // ── Call every frame (or when distance changes) from GameManager ──────
        public void OnDistanceUpdate(float metres)
        {
            for (int i = 0; i < _milestones.Length; i++)
            {
                if (!_milestonesHit[i] && metres >= _milestones[i])
                {
                    _milestonesHit[i] = true;
                    Increment(K_MILE_PRE + _milestones[i]);
                }
            }
        }

        // ── Call when player hits a traffic vehicle or obstacle ───────────────
        /// <param name="tileName">
        /// The name of the tile or vehicle type — e.g. "Matatu", "Pothole", "Rock".
        /// Match this to the tileName field on your prefabs.
        /// </param>
        public void OnCrash(string tileName, float distance)
        {
            Increment(K_CRASH_PRE + tileName);
            AddFloat(K_TOTAL_DIST, distance);
        }

        // ── Call when run ends (crash or session timer) ───────────────────────
        public void OnRunEnd(int finalScore, float finalDistance)
        {
            float duration = Time.realtimeSinceStartup - _sessionStart;
            AddFloat(K_TOTAL_TIME, duration);
            Increment(K_TOTAL_SCORE, finalScore);
            AddFloat(K_TOTAL_DIST, finalDistance);
        }

        // ── Report generation ─────────────────────────────────────────────────
        /// <summary>
        /// Generates a plain text report of all tracked data.
        /// Call from a hidden debug button or shake gesture.
        /// The report is also saved to Application.persistentDataPath
        /// where it can be retrieved via Xcode or File Sharing.
        /// </summary>
        public string GenerateReport()
        {
            int   runs    = PlayerPrefs.GetInt  (K_TOTAL_RUNS,  0);
            float totDist = PlayerPrefs.GetFloat(K_TOTAL_DIST,  0f);
            float totTime = PlayerPrefs.GetFloat(K_TOTAL_TIME,  0f);
            int   totScore= PlayerPrefs.GetInt  (K_TOTAL_SCORE, 0);

            var sb = new StringBuilder();
            sb.AppendLine("=== ROADS OF NAIROBI — PLAYTEST REPORT ===");
            sb.AppendLine($"Generated:           {System.DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"Total runs:          {runs}");
            sb.AppendLine($"Avg session time:    {(runs > 0 ? totTime / runs : 0f):F1}s");
            sb.AppendLine($"Avg distance:        {(runs > 0 ? totDist / runs : 0f):F0}m");
            sb.AppendLine($"Avg score/run:       {(runs > 0 ? totScore / runs : 0)}");

            sb.AppendLine("\n-- Players reaching milestones --");
            foreach (var m in _milestones)
            {
                int   count = PlayerPrefs.GetInt(K_MILE_PRE + m, 0);
                float pct   = runs > 0 ? (count / (float)runs) * 100f : 0f;
                sb.AppendLine($"  {m,5}m reached:  {count,4} runs  ({pct:F0}%)");
            }

            sb.AppendLine("\n-- Crashes by obstacle / tile type --");
            // Add tile names here to match the tileName strings your prefabs use
            string[] knownTypes = {
                "Matatu", "OncomingMatatu", "Pothole", "Rock",
                "SpeedBump", "Straight", "Market", "TrafficJam", "VendorRow"
            };
            bool anyCrashes = false;
            foreach (var tile in knownTypes)
            {
                int n = PlayerPrefs.GetInt(K_CRASH_PRE + tile, 0);
                if (n > 0)
                {
                    float pct = runs > 0 ? (n / (float)runs) * 100f : 0f;
                    sb.AppendLine($"  {tile,-18}: {n,4} crashes  ({pct:F0}% of runs)");
                    anyCrashes = true;
                }
            }
            if (!anyCrashes) sb.AppendLine("  (no crash data yet)");

            sb.AppendLine("\n-- Time of day popularity --");
            foreach (var tod in new[] { "Morning", "Midday", "Sunset" })
            {
                int   n   = PlayerPrefs.GetInt(K_TOD_PRE + tod, 0);
                float pct = runs > 0 ? (n / (float)runs) * 100f : 0f;
                sb.AppendLine($"  {tod,-10}: {n,4} runs  ({pct:F0}%)");
            }

            string report = sb.ToString();

            // Save to device for retrieval via Xcode / File Sharing
            try
            {
                string path = System.IO.Path.Combine(
                    Application.persistentDataPath, "roads_playtest_report.txt");
                System.IO.File.WriteAllText(path, report);
                Debug.Log($"[AnalyticsManager] Report saved to: {path}");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[AnalyticsManager] Could not save report: {e.Message}");
            }

            return report;
        }

        /// <summary>Wipe all analytics data. Call from debug menu.</summary>
        public void ResetAll()
        {
            PlayerPrefs.DeleteKey(K_TOTAL_RUNS);
            PlayerPrefs.DeleteKey(K_TOTAL_DIST);
            PlayerPrefs.DeleteKey(K_TOTAL_TIME);
            PlayerPrefs.DeleteKey(K_TOTAL_SCORE);
            Debug.Log("[AnalyticsManager] All analytics data wiped.");
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private static void Increment(string key, int by = 1) =>
            PlayerPrefs.SetInt(key, PlayerPrefs.GetInt(key, 0) + by);

        private static void AddFloat(string key, float by) =>
            PlayerPrefs.SetFloat(key, PlayerPrefs.GetFloat(key, 0f) + by);
    }
}
