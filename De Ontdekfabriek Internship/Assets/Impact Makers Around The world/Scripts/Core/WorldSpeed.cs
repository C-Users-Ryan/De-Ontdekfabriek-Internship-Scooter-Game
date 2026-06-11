using UnityEngine;

namespace OvertakeGame
{
    /// <summary>
    /// Single source of truth for how fast the world moves toward the player.
    /// The player stays at fixed Z. Everything reads WorldSpeed.Instance.Current.
    /// Gas/brake from PlayerController calls SetInput() each frame.
    /// CheckpointManager brakes via OverrideSpeed() / ClearOverride().
    /// </summary>
    public class WorldSpeed : MonoBehaviour
    {
        public static WorldSpeed Instance { get; private set; }

        [Header("Speed Settings")]
        public float baseSpeed           = 10f;
        public float maxSpeed            = 30f;
        public float accelerationForce   = 15f;
        public float brakeForce          = 20f;
        public float naturalDeceleration = 5f;

        public float Current    { get; private set; }
        public float CurrentKmh => Current * 3.6f;

        private bool _overrideActive;
        private bool _inputThisFrame;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            Current  = baseSpeed;
        }

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
            if (!_inputThisFrame && !_overrideActive)
                Current = Mathf.MoveTowards(Current, baseSpeed, naturalDeceleration * Time.deltaTime);
            _inputThisFrame = false;

            // Feed speed ratio to AudioManager each frame
            if (AudioManager.I != null && maxSpeed > 0f)
                AudioManager.I.OnSpeedChanged(Current / maxSpeed);
        }

        public void OverrideSpeed(float speed) { _overrideActive = true;  Current = Mathf.Max(0f, speed); }
        public void ClearOverride()             { _overrideActive = false; Current = baseSpeed; }
    }
}