using UnityEngine;
using System.Collections;

namespace OvertakeGame
{
    /// <summary>
    /// Manages relay checkpoints. When the timer expires, a charge-station checkpoint
    /// spawns ahead; the player drives to it, the world brakes, the checkpoint screen
    /// shows turn score + group total + rank, then the next student's session begins.
    ///
    /// SETUP: Assign checkpointPrefab. The prefab must have a CheckpointTrigger component.
    /// </summary>
    public class CheckpointManager : MonoBehaviour
    {
        [Header("Checkpoint")]
        public GameObject checkpointPrefab;
        [Tooltip("Distance ahead of the player (along RoadDirection.Current) to spawn the station.")]
        public float checkpointSpawnDistance = 80f;
        public float checkpointY             = 0f;

        [Header("Handoff Timing")]
        [Tooltip("Seconds the checkpoint screen stays up. Must cover a physical iPad handoff (~10 s).")]
        public float pauseDuration       = 10f;
        public float checkpointBrakeRate = 25f;

        [Header("References")]
        public TrafficManager    trafficManager;
        public ScoreManager      scoreManager;
        public UIManager         uiManager;
        public GameManager       gameManager;
        public GroupScoreManager groupScoreManager;
        public RewindSystem      rewindSystem;

        private GameObject _activeCheckpoint;
        private bool       _checkpointSpawned;
        private bool       _atCheckpoint;
        private int        _roundNumber = 1;

        // ── Spawn ──────────────────────────────────────────────────────────────

        public void SpawnCheckpoint()
        {
            if (checkpointPrefab == null || _checkpointSpawned) return;
            _checkpointSpawned = true;
            trafficManager?.StopSpawning();

            var pc       = FindFirstObjectByType<PlayerController>();
            var origin   = pc != null ? pc.transform.position : Vector3.zero;
            var spawnPos = origin + RoadDirection.Current * checkpointSpawnDistance;
            spawnPos.y   = checkpointY;

            _activeCheckpoint = Object.Instantiate(checkpointPrefab, spawnPos, Quaternion.identity);

            var trigger   = _activeCheckpoint.GetComponent<CheckpointTrigger>()
                         ?? _activeCheckpoint.AddComponent<CheckpointTrigger>();
            trigger.manager = this;
        }

        // ── Reached ───────────────────────────────────────────────────────────

        public void OnPlayerReachedCheckpoint()
        {
            if (_atCheckpoint) return;
            _atCheckpoint = true;
            StartCoroutine(CheckpointSequence());
        }

        private IEnumerator CheckpointSequence()
        {
            // Brake world to a stop
            while (WorldSpeed.Instance != null && WorldSpeed.Instance.Current > 0.1f)
            {
                WorldSpeed.Instance.OverrideSpeed(
                    Mathf.MoveTowards(WorldSpeed.Instance.Current, 0f,
                                      checkpointBrakeRate * Time.deltaTime));
                yield return null;
            }
            WorldSpeed.Instance?.OverrideSpeed(0f);

            // Commit this turn's score to the group total
            groupScoreManager?.CommitTurn(_roundNumber);

            int turnScore  = scoreManager?.CurrentScore ?? 0;
            int groupTotal = groupScoreManager?.GroupTotal ?? turnScore;
            int rank       = LeaderboardManager.Instance?.GetCurrentRank(groupTotal) ?? 0;

            uiManager?.ShowCheckpointScreen(_roundNumber, turnScore, groupTotal, rank, pauseDuration);
            yield return new WaitForSeconds(pauseDuration);
            uiManager?.HideCheckpointScreen();

            // Clean up checkpoint object
            if (_activeCheckpoint != null) Object.Destroy(_activeCheckpoint);
            _checkpointSpawned = false;
            _atCheckpoint      = false;
            _roundNumber++;

            // Reset per-turn state
            scoreManager?.ResetScore();
            rewindSystem?.ResetRewindCount();
            groupScoreManager?.BeginTurn();
            WorldSpeed.Instance?.ClearOverride();
            gameManager?.StartNextRound();
        }
    }

    /// <summary>
    /// Place on the checkpoint prefab root. Fires once when the player's collider enters.
    /// The manager reference is assigned by CheckpointManager.SpawnCheckpoint().
    /// </summary>
    public class CheckpointTrigger : MonoBehaviour
    {
        [HideInInspector] public CheckpointManager manager;
        public string playerTag = "Player";
        private bool _triggered;

        void OnTriggerEnter(Collider other)
        {
            if (_triggered || !other.CompareTag(playerTag)) return;
            _triggered = true;
            manager?.OnPlayerReachedCheckpoint();
        }
    }
}
