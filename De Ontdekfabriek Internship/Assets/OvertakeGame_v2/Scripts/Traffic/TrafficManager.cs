using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace OvertakeGame
{
    public class TrafficManager : MonoBehaviour
    {
        [Header("Lane Configuration")]
        public RoadSideConfig roadConfig;
        public float playerLaneX   =  1.5f;
        public float oncomingLaneX = -1.5f;

        [Header("Spawn Y")]
        [Tooltip("World Y for all traffic. Set to road surface Y — never copy player Y.")]
        public float spawnY = 0f;

        [Header("Same-Direction Lane")]
        public float prewarmStartDistance  = 15f;
        public float prewarmEndDistance    = 200f;
        public float sameDirectionSpawnMin = 3f;
        public float sameDirectionSpawnMax = 6f;

        [Header("Oncoming Lane")]
        public float oncomingSpawnDistance = 120f;
        public float oncomingSpawnMin      = 1.5f;
        public float oncomingSpawnMax      = 3.5f;

        [Header("Despawn")]
        public float despawnDistanceBehind = 30f;

        [Header("Vehicle Prefabs")]
        public List<GameObject> sameDirectionPrefabs;
        public List<GameObject> oncomingPrefabs;

        [Header("Speed Ranges (m/s relative to world speed)")]
        public float sameDirectionSpeedMin =  3f;
        public float sameDirectionSpeedMax =  7f;
        public float oncomingSpeedMin      =  5f;
        public float oncomingSpeedMax      = 10f;

        [Header("Pool Settings")]
        public int poolSizePerLane = 16;

        [Header("Minimum Gap (Same-Direction)")]
        public float minimumCarGap = 12f;
        public float carLength     =  4f;

        private Transform            _player;
        private List<TrafficVehicle> _samePool     = new();
        private List<TrafficVehicle> _oncomingPool = new();
        private bool _spawning;

        void Start()
        {
            _player = FindFirstObjectByType<PlayerController>()?.transform;
            InitPool(sameDirectionPrefabs, _samePool,     poolSizePerLane, false);
            InitPool(oncomingPrefabs,      _oncomingPool, poolSizePerLane, true);
            _spawning = true;
            PrewarmSameLane();
            StartCoroutine(SameLaneRoutine());
            StartCoroutine(OncomingRoutine());
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
            StartCoroutine(SameLaneRoutine());
            StartCoroutine(OncomingRoutine());
        }

        private void PrewarmSameLane()
        {
            if (_player == null) return;
            float laneX = GetLaneX(false);
            float nextZ = _player.position.z + prewarmStartDistance;
            float limitZ= _player.position.z + prewarmEndDistance;
            while (nextZ < limitZ)
            {
                var tv = GetAvailable(_samePool);
                if (tv == null) break;
                tv.Activate(new Vector3(laneX, spawnY, nextZ),
                    Random.Range(sameDirectionSpeedMin, sameDirectionSpeedMax), false);
                nextZ += carLength + minimumCarGap + Random.Range(0f, 5f);
            }
        }

        private IEnumerator SameLaneRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(sameDirectionSpawnMin, sameDirectionSpawnMax));
                if (!_spawning) yield break;
                if (_player == null || GameManager.Instance?.CurrentState != GameManager.GameState.Playing) continue;
                var tv = GetAvailable(_samePool);
                if (tv == null) continue;
                float desiredZ = _player.position.z + prewarmEndDistance * 0.6f;
                float front    = GetFurthestFrontZ(_samePool);
                if (front > float.MinValue) { float earliest = front + minimumCarGap + carLength; if (desiredZ < earliest) desiredZ = earliest; }
                tv.Activate(new Vector3(GetLaneX(false), spawnY, desiredZ),
                    Random.Range(sameDirectionSpeedMin, sameDirectionSpeedMax), false);
            }
        }

        private IEnumerator OncomingRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(oncomingSpawnMin, oncomingSpawnMax));
                if (!_spawning) yield break;
                if (_player == null || GameManager.Instance?.CurrentState != GameManager.GameState.Playing) continue;
                var tv = GetAvailable(_oncomingPool);
                if (tv == null) continue;
                tv.Activate(new Vector3(GetLaneX(true), spawnY,
                    _player.position.z + oncomingSpawnDistance),
                    Random.Range(oncomingSpeedMin, oncomingSpeedMax), true);
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
                var go = Instantiate(prefabs[Random.Range(0, prefabs.Count)], new Vector3(0f,-1000f,0f), Quaternion.identity, transform);
                go.SetActive(false); go.tag = "Traffic";
                var tv = go.GetComponent<TrafficVehicle>() ?? go.AddComponent<TrafficVehicle>();
                tv.isOncoming = oncoming;
                pool.Add(tv);
            }
        }

        private TrafficVehicle GetAvailable(List<TrafficVehicle> pool)
        { foreach (var tv in pool) if (!tv.gameObject.activeSelf) return tv; return null; }

        private void RecycleOutOfRange(List<TrafficVehicle> pool)
        { foreach (var tv in pool) if (tv.gameObject.activeSelf && _player.position.z - tv.transform.position.z > despawnDistanceBehind) tv.Deactivate(); }

        private float GetFurthestFrontZ(List<TrafficVehicle> pool)
        {
            float f = float.MinValue;
            foreach (var tv in pool) { if (!tv.gameObject.activeSelf) continue; float z = tv.transform.position.z + carLength * 0.5f; if (z > f) f = z; }
            return f;
        }
    }
}
