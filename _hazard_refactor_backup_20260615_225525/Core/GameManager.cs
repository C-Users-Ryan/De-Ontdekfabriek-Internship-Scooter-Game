using UnityEngine;
using UnityEngine.InputSystem;
using KenyaScooter.Config;
using KenyaScooter.SafetyNet;
using KenyaScooter.Scoring;
using KenyaScooter.Session;
using KenyaScooter.Traffic;

namespace KenyaScooter.Core
{
    /// <summary>What happened after a hard crash was routed through GameManager (M15→M17).</summary>
    public enum CrashOutcome { Rewound, GameOver, Recovered }

    /// <summary>
    /// The only writer of the game state (Req §9.3) and the session orchestrator:
    /// Ready (tap to start) → Playing → [Rewinding] → AtCheckpoint / Finished /
    /// GameOver → Ready. External systems request transitions (HandleHardCrash,
    /// timer expiry, checkpoint arrival); they never set state. Also owns the turn's
    /// SessionStats and the once-per-turn commit into the relay group score (M27).
    /// gameOverOnCollision defaults OFF: the workshop completion guarantee (MDA
    /// Tension 1) — a hard crash without rewinds left recovers instead of ending.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }
        public static GameState State { get; private set; } = GameState.Ready;
        /// <summary>The scooter — for systems that need a distance to the player without a serialized reference.</summary>
        public static Transform Player => Instance != null ? Instance.player : null;

        [SerializeField] private RoadSideConfig roadSideConfig;
        [SerializeField] private Transform player;
        [SerializeField] private TimerManager timer;
        [SerializeField] private RewindSystem rewind;
        [SerializeField] private CheckpointController checkpoint;

        [Header("Workshop configuration (Req §16)")]
        [Tooltip("OFF (default): hard crashes without a rewind recover in place — every student finishes. ON: they end the session.")]
        public bool gameOverOnCollision = false;

        public SessionStats Stats { get; } = new SessionStats();

        private bool turnCommitted;
        private float threeFingerHold;

        private void Awake()
        {
            Instance = this;
            State = GameState.Ready;
            RoadSideConfig.Active = roadSideConfig;
            Application.targetFrameRate = 60; // iPad target (Req §17)
        }

        private void OnEnable()
        {
            timer.OnTimerExpired += HandleTimerExpired;
            GameEvents.CheckpointReached += CommitTurn;
            GameEvents.RewindCompleted += HandleRewindCompleted;

            GameEvents.OvertakeCompleted += CountOvertake;
            GameEvents.NearMiss += CountNearMiss;
            GameEvents.CollisionOccurred += CountCollision;
            GameEvents.HazardHit += CountHazard;
            GameEvents.WrongLaneTick += CountWrongLaneTick;
            GameEvents.SpeedingTick += CountSpeedingTick;
            GameEvents.StreakChanged += TrackBestStreak;
        }

        private void OnDisable()
        {
            timer.OnTimerExpired -= HandleTimerExpired;
            GameEvents.CheckpointReached -= CommitTurn;
            GameEvents.RewindCompleted -= HandleRewindCompleted;

            GameEvents.OvertakeCompleted -= CountOvertake;
            GameEvents.NearMiss -= CountNearMiss;
            GameEvents.CollisionOccurred -= CountCollision;
            GameEvents.HazardHit -= CountHazard;
            GameEvents.WrongLaneTick -= CountWrongLaneTick;
            GameEvents.SpeedingTick -= CountSpeedingTick;
            GameEvents.StreakChanged -= TrackBestStreak;
        }

        private void Update()
        {
            if (State == GameState.Ready && StartPressed())
                StartSession();

            CheckFacilitatorGesture();
        }

        // ---- Session flow -------------------------------------------------------------

        /// <summary>Begins a turn: resets every system, then opens play. Also wired to UI buttons.</summary>
        public void StartSession()
        {
            if (State == GameState.Playing || State == GameState.Rewinding)
                return;

            Stats.Reset();
            turnCommitted = false;
            RoadDirection.ResetToDefault();
            GameEvents.RaiseSessionReset();
            SetState(GameState.Playing);
            GameEvents.RaiseSessionStarted();
        }

