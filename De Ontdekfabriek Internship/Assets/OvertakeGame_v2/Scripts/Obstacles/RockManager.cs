using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace OvertakeGame
{
    /// <summary>
    /// Spawns and recycles rock obstacles on the road using an object pool.
    /// Follows the same pattern as PotholeManager.
    ///
    /// Rocks can be placed in:
    ///   - The player lane only
    ///   - The oncoming lane only
    ///   - The road shoulders (off-road, combined with haptic rumble)
    ///   - Both lanes
    ///   - Anywhere across the full road width
    ///
    /// Individual rock size is randomised between minScale and maxScale.
    /// Density ramps over time if enabled.
    ///
    /// SETUP:
    ///   1. Create a Rock prefab (see RockObstacle.cs header for requirements).
    ///   2. Add this component to any manager GameObject.
    ///   3. Assign rockPrefabs list (one or more variants for visual variety).
    ///   4. Wire RockManager into GameManager.rockManager.
    ///   5. Add "Rock" tag in Edit > Project Settings > Tags.
    /// </summary>
    public class RockManager : MonoBehaviour
    {
        // ── Spawn zone ────────────────────────────────────────────────────────
        public enum SpawnZone
        {
            PlayerLaneOnly,
            OncomingLaneOnly,
            BothLanes,
            ShouldersOnly,       // off-road — triggers haptic rumble if player hits
            FullRoadWidth,       // anywhere across the entire road including shoulders
        }

        [Header("Spawn Zone")]
        [Tooltip("Where rocks can appear on the road.")]
        public SpawnZone spawnZone = SpawnZone.PlayerLaneOnly;

        [Header("Lane & Road Dimensions")]
        [Tooltip("X centre of the player lane. Match TrafficManager.playerLaneX.")]
        public float playerLaneX   =  1.5f;
        [Tooltip("X centre of the oncoming lane.")]
        public float oncomingLaneX = -1.5f;
        [Tooltip("Half-width of a lane (rocks randomise within this of the lane centre).")]
        public float laneHalfWidth = 1.0f;
        [Tooltip("X position of the road edge (shoulder starts here). " +
                 "Match PlayerController.roadHalfWidth.")]
        public float roadEdgeX     = 4f;
        [Tooltip("How far onto the shoulder rocks can spawn (for ShouldersOnly mode).")]
        public float shoulderSpawnWidth = 0.8f;

        [Header("Spawn Y")]
        [Tooltip("World Y for rocks. Should be just above the road surface (e.g. 0.15 for a rock that sits on the ground).")]
        public float spawnY = 0.15f;

        [Header("Spawn Distance")]
        public float spawnDistanceAhead    = 80f;
        public float despawnDistanceBehind = 20f;

        [Header("Spawn Interval (seconds)")]
        public float spawnIntervalMin = 4f;
        public float spawnIntervalMax = 8f;

        [Header("Density Ramp")]
        public bool  rampDensityOverTime = true;
        public float rampDuration        = 90f;
        public float minIntervalAtPeak   = 1.5f;

        [Header("Rock Size")]
        [Tooltip("Minimum scale. A scale of 1 = prefab's natural size.")]
        public float minScale = 0.3f;
        [Tooltip("Maximum scale.")]
        public float maxScale = 0.9f;

        [Header("Cluster Spawning")]
        [Tooltip("Occasionally spawn a cluster of rocks close together instead of single rocks.")]
        public bool  enableClusters = true;
        [Tooltip("Chance (0-1) that any given spawn event is a cluster rather than a single rock.")]
        [Range(0f, 1f)]
        public float clusterChance = 0.25f;
        [Tooltip("How many rocks in a cluster.")]
        public int   clusterCount  = 3;
        [Tooltip("Random X offset between rocks in a cluster.")]
        public float clusterSpread = 1.2f;
        [Tooltip("Z spacing between rocks in a cluster.")]
        public float clusterZSpacing = 2.5f;

        [Header("Prefabs & Pool")]
        [Tooltip("One or more rock prefab variants. Multiple variants add visual variety.")]
        public List<GameObject> rockPrefabs;
        public int              poolSize = 20;

        // ── Internal ──────────────────────────────────────────────────────────
        private Transform           _player;
        private List<RockObstacle>  _pool    = new List<RockObstacle>();
        private bool                _spawning;
        private float               _elapsed;

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

        public void StopSpawning()  => _spawning = false;
        public void ResumeSpawning()
        {
            if (_spawning) return;
            _spawning = true;
            StartCoroutine(SpawnRoutine());
        }

        // ── Pool ──────────────────────────────────────────────────────────────
        private void InitPool()
        {
            if (rockPrefabs == null || rockPrefabs.Count == 0)
            {
                Debug.LogWarning("[RockManager] No rock prefabs assigned!");
                return;
            }
            for (int i = 0; i < poolSize; i++)
            {
                var prefab = rockPrefabs[Random.Range(0, rockPrefabs.Count)];
                var go     = Instantiate(prefab, new Vector3(0f, -1000f, 0f),
                                         Quaternion.identity, transform);
                go.SetActive(false);
                go.tag = "Rock";
                foreach (var col in go.GetComponentsInChildren<Collider>()) col.isTrigger = true;
                var rock = go.GetComponent<RockObstacle>() ?? go.AddComponent<RockObstacle>();
                _pool.Add(rock);
            }
        }

        // ── Spawn coroutine ───────────────────────────────────────────────────
        private IEnumerator SpawnRoutine()
        {
            while (true)
            {
                float t    = rampDensityOverTime ? Mathf.Clamp01(_elapsed / rampDuration) : 0f;
                float minI = Mathf.Lerp(spawnIntervalMin, minIntervalAtPeak, t);
                float maxI = Mathf.Lerp(spawnIntervalMax, minIntervalAtPeak * 1.4f, t);
                yield return new WaitForSeconds(Random.Range(minI, maxI));

                if (!_spawning) yield break;
                if (_player == null) continue;
                if (GameManager.Instance?.CurrentState != GameManager.GameState.Playing) continue;

                bool doCluster = enableClusters && Random.value < clusterChance;

                if (doCluster)
                    SpawnCluster();
                else
                    SpawnSingle(_player.position.z + spawnDistanceAhead);
            }
        }

        private void SpawnSingle(float spawnZ)
        {
            var rock = GetAvailable();
            if (rock == null) return;
            float x     = PickSpawnX();
            float scale = Random.Range(minScale, maxScale);
            rock.Activate(new Vector3(x, spawnY, spawnZ), scale);
        }

        private void SpawnCluster()
        {
            float baseZ = _player.position.z + spawnDistanceAhead;
            float baseX = PickSpawnX();

            for (int i = 0; i < clusterCount; i++)
            {
                var rock = GetAvailable();
                if (rock == null) break;

                float x     = baseX + Random.Range(-clusterSpread, clusterSpread);
                float z     = baseZ + i * clusterZSpacing + Random.Range(-0.5f, 0.5f);
                float scale = Random.Range(minScale, maxScale * 0.8f); // clusters = smaller rocks
                rock.Activate(new Vector3(x, spawnY, z), scale);
            }
        }

        // ── Spawn X by zone ───────────────────────────────────────────────────
        private float PickSpawnX()
        {
            switch (spawnZone)
            {
                case SpawnZone.PlayerLaneOnly:
                    return playerLaneX + Random.Range(-laneHalfWidth, laneHalfWidth);

                case SpawnZone.OncomingLaneOnly:
                    return oncomingLaneX + Random.Range(-laneHalfWidth, laneHalfWidth);

                case SpawnZone.BothLanes:
                    float laneX = Random.value > 0.5f ? playerLaneX : oncomingLaneX;
                    return laneX + Random.Range(-laneHalfWidth, laneHalfWidth);

                case SpawnZone.ShouldersOnly:
                    // Left or right shoulder
                    float side = Random.value > 0.5f ? 1f : -1f;
                    return side * (roadEdgeX + Random.Range(0.1f, shoulderSpawnWidth));

                case SpawnZone.FullRoadWidth:
                    return Random.Range(-(roadEdgeX + shoulderSpawnWidth),
                                          roadEdgeX + shoulderSpawnWidth);

                default:
                    return playerLaneX;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private RockObstacle GetAvailable()
        {
            foreach (var r in _pool) if (!r.gameObject.activeSelf) return r;
            return null;
        }

        private void RecycleOutOfRange()
        {
            foreach (var r in _pool)
                if (r.gameObject.activeSelf &&
                    _player.position.z - r.transform.position.z > despawnDistanceBehind)
                    r.Deactivate();
        }
    }
}
