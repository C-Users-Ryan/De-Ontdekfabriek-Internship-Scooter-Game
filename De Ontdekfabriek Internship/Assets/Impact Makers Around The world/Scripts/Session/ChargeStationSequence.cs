using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;
using KenyaScooter.Player;
using KenyaScooter.Settings;

namespace KenyaScooter.Session
{
    /// <summary>
    /// The diegetic charge-station relay, the loop beat the client asked for. When the turn timer runs out and
    /// the bike brakes to a stop at the charge station, instead of cutting straight to the score screen the bike
    /// LOSES control, eases off the road INTO the bay, CHARGES for a short positive beat (the HUD battery refills
    /// and the score stays on screen), THEN the hand-off screen appears. When the next student starts their turn,
    /// the bike PULLS OUT of the bay and the world ramps back up to cruising speed before control returns.
    ///
    /// Sessions also BEGIN at the bay: whenever the game sits in Ready with the relay on, the bike waits parked
    /// at the station with the world stopped, so the very FIRST turn (and every restart) pulls out from a
    /// standstill on the start input instead of starting mid-road at cruising speed.
    ///
    /// Additive, self-bootstrapping, and only active when <c>SessionConfig.cinematicRelay</c> is on
    /// (CheckpointController checks that and calls <see cref="PlayArrival"/>). With it off, the proven instant
    /// relay runs and this component never touches anything, so the liked straight-road defence build is safe.
    ///
    /// Architecture note: the bike is pinned at the origin and the world scrolls past it, so it cannot drive
    /// forward into a station ahead. The pull-in is therefore a slide to the near shoulder plus a yaw toward the
    /// bay, with the world held at a stop, which reads as pulling over to a roadside charging bay. The pose runs
    /// through <see cref="PlayerController"/>'s scripted-pose mode, so it never fights player input.
    /// </summary>
    public sealed class ChargeStationSequence : MonoBehaviour
    {
        public static ChargeStationSequence Instance { get; private set; }