        /// <summary>Back to the start overlay for the next student (relay, Req §1).</summary>
        public void PrepareNextTurn()
        {
            if (State != GameState.Playing && State != GameState.Rewinding)
                SetState(GameState.Ready);
        }

        /// <summary>Facilitator escape hatch (Req §16): abandon the current turn without commit.</summary>
        public void ForceReset() => SetState(GameState.Ready);

        /// <summary>
        /// Consequence routing for a hard crash (M15, M17, MDA Chain 4):
        /// rewind if available; otherwise game over only when explicitly enabled;
        /// otherwise recover in place at base speed — the session continues.
        /// </summary>
        public CrashOutcome HandleHardCrash()
        {
            if (rewind != null && rewind.CanRewind)
            {
                SetState(GameState.Rewinding);
                rewind.BeginRewind();
                return CrashOutcome.Rewound;
            }

            if (gameOverOnCollision)
            {
                EndTurn(GameState.GameOver);
                return CrashOutcome.GameOver;
            }

            WorldSpeed.Instance.SetCurrent(WorldSpeed.Instance.BaseSpeed);
            return CrashOutcome.Recovered;
        }

        private void HandleTimerExpired()
        {
            if (State != GameState.Playing)
                return;

            if (checkpoint != null && checkpoint.HasCheckpoint)
            {
                SetState(GameState.AtCheckpoint);
                checkpoint.Begin();
            }
            else
            {
                EndTurn(GameState.Finished);
            }
        }

        private void HandleRewindCompleted()
        {
            if (State == GameState.Rewinding)
                SetState(GameState.Playing);
        }

        private void EndTurn(GameState endState)
        {
            CommitTurn();
            SetState(endState);
        }

        private void CommitTurn()
        {
            if (turnCommitted)
                return;
            turnCommitted = true;

            Stats.FinalScore = ScoreManager.Instance != null ? ScoreManager.Instance.Score : 0;
            Stats.DistanceMetres = WorldSpeed.Instance.DistanceTravelled;
            if (GroupScoreManager.Instance != null)
                GroupScoreManager.Instance.CommitTurn(Stats.FinalScore);
        }

        private void SetState(GameState to)
        {
            if (State == to)
                return;
            GameState from = State;
            State = to;
            GameEvents.RaiseStateChanged(from, to);
        }

        // ---- Input --------------------------------------------------------------------

        private static bool StartPressed()
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
                return true;
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
                return true;
            if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
                return true;
            return false;
        }

        /// <summary>3-finger hold for 1.5 s restarts mid-session without menus (Req §16).</summary>
        private void CheckFacilitatorGesture()
        {
            Touchscreen screen = Touchscreen.current;
            if (screen == null)
                return;

            int pressed = 0;
            var touches = screen.touches;
            for (int i = 0; i < touches.Count; i++)
                if (touches[i].press.isPressed)
                    pressed++;

            if (pressed >= 3)
            {
                threeFingerHold += Time.deltaTime;
                if (threeFingerHold >= 1.5f)
                {
                    threeFingerHold = float.NegativeInfinity; // one-shot until released
                    ForceReset();
                }
            }
            else
            {
                threeFingerHold = 0f;
            }
        }

        // ---- Stats bookkeeping ----------------------------------------------------------

        private void CountOvertake(TrafficVehicle vehicle) => Stats.Overtakes++;
        private void CountNearMiss(TrafficVehicle vehicle) => Stats.NearMisses++;

        private void CountCollision(CollisionSeverity severity, float relativeKmh, Vector3 position, bool absorbed)
        {
            if (absorbed) Stats.GracesUsed++;
            else if (severity == CollisionSeverity.Hard) Stats.HardCollisions++;
            else Stats.LightCollisions++;
        }

        private void CountHazard(HazardKind kind, float playerKmh, Vector3 position)
        {
            switch (kind)
            {
                case HazardKind.Pothole: Stats.PotholeHits++; break;
                case HazardKind.Rock: Stats.RockHits++; break;
                default: Stats.SpeedBumpHits++; break;
            }
        }

        private void CountWrongLaneTick() => Stats.WrongLaneTicks++;
        private void CountSpeedingTick(int tier) => Stats.SpeedingTicks++;

        private void TrackBestStreak(int streakCount, float multiplier)
        {
            if (streakCount > Stats.BestStreak)
                Stats.BestStreak = streakCount;
        }
    }
}
