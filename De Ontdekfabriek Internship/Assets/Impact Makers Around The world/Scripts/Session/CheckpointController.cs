using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;

namespace KenyaScooter.Session
{
    /// <summary>
    /// The relay checkpoint (Req §9.2): when the timer expires, a charge station
    /// spawns ahead in the player's lane. The player drives to it; close to the
    /// station a speed override brakes the world to a stop (start distance computed
    /// from v²/2a, so it works from any speed), then CheckpointReached fires —
    /// GameManager commits the turn and the checkpoint screen takes over.
    /// Separate from GameManager by design (answer to open question Q1): session
    /// states live in one place, checkpoint choreography in another.
    /// </summary>
    public sealed class CheckpointController : MonoBehaviour
    {
        [SerializeField] private SessionConfig config;
        [SerializeField] private GameObject checkpointPrefab;
        [SerializeField] private Transform player;

        public bool HasCheckpoint => instance != null;

        private GameObject instance;
        private bool braking;
        private bool reached;

        private void Start()
        {
            // Single instance, created once — never instantiated during play.
            if (checkpointPrefab != null)
            {
                instance = Instantiate(checkpointPrefab, transform);
                instance.SetActive(false);
            }
        }

        private void OnEnable() => GameEvents.SessionReset += HandleSessionReset;
        private void OnDisable() => GameEvents.SessionReset -= HandleSessionReset;

        /// <summary>Called by GameManager when the timer expires and the state becomes AtCheckpoint.</summary>
        public void Begin()
        {
            if (instance == null)
                return;

            float playerLong = RoadDirection.Longitudinal(player.position);
            instance.transform.SetPositionAndRotation(
                RoadDirection.Current * (playerLong + config.checkpointDistance)
                    + RoadDirection.SteerAxis * RoadSideConfig.Active.OwnLaneCentre,
                Quaternion.LookRotation(RoadDirection.Current));
            instance.SetActive(true);
            braking = false;
            reached = false;
        }

        private void Update()
        {
            if (GameManager.State != GameState.AtCheckpoint || instance == null || !instance.activeSelf || reached)
                return;

            float speed = WorldSpeed.Instance.Current;
            instance.transform.position += -RoadDirection.Current * (speed * Time.deltaTime);

            float ahead = RoadDirection.Longitudinal(instance.transform.position)
                - RoadDirection.Longitudinal(player.position);

            if (!braking)
            {
                // Physical stopping distance from the current speed, plus a margin.
                float stopDistance = speed * speed / (2f * config.checkpointBrakeRate) + 2f;
                if (ahead <= stopDistance)
                {
                    braking = true;
                    WorldSpeed.Instance.BeginOverride(0f, config.checkpointBrakeRate);
                }
                return;
            }

            if (speed <= config.checkpointStopSpeed)
            {
                reached = true;
                WorldSpeed.Instance.SetCurrent(0f);
                GameEvents.RaiseCheckpointReached();
            }
        }

        private void HandleSessionReset()
        {
            if (instance != null)
                instance.SetActive(false);
            braking = false;
            reached = false;
        }
    }
}
