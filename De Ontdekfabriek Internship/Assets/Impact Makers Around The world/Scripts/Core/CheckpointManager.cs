using UnityEngine;
using System.Collections;

namespace OvertakeGame
{
    /// <summary>
    /// Manages charge-station checkpoints. When the timer expires a station spawns
    /// ahead; the player drives to it, the world brakes, the checkpoint screen shows
    /// turn score + group total + live rank, then the next student starts.
    /// </summary>
    public class CheckpointManager : MonoBehaviour
    {
        [Header("Checkpoint Prefab")]
        public GameObject checkpointPrefab;
        public float checkpointSpawnDistance = 80f;
        public float checkpointY             = 0f;

        [Header("Pause at Checkpoint")]
        [Tooltip("Seconds the checkpoint screen stays up — needs to cover a physical iPad handoff (8–12 s recommended).")]
        public float pauseDuration       = 10f;
        public float checkpointBrakeRate = 25f;

        [Header("References")]
        public TimerManager      timerManager;
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

        // ── Lifecycle ──────────────────────────────────────────────────────────

        void OnEnable()  { if (timerManager != null) timerManager.OnTimerExpired += OnTimerExpired; }
        void OnDisable() { if (timerManager != null) timerManager.OnTimerExpired -= OnTimerExpired; }

        private void OnTimerExpired()
        {
            if (!_checkpointSpawned) SpawnCheckpoint();
        }

        // ── Spawn ──────────────────────────────────────────────────────────────

        public void SpawnCheckpoint()
        {
            if (checkpointPrefab == null)
            {
                Debug.LogWarning("[CheckpointManager] No checkpoint prefab assigned.");
                return;
            }
            _checkpointSpawned = true;
            trafficManager?.StopSpawning();

            var pc = FindFirstObjectByType<PlayerController>();
            Vector3 spawnPos = (pc != null ? pc.transform.position : Vector3.zero)
                             + (-RoadDirection.Current) * checkpointSpawnDistance;
            spawnPos.y = checkpointY;

            _activeCheckpoint = Object.Instantiate(checkpointPrefab, spawnPos, Quaternion.identity);

            var trigger = _activeCheckpoint.GetComponent<CheckpointTrigger>()
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
            // Brake world to stop
            if (WorldSpeed.Instance != null)
            {
                while (WorldSpeed.Instance.Current > 0.1f)
                {
                    WorldSpeed.Instance.OverrideSpeed(
                        Mathf.MoveTowards(WorldSpeed.Instance.Current, 0f,
                                          checkpointBrakeRate * Time.deltaTime));
                    yield return null;
                }
                WorldSpeed.Instance.OverrideSpeed(0f);
            }

            groupScoreManager?.CommitTurn(_roundNumber);

            int turnScore  = scoreManager?.CurrentScore ?? 0;
            int groupTotal = groupScoreManager != null ? groupScoreManager.GroupTotal : turnScore;
            int rank       = LeaderboardManager.Instance?.GetCurrentRank(groupTotal) ?? 0;

            uiManager?.ShowCheckpointScreen(_roundNumber, turnScore, pauseDuration);
            yield return new WaitForSeconds(pauseDuration);
            uiManager?.HideCheckpointScreen();

            if (_activeCheckpoint != null) Destroy(_activeCheckpoint);
            _checkpointSpawned = false;
            _atCheckpoint      = false;
            _roundNumber++;

            scoreManager?.ResetScore();
            rewindSystem?.ResetRewindCount();
            groupScoreManager?.BeginTurn();
            WorldSpeed.Instance?.ClearOverride();
            gameManager?.StartNextRound();
        }
    }

    /// <summary>
    /// Place on the checkpoint prefab. Fires once when the player enters.
    /// Manager reference is set by CheckpointManager.SpawnCheckpoint().
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
