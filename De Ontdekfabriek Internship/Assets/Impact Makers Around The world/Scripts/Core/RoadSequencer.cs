using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace OvertakeGame
{
    /// <summary>
    /// Controls WHICH tiles are placed and in what order by curating RoadSequences.
    /// Sits alongside RoadTileRecycler — the recycler handles movement and pooling,
    /// the sequencer decides what tile prefab to use next.
    ///
    /// SETUP:
    ///   1. Add this component to the same GameObject as RoadTileRecycler.
    ///   2. Assign RoadSequence assets to the sequences list.
    ///   3. Optionally assign a startingSequence for a clean intro stretch.
    ///   4. RoadTileRecycler will call GetNextTileEntry() instead of its own random picker.
    ///
    /// SEQUENCE GRAMMAR:
    ///   The sequencer reads contextTags / allowedPreviousTags to build a soft grammar.
    ///   Set allowedPreviousTags on "TownApproach" to {"savanna"} → town can only appear
    ///   after open savanna road. Leave it empty → that sequence can appear anywhere.
    ///
    /// TURN SEQUENCES:
    ///   Sequences with containsTurn = true must have a TurnTrigger embedded in one of
    ///   their tile prefabs. That trigger fires RoadDirection.SetDirection() automatically.
    ///   After the turn, new sequences are picked as normal — the changed RoadDirection
    ///   means all world objects follow the new axis automatically.
    ///
    /// TRAFFIC:
    ///   When a new sequence starts, OnSequenceStarted fires. TrafficManager listens and
    ///   switches to the sequence's TrafficBehaviourProfile if one is assigned.
    /// </summary>
    [RequireComponent(typeof(RoadTileRecycler))]
    public class RoadSequencer : MonoBehaviour
    {
        public static RoadSequencer Instance { get; private set; }

        [Header("Sequences")]
        [Tooltip("All available RoadSequence assets. At least one required.")]
        public List<RoadSequence> sequences = new();

        [Header("Starting Sequence")]
        [Tooltip("Optional. If set, always plays first before random selection begins.")]
        public RoadSequence startingSequence;

        [Header("Debug")]
        public bool logSequenceChanges = true;

        // ── Events ────────────────────────────────────────────────────────────

        /// <summary>Fired whenever a new sequence becomes active.</summary>
        public static event System.Action<RoadSequence> OnSequenceStarted;

        // ── Public state ──────────────────────────────────────────────────────

        public RoadSequence CurrentSequence => _current;
        public IReadOnlyList<string> CurrentContextTags => _currentContextTags;
        public bool CurrentHasTag(string tag) => _currentContextTags.Contains(tag);

        // ── Private state ─────────────────────────────────────────────────────

        private RoadSequence      _current;
        private int               _tileIndex;              // index into _current.tiles
        private List<string>      _currentContextTags = new();
        private List<string>      _previousContextTags = new();
        private List<int>         _recentSequenceIndices = new(); // for cooldown tracking
        private bool              _started;

        // ── Unity lifecycle ───────────────────────────────────────────────────

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

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Called by RoadTileRecycler when it needs the next tile prefab.
        /// Returns null if no sequences are configured.
        /// </summary>
        public RoadSequence.TileEntry GetNextTileEntry()
        {
            if (!_started) return null;
            if (_current == null || _current.tiles.Count == 0) { AdvanceSequence(); }
            if (_current == null) return null;

            var entry  = _current.tiles[_tileIndex];
            _tileIndex++;

            if (_tileIndex >= _current.tiles.Count)
                AdvanceSequence();

            return entry;
        }

        // ── Internal ──────────────────────────────────────────────────────────

        private void AdvanceSequence()
        {
            // Record the completed sequence for cooldown tracking
            if (_current != null)
            {
                int idx = sequences.IndexOf(_current);
                if (idx >= 0)
                {
                    _recentSequenceIndices.Add(idx);
                    const int maxHistory = 10;
                    if (_recentSequenceIndices.Count > maxHistory)
                        _recentSequenceIndices.RemoveAt(0);
                }
            }

            _previousContextTags = new List<string>(_currentContextTags);
            _current   = PickNext();
            _tileIndex = 0;
            FireSequenceStarted();
        }

        private RoadSequence PickNext()
        {
            if (sequences == null || sequences.Count == 0) return null;

            // Build candidate list with weight, filtered by cooldown + unlock time + previous-tag rule
            float sessionTime = GameManager.Instance?.SessionTime ?? 0f;
            var candidates = new List<(RoadSequence seq, float w)>();

            for (int i = 0; i < sequences.Count; i++)
            {
                var seq = sequences[i];
                if (seq == null || seq.weight <= 0f) continue;

                // ── Cooldown ──
                int recentPos = _recentSequenceIndices.LastIndexOf(i);
                if (recentPos >= 0)
                {
                    int playedAgo = _recentSequenceIndices.Count - 1 - recentPos;
                    if (playedAgo < seq.repeatCooldown) continue;
                }

                // ── Session-time unlock ──
                if (seq.unlockAtTime > 0f && sessionTime < seq.unlockAtTime) continue;

                // ── Previous-tag compatibility ──
                if (seq.allowedPreviousTags != null && seq.allowedPreviousTags.Length > 0)
                {
                    bool ok = seq.allowedPreviousTags.Any(t => _previousContextTags.Contains(t));
                    if (!ok) continue;
                }

                candidates.Add((seq, seq.weight));
            }

            // Fallback: ignore constraints if nothing qualifies
            if (candidates.Count == 0)
            {
                var fallback = sequences
                    .Where(s => s != null && s.weight > 0f)
                    .Select(s => (s, s.weight))
                    .ToList();
                if (fallback.Count == 0) return null;
                candidates = fallback;
            }

            // Weighted random pick
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
                Debug.Log($"[RoadSequencer] → {_current.sequenceId}  tags:[{string.Join(",",_currentContextTags)}]");

            OnSequenceStarted?.Invoke(_current);
        }
    }
}
