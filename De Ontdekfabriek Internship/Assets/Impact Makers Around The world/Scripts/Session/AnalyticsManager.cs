using System.Collections.Generic;
using System.Text;
using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;

namespace KenyaScooter.Session
{
    /// <summary>
    /// Local-only usage analytics (Req §14): runs, distance, crashes by cause and
    /// play time per day phase, persisted in PlayerPrefs — no network, ever. Counters
    /// accumulate in memory during a turn and flush once when it ends. GenerateReport
    /// renders a plaintext summary for the facilitator debug panel; data resets only
    /// from there.
    /// </summary>
    public sealed class AnalyticsManager : MonoBehaviour
    {
        public static AnalyticsManager Instance { get; private set; }

        private const string KeyRuns = "ksg.analytics.runs";
        private const string KeyDistance = "ksg.analytics.distance";
        private const string KeyCrashTraffic = "ksg.analytics.crash.traffic";
        private const string KeyCrashPothole = "ksg.analytics.crash.pothole";
        private const string KeyCrashRock = "ksg.analytics.crash.rock";
        private const string KeyPhasePrefix = "ksg.analytics.phase.";

        private static readonly float[] MilestonesKm = { 1f, 5f, 10f, 25f, 50f, 100f };

        private readonly Dictionary<string, float> phaseSeconds = new Dictionary<string, float>();
        private string currentPhaseLabel = string.Empty;
        private int pendingCrashTraffic;
        private int pendingCrashPothole;
        private int pendingCrashRock;
        private bool turnFlushed;

        private void Awake() => Instance = this;

        private void OnEnable()
        {
            GameEvents.SessionStarted += HandleSessionStarted;
            GameEvents.StateChanged += HandleStateChanged;
            GameEvents.CheckpointReached += FlushTurn;
            GameEvents.CollisionOccurred += HandleCollision;
            GameEvents.HazardHit += HandleHazard;
            GameEvents.DayPhaseChanged += HandleDayPhase;
        }

        private void OnDisable()
        {
            GameEvents.SessionStarted -= HandleSessionStarted;
            GameEvents.StateChanged -= HandleStateChanged;
            GameEvents.CheckpointReached -= FlushTurn;
            GameEvents.CollisionOccurred -= HandleCollision;
            GameEvents.HazardHit -= HandleHazard;
            GameEvents.DayPhaseChanged -= HandleDayPhase;
        }

        private void Update()
        {
            if (GameManager.State != GameState.Playing || string.IsNullOrEmpty(currentPhaseLabel))
                return;

            phaseSeconds.TryGetValue(currentPhaseLabel, out float seconds);
            phaseSeconds[currentPhaseLabel] = seconds + Time.deltaTime;
        }

        // ---- Event handlers ------------------------------------------------------------

        private void HandleSessionStarted()
        {
            turnFlushed = false;
            pendingCrashTraffic = 0;
            pendingCrashPothole = 0;
            pendingCrashRock = 0;
            phaseSeconds.Clear();
        }

        private void HandleStateChanged(GameState from, GameState to)
        {
            if (to == GameState.GameOver || to == GameState.Finished)
                FlushTurn();
        }

        private void HandleCollision(CollisionSeverity severity, float relativeKmh, Vector3 position, bool absorbed)
        {
            if (!absorbed)
                pendingCrashTraffic++;
        }

        private void HandleHazard(HazardSpawnConfig definition, float playerKmh, Vector3 position)
        {
            if (definition == null) return;
            if (definition.response == HazardResponse.StaticObstacle) pendingCrashRock++;
            else pendingCrashPothole++;
        }

        private void HandleDayPhase(int index, string label) => currentPhaseLabel = label;

        // ---- Persistence -----------------------------------------------------------------

        private void FlushTurn()
        {
            if (turnFlushed)
                return;
            turnFlushed = true;

            PlayerPrefs.SetInt(KeyRuns, PlayerPrefs.GetInt(KeyRuns, 0) + 1);
            PlayerPrefs.SetFloat(KeyDistance,
                PlayerPrefs.GetFloat(KeyDistance, 0f) + WorldSpeed.Instance.DistanceTravelled);
            PlayerPrefs.SetInt(KeyCrashTraffic, PlayerPrefs.GetInt(KeyCrashTraffic, 0) + pendingCrashTraffic);
            PlayerPrefs.SetInt(KeyCrashPothole, PlayerPrefs.GetInt(KeyCrashPothole, 0) + pendingCrashPothole);
            PlayerPrefs.SetInt(KeyCrashRock, PlayerPrefs.GetInt(KeyCrashRock, 0) + pendingCrashRock);

            foreach (KeyValuePair<string, float> pair in phaseSeconds)
            {
                string key = KeyPhasePrefix + pair.Key;
                PlayerPrefs.SetFloat(key, PlayerPrefs.GetFloat(key, 0f) + pair.Value);
            }
            PlayerPrefs.Save();
        }

        /// <summary>Plaintext summary for the facilitator debug panel (Req §14). Allocation is fine here — debug path only.</summary>
        public string GenerateReport()
        {
            float distanceKm = PlayerPrefs.GetFloat(KeyDistance, 0f) / 1000f;
            float milestone = 0f;
            for (int i = 0; i < MilestonesKm.Length; i++)
                if (distanceKm >= MilestonesKm[i])
                    milestone = MilestonesKm[i];

            var report = new StringBuilder(256);
            report.AppendLine("— KENYA SCOOTER ANALYTICS —");
            report.AppendLine($"Runs: {PlayerPrefs.GetInt(KeyRuns, 0)}");
            report.AppendLine($"Distance: {distanceKm:0.0} km (milestone: {milestone:0} km)");
            report.AppendLine($"Crashes — traffic: {PlayerPrefs.GetInt(KeyCrashTraffic, 0)}, " +
                $"pothole: {PlayerPrefs.GetInt(KeyCrashPothole, 0)}, rock: {PlayerPrefs.GetInt(KeyCrashRock, 0)}");
            report.AppendLine("Play time per phase:");
            AppendPhase(report, "ASUBUHI");
            AppendPhase(report, "MCHANA");
            AppendPhase(report, "ALASIRI");
            AppendPhase(report, "JIONI");
            return report.ToString();
        }

        private static void AppendPhase(StringBuilder report, string label)
        {
            float seconds = PlayerPrefs.GetFloat(KeyPhasePrefix + label, 0f);
            if (seconds > 0f)
                report.AppendLine($"  {label}: {seconds / 60f:0.0} min");
        }

        public void ResetAnalytics()
        {
            PlayerPrefs.DeleteKey(KeyRuns);
            PlayerPrefs.DeleteKey(KeyDistance);
            PlayerPrefs.DeleteKey(KeyCrashTraffic);
            PlayerPrefs.DeleteKey(KeyCrashPothole);
            PlayerPrefs.DeleteKey(KeyCrashRock);
            PlayerPrefs.DeleteKey(KeyPhasePrefix + "ASUBUHI");
            PlayerPrefs.DeleteKey(KeyPhasePrefix + "MCHANA");
            PlayerPrefs.DeleteKey(KeyPhasePrefix + "ALASIRI");
            PlayerPrefs.DeleteKey(KeyPhasePrefix + "JIONI");
            PlayerPrefs.Save();
        }
    }
}
