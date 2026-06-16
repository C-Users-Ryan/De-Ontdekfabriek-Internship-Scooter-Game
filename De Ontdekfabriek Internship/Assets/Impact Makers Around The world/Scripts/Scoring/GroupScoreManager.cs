using System;
using UnityEngine;

namespace KenyaScooter.Scoring
{
    /// <summary>
    /// Relay scoring (M27, Req §13): each student's turn ADDS to the running group
    /// total — no turn can subtract, which is the whole social design (MDA Tension 3:
    /// a cautious 50-point turn is still a positive contribution). Deliberately does
    /// NOT listen to SessionReset: the group total survives student turns and only
    /// resets when the facilitator starts a new group.
    /// </summary>
    public sealed class GroupScoreManager : MonoBehaviour
    {
        public static GroupScoreManager Instance { get; private set; }

        public int GroupTotal { get; private set; }
        public int TurnCount { get; private set; }

        /// <summary>(group total, points the last turn added).</summary>
        public event Action<int, int> GroupChanged;

        private void Awake() => Instance = this;

        /// <summary>Called once per turn by GameManager when the turn ends (checkpoint, finish or game over).</summary>
        public void CommitTurn(int turnScore)
        {
            GroupTotal += Mathf.Max(0, turnScore);
            TurnCount++;
            GroupChanged?.Invoke(GroupTotal, turnScore);
        }

        /// <summary>Facilitator action for a new class group (debug panel / leaderboard admin).</summary>
        public void ResetGroup()
        {
            GroupTotal = 0;
            TurnCount = 0;
            GroupChanged?.Invoke(0, 0);
        }
    }
}
