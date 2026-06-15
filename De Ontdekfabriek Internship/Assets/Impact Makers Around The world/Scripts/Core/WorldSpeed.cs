using UnityEngine;
using KenyaScooter.Config;

namespace KenyaScooter.Core
{
    /// <summary>
    /// Single owner of the world scroll speed (M1, M2). The player never translates
    /// along the travel axis — every world object scrolls past at -Current instead.
    /// PlayerController is the only caller of ApplyThrottle; external systems that
    /// need to take over the speed (checkpoint braking, Req §3.2) use the override
    /// pipeline so gas/brake input is suspended rather than fought.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public sealed class WorldSpeed : MonoBehaviour
    {
        public const float MsToKmh = 3.6f;

        public static WorldSpeed Instance { get; private set; }

        [SerializeField] private ScooterConfig config;

        /// <summary>Current world scroll speed in m/s.</summary>
        public float Current { get; private set; }
        public float CurrentKmh => Current * MsToKmh;
        public float BaseSpeed => config.baseSpeed;
        public float MaxSpeed => config.maxSpeed;
        /// <summary>0 at standstill, 1 at max speed. Drives engine audio and speed lines.</summary>
        public float SpeedRatio => config.maxSpeed > 0f ? Mathf.Clamp01(Current / config.maxSpeed) : 0f;
        /// <summary>Metres of road scrolled past the player this turn.</summary>
        public float DistanceTravelled { get; private set; }

        private bool overrideActive;
        private float overrideTarget;
        private float overrideRate;

        private void Awake()
        {
            Instance = this;
            Current = config != null ? config.baseSpeed : 0f;
        }

        private void OnEnable() => GameEvents.SessionReset += HandleSessionReset;
        private void OnDisable() => GameEvents.SessionReset -= HandleSessionReset;

        private void Update()
        {
            GameState state = GameManager.State;
            if (state == GameState.Playing || state == GameState.AtCheckpoint)
                DistanceTravelled += Current * Time.deltaTime;
        }

        /// <summary>
        /// Integrates one frame of gas/brake input (M2): gas accelerates toward max,
        /// brake decelerates toward min, no input settles back to base speed.
        /// </summary>
        public void ApplyThrottle(float gas, float brake, float deltaTime)
        {
            if (overrideActive)
            {
                Current = Mathf.MoveTowards(Current, overrideTarget, overrideRate * deltaTime);
                return;
            }

            if (brake > 0.01f)
                Current = Mathf.MoveTowards(Current, config.minSpeed, config.brakeDeceleration * brake * deltaTime);
            else if (gas > 0.01f)
                Current = Mathf.MoveTowards(Current, config.maxSpeed, config.acceleration * gas * deltaTime);
            else
                Current = Mathf.MoveTowards(Current, config.baseSpeed, config.naturalDeceleration * deltaTime);
        }

        /// <summary>Suspends the gas/brake pipeline and steers speed toward a target (checkpoint braking).</summary>
        public void BeginOverride(float targetSpeed, float rate)
        {
            overrideActive = true;
            overrideTarget = targetSpeed;
            overrideRate = rate;
        }

        public void EndOverride() => overrideActive = false;

        /// <summary>Direct restore used by RewindSystem when applying a snapshot (M17).</summary>
        public void SetCurrent(float speed) => Current = Mathf.Clamp(speed, 0f, config.maxSpeed);

        private void HandleSessionReset()
        {
            overrideActive = false;
            Current = config.baseSpeed;
            DistanceTravelled = 0f;
        }
    }
}
