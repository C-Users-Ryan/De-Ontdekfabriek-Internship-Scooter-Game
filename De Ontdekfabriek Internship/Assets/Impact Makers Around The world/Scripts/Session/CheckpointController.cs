using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;
using KenyaScooter.Roads;

namespace KenyaScooter.Session
{
    /// <summary>
    /// The relay checkpoint (Req §9.2): when the timer expires, the road is capped a controlled distance
    /// ahead (SessionConfig.checkpointDistance) with a charge-station tile (RoadSequencer.SpawnCheckpoint).
    /// The world keeps scrolling normally, so the player coasts toward it as part of the road — no prefab
    /// teleports in. The remaining distance is measured ALONG the road (arc), so it stays correct through any
    /// turns between the player and the station; close to the tile's stop marker a speed override brakes the
    /// world to a stop (start distance from v²/2a, so it works from any speed), then CheckpointReached fires —
    /// GameManager commits the turn and the checkpoint screen takes over.
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
        private float stopArc; // the road-metre the world should brake to a stop on

        private void OnEnable() => GameEvents.SessionReset += HandleSessionReset;
        private void OnDisable() => GameEvents.SessionReset -= HandleSessionReset;

        /// <summary>Called by GameManager when the timer expires and the state becomes AtCheckpoint.</summary>
        public void Begin()
        {
            braking = false;
            reached = false;
            armed = false;
            if (RoadSequencer.Instance != null)
                RoadSequencer.Instance.SpawnCheckpoint(config != null ? config.checkpointDistance : 0f);
        }

        private void Update()
        {
            if (GameManager.State != GameState.AtCheckpoint || reached)
                return;

            RoadSequencer seq = RoadSequencer.Instance;
            RoadTile tile = seq != null ? seq.ActiveCheckpointTile : null;
            if (tile == null)
                return;

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
        }
    }
}
