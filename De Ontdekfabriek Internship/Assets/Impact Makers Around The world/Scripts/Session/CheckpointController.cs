using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;
using KenyaScooter.Roads;

namespace KenyaScooter.Session
{
    /// <summary>
    /// The relay checkpoint (Req §9.2): when the timer expires, the road is capped with a
    /// charge-station tile at the end of the chain (RoadSequencer.SpawnCheckpoint). The world keeps
    /// scrolling normally, so the player coasts toward it as part of the road — no prefab teleports in.
    /// Close to the tile's stop marker a speed override brakes the world to a stop (start distance from
    /// v²/2a, so it works from any speed), then CheckpointReached fires — GameManager commits the turn
    /// and the checkpoint screen takes over.
    /// Separate from GameManager by design (Q1): session states live in one place, checkpoint
    /// choreography in another.
    /// </summary>
    public sealed class CheckpointController : MonoBehaviour
    {
        [SerializeField] private SessionConfig config;
        [SerializeField] private Transform player;

        /// <summary>True when a checkpoint tile is configured on the sequencer; otherwise the timer ends the session.</summary>
        public bool HasCheckpoint =>
            RoadSequencer.Instance != null && RoadSequencer.Instance.HasCheckpointTile;

        private bool braking;
        private bool reached;

        private void OnEnable() => GameEvents.SessionReset += HandleSessionReset;
        private void OnDisable() => GameEvents.SessionReset -= HandleSessionReset;

        /// <summary>Called by GameManager when the timer expires and the state becomes AtCheckpoint.</summary>
        public void Begin()
        {
            braking = false;
            reached = false;
            if (RoadSequencer.Instance != null)
                RoadSequencer.Instance.SpawnCheckpoint();
        }

        private void Update()
        {
            if (GameManager.State != GameState.AtCheckpoint || reached)
                return;

            RoadTile tile = RoadSequencer.Instance != null ? RoadSequencer.Instance.ActiveCheckpointTile : null;
            if (tile == null)
                return;

            float speed = WorldSpeed.Instance.Current;

            // Road length between the player and the tile's stop marker, along the direction of travel.
            // The sequencer scrolls the tile with the rest of the world, so this shrinks on its own.
            float ahead = RoadDirection.Longitudinal(tile.StopPosition)
                - RoadDirection.Longitudinal(player.position);

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
                GameEvents.RaiseCheckpointReached();
            }
        }

        private void HandleSessionReset()
        {
            braking = false;
            reached = false;
        }
    }
}
