using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace OvertakeGame
{
    /// <summary>
    /// Spawns and recycles potholes on the road using a small object pool.
    /// Potholes can appear in the player lane, oncoming lane, or both.
    /// Density ramps up over time so the game gets progressively harder.
    ///
    /// SETUP:
    ///   1. Create a Pothole prefab:
    ///        - A flat disc/quad mesh (scale ~0.8-1.2 on X/Z, very flat on Y)
    ///        - A Box or Sphere Collider set to IsTrigger = true
    ///        - Tag: "Pothole"  (create this tag in Edit > Project Settings > Tags first)
    ///   2. Add PotholeManager to any scene GameObject (e.g. the Managers object)
    ///   3. Assign the prefab and configure settings below
    ///   4. Wire PotholeManager into GameManager Inspector slot
    /// </summary>
    public class PotholeManager : MonoBehaviour
    {
        public enum LaneSide { PlayerLaneOnly, OncomingLaneOnly, Both }

        [Header("Lane Selection")]
        [Tooltip("Which side of the road potholes can appear on.")]
        public LaneSide spawnSide = LaneSide.PlayerLaneOnly;

        [Header("Lane X Positions")]
        [Tooltip("Centre X of the player lane. Match your TrafficManager setting.")]
        public float playerLaneX   =  1.5f;
        [Tooltip("Centre X of the oncoming lane.")]
        public float oncomingLaneX = -1.5f;
        [Tooltip("Half-width of a lane. Potholes randomise within this range of the lane centre.")]
        public float laneHalfWidth = 0.8f;

        [Header("Spawn Y")]
        [Tooltip("World Y to place potholes. Set to road surface Y (usually 0.01 so they sit on top).")]
        public float spawnY = 0.01f;

        [Header("Spawn Distance")]
        public float spawnDistanceAhead    = 70f;
        public float despawnDistanceBehind = 20f;

        [Header("Spawn Interval (seconds)")]
        public float spawnIntervalMin = 3f;
        public float spawnIntervalMax = 6f;

        [Header("Density Ramp Over Time")]
        public bool  rampDensityOverTime = true;
        [Tooltip("After this many seconds the spawn interval reaches its minimum.")]
        public float rampDuration        = 90f;
        [Tooltip("Minimum spawn interval at peak difficulty.")]
        public float minIntervalAtPeak   = 1f;

        [Header("Size Variation")]
        public float minScale = 0.7f;
        public float maxScale = 1.4f;

        [Header("Prefab & Pool")]
        public GameObject potholePrefab;
        public int        poolSize = 12;

        private Transform     _player;
        private List<Pothole> _pool = new List<Pothole>();
        private bool          _spawning;
        private float         _elapsed;

        void Start()
        {
            _player = FindFirstObjectByType<PlayerController>()?.transform;
            InitPool();
            _spawning = true;
            StartCoroutine(SpawnRoutine());
        }

        void Update()
        {
            if (_player == null) return;
            if (rampDensityOverTime && _spawning) _elapsed += Time.deltaTime;
            RecycleOutOfRange();
        }

        public void StopSpawning() => _spawning = false;

        public void ResumeSpawning()
        {
            if (_spawning) return;
            _spawning = true;
            StartCoroutine(SpawnRoutine());
        }

        private void InitPool()
        {
            if (potholePrefab == null)
            {
                Debug.LogWarning("[PotholeManager] No pothole prefab assigned!");
                return;
            }
            for (int i = 0; i < poolSize; i++)
            {
                var go = Instantiate(potholePrefab, new Vector3(0f, -1000f, 0f),
                                     Quaternion.identity, transform);
                go.SetActive(false);
                go.tag = "Pothole";

                var col = go.GetComponent<Collider>();
                if (col != null) col.isTrigger = true;

                var ph = go.GetComponent<Pothole>() ?? go.AddComponent<Pothole>();
                _pool.Add(ph);
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
                if (_player == null) continue;
                if (GameManager.Instance?.CurrentState != GameManager.GameState.Playing) continue;

                var ph = GetAvailable();
                if (ph == null) continue;

                float laneX   = PickLaneX();
                float xOffset = Random.Range(-laneHalfWidth, laneHalfWidth);
                float scale   = Random.Range(minScale, maxScale);

                ph.Activate(new Vector3(laneX + xOffset, spawnY,
                                        _player.position.z + spawnDistanceAhead), scale);
            }
        }

        private float PickLaneX() => spawnSide switch
        {
            LaneSide.PlayerLaneOnly   => playerLaneX,
            LaneSide.OncomingLaneOnly => oncomingLaneX,
            LaneSide.Both             => Random.value > 0.5f ? playerLaneX : oncomingLaneX,
            _                         => playerLaneX
        };

        private Pothole GetAvailable()
        {
            foreach (var ph in _pool) if (!ph.gameObject.activeSelf) return ph;
            return null;
        }

        private void RecycleOutOfRange()
        {
            foreach (var ph in _pool)
                if (ph.gameObject.activeSelf &&
                    _player.position.z - ph.transform.position.z > despawnDistanceBehind)
                    ph.Deactivate();
        }
    }
}
