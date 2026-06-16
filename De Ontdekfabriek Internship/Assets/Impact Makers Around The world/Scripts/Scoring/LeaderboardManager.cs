using System;
using System.Collections.Generic;
using UnityEngine;

namespace KenyaScooter.Scoring
{
    /// <summary>
    /// Persistent group score history (Req §13): committed group totals stored as
    /// JSON in PlayerPrefs, ranked descending. Powers the leaderboard panel and the
    /// "JULLIE STAAN OP PLEK X!" announcement. Reset only via the facilitator's
    /// long-press admin flow, for a new exhibition period.
    /// </summary>
    public sealed class LeaderboardManager : MonoBehaviour
    {
        public static LeaderboardManager Instance { get; private set; }

        private const string Key = "ksg.leaderboard";
        private const int MaxEntries = 50;

        [Serializable]
        public sealed class Entry
        {
            public string label;
            public int score;
            public string date;
        }

        [Serializable]
        private sealed class SaveData
        {
            public List<Entry> entries = new List<Entry>();
        }

        private SaveData data;

        public IReadOnlyList<Entry> Entries => data.entries;

        private void Awake()
        {
            Instance = this;
            string json = PlayerPrefs.GetString(Key, string.Empty);
            data = string.IsNullOrEmpty(json) ? new SaveData() : JsonUtility.FromJson<SaveData>(json);
            if (data == null || data.entries == null)
                data = new SaveData();
        }

        /// <summary>Commits a finished group's total and returns its rank (1-based).</summary>
        public int CommitGroup(int groupTotal)
        {
            var entry = new Entry
            {
                label = $"Groep {data.entries.Count + 1}",
                score = groupTotal,
                date = DateTime.Now.ToString("yyyy-MM-dd")
            };
            data.entries.Add(entry);
            data.entries.Sort((a, b) => b.score.CompareTo(a.score));
            if (data.entries.Count > MaxEntries)
                data.entries.RemoveRange(MaxEntries, data.entries.Count - MaxEntries);
            Save();
            return RankOf(groupTotal);
        }

        /// <summary>Where a score would place among the stored groups, 1-based.</summary>
        public int RankOf(int score)
        {
            int rank = 1;
            for (int i = 0; i < data.entries.Count; i++)
                if (data.entries[i].score > score)
                    rank++;
            return rank;
        }

        public void ResetLeaderboard()
        {
            data.entries.Clear();
            Save();
        }

        private void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }
    }
}
