using System.Collections.Generic;
using UnityEngine;

namespace OvertakeGame
{
    /// <summary>
    /// Picks RoadSequence ScriptableObjects using weighted random selection, enforcing per-sequence
    /// cooldowns and session-time unlock gates. Call NextTile() whenever RoadTileRecycler needs the
    /// next prefab; it advances to a new sequence automatically when the current one is exhausted.
    /// </summary>
    public class RoadSequencer : MonoBehaviour
    {
        [Header("Sequences")]
        [Tooltip("All RoadSequence assets the sequencer can draw from.")]
        public List<RoadSequence> sequences = new();

        private RoadSequence _active;
        private int _tileIndex;
        private readonly Dictionary<RoadSequence, int> _cooldowns = new();

        /// <summary>Returns the next tile prefab, stepping into a new sequence when the current one is done.</summary>
        public GameObject NextTile()
        {
            if (_active == null || _tileIndex >= _active.tiles.Count)
                Advance();

            if (_active == null || _active.tiles.Count == 0)
                return null;

            return _active.tiles[_tileIndex++];
        }

        private void Advance()
        {
            // Lock the just-finished sequence behind its cooldown.
            if (_active != null)
                _cooldowns[_active] = _active.cooldown;

            // Tick every cooldown down by one play.
            foreach (var seq in new List<RoadSequence>(_cooldowns.Keys))
                if (_cooldowns[seq] > 0) _cooldowns[seq]--;

            _active     = Pick();
            _tileIndex  = 0;
        }

        private RoadSequence Pick()
        {
            float t = GameManager.Instance?.SessionTime ?? 0f;

            float total = 0f;
            foreach (var seq in sequences)
                if (Eligible(seq, t)) total += seq.weight;

            if (total <= 0f) return null;

            float r = Random.Range(0f, total), acc = 0f;
            foreach (var seq in sequences)
            {
                if (!Eligible(seq, t)) continue;
                acc += seq.weight;
                if (r <= acc) return seq;
            }
            return null;
        }

        private bool Eligible(RoadSequence seq, float sessionTime)
        {
            if (seq == null || seq.weight <= 0f || seq.tiles == null || seq.tiles.Count == 0)
                return false;
            if (seq.unlockAtTime > 0f && sessionTime < seq.unlockAtTime)
                return false;
            if (_cooldowns.TryGetValue(seq, out int rem) && rem > 0)
                return false;
            return true;
        }
    }
}
