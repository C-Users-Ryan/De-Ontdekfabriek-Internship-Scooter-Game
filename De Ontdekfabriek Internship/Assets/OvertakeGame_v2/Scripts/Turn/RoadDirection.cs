using UnityEngine;

namespace OvertakeGame
{
    /// <summary>
    /// Singleton that tracks the current road direction as a world-space Vector3.
    /// All world objects (tiles, traffic, potholes, rocks) read from this instead
    /// of hardcoding Vector3.back.
    ///
    /// Starting direction is Vector3.back (-Z) — the default.
    /// At a right turn it becomes Vector3.left  (-X).
    /// At a left  turn it becomes Vector3.right (+X).
    /// At a U-turn it becomes Vector3.forward  (+Z).
    ///
    /// This is the only script you need to change to add a new axis of travel.
    ///
    /// ── HOW TO MIGRATE EXISTING SCRIPTS ─────────────────────────────────────
    /// Anywhere you currently write:
    ///   transform.Translate(Vector3.back * speed * Time.deltaTime, Space.World);
    /// Replace with:
    ///   transform.Translate(RoadDirection.Current * speed * Time.deltaTime, Space.World);
    ///
    /// That one-line change is all each world object needs.
    /// </summary>
    public class RoadDirection : MonoBehaviour
    {
        public static RoadDirection Instance { get; private set; }

        [Header("Starting Direction")]
        [Tooltip("The initial travel direction. Usually Vector3.back (-Z).")]
        public Vector3 startingDirection = Vector3.back;

        /// <summary>
        /// The current normalised direction the world moves in.
        /// All world objects translate by (Current * WorldSpeed.Current * dt).
        /// </summary>
        public static Vector3 Current { get; private set; } = Vector3.back;

        /// <summary>
        /// The perpendicular axis the player steers on.
        /// If travel is -Z, steering is on X.
        /// If travel is -X, steering is on Z.
        /// </summary>
        public static Vector3 SteerpAxis { get; private set; } = Vector3.right;

        /// <summary>Cumulative Y rotation applied so far (for camera tracking).</summary>
        public static float   TotalYRotation { get; private set; } = 0f;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            SetDirection(startingDirection.normalized);
        }

        /// <summary>
        /// Change travel direction. Called by TurnTrigger when the player
        /// reaches a corner. Direction must be axis-aligned (no diagonals).
        /// </summary>
        public static void SetDirection(Vector3 newDirection)
        {
            Current = newDirection.normalized;

            // Steer axis is always perpendicular to travel in the XZ plane
            // Cross product with up gives the right-hand perpendicular
            SteerpAxis = Vector3.Cross(Vector3.up, Current).normalized;

            // Track cumulative rotation for camera / player visual alignment
            float newYRot = Mathf.Atan2(Current.x, Current.z) * Mathf.Rad2Deg;
            TotalYRotation = newYRot;
        }
    }
}
