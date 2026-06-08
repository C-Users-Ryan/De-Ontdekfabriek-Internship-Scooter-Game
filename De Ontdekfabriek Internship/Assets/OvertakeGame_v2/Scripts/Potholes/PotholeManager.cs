using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace OvertakeGame
{
    public class PotholeManager : MonoBehaviour
    {
        public enum LaneSide { PlayerLaneOnly, OncomingLaneOnly, Both }

        [Header("Lane Selection")]
        public LaneSide spawnSide = LaneSide.PlayerLaneOnly;

        [Header("Lane X Positions")]
        public float playerLaneX   =  1.5f;
        public float oncomingLaneX = -1.5f;
        public float laneHalfWidth =  0.8f;

        [Header("Spawn Y")]
        public float spawnY = 0.01f;

        [Header("Spawn Distance")]
        public float spawnDistanceAhead    = 70f;
        public float despawnDistanceBehind = 20f;

        [Header("Spawn Interval (seconds)")]
        public float spawnIntervalMin = 3f;
        public float spawnIntervalMax = 6f;

        [Header("Density Ramp")]
        public bool  rampDensityOverTime = true;
        public float rampDuration        = 90f;
        public float minIntervalAtPeak   = 1f;

        [Header("Size Variation")]
        public float minScale = 0.7f;
        public float maxScale = 1.4f;

        [Header("Prefab & Pool")]
        public GameObject potholePrefab;
        public int        poolSize = 12;

        private Transform     _player;
        private List<Pothole> _pool = new();
        private bool          _spawning;
        private float         _elapsed;

        void Start()
        {
            _player = FindFirstObjectByType<PlayerController>()?.transform;
            InitPool(); _spawning = true; StartCoroutine(SpawnRoutine());
        }

        void Update()
        {
            if (_player == null) return;
            if (rampDensityOverTime && _spawning) _elapsed += Time.deltaTime;
            RecycleOutOfRange();
        }

        public void StopSpawning()  => _spawning = false;
        public void ResumeSpawning() { if (_spawning) return; _spawning = true; StartCoroutine(SpawnRoutine()); }

        private void InitPool()
        {
            if (potholePrefab == null) { Debug.LogWarning("[PotholeManager] No prefab!"); return; }
            for (int i = 0; i < poolSize; i++)
            {
                var go = Instantiate(potholePrefab, new Vector3(0f,-1000f,0f), Quaternion.identity, transform);
                go.SetActive(false); go.tag = "Pothole";
                foreach (var col in go.GetComponentsInChildren<Collider>()) col.isTrigger = true;
                _pool.Add(go.GetComponent<Pothole>() ?? go.AddComponent<Pothole>());
            }
        }

        private IEnumerator SpawnRoutine()
        {
            while (true)
            {
                float t    = rampDensityOverTime ? Mathf.Clamp01(_elapsed / rampDuration) : 0f;
                float minI = Mathf.Lerp(spawnIntervalMin, minIntervalAtPeak, t);
                float maxI = Mathf.Lerp(spawnIntervalMax, minIntervalAtPeak * 1.5f, t);
                yield return new WaitForSeconds(Random.Range(minI, maxI));
                if (!_spawning) yield break;
                if (_player == null || GameManager.Instance?.CurrentState != GameManager.GameState.Playing) continue;
                var ph = GetAvailable();
                if (ph == null) continue;
                float laneX   = spawnSide switch { LaneSide.PlayerLaneOnly => playerLaneX, LaneSide.OncomingLaneOnly => oncomingLaneX, _ => Random.value > 0.5f ? playerLaneX : oncomingLaneX };
                float xOffset = Random.Range(-laneHalfWidth, laneHalfWidth);
                ph.Activate(new Vector3(laneX + xOffset, spawnY, _player.position.z + spawnDistanceAhead), Random.Range(minScale, maxScale));
            }
        }

        private Pothole GetAvailable() { foreach (var ph in _pool) if (!ph.gameObject.activeSelf) return ph; return null; }
        private void RecycleOutOfRange() { foreach (var ph in _pool) if (ph.gameObject.activeSelf && _player.position.z - ph.transform.position.z > despawnDistanceBehind) ph.Deactivate(); }
    }
}
