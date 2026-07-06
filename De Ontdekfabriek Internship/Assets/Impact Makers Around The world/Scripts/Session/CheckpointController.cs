using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;
using KenyaScooter.Roads;

namespace KenyaScooter.Session
{
    /// <summary>
    /// The relay checkpoint (Req §9.2): a few seconds BEFORE the timer runs out (SessionConfig.checkpointLeadSeconds)
    /// the charge-station tile is woven into the road at the far draw horizon, in the haze, with no cap or release
    /// (RoadSequencer.SpawnCheckpoint(0)) — so nothing on the visible road changes and the player watches the station
    /// emerge from the distance and grow as they ride up to it, rather than it popping in at T=0. The world keeps
    /// scrolling normally, so the player rides toward it as part of the road. The remaining distance is measured
    /// ALONG the road (arc), so it stays correct through any turns between the player and the station; close to the
    /// tile's stop marker a speed override brakes the world to a stop (start distance from v²/2a, so it works from
    /// any speed) — braking runs even while still Playing so a fast player can't overrun the capped road end, but the
    /// turn only ENDS once the time is up. Then CheckpointReached fires, GameManager commits the turn, and the
    /// checkpoint screen takes over.
    /// Separate from GameManager by design (Q1): session states live in one place, checkpoint
    /// choreography in another.
    /// </summary>
    public sealed class CheckpointController : MonoBehaviour
    {
        [SerializeField] private SessionConfig config;

        /// <summary>True when a checkpoint tile is configured on the sequencer; otherwise the timer ends the session.</summary>
        public bool HasCheckpoint =>
            RoadSequencer.Instance != null && RoadSequencer.Instance.HasCheckpointTile;

        private bool braking;
        private bool reached;
        private bool armed;   // stopArc has been captured for the current checkpoint tile
        private bool woven;   // the station has been laid into the road (the lead-time weave, or Begin's fallback)
        private float stopArc; // the road-metre the world should brake to a stop on

        private void OnEnable() => GameEvents.SessionReset += HandleSessionReset;
        private void OnDisable() => GameEvents.SessionReset -= HandleSessionReset;

        /// <summary>Called by GameManager when the timer expires and the state becomes AtCheckpoint.</summary>
        public void Begin()
        {
            braking = false;
            reached = false;
            armed = false;
            // Normally the station was already woven in by the lead-time pass below, so this no-ops. If it wasn't
            // (e.g. a session shorter than the lead time), weave it now — still at the draw horizon, still no pop.
            if (RoadSequencer.Instance != null)
                RoadSequencer.Instance.SpawnCheckpoint(0f);
        }

        private void Update()
        {
            RoadSequencer seq = RoadSequencer.Instance;

            // Weave the charge station into the road a few seconds BEFORE the timer runs out, at the far build
            // horizon (in the haze), with no cap/release — so nothing on the visible road changes and the player
            // watches the station emerge from the distance and grow as they ride up to it, instead of it popping
            // into view when the time hits 0. Runs once, while still Playing.
            if (!woven && seq != null && config != null && HasCheckpoint
                && GameManager.State == GameState.Playing
                && TimerManager.Instance != null
                && TimerManager.Instance.Remaining <= config.checkpointLeadSeconds)
            {
                seq.SpawnCheckpoint(0f);
                woven = true;
            }

            if (reached)
                return;

            RoadTile tile = seq != null ? seq.ActiveCheckpointTile : null;
            if (tile == null)
                return; // no station in the road yet (before the lead-time weave)

            // Run the arrival braking once the station is in the road — in Playing too, not only AtCheckpoint — so
            // a fast player can never overrun it and ride off the (now capped) road end before the timer expires.
            // The turn only actually ENDS once the time is up (AtCheckpoint); a player who reaches the bay early
            // just brakes to a stop and waits there.
            bool timeUp = GameManager.State == GameState.AtCheckpoint;
            if (!timeUp && GameManager.State != GameState.Playing)
                return; // Rewinding / Ready — leave the world alone

            // The road-metre the world should stop on: the arc of the tile's stop point (purple ball). Cached
            // once — the nearest-point search behind StopRunDistance is not worth re-running each frame, and the
            // tile's arc is fixed the moment it is appended to the chain.
            if (!armed)
            {
                stopArc = tile.StartArc + tile.StopRunDistance;
                armed = true;
            }

            float speed = WorldSpeed.Instance.Current;

            // Road still to ride to the bay, measured ALONG the road (arc). The sequencer scrolls the tile with
            // the world, so PlayerArc climbs toward stopArc on its own. Using arc — not a straight-line world
            // distance — is what keeps this correct through the turns between the player and the station: the
            // old world-Z distance read the station as "already here" the moment the road curved, braking the
            // world to a halt while the charge tile was still far up the road and never came into view.
            float ahead = stopArc - seq.PlayerArc;

            if (!braking)
            {
                // Physical stopping distance from the current speed, plus a small margin.
                float stopDistance = speed * speed / (2f * config.checkpointBrakeRate) + 2f;
                if (ahead <= stopDistance)
                {
                    braking = true;
                    WorldSpeed.Instance.BeginOverride(0f, config.checkpointBrakeRate);
                }
                return;
            }

            if (speed <= config.checkpointStopSpeed || ahead <= 0f)
            {
                if (!timeUp)
                    return; // stopped at the bay before the time is up — hold here (world already braked) until it is
                reached = true;
                WorldSpeed.Instance.SetCurrent(0f);
                // Cinematic relay: hand the stop to the charge-station sequence, which pulls the bike into the bay,
                // plays the short charge, then raises CheckpointReached itself (so the hand-off screen lands after
                // the charge, not over it). Falls back to the instant relay if disabled or the sequence is absent.
                if (config != null && config.cinematicRelay && ChargeStationSequence.Instance != null)
                    ChargeStationSequence.Instance.PlayArrival();
                else
                    GameEvents.RaiseCheckpointReached();
            }
        }

        private void HandleSessionReset()
        {
            braking = false;
            reached = false;
            armed = false;
            woven = false;
        }
    }
}
