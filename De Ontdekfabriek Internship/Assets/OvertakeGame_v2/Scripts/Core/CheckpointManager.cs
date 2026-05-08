using UnityEngine;
using System.Collections;

namespace OvertakeGame
{
    /// <summary>
    /// Manages the end-of-round checkpoint flow.
    ///
    /// When the session timer expires:
    ///   1. Traffic stops spawning.
    ///   2. A checkpoint prefab is instantiated ahead of the player on the road.
    ///   3. The player drives forward and enters the checkpoint trigger.
    ///   4. The player car gradually brakes to a stop.
    ///   5. A pause screen shows the round score for pauseDuration seconds.
    ///   6. The next round begins: timer resets, traffic resumes, checkpoint removed.
    ///
    /// The checkpoint prefab needs a trigger collider and the CheckpointTrigger
    /// component (provided below) on it — or you can tag it "Checkpoint" and
    /// let this script detect the tag via OnTriggerEnter on the player.
    ///
    /// Attach to any persistent GameObject (e.g. GameManager object).
    /// </summary>
    public class CheckpointManager : MonoBehaviour
    {
        [Header("Checkpoint Prefab")]
        [Tooltip("Prefab to instantiate at the end of each round. " +
                 "Must have a trigger collider. Can be a gate, finish line, etc.")]
        public GameObject checkpointPrefab;

        [Tooltip("How far ahead of the player's current position the checkpoint spawns " +
                 "when the timer expires.")]
        public float checkpointSpawnDistance = 80f;

        [Tooltip("Y position of the checkpoint (should sit on road surface).")]
        public float checkpointY = 0f;

        [Header("Pause at Checkpoint")]
        [Tooltip("How many seconds the game pauses at the checkpoint before the next round.")]
        public float pauseDuration = 5f;

        [Tooltip("How quickly the player car decelerates to a stop at the checkpoint (m/s per second).")]
        public float checkpointBrakeForce = 25f;

        [Header("References")]
        public PlayerController playerController;
        public TimerManager     timerManager;
        public TrafficManager   trafficManager;
        public ScoreManager     scoreManager;
        public UIManager        uiManager;
        public GameManager      gameManager;

        // ─── State ─────────────────────────────────────────────────────────────
        private GameObject _activeCheckpoint;
        private bool       _checkpointSpawned;
        private bool       _atCheckpoint;
        private int        _roundNumber = 1;

        void OnEnable()
        {
            // Listen to timer expiry
            if (timerManager != null)
                timerManager.OnTimerExpired += OnTimerExpired;
        }

        void OnDisable()
        {
            if (timerManager != null)
                timerManager.OnTimerExpired -= OnTimerExpired;
        }

        // Called by TimerManager when session time runs out
        private void OnTimerExpired()
        {
            if (_checkpointSpawned) return;
            SpawnCheckpoint();
        }

        // ─── Public entry point — also callable from GameManager ──────────────
        public void SpawnCheckpoint()
        {
            if (checkpointPrefab == null)
            {
                Debug.LogWarning("[CheckpointManager] No checkpoint prefab assigned!");
                return;
            }

            _checkpointSpawned = true;
            trafficManager?.StopSpawning();

            Vector3 spawnPos = new Vector3(
                playerController != null ? playerController.transform.position.x : 0f,
                checkpointY,
                playerController != null
                    ? playerController.transform.position.z + checkpointSpawnDistance
                    : checkpointSpawnDistance);

            _activeCheckpoint = Instantiate(checkpointPrefab, spawnPos, Quaternion.identity);

            // Hook up the trigger — CheckpointTrigger calls back to us
            var trigger = _activeCheckpoint.GetComponent<CheckpointTrigger>();
            if (trigger == null) trigger = _activeCheckpoint.AddComponent<CheckpointTrigger>();
            trigger.manager = this;

            Debug.Log($"[CheckpointManager] Checkpoint spawned at Z={spawnPos.z:F1}");
        }

        /// <summary>
        /// Called by CheckpointTrigger when the player enters the checkpoint zone.
        /// </summary>
        public void OnPlayerReachedCheckpoint()
        {
            if (_atCheckpoint) return;
            _atCheckpoint = true;
            StartCoroutine(CheckpointSequence());
        }

        private IEnumerator CheckpointSequence()
        {
            // ── 1. Brake player to a stop ──────────────────────────────────────
            if (playerController != null)
            {
                while (playerController.CurrentSpeedMs > 0.1f)
                {
                    // Directly reduce the player's base speed override
                    playerController.OverrideSpeed(
                        Mathf.MoveTowards(playerController.CurrentSpeedMs, 0f,
                                          checkpointBrakeForce * Time.deltaTime));
                    yield return null;
                }
                playerController.OverrideSpeed(0f);
            }

            // ── 2. Show checkpoint / round summary ─────────────────────────────
            _roundNumber++;
            int score = scoreManager?.CurrentScore ?? 0;
            uiManager?.ShowCheckpointScreen(_roundNumber - 1, score, pauseDuration);

            yield return new WaitForSeconds(pauseDuration);

            // ── 3. Hide screen, clean up checkpoint ───────────────────────────
            uiManager?.HideCheckpointScreen();
            if (_activeCheckpoint != null) Destroy(_activeCheckpoint);

            // ── 4. Start next round ────────────────────────────────────────────
            _checkpointSpawned = false;
            _atCheckpoint      = false;
            playerController?.ClearSpeedOverride();
            gameManager?.StartNextRound();
        }
    }
}
