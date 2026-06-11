using UnityEngine;
using System.Collections.Generic;

namespace OvertakeGame
{
    /// <summary>
    /// A curated, ordered list of road tiles that creates a specific road scenario.
    /// Add these as assets (Create > OvertakeGame > Road Sequence) and assign them
    /// to RoadSequencer to control what the player drives through.
    ///
    /// CHANGES FROM PREVIOUS VERSION:
    ///   + speedLimitKmh field added under [Header("Speed Zone")].
    ///     SpeedZoneManager reads this when the sequence starts and broadcasts the new
    ///     limit to SpeedLimitHUD and SpeedMonitor. Set to 0 to use the SpeedZoneManager
    ///     default (configured in the SpeedZoneManager component).
    ///
    /// SPEED ZONE DESIGN GUIDE:
    ///   TOWNSHIP / urban area     → 50 km/h
    ///   SAVANNA / open highway    → 80 km/h
    ///   CONSTRUCTION zone         → 40 km/h
    ///   DIRT / rural road         → 60 km/h
    ///   MARKET / pedestrian area  → 30 km/h
    ///   BRIDGE / narrow section   → 60 km/h
    ///   Leave 0 for any sequence where the zone type inherits the previous limit.
    ///
    /// EXAMPLES OF SEQUENCES YOU MIGHT CREATE:
    ///   Sequence_SavannaStraight  — 6 straight tiles, low traffic, wildlife sign at end
    ///   Sequence_TownApproach     — road narrows, speed bumps, pedestrians appear
    ///   Sequence_TrafficJam       — dense slow vehicles packed in player lane
    ///   Sequence_DirtRoad         — rough surface tiles, dense potholes, rocks frequent
    ///   Sequence_TurnLeft         — 2 straight → corner tile → 2 straight new dir
    ///   Sequence_Alleyway         — narrow tile, parked vehicles on shoulders, no oncoming
    ///   Sequence_WildlifeCrossing — warning tile → wildlife tile → clear tile
    ///
    /// CONNECTIVITY SYSTEM:
    ///   contextTags       — what this sequence IS (e.g. "savanna", "town", "dirt")
    ///   allowedPrevTags   — what must have come before (empty = anything allowed)
    ///   preferredNextTags — hints to the sequencer about what to play next
    ///
    /// This lets you build a grammar for the road:
    ///   "town_approach" can only follow "savanna" → town must be preceded by open road
    ///   "turn_left" can follow anything but "turn_right" → no immediate double-back
    /// </summary>
    [CreateAssetMenu(fileName = "Sequence_New", menuName = "OvertakeGame/Road Sequence")]
    public class RoadSequence : ScriptableObject
    {
        // ── Identity ──────────────────────────────────────────────────────────

        [Header("Identity")]
        public string sequenceId;
        [TextArea(2, 4)]
        public string description;

        // ── Tiles ─────────────────────────────────────────────────────────────

        [Header("Tiles — in order")]
        [Tooltip("Each entry is one tile placed in sequence. Order matters.")]
        public List<TileEntry> tiles = new();

        // ── Speed Zone ────────────────────────────────────────────────────────

        [Header("Speed Zone")]
        [Tooltip("Posted speed limit for this sequence in km/h. "
               + "SpeedZoneManager reads this when the sequence starts and updates SpeedLimitHUD + SpeedMonitor. "
               + "Set to 0 to inherit the SpeedZoneManager default (no zone change when sequence begins).")]
        public float speedLimitKmh = 0f;

        // ── Context / connectivity ─────────────────────────────────────────────

        [Header("Context Tags")]
        [Tooltip("Tags that describe this sequence. Used by other sequences' allowedPrevTags.")]
        public string[] contextTags;

        [Tooltip("Tags that the PREVIOUS sequence must have provided. Leave empty = accept anything.")]
        public string[] allowedPreviousTags;

        [Tooltip("Tags preferred in the NEXT sequence. The sequencer uses this as a soft hint.")]
        public string[] preferredNextTags;

        // ── Traffic ───────────────────────────────────────────────────────────

        [Header("Traffic")]
        [Tooltip("Traffic behaviour during this sequence. Null = keep whatever is currently active.")]
        public TrafficBehaviourProfile trafficProfile;

        // ── Turn ──────────────────────────────────────────────────────────────

        [Header("Turn")]
        [Tooltip("True if one of this sequence's tile prefabs contains a TurnTrigger that fires mid-sequence.")]
        public bool containsTurn;

        [Tooltip("The road direction AFTER the turn. Only meaningful if containsTurn = true.")]
        public Vector3 postTurnDirection = Vector3.back;

        // ── Selection ─────────────────────────────────────────────────────────

        [Header("Selection")]
        [Tooltip("Relative probability weight. 0 = disabled. Higher = more common.")]
        public float weight = 1f;

        [Tooltip("How many other sequences must play before this one can repeat. 0 = no cooldown.")]
        public int repeatCooldown = 2;

        [Tooltip("Session time in seconds that must elapse before this sequence becomes eligible. 0 = available from session start.")]
        public float unlockAtTime = 0f;

        // ── Tile entry ────────────────────────────────────────────────────────

        [System.Serializable]
        public class TileEntry
        {
            [Tooltip("The tile prefab. Root pivot must be at back edge (Z=0), front edge at Z=tileLength.")]
            public GameObject prefab;

            [Tooltip("Allow normal traffic to spawn on this tile (via TrafficManager).")]
            public bool allowTraffic = true;

            [Tooltip("Allow potholes and rocks to spawn on this tile.")]
            public bool allowHazards = true;

            [Tooltip("Multiplier on hazard spawn density for this tile. 0=none, 1=normal, 2=double.")]
            [Range(0f, 3f)]
            public float hazardDensityMultiplier = 1f;
        }
    }
}
