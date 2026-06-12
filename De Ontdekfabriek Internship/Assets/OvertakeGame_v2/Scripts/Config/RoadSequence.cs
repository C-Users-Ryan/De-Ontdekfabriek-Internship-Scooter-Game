using UnityEngine;
using System.Collections.Generic;

namespace OvertakeGame
{
    /// <summary>
    /// A curated, ordered list of road tiles that forms one named road scenario.
    /// RoadSequencer selects sequences based on weight, cooldown, time-unlock, and grammar tags.
    ///
    /// CREATING A SEQUENCE:
    ///   Create > OvertakeGame > Road Sequence
    ///   Add tile prefabs in order. Set contextTags to describe the environment.
    ///   Set speedLimitKmh if this sequence has a posted limit. Set trafficProfile if needed.
    ///
    /// GRAMMAR RULES:
    ///   contextTags         — what this sequence IS (e.g. "savanna", "township", "murram")
    ///   forbiddenPrevTags   — hard rule: this sequence cannot follow sequences with these tags
    ///   preferredPrevTags   — soft hint: this sequence prefers to follow these tags
    /// </summary>
    [CreateAssetMenu(fileName = "Sequence_New", menuName = "OvertakeGame/Road Sequence")]
    public class RoadSequence : ScriptableObject
    {
        [Header("Identity")]
        public string sequenceId;
        [TextArea(1, 3)]
        public string description;

        // ── Tiles ──────────────────────────────────────────────────────────────

        [Header("Tiles — in order")]
        public List<TileEntry> tiles = new();

        // ── Context ────────────────────────────────────────────────────────────

        [Header("Context Tags")]
        [Tooltip("Tags describing this sequence's environment. Used by grammar rules and spawn filters.")]
        public string[] contextTags = new string[0];

        [Tooltip("Hard rule: cannot follow any sequence whose tags include one of these.")]
        public string[] forbiddenPrevTags = new string[0];

        [Tooltip("Soft hint: prefers to follow sequences whose tags include one of these.")]
        public string[] preferredPrevTags = new string[0];

        // ── Gameplay overrides ─────────────────────────────────────────────────

        [Header("Gameplay")]
        [Tooltip("Posted speed limit (km/h) for this sequence. 0 = no change.")]
        public float speedLimitKmh = 0f;

        [Tooltip("Traffic profile to activate when this sequence begins. Null = no change.")]
        public TrafficBehaviourProfile trafficProfile;

        // ── Selection ──────────────────────────────────────────────────────────

        [Header("Selection")]
        [Tooltip("Relative probability weight. 0 = removed from rotation (forced or disabled).")]
        public float weight = 1f;

        [Tooltip("Minimum tile placements before this sequence can repeat. 0 = no cooldown.")]
        public int cooldownTiles = 2;

        [Tooltip("Session time (seconds) that must pass before this sequence becomes available. 0 = always.")]
        public float unlockAtTime = 0f;

        // ── Turn support ───────────────────────────────────────────────────────

        [Header("Turn")]
        [Tooltip("True if one tile in this sequence contains a TurnTrigger.")]
        public bool containsTurn = false;

        [Tooltip("Road direction after the turn. Only used when containsTurn = true.")]
        public Vector3 postTurnDirection = Vector3.back;

        // ── Helpers ────────────────────────────────────────────────────────────

        public bool HasTag(string tag)
        {
            foreach (var t in contextTags)
                if (t == tag) return true;
            return false;
        }

        public bool ForbidsFollowing(string prevTag)
        {
            foreach (var t in forbiddenPrevTags)
                if (t == prevTag) return true;
            return false;
        }

        // ── Per-tile entry ─────────────────────────────────────────────────────

        [System.Serializable]
        public class TileEntry
        {
            [Tooltip("Tile prefab. Root pivot must be at the back edge (Z=0 by convention).")]
            public GameObject prefab;

            [Tooltip("Allow traffic to spawn on this tile.")]
            public bool allowTraffic = true;

            [Tooltip("Allow potholes and rocks to spawn on this tile.")]
            public bool allowHazards = true;

            [Tooltip("Hazard spawn density relative to normal. 0=none, 1=normal, 2=double.")]
            [Range(0f, 3f)]
            public float hazardDensityMultiplier = 1f;
        }
    }
}