        private enum Phase { Idle, PullIn, Charging, Parked, PullOut }
        private Phase phase = Phase.Idle;
        private float timer;
        private PlayerController playerCached;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null || FindObjectOfType<ChargeStationSequence>() != null)
                return;
            var go = new GameObject("ChargeStationSequence (auto)");
            go.AddComponent<ChargeStationSequence>();
            DontDestroyOnLoad(go);
        }

        private void Awake() => Instance = this;

        // The initial Ready state is set before this component exists, so StateChanged never fires for it:
        // park explicitly once the scene is up.
        private void Start() => TryParkAtReady();

        private void OnEnable()
        {
            GameEvents.SessionStarted += OnSessionStarted;
            GameEvents.SessionReset += OnSessionReset;
            GameEvents.StateChanged += OnStateChanged;
        }

        private void OnDisable()
        {
            GameEvents.SessionStarted -= OnSessionStarted;
            GameEvents.SessionReset -= OnSessionReset;
            GameEvents.StateChanged -= OnStateChanged;
        }

        // Back to Ready (game over, finished, or a facilitator reset): put the bike at the bay for the next start.
        private void OnStateChanged(GameState from, GameState to)
        {
            if (to == GameState.Ready)
                TryParkAtReady();
        }

        private SessionConfig Config => ConfigLocator.Session;

        private PlayerController Player
        {
            get
            {
                if (playerCached == null)
                    playerCached = FindObjectOfType<PlayerController>();
                return playerCached;
            }
        }

        /// <summary>Called by CheckpointController once the world has braked to a stop at the station.</summary>
        public void PlayArrival()
        {
            SessionConfig cfg = Config;
            PlayerController player = Player;
            // WorldSpeed too (used just below): degrade instead of NRE-ing in a bare test scene without it.
            if (cfg == null || player == null || WorldSpeed.Instance == null)
            {
                // Cannot choreograph without the config, the bike or the world speed — fall back to the instant relay.
                GameEvents.RaiseCheckpointReached();
                phase = Phase.Idle;
                return;
            }

            // Hold the world stopped while parked, and ease the bike into the bay on the near (driving-side) shoulder.
            WorldSpeed.Instance.SetCurrent(0f);
            WorldSpeed.Instance.BeginOverride(0f, 1000f);

            float ownSide = RoadSideConfig.Active != null ? RoadSideConfig.Active.OwnSide : -1f;
            float bayLateral = ownSide * Mathf.Abs(cfg.bayLateral);
            float bayYaw = ownSide * Mathf.Abs(cfg.bayYaw);
            float t = Mathf.Max(0.1f, cfg.pullInSeconds);
            float lateralRate = Mathf.Abs(bayLateral - player.CurrentLateral) / t;
            player.BeginScriptedPose(bayLateral, bayYaw, lateralRate, Mathf.Abs(bayYaw) / t);

            phase = Phase.PullIn;
            timer = 0f;
        }

        /// <summary>
        /// Sessions start AT the station: whenever the game sits in Ready with the cinematic relay on, snap the
        /// bike into the parked bay pose and hold the world stopped. The phase becomes Parked, so the first
        /// SessionStarted plays the exact same pull-out a relay hand-off gets — the turn begins from a standstill
        /// at the charge bay instead of mid-road at cruising speed. The snap (not an eased pull-in) is deliberate:
        /// in Ready the player Update does not tick, and the pose sits behind the menu screens anyway.
        /// </summary>
        private void TryParkAtReady()
        {
            if (phase != Phase.Idle || GameManager.State != GameState.Ready)
                return;
            SessionConfig cfg = Config;
            PlayerController player = Player;
            if (cfg == null || !cfg.cinematicRelay || player == null)
                return;

            float ownSide = RoadSideConfig.Active != null ? RoadSideConfig.Active.OwnSide : -1f;
            player.SnapScriptedPose(ownSide * Mathf.Abs(cfg.bayLateral), ownSide * Mathf.Abs(cfg.bayYaw));
            if (WorldSpeed.Instance != null)
            {
                WorldSpeed.Instance.SetCurrent(0f);
                WorldSpeed.Instance.BeginOverride(0f, 1000f); // held stopped, exactly like the relay park
            }
            phase = Phase.Parked;
        }

        private void Update()
        {
            // Fail-safe (2026-07-05): a scripted pose that survives into normal play with NO cinematic phase
            // owning it is orphaned (an interrupted pull-out, a state change mid-park, a missed hand-off).
            // Without this the bike rides permanently yawed at the shoulder while steering/collisions assume
            // the normal pose — "the hitbox and the scooter are disaligned". Release it the moment we see it.
            if (phase == Phase.Idle && GameManager.State == GameState.Playing)
            {
                PlayerController orphaned = Player;
                if (orphaned != null && orphaned.Scripted)
                    orphaned.EndScriptedPose();
                return;
            }

            switch (phase)
            {
                case Phase.Idle:
                    return;
                case Phase.Parked:
                    // Parked waits for the next student's SessionStarted to run the pull-out. If the game is
                    // somehow already Playing while we still sit Parked, that trigger was missed — run the
                    // pull-out now instead of holding the bike (and the world speed) hostage forever.
                    if (GameManager.State == GameState.Playing)
                        OnSessionStarted();
                    return;
                case Phase.PullIn:
                case Phase.Charging:
                    if (GameManager.State != GameState.AtCheckpoint) { Abort(); return; } // force-reset out from under us
                    break;
                case Phase.PullOut:
                    if (GameManager.State != GameState.Playing) { Abort(); return; } // a crash/rewind interrupted the pull-out
                    break;
            }

            SessionConfig cfg = Config;
            if (cfg == null) { Abort(); return; }
            timer += Time.deltaTime;

            switch (phase)
            {
                case Phase.PullIn:
                    if (timer >= cfg.pullInSeconds)
                    {
                        phase = Phase.Charging;
                        timer = 0f;
                        GameEvents.RaiseChargingStarted(); // HUD battery refill + any charge glow
                    }
                    break;

                case Phase.Charging:
                    if (timer >= cfg.chargeSeconds)
                    {
                        // Charge done: commit the turn and show the hand-off screen now (CheckpointReached), with
                        // the bike kept parked at the bay until the next student starts.
                        phase = Phase.Parked;
                        GameEvents.RaiseCheckpointReached();
                    }
                    break;

                case Phase.PullOut:
                    if (timer >= cfg.pullOutSeconds)
                    {
                        EndScripted();
                        phase = Phase.Idle;
                    }
                    break;
            }
        }

        // The next student has started: pull the bike out of the bay and ramp the world back up to cruise.
        private void OnSessionStarted()
        {
            if (phase != Phase.Parked)
                return; // not mid-cinematic (or cinematic off) — leave the normal start untouched

            SessionConfig cfg = Config;
            PlayerController player = Player;
            // WorldSpeed too (driven just below): bail cleanly rather than NRE-ing in a bare test scene without it.
            if (cfg == null || player == null || WorldSpeed.Instance == null) { EndScripted(); phase = Phase.Idle; return; }

            // Start the new turn stopped and ramp up, so the bike pulls away instead of snapping to speed.
            WorldSpeed.Instance.SetCurrent(0f);
            WorldSpeed.Instance.BeginOverride(WorldSpeed.Instance.BaseSpeed, Mathf.Max(0.5f, cfg.pullOutAccel));

            float ownLane = RoadSideConfig.Active != null ? RoadSideConfig.Active.OwnLaneCentre : 0f;
            float t = Mathf.Max(0.1f, cfg.pullOutSeconds);
            float lateralRate = Mathf.Abs(player.CurrentLateral - ownLane) / t;
            player.BeginScriptedPose(ownLane, 0f, lateralRate, Mathf.Abs(cfg.bayYaw) / t);

            phase = Phase.PullOut;
            timer = 0f;
        }

        private void OnSessionReset()
        {
            // Parked: the imminent SessionStarted runs the pull-out, so leave it. Idle: nothing to do. Any other
            // phase means the turn is being reset out from under the cinematic (e.g. a facilitator force-reset).
            if (phase == Phase.Parked || phase == Phase.Idle)
                return;
            Abort();
        }

        private void Abort()
        {
            EndScripted();
            phase = Phase.Idle;
            // If the abort landed us back in Ready (a facilitator force-reset mid-cinematic), park for the
            // next start rather than leaving the bike stranded mid-road.
            TryParkAtReady();
        }

        private void EndScripted()
        {
            PlayerController player = Player;
            if (player != null) player.EndScriptedPose();
            if (WorldSpeed.Instance != null) WorldSpeed.Instance.EndOverride();
        }
    }
}
