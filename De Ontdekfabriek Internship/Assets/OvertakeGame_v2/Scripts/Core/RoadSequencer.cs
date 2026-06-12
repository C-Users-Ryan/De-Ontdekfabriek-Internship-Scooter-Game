using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace OvertakeGame
{
    /// <summary>
    /// Controls WHICH tiles are placed by curating RoadSequences.
    /// Sits alongside RoadTileRecycler — the recycler handles movement and pooling,
    /// the sequencer decides the next tile prefab.
    ///
    /// SETUP
    ///   1. Add this component to the same GameObject as RoadTileRecycler.
    ///   2. Assign RoadSequence assets to the sequences list.
    ///   3. Optionally assign a startingSequence for a clean intro stretch.
    ///
    /// SEQUENCE GRAMMAR
    ///   contextTags       — what this sequence IS ("savanna", "township", "murram")
    ///   forbiddenPrevTags — hard rule: cannot follow a sequence with these tags
    ///   preferredPrevTags — soft hint: 2× weight boost when previous had these tags
    ///
    /// COOLDOWN
    ///   cooldownTiles: minimum tiles placed before the sequence can repeat.
    ///   Counter increments per tile placed; resets to 0 when the sequence starts.
    ///
    /// TURN SEQUENCES
    ///   Sequences with containsTurn = true must have a TurnTrigger in one tile prefab.
    ///   That trigger fires RoadDirection.SetDirection() automatically.
    ///
    /// TRAFFIC PROFILE
    ///   OnSequenceStarted fires when a new sequence starts. TrafficManager listens to
    ///   switch to the sequence's TrafficBehaviourProfile.
    /// </summary>
    [RequireComponent(typeof(RoadTileRecycler))]
    public class RoadSequencer : MonoBehaviour
    {
        public static RoadSequencer Instance { get; private set; }

        [Header("Sequences")]
        [Tooltip("All available RoadSequence assets. At least one required.")]
        public List<RoadSequence> sequences = new();

        [Header("Starting Sequence")]
        [Tooltip("Optional. Plays first before random selection begins.")]
        public RoadSequence startingSequence;

        [Header("Debug")]
        public bool logSequenceChanges = true;

        // ── Events ──────────────────────────────────────────────────────────────

        public static event System.Action<RoadSequence> OnSequenceStarted;

        // ── Public state ─────────────────────────────────────────────────────────

        public RoadSequence CurrentSequence => _current;
        public IReadOnlyList<string> CurrentContextTags => _currentContextTags;
        public bool CurrentHasTag(string tag) => _currentContextTags.Contains(tag);

        // ── Private state ─────────────────────────────────────────────────────────

        private RoadSequence  _current;
        private int           _tileIndex;
        private List<string>  _currentContextTags  = new();
        private List<string>  _previousContextTags = new();
        private bool          _started;

        // Per-sequence tile-count since last start. Key = index in sequences list.
        private Dictionary<int, int> _tileCooldowns = new();

        // ── Unity lifecycle ────────────────────────────────────────────────────────

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void Start()
        {
            _current   = startingSequence != null ? startingSequence : PickNext();
            _tileIndex = 0;
            FireSequenceStarted();
            _started = true;
        }

        // ── Public API ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Called by RoadTileRecycler when it needs the next tile prefab.
        /// Increments all cooldown counters once per call (one tile placed per call).
        /// </summary>
        public RoadSequence.TileEntry GetNextTileEntry()
        {
            if (!_started) return null;
            if (_current == null || _current.tiles.Count == 0) AdvanceSequence();
            if (_current == null) return null;

            // Increment all cooldown counters for this tile placement
            for (int i = 0; i < sequences.Count; i++)
            {
                _tileCooldowns.TryGetValue(i, out int c);
                _tileCooldowns[i] = c + 1;
            }

            var entry  = _current.tiles[_tileIndex];
            _tileIndex++;
            if (_tileIndex >= _current.tiles.Count)
                AdvanceSequence();

            return entry;
        }

        // ── Internal ───────────────────────────────────────────────────────────────

        private void AdvanceSequence()
        {
            // Reset cooldown counter for the sequence that just ended
            int prev = sequences.IndexOf(_current);
            if (prev >= 0) _tileCooldowns[prev] = 0;

            _previousContextTags = new List<string>(_currentContextTags);
            _current   = PickNext();
            _tileIndex = 0;
            FireSequenceStarted();
        }

        private RoadSequence PickNext()
        {
            if (sequences == null || sequences.Count == 0) return null;

            float sessionTime = GameManager.Instance?.SessionTime ?? 0f;
            var candidates = new List<(RoadSequence seq, float w)>();

            for (int i = 0; i < sequences.Count; i++)
            {
                var seq = sequences[i];
                if (seq == null || seq.weight <= 0f) continue;

                // ── Cooldown (tiles placed since last use) ──
                if (seq.cooldownTiles > 0
                    && _tileCooldowns.TryGetValue(i, out int tiles)
                    && tiles < seq.cooldownTiles)
                    continue;

                // ── Session-time unlock ──
                if (seq.unlockAtTime > 0f && sessionTime < seq.unlockAtTime) continue;

                // ── Forbidden-previous hard filter ──
                if (seq.forbiddenPrevTags != null && seq.forbiddenPrevTags.Length > 0)
                {
                    bool blocked = seq.forbiddenPrevTags.Any(t => _previousContextTags.Contains(t));
                    if (blocked) continue;
                }

                // ── Preferred-previous soft weight boost ──
                float weight = seq.weight;
                if (seq.preferredPrevTags != null && seq.preferredPrevTags.Length > 0)
                {
                    bool preferred = seq.preferredPrevTags.Any(t => _previousContextTags.Contains(t));
                    if (preferred) weight *= 2f;
                }

                candidates.Add((seq, weight));
            }

            // Fallback: relax all constraints if nothing qualifies
            if (candidates.Count == 0)
            {
                var fallback = sequences
                    .Where(s => s != null && s.weight > 0f)
                    .Select(s => (s, s.weight))
                    .ToList();
                if (fallback.Count == 0) return null;
                candidates = fallback;
            }

            float total = candidates.Sum(c => c.w);
            float roll  = Random.Range(0f, total);
            float accum = 0f;
            foreach (var (seq, w) in candidates)
            {
                accum += w;
                if (roll <= accum) return seq;
            }
            return candidates[candidates.Count - 1].seq;
        }

        private void FireSequenceStarted()
        {
            _currentContextTags.Clear();
            if (_current?.contextTags != null)
                _currentContextTags.AddRange(_current.contextTags);

            if (logSequenceChanges && _current != null)
                Debug.Log($"[RoadSequencer] → {_current.sequenceId}  " +
                          $"tags:[{string.Join(",", _currentContextTags)}]");

            OnSequenceStarted?.Invoke(_current);
        }
    }
}
