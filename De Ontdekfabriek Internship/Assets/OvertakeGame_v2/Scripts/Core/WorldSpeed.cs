using UnityEngine;

namespace OvertakeGame
{
    /// <summary>
    /// Single source of truth for how fast the world scrolls toward the player.
    /// PlayerController calls SetInput(gas, brake) every Update frame.
    /// CheckpointManager can freeze speed via OverrideSpeed() / ClearOverride().
    /// AudioManager is notified in LateUpdate so it always gets the final speed for that frame.
    /// </summary>
    public class WorldSpeed : MonoBehaviour
    {
        public static WorldSpeed Instance { get; private set; }

        [Header("Speed (m/s)")]
        public float baseSpeed           = 10f;
        public float maxSpeed            = 30f;
        public float accelerationForce   = 15f;
        public float brakeForce          = 20f;
        public float naturalDeceleration =  5f;

        /// <summary>Current world speed in m/s.</summary>
        public float Current    { get; private set; }
        /// <summary>Current world speed in km/h.</summary>
        public float CurrentKmh => Current * 3.6f;

        private bool  _overrideActive;
        private bool  _inputThisFrame;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Current  = baseSpeed;
        }

        /// <summary>
        /// Called by PlayerController each Update. Drives acceleration / braking.
        /// Ignored while an override is active.
        /// </summary>
        public void SetInput(bool gas, bool brake)
        {
            _inputThisFrame = true;
            if (_overrideActive) return;

            if (gas)
                Current = Mathf.MoveTowards(Current, maxSpeed,  accelerationForce   * Time.deltaTime);
            else if (brake)
                Current = Mathf.MoveTowards(Current, 0f,        brakeForce          * Time.deltaTime);
            else
                Current = Mathf.MoveTowards(Current, baseSpeed, naturalDeceleration * Time.deltaTime);
        }

        void LateUpdate()
        {
            // Apply natural deceleration when no input arrived this frame
            if (!_inputThisFrame && !_overrideActive)
                Current = Mathf.MoveTowards(Current, baseSpeed, naturalDeceleration * Time.deltaTime);

            _inputThisFrame = false;

            // Notify AudioManager last, after speed is settled for this frame
            if (AudioManager.I != null && maxSpeed > 0f)
                AudioManager.I.OnSpeedChanged(Current / maxSpeed);
        }

        /// <summary>Locks speed to a fixed value (e.g. during checkpoint braking).</summary>
        public void OverrideSpeed(float speed)
        {
            _overrideActive = true;
            Current         = Mathf.Max(0f, speed);
        }

        /// <summary>Releases a speed override and returns to player-driven speed.</summary>
        public void ClearOverride()
        {
            _overrideActive = false;
        }
    }
}
