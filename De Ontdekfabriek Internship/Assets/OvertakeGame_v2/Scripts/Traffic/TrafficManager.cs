using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace OvertakeGame
{
    /// <summary>
    /// Spawns traffic in both lanes.
    /// In the world-moves model, spawn positions are fixed distances AHEAD of
    /// the player on Z. Vehicles move toward the player via TrafficVehicle.Update.
    /// The player's Z never changes, so spawn distances stay constant forever.
    /// </summary>
    public class TrafficManager : MonoBehaviour
    {
        [Header("Lane Configuration")]
        public RoadSideConfig roadConfig;
        public float playerLaneX   =  1.5f;
        public float oncomingLaneX = -1.5f;

        [Header("Spawn Y")]
        [Tooltip("World Y for all traffic. Set to your road surface Y.")]
        public float spawnY = 0f;

        [Header("Same-Direction Lane")]
        public float prewarmStartDistance  = 15f;
        public float prewarmEndDistance    = 200f;
        public float sameDirectionSpawnMin = 3f;
        public float sameDirectionSpawnMax = 6f;

        [Header("Oncoming Lane")]
        [Tooltip("How far ahead oncoming cars spawn. Player Z is fixed so this is always the same point.")]
        public float oncomingSpawnDistance = 120f;
        public float oncomingSpawnMin      = 1.5f;
        public float oncomingSpawnMax      = 3.5f;

        [Header("Despawn")]
        [Tooltip("How far behind the player before a vehicle is recycled.")]
        public float despawnDistanceBehind = 30f;

        [Header("Vehicle Prefabs")]
        public List<GameObject> sameDirectionPrefabs;
        public List<GameObject> oncomingPrefabs;

        [Header("Own Speed Ranges (m/s) — relative to world speed")]
        [Tooltip("Same-direction cars move this much slower than the world, making them overtakeable.")]
        public float sameDirectionSpeedMin =  3f;
        public float sameDirectionSpeedMax =  7f;
        [Tooltip("Oncoming cars move this much faster than world speed toward the player.")]
        public float oncomingSpeedMin      =  5f;
        public float oncomingSpeedMax      = 10f;

        [Header("Pool Settings")]
        public int poolSizePerLane = 16;

        [Header("Minimum Gap (Same-Direction)")]
        public float minimumCarGap = 12f;
        public float carLength     = 4f;

        private Transform            _player;
        private List<TrafficVehicle> _samePool     = new List<TrafficVehicle>();
        private List<TrafficVehicle> _oncomingPool = new List<TrafficVehicle>();
        private bool _spawning;

        void Start()
        {
            _player = FindFirstObjectByType<PlayerController>()?.transform;
            InitPool(sameDirectionPrefabs, _samePool,     poolSizePerLane, false);
            InitPool(oncomingPrefabs,      _oncomingPool, poolSizePerLane, true);
            _spawning = true;
            PrewarmSameLane();
            StartCoroutine(SameLaneSpawnRoutine());
            StartCoroutine(OncomingSpawnRoutine());
        }

        void Update()
        {
            if (_player == null) return;
            RecycleOutOfRange(_samePool);
            RecycleOutOfRange(_oncomingPool);
        }

        public void StopSpawning()  => _spawning = false;
        public void ResumeSpawning()
        {
            if (_spawning) return;
            _spawning = true;
            PrewarmSameLane();
            StartCoroutine(SameLaneSpawnRoutine());
            StartCoroutine(OncomingSpawnRoutine());
        }

        private void PrewarmSameLane()
        {
            if (_player == null) return;
            float laneX = GetLaneX(false);
            float nextZ = _player.position.z + prewarmStartDistance;
            float limitZ = _player.position.z + prewarmEndDistance;

            while (nextZ < limitZ)
            {
                var tv = GetAvailable(_samePool);
                if (tv == null) break;
                float speed = Random.Range(sameDirectionSpeedMin, sameDirectionSpeedMax);
                tv.Activate(new Vector3(laneX, spawnY, nextZ), speed, false);
                nextZ += carLength + minimumCarGap + Random.Range(0f, 5f);
            }
        }

        private IEnumerator SameLaneSpawnRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(sameDirectionSpawnMin, sameDirectionSpawnMax));
                if (!_spawning) yield break;
                if (_player == null) continue;
                if (GameManager.Instance?.CurrentState != GameManager.GameState.Playing) continue;

                var tv = GetAvailable(_samePool);
                if (tv == null) continue;

                float laneX    = GetLaneX(false);
                float desiredZ = _player.position.z + prewarmEndDistance * 0.6f;
                float frontOfQueue = GetFurthestActiveFrontZ(_samePool);
                if (frontOfQueue > float.MinValue)
                {
                    float earliest = frontOfQueue + minimumCarGap + carLength;
                    if (desiredZ < earliest) desiredZ = earliest;
                }
                float speed = Random.Range(sameDirectionSpeedMin, sameDirectionSpeedMax);
                tv.Activate(new Vector3(laneX, spawnY, desiredZ), speed, false);
            }
        }

        private IEnumerator OncomingSpawnRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(oncomingSpawnMin, oncomingSpawnMax));
                if (!_spawning) yield break;
                if (_player == null) continue;
                if (GameManager.Instance?.CurrentState != GameManager.GameState.Playing) continue;

                var tv = GetAvailable(_oncomingPool);
                if (tv == null) continue;

                float speed  = Random.Range(oncomingSpeedMin, oncomingSpeedMax);
                float spawnZ = _player.position.z + oncomingSpawnDistance;
                tv.Activate(new Vector3(GetLaneX(true), spawnY, spawnZ), speed, true);
            }
        }

        private float GetLaneX(bool oncoming)
        {
            if (roadConfig == null) return oncoming ? oncomingLaneX : playerLaneX;
            return oncoming
                ? (roadConfig.driveOnRight ? oncomingLaneX : playerLaneX)
                : (roadConfig.driveOnRight ? playerLaneX   : oncomingLaneX);
        }

        private void InitPool(List<GameObject> prefabs, List<TrafficVehicle> pool, int count, bool oncoming)
        {
            if (prefabs == null || prefabs.Count == 0) { Debug.LogWarning($"[TrafficManager] No prefabs for {(oncoming?"oncoming":"same-dir")} lane."); return; }
            for (int i = 0; i < count; i++)
            {
                var go = Instantiate(prefabs[Random.Range(0, prefabs.Count)], new Vector3(0f, -1000f, 0f), Quaternion.identity, transform);
                go.SetActive(false);
                go.tag = "Traffic";
                var tv = go.GetComponent<TrafficVehicle>() ?? go.AddComponent<TrafficVehicle>();
                tv.isOncoming = oncoming;
                pool.Add(tv);
            }
        }

        private TrafficVehicle GetAvailable(List<TrafficVehicle> pool)
        {
            foreach (var tv in pool) if (!tv.gameObject.activeSelf) return tv;
            return null;
        }

        private void RecycleOutOfRange(List<TrafficVehicle> pool)
        {
            if (_player == null) return;
            foreach (var tv in pool)
                if (tv.gameObject.activeSelf && _player.position.z - tv.transform.position.z > despawnDistanceBehind)
                    tv.Deactivate();
        }

        private float GetFurthestActiveFrontZ(List<TrafficVehicle> pool)
        {
            float furthest = float.MinValue;
            foreach (var tv in pool)
            {
                if (!tv.gameObject.activeSelf) continue;
                float f = tv.transform.position.z + carLength * 0.5f;
                if (f > furthest) furthest = f;
            }
            return furthest;
        }
    }
}
