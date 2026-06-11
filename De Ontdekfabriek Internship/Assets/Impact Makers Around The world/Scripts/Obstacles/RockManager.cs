using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace OvertakeGame
{
    public class RockManager : MonoBehaviour
    {
        public enum SpawnZone { PlayerLaneOnly, OncomingLaneOnly, BothLanes, ShouldersOnly, FullRoadWidth }

        [Header("Spawn Zone")]
        public SpawnZone spawnZone = SpawnZone.PlayerLaneOnly;

        [Header("Lane & Road Dimensions")]
        public float playerLaneX       =  1.5f;
        public float oncomingLaneX     = -1.5f;
        public float laneHalfWidth     =  1.0f;
        public float roadEdgeX         =  4f;
        public float shoulderSpawnWidth=  0.8f;

        [Header("Spawn Y")]
        public float spawnY = 0.15f;

        [Header("Spawn Distance")]
        public float spawnDistanceAhead    = 80f;
        public float despawnDistanceBehind = 20f;

        [Header("Spawn Interval")]
        public float spawnIntervalMin = 4f;
        public float spawnIntervalMax = 8f;

        [Header("Density Ramp")]
        public bool  rampDensityOverTime = true;
        public float rampDuration        = 90f;
        public float minIntervalAtPeak   = 1.5f;

        [Header("Rock Size")]
        public float minScale = 0.3f;
        public float maxScale = 0.9f;

        [Header("Clusters")]
        public bool  enableClusters  = true;
        [Range(0f,1f)]
        public float clusterChance   = 0.25f;
        public int   clusterCount    = 3;
        public float clusterSpread   = 1.2f;
        public float clusterZSpacing = 2.5f;

        [Header("Prefabs & Pool")]
        public List<GameObject> rockPrefabs;
        public int              poolSize = 20;

        private Transform          _player;
        private List<RockObstacle> _pool = new();
        private bool   _spawning;
        private float  _elapsed;

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
            if (rockPrefabs == null || rockPrefabs.Count == 0) { Debug.LogWarning("[RockManager] No prefabs!"); return; }
            for (int i = 0; i < poolSize; i++)
            {
                var go = Instantiate(rockPrefabs[Random.Range(0, rockPrefabs.Count)], new Vector3(0f,-1000f,0f), Quaternion.identity, transform);
                go.SetActive(false); go.tag = "Rock";
                foreach (var col in go.GetComponentsInChildren<Collider>()) col.isTrigger = true;
                _pool.Add(go.GetComponent<RockObstacle>() ?? go.AddComponent<RockObstacle>());
            }
        }

        private IEnumerator SpawnRoutine()
        {
            while (true)
            {
                float t = rampDensityOverTime ? Mathf.Clamp01(_elapsed / rampDuration) : 0f;
                yield return new WaitForSeconds(Random.Range(Mathf.Lerp(spawnIntervalMin, minIntervalAtPeak, t), Mathf.Lerp(spawnIntervalMax, minIntervalAtPeak*1.4f, t)));
                if (!_spawning) yield break;
                if (_player == null || GameManager.Instance?.CurrentState != GameManager.GameState.Playing) continue;
                if (enableClusters && Random.value < clusterChance) SpawnCluster();
                else SpawnSingle();
            }
        }

        // Spawn position is road-direction-aware: works before and after 90° turns.
        // Travel axis: -RoadDirection.Current. Lateral (steer) axis: RoadDirection.SteerpAxis.
        private void SpawnSingle()
        {
            var r = GetAvailable(); if (r == null) return;
            Vector3 pos = _player.position
                        + (-RoadDirection.Current) * spawnDistanceAhead
                        + RoadDirection.SteerpAxis * PickSpawnSteer();
            pos.y = spawnY;
            r.Activate(pos, Random.Range(minScale, maxScale));
        }

        private void SpawnCluster()
        {
            Vector3 basePos = _player.position + (-RoadDirection.Current) * spawnDistanceAhead;
            float   baseSteer = PickSpawnSteer();
            for (int i = 0; i < clusterCount; i++)
            {
                var r = GetAvailable(); if (r == null) break;
                // Spread along both axes: steer spread stays perpendicular to travel
                float steer = baseSteer + Random.Range(-clusterSpread, clusterSpread);
                Vector3 pos = basePos
                            + (-RoadDirection.Current) * (i * clusterZSpacing + Random.Range(-0.5f, 0.5f))
                            + RoadDirection.SteerpAxis * steer;
                pos.y = spawnY;
                r.Activate(pos, Random.Range(minScale, maxScale * 0.8f));
            }
        }

        // Returns a signed lateral offset along RoadDirection.SteerpAxis.
        // Renamed from PickSpawnX: after a turn, X is no longer the lateral axis.
        private float PickSpawnSteer() => spawnZone switch
        {
            SpawnZone.PlayerLaneOnly   => playerLaneX + Random.Range(-laneHalfWidth, laneHalfWidth),
            SpawnZone.OncomingLaneOnly => oncomingLaneX + Random.Range(-laneHalfWidth, laneHalfWidth),
            SpawnZone.BothLanes        => (Random.value > 0.5f ? playerLaneX : oncomingLaneX) + Random.Range(-laneHalfWidth, laneHalfWidth),
            SpawnZone.ShouldersOnly    => (Random.value > 0.5f ? 1f : -1f) * (roadEdgeX + Random.Range(0.1f, shoulderSpawnWidth)),
            _                          => Random.Range(-(roadEdgeX + shoulderSpawnWidth), roadEdgeX + shoulderSpawnWidth)
        };

        // for-loops avoid foreach enumerator allocation and are explicit about no captures
        private RockObstacle GetAvailable()
        {
            for (int i = 0; i < _pool.Count; i++)
                if (!_pool[i].gameObject.activeSelf) return _pool[i];
            return null;
        }

        private void RecycleOutOfRange()
        {
            // Dot-product despawn: works on any travel axis, not just Z
            Vector3 forwardDir  = -RoadDirection.Current;
            float   playerAhead = Vector3.Dot(_player.position, forwardDir);
            for (int i = 0; i < _pool.Count; i++)
            {
                if (!_pool[i].gameObject.activeSelf) continue;
                float rAhead = Vector3.Dot(_pool[i].transform.position, forwardDir);
                if (playerAhead - rAhead > despawnDistanceBehind) _pool[i].Deactivate();
            }
        }
    }
}