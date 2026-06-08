using UnityEngine;
using System.Collections;

namespace OvertakeGame
{
    public class CheckpointManager : MonoBehaviour
    {
        [Header("Checkpoint Prefab")]
        public GameObject checkpointPrefab;
        public float checkpointSpawnDistance = 80f;
        public float checkpointY             = 0f;

        [Header("Pause at Checkpoint")]
        public float pauseDuration       = 5f;
        public float checkpointBrakeRate = 25f;

        [Header("References")]
        public TimerManager   timerManager;
        public TrafficManager trafficManager;
        public ScoreManager   scoreManager;
        public UIManager      uiManager;
        public GameManager    gameManager;

        private GameObject _activeCheckpoint;
        private bool       _checkpointSpawned;
        private bool       _atCheckpoint;
        private int        _roundNumber = 1;

        void OnEnable()  { if (timerManager != null) timerManager.OnTimerExpired += OnTimerExpired; }
        void OnDisable() { if (timerManager != null) timerManager.OnTimerExpired -= OnTimerExpired; }

        private void OnTimerExpired() { if (!_checkpointSpawned) SpawnCheckpoint(); }

        public void SpawnCheckpoint()
        {
            if (checkpointPrefab == null) { Debug.LogWarning("[CheckpointManager] No prefab assigned!"); return; }
            _checkpointSpawned = true;
            trafficManager?.StopSpawning();

            var pc = FindFirstObjectByType<PlayerController>();
            float px = pc != null ? pc.transform.position.x : 0f;
            float pz = pc != null ? pc.transform.position.z : 0f;

            _activeCheckpoint = Instantiate(checkpointPrefab,
                new Vector3(px, checkpointY, pz + checkpointSpawnDistance), Quaternion.identity);

            var trigger = _activeCheckpoint.GetComponent<CheckpointTrigger>()
                       ?? _activeCheckpoint.AddComponent<CheckpointTrigger>();
            trigger.manager = this;
        }

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

            _roundNumber++;
            uiManager?.ShowCheckpointScreen(_roundNumber - 1, scoreManager?.CurrentScore ?? 0, pauseDuration);
            yield return new WaitForSeconds(pauseDuration);

            uiManager?.HideCheckpointScreen();
            if (_activeCheckpoint != null) Destroy(_activeCheckpoint);

            _checkpointSpawned = false;
            _atCheckpoint      = false;
            WorldSpeed.Instance?.ClearOverride();
            gameManager?.StartNextRound();
        }
    }
}
