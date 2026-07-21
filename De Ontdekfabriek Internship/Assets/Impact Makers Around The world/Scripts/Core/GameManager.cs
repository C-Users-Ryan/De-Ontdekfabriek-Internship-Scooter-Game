using UnityEngine;
using UnityEngine.InputSystem;
using KenyaScooter.Config;
using KenyaScooter.SafetyNet;
using KenyaScooter.Scoring;
using KenyaScooter.Session;
using KenyaScooter.Settings; // ConfigLocator — the live SessionConfig, for the endless-mode life count
using KenyaScooter.Traffic;

namespace KenyaScooter.Core
{
    /// <summary>What happened after a hard crash was routed through GameManager (M15→M17).</summary>
    public enum CrashOutcome { Rewound, GameOver, Recovered }

    /// <summary>
    /// The only writer of the game state (Req §9.3), and the session orchestrator:
    /// Ready (tap to start) → Playing → [Rewinding] → AtCheckpoint / Finished / GameOver → Ready.
    /// Other systems ask for transitions (a hard crash, the timer expiring, reaching the checkpoint);
    /// they never set the state themselves. It also holds the turn's SessionStats and commits the
    /// score into the relay group total once per turn (M27). gameOverOnCollision is OFF by default —
    /// the workshop promise (MDA Tension 1) that every student finishes, so a hard crash with no
    /// rewinds left recovers in place instead of ending the run.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }
        public static GameState State { get; private set; } = GameState.Ready;
        /// <summary>The scooter — for systems that need a distance to the player without a serialized reference.</summary>
        public static Transform Player => Instance != null ? Instance.player : null;

        /// <summary>Gate for the legacy "tap anywhere to start" affordance. Now DEFAULTS FALSE (2026-07-05): the
        /// title screen has an explicit ANZA! button, so tapping anywhere else must NOT start the game. The menu's
        /// Show() only ever enables it for the null (in-game) case, where the Ready guard makes it moot — so
        /// tap-to-start is effectively retired while the framing screens are up. Defaulting false also closes the
        /// boot-window race where <see cref="StartPressed"/> (raw touch, not a UI button) could fire before the
        /// menu's first Show() ran. (Earlier fix 2026-06-24 stopped a stray tap starting the game as TEAM SIMBA;
        /// this removes the last path where a background tap advanced the title.)</summary>
        public static bool AllowTapToStart { get; set; } = false;

        /// <summary>Which session shape is running (GroupRelay = the timed team relay, the default; Endless = the
        /// solo Vrij-rijden arcade run with lives). Static like <see cref="AllowTapToStart"/>, set once before
        /// StartSession by the menu / attract mode. It MUST be set at every entry point (the menu sets GroupRelay
        /// on the group path and Endless on the Vrij-rijden button; attract forces GroupRelay) so a previous
        /// endless run can never leave the next group session's timer disabled.</summary>
        public static GameMode Mode { get; set; } = GameMode.GroupRelay;

        /// <summary>Endless mode: lives left this run. Reset on StartSession; a hard crash decrements it; at 0 the run ends.</summary>
        public static int LivesRemaining { get; private set; }
        /// <summary>The life count an endless run STARTED with, so the HUD's lives gauge knows what "full" is. Set on StartSession.</summary>
        public static int EndlessMaxLives { get; private set; } = 3;
        private const int DefaultEndlessLives = 3;

        /// <summary>PlayerPrefs key for the facilitator's persisted choice of whether the game runs in Endless
        /// (Vrij rijden) mode. Set ONLY from the settings menu / a Profiel (behind the access code), never from a
        /// child-facing button on the title screen.</summary>
        public const string EndlessPrefKey = "ksg.endlessMode";

