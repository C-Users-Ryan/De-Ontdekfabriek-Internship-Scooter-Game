using UnityEngine;

namespace OvertakeGame
{
    /// <summary>
    /// Attach to the root of every road tile prefab.
    /// Describes this tile's difficulty, hazard type, and how it bends the road.
    ///
    /// CURVE CONVENTION
    ///   curveAngle > 0  = bend LEFT  (road swings to player's left)
    ///   curveAngle < 0  = bend RIGHT
    ///   curveAngle = 0  = straight
    ///
    /// The RoadTileRecycler reads these fields when placing and queuing tiles.
    /// </summary>
    public class TileDefinition : MonoBehaviour
    {
        // ── Difficulty ──────────────────────────────────────────────────────────

        public enum DifficultyLevel { Clear, Low, Medium, High }

        [Header("Difficulty")]
        public DifficultyLevel difficulty = DifficultyLevel.Clear;

        // ── Hazard taxonomy ─────────────────────────────────────────────────────

        public enum HazardCategory
        {
            None,
            RoadSurface,    // potholes, speed bumps, rocks on road
            Roadside,       // rocks on verge (visual only, no collision)
            Traffic,        // matatus, lorries, boda bodas
            Pedestrian,     // market street, crossings
            Wildlife,       // elephant, giraffe, zebra, impala
            WildlifeWarning // warning sign only — place 1–2 tiles before wildlife tile
        }

        [Header("Hazard")]
        public HazardCategory hazardCategory = HazardCategory.None;

        [Tooltip("Human-readable label shown in editor and debug HUD (e.g. 'Two potholes', 'Slow matatu').")]
        public string hazardLabel = "";

        // ── Curve / direction ───────────────────────────────────────────────────

        [Header("Road Curve")]
        [Range(-45f, 45f)]
        [Tooltip("Degrees this tile adds to the world heading. +ve = left, -ve = right, 0 = straight.")]
        public float curveAngle = 0f;

        // ── Helper properties ───────────────────────────────────────────────────

        public bool IsClear     => difficulty == DifficultyLevel.Clear;
        public bool IsLow       => difficulty == DifficultyLevel.Low;
        public bool IsMedium    => difficulty == DifficultyLevel.Medium;
        public bool IsHigh      => difficulty == DifficultyLevel.High;
        public bool HasHazard   => difficulty != DifficultyLevel.Clear;

        // ── Editor visual ───────────────────────────────────────────────────────

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Color c = difficulty switch
            {
                DifficultyLevel.Clear  => new Color(0.2f, 0.9f, 0.3f, 0.6f),
                DifficultyLevel.Low    => new Color(1f,   0.9f, 0.1f, 0.6f),
                DifficultyLevel.Medium => new Color(1f,   0.55f,0f,   0.6f),
                DifficultyLevel.High   => new Color(1f,   0.1f, 0.1f, 0.7f),
                _                     => Color.white
            };
            UnityEditor.Handles.color = c;

            // Show difficulty label above tile
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 2f,
                $"{difficulty}  |  {hazardCategory}{(hazardLabel.Length > 0 ? " — " + hazardLabel : "")}\ncurve: {curveAngle:+0.#;-0.#;0}°");
        }
#endif
    }
}
