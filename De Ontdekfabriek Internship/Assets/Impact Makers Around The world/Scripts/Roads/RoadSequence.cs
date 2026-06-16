using UnityEngine;
using KenyaScooter.Traffic;

namespace KenyaScooter.Roads
{
    /// <summary>
    /// A curated run of tiles telling a small environmental story (M23) — one zone of
    /// the Journey Arc (M24, Req §4.2). Selection is weighted random constrained by a
    /// tag grammar (forbidden/preferred previous tags), a tile-count cooldown and a
    /// session-time unlock gate. New locations are new sets of these assets (SC4).
    /// </summary>
    [CreateAssetMenu(menuName = "Kenya Scooter/Road Sequence", fileName = "Seq_")]
    public sealed class RoadSequence : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Zone name for debugging and analytics (HIGHLAND, ATHI_PLAINS, TSAVO...).")]
        public string zoneName = "HIGHLAND";
        [Tooltip("Context tags: drive ambient audio, hazard filters and the grammar (savanna, murram, township, tsavo_wildlife...).")]
        public string[] contextTags;

        [Header("Tiles")]
        [Tooltip("The tiles that make up this zone. By default they spawn in this order.")]
        public RoadTile[] tiles;
        [Tooltip("Remix the tile order each time this sequence plays, for variety (M23). " +
                 "Leave OFF when order matters — e.g. a warning-sign tile that must come before its hazard, or a hand-placed run of turns.")]
        public bool shuffleTiles = false;

        [Header("Selection (M23)")]
        public float weight = 1f;
        [Tooltip("Minimum tiles spawned before this sequence may repeat.")]
        public int cooldownTiles = 10;
        [Tooltip("Earliest session time (seconds) this sequence can appear — the Journey Arc gate (M24).")]
        public float unlockAtTime = 0f;
        [Tooltip("Never directly after a sequence carrying any of these tags.")]
        public string[] forbiddenPrevTags;
        [Tooltip("Weight is doubled after a sequence carrying any of these tags.")]
        public string[] preferredPrevTags;

        [Header("Gameplay modifiers")]
        [Tooltip("Traffic character while this sequence is active. Empty = spawner default (Req §5.2).")]
        public TrafficBehaviourProfile trafficProfileOverride;
        [Tooltip("Speed limit in km/h for the speeding tiers (Req §7.3). 0 = no limit.")]
        public float speedLimitKmh = 0f;

        public bool HasAnyTag(string[] queryTags)
        {
            if (queryTags == null || contextTags == null)
                return false;
            for (int i = 0; i < queryTags.Length; i++)
                for (int j = 0; j < contextTags.Length; j++)
                    if (queryTags[i] == contextTags[j])
                        return true;
            return false;
        }
    }
}
