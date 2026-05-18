using UnityEngine;

namespace OvertakeGame
{
    /// <summary>
    /// Single source of truth for how fast the world moves toward the player.
    /// The player stays at a fixed Z. Everything else reads this value and
    /// moves in -Z at WorldSpeed.Current each frame.
    ///
    /// Gas/brake input from PlayerController writes here.
    /// Traffic, potholes, and road tiles all read from here.
    /// </summary>
    public class WorldSpeed : MonoBehaviour
    {
        public static WorldSpeed Instance { get; private set; }

        [Header("Speed Settings")]
        [Tooltip("Speed the world moves when no input is held (m/s).")]
        public float baseSpeed          = 10f;
        [Tooltip("Maximum world speed with gas held (m/s).")]
        public float maxSpeed           = 30f;
        [Tooltip("Acceleration rate when gas is held.")]
        public float accelerationForce  = 15f;
        [Tooltip("Deceleration rate when brake is held.")]
        public float brakeForce         = 20f;
        [Tooltip("Rate of return to base speed when no input.")]
        public float naturalDeceleration = 5f;

        /// <summary>Current world movement speed in m/s. Read by all world objects.</summary>
        public float Current { get; private set; }

        /// <summary>Speed in km/h for the speedometer.</summary>
        public float CurrentKmh => Current * 3.6f;

        private bool _overrideActive;
        private float _overrideValue;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            Current  = baseSpeed;
        }

        /// <summary>Called by PlayerController with gas/brake input each frame.</summary>
        public void SetInput(bool gas, bool brake)
        {
            if (_overrideActive) return;

            if (gas)
                Current = Mathf.MoveTowards(Current, maxSpeed,   accelerationForce   * Time.deltaTime);
            else if (brake)
                Current = Mathf.MoveTowards(Current, 0f,         brakeForce          * Time.deltaTime);
            else
                Current = Mathf.MoveTowards(Current, baseSpeed,  naturalDeceleration * Time.deltaTime);
        }

        /// <summary>Used by CheckpointManager to brake the world to a stop.</summary>
        public void OverrideSpeed(float speed)
        {
            _overrideActive = true;
            Current         = Mathf.Max(0f, speed);
        }

        public void ClearOverride()
        {
            _overrideActive = false;
            Current         = baseSpeed;
        }
    }
}