        /// <summary>The facilitator's saved choice: does pressing ANZA start a solo Endless run (lives, no team,
        /// no relay) instead of the timed class relay? The menu reads this when a run begins and sets
        /// <see cref="Mode"/> accordingly. Distinct from <see cref="Mode"/>, which is the live per-run value.</summary>
        public static bool EndlessSelected
        {
            get => PlayerPrefs.GetInt(EndlessPrefKey, 0) == 1;
            set { PlayerPrefs.SetInt(EndlessPrefKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        [SerializeField] private RoadSideConfig roadSideConfig;
        [SerializeField] private Transform player;
        [SerializeField] private TimerManager timer;
        [SerializeField] private RewindSystem rewind;
        [SerializeField] private CheckpointController checkpoint;

        [Header("Workshop configuration (Req §16)")]
        [Tooltip("OFF (default): hard crashes without a rewind recover in place — every student finishes. ON: they end the session.")]
        public bool gameOverOnCollision = false;

        /// <summary>PlayerPrefs key the facilitator "Stoppen bij zware botsing" setting writes; read on Awake so the
        /// choice applies on startup with no scene wiring (the settings menu cannot edit a scene component at boot).</summary>
        public const string GameOverPrefKey = "ksg.gameOverOnCollision";

        public SessionStats Stats { get; } = new SessionStats();

        private bool turnCommitted;
        private float threeFingerHold;

        private void Awake()
        {
            Instance = this;
            State = GameState.Ready;
            RoadSideConfig.Active = roadSideConfig;
            // Apply the facilitator's saved "stop on a hard crash" choice (defaults to the inspector value).
            gameOverOnCollision = PlayerPrefs.GetInt(GameOverPrefKey, gameOverOnCollision ? 1 : 0) == 1;
            Application.targetFrameRate = 60; // iPad target (Req §17)
            // Kiosk: the tablet must never dim or sleep mid-workshop — Android's screen timeout would
            // otherwise blank the attract/title screen whenever nobody touches it for a few minutes (Req §16).
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
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
            if (State == GameState.Ready && AllowTapToStart && StartPressed())
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
            if (Mode == GameMode.Endless)
            {
                LivesRemaining = ConfigLocator.Session != null && ConfigLocator.Session.endlessLives > 0
                    ? ConfigLocator.Session.endlessLives : DefaultEndlessLives;
                EndlessMaxLives = LivesRemaining;
                GameEvents.RaiseLivesChanged(LivesRemaining);
            }
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
            // Endless (Vrij rijden): a hard crash costs a LIFE instead of rewinding; the run ends only when the
            // last life is spent. Light/medium hits are still soaked by grace upstream, so only real crashes cost.
            if (Mode == GameMode.Endless)
            {
                LivesRemaining = Mathf.Max(0, LivesRemaining - 1);
                GameEvents.RaiseLivesChanged(LivesRemaining);
                if (LivesRemaining <= 0)
                {
                    EndTurn(GameState.GameOver);
                    return CrashOutcome.GameOver;
                }
                WorldSpeed.Instance.SetCurrent(WorldSpeed.Instance.BaseSpeed);
                return CrashOutcome.Recovered;
            }

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

        /// <summary>
        /// Freezes this turn's final score into <see cref="SessionStats"/> and adds it to the group total —
        /// exactly once per turn (guarded). Public and idempotent so the relay screen can force the commit before
        /// it reads FinalScore / the group total: on the checkpoint-relay path this and the screen both fire on
        /// CheckpointReached and the event does not guarantee this runs first, which otherwise let the screen read
        /// the score before it was written and show "+0" on a turn that earned points.
        /// </summary>
        public void CommitTurn()
        {
            if (turnCommitted)
                return;
            turnCommitted = true;

            Stats.FinalScore = ScoreManager.Instance != null ? ScoreManager.Instance.Score : 0;
            Stats.DistanceMetres = WorldSpeed.Instance.DistanceTravelled;
            // Endless is a solo run — it must never add to the shared class relay total (the leaderboard commit
            // itself lives in the menu's group flow, which endless never enters).
            if (Mode == GameMode.GroupRelay && GroupScoreManager.Instance != null)
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
            // Exclude Escape: Android delivers the hardware BACK button as Escape, so a BACK press on an unpinned
            // tablet would otherwise start a game from the title screen (the interaction documented in KioskLock.cs).
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame
                && !Keyboard.current.escapeKey.wasPressedThisFrame)
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

        private void CountHazard(HazardSpawnConfig definition, float playerKmh, Vector3 position)
        {
            if (definition == null)
                return;
            switch (definition.response)
            {
                case HazardResponse.SurfaceDefect: Stats.PotholeHits++; break;
                case HazardResponse.StaticObstacle: Stats.RockHits++; break;
                default: Stats.SpeedBumpHits++; break; // ForcedSlowdown
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
