using UnityEngine;

namespace OvertakeGame
{
    /// <summary>
    /// Singleton tracking the road's current travel direction and steer axis.
    /// Default: travel = Vector3.back (-Z), steer = Vector3.right (+X).
    /// TurnTrigger calls SetDirection() when the player crosses a corner.
    ///
    /// All world-moving scripts MUST read Current / SteerAxis.
    /// Never hardcode Vector3.back or Vector3.right in movement code.
    /// </summary>
    public class RoadDirection : MonoBehaviour
    {
        public static RoadDirection Instance { get; private set; }

        [Header("Starting Direction")]
        [Tooltip("Initial travel direction. Normally Vector3.back (-Z).")]
        public Vector3 startingDirection = Vector3.back;

        /// <summary>Normalised direction world objects travel (toward the player).</summary>
        public static Vector3 Current { get; private set; } = Vector3.back;

        /// <summary>
        /// Lateral axis — perpendicular to Current on the XZ plane.
        /// If Current = -Z then SteerAxis = +X (right).
        /// If Current = -X then SteerAxis = +Z (forward in old frame).
        /// </summary>
        public static Vector3 SteerAxis { get; private set; } = Vector3.right;

        /// <summary>Cumulative Y-axis rotation since session start (degrees). Used by TurnTrigger to rotate the camera rig.</summary>
        public static float TotalYRotation { get; private set; } = 0f;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            SetDirection(startingDirection.normalized);
        }

        /// <summary>
        /// Change the active travel direction. Called by TurnTrigger.
        /// Direction should be axis-aligned (no diagonals).
        /// </summary>
        public static void SetDirection(Vector3 newDirection)
        {
            Current = newDirection.normalized;
            // Steer axis = rightward perpendicular to travel, staying in XZ plane
            SteerAxis = Vector3.Cross(Vector3.up, Current).normalized;
            TotalYRotation = Mathf.Atan2(Current.x, Current.z) * Mathf.Rad2Deg;
        }
    }
}
