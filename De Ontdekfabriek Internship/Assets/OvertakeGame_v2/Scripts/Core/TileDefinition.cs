using UnityEngine;

namespace OvertakeGame
{
    /// <summary>
    /// Attach to the root of every road tile prefab.
    /// Carries the metadata that RoadSequencer, managers, and spawn systems read at runtime.
    ///
    /// CURVE CONVENTION
    ///   curveAngle > 0 = bend LEFT  (road swings to player's left)
    ///   curveAngle < 0 = bend RIGHT
    ///   curveAngle = 0 = straight
    /// </summary>
    public class TileDefinition : MonoBehaviour
    {
        public enum DifficultyLevel { Clear, Low, Medium, High }

        [Header("Difficulty")]
        public DifficultyLevel difficulty = DifficultyLevel.Clear;

        [Header("Context")]
        [Tooltip("Environment tags used by RoadSequencer grammar and manager spawn filtering. " +
                 "Examples: savanna, murram, tsavo_wildlife, township, highland, construction.")]
        public string[] contextTags = new string[0];

        [Header("Road")]
        [Tooltip("Degrees this tile bends the road. Positive = left, negative = right, 0 = straight.")]
        [Range(-90f, 90f)]
        public float curveAngle = 0f;

        [Tooltip("Speed limit override (km/h) while the player is on this tile. 0 = inherit from RoadSequence.")]
        public float speedLimitOverride = 0f;

        // ── Spawn permission flags (read by manager pool systems) ──────────────

        [Header("Spawn Flags")]
        public bool allowTraffic  = true;
        public bool allowHazards  = true;
        [Range(0f, 3f)]
        public float hazardDensityMultiplier = 1f;

        // ── Helpers ────────────────────────────────────────────────────────────

        public bool IsClear   => difficulty == DifficultyLevel.Clear;
        public bool HasHazard => difficulty != DifficultyLevel.Clear;

        public bool HasTag(string tag)
        {
            foreach (var t in contextTags)
                if (t == tag) return true;
            return false;
        }

        // ── Editor visual ──────────────────────────────────────────────────────

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            UnityEditor.Handles.color = difficulty switch
            {
                DifficultyLevel.Low    => new Color(1f, 0.9f, 0.1f, 0.6f),
                DifficultyLevel.Medium => new Color(1f, 0.55f, 0f,  0.6f),
                DifficultyLevel.High   => new Color(1f, 0.1f, 0.1f, 0.7f),
                _                      => new Color(0.2f, 0.9f, 0.3f, 0.5f)
            };
            string tagStr = contextTags.Length > 0 ? string.Join(", ", contextTags) : "—";
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 2f,
                $"{difficulty} | [{tagStr}]\ncurve: {curveAngle:+0.#;-0.#;0}°  limit: {(speedLimitOverride > 0 ? speedLimitOverride + " km/h" : "inherit")}");
        }
#endif
    }
}
