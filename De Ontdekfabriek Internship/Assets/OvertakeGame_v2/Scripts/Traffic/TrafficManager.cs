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
        [Tooltip("World-space Y for all spawned vehicles. Set to your road surface Y. Never copy player Y.")]
        public float spawnY = 0f;

        // ── Same-direction (player lane) ─────────────────────────────────────
        [Header("Same-Direction Lane")]
        [Tooltip("How far ahead of the player the pre-warmed line starts at game start.")]
        public float prewarmStartDistance  = 15f;
        [Tooltip("How far ahead the pre-warmed line reaches at game start. " +
                 "Should be at least poolSizePerLane * (carLength + minimumCarGap) to fill the pool.")]
        public float prewarmEndDistance    = 200f;
        [Tooltip("Seconds between spawning a new same-direction car during play.")]
        public float sameDirectionSpawnMin = 3f;
        public float sameDirectionSpawnMax = 6f;

        // ── Oncoming ─────────────────────────────────────────────────────────
        [Header("Oncoming Lane")]
        [Tooltip("How far ahead oncoming cars spawn. Set high so the player has time to react.")]
        public float oncomingSpawnDistance = 120f;
        [Tooltip("Seconds between oncoming spawns.")]
        public float oncomingSpawnMin      = 1.5f;
        public float oncomingSpawnMax      = 3.5f;

        [Header("Despawn")]
        public float despawnDistanceBehind = 40f;

        [Header("Vehicle Prefabs")]
        public List<GameObject> sameDirectionPrefabs;
        public List<GameObject> oncomingPrefabs;

        [Header("Speed Ranges (m/s)")]
        public float sameDirectionSpeedMin =  4f;
        public float sameDirectionSpeedMax =  8f;
        public float oncomingSpeedMin      =  8f;
        public float oncomingSpeedMax      = 14f;

        [Header("Pool Settings")]
        [Tooltip("Pool size for each lane. Make this large enough to cover prewarmEndDistance.")]
        public int poolSizePerLane = 16;

        [Header("Minimum Gap (Same-Direction)")]
        [Tooltip("Clear space in world units between back of one car and front of the next.")]
        public float minimumCarGap = 12f;
        [Tooltip("Approximate Z length of a traffic car in world units. Match your prefab.")]
        public float carLength = 4f;

        // ── Internal ──────────────────────────────────────────────────────────
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

            // Fill the same-direction lane immediately so player starts behind a queue
            PrewarmSameLane();

            // Kick off ongoing spawn coroutines
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

        // ── Pre-warm: fill the same-direction lane from start ─────────────────
        private void PrewarmSameLane()
        {
            if (_player == null) return;
            float laneX  = GetLaneX(false);
            float nextZ  = _player.position.z + prewarmStartDistance;
            float limitZ = _player.position.z + prewarmEndDistance;

            while (nextZ < limitZ)
            {
                var tv = GetAvailable(_samePool);
                if (tv == null) break; // pool exhausted

                float speed = Random.Range(sameDirectionSpeedMin, sameDirectionSpeedMax);
                tv.Activate(new Vector3(laneX, spawnY, nextZ), speed, false);

                // Advance by car length + required gap so no overlap
                nextZ += carLength + minimumCarGap + Random.Range(0f, 6f); // small random variation
            }
        }

        // ── Same-direction ongoing routine ────────────────────────────────────
        // Continuously tops up the lane so it never feels like it ends.
        // Always places the new car ahead of the furthest active car.
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

                float laneX = GetLaneX(false);

                // Place new car ahead of the furthest existing car, respecting the gap
                float frontOfQueue = GetFurthestActiveFrontZ(_samePool);
                float desiredZ     = _player.position.z + prewarmEndDistance * 0.6f;

                if (frontOfQueue > float.MinValue)
                {
                    float earliestSafe = frontOfQueue + minimumCarGap + carLength;
                    if (desiredZ < earliestSafe) desiredZ = earliestSafe;
                }

                float speed = Random.Range(sameDirectionSpeedMin, sameDirectionSpeedMax);
                tv.Activate(new Vector3(laneX, spawnY, desiredZ), speed, false);
            }
        }

        // ── Oncoming routine ──────────────────────────────────────────────────
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

                float laneX  = GetLaneX(true);
                float spawnZ = _player.position.z + oncomingSpawnDistance; // far ahead
                float speed  = Random.Range(oncomingSpeedMin, oncomingSpeedMax);
                tv.Activate(new Vector3(laneX, spawnY, spawnZ), speed, true);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private void InitPool(List<GameObject> prefabs, List<TrafficVehicle> pool, int count, bool oncoming)
        {
            if (prefabs == null || prefabs.Count == 0)
            {
                Debug.LogWarning($"[TrafficManager] No prefabs for {(oncoming ? "oncoming" : "same-direction")} lane.");
                return;
            }
            for (int i = 0; i < count; i++)
            {
                var go = Instantiate(prefabs[Random.Range(0, prefabs.Count)],
                                     new Vector3(0f, -1000f, 0f), Quaternion.identity, transform);
                go.SetActive(false);
                go.tag = "Traffic";
                var tv = go.GetComponent<TrafficVehicle>() ?? go.AddComponent<TrafficVehicle>();
                tv.isOncoming = oncoming;
                pool.Add(tv);
            }
        }

        private float GetLaneX(bool oncoming)
        {
            if (roadConfig == null) return oncoming ? oncomingLaneX : playerLaneX;
            return oncoming
                ? (roadConfig.driveOnRight ? oncomingLaneX : playerLaneX)
                : (roadConfig.driveOnRight ? playerLaneX   : oncomingLaneX);
        }

        private TrafficVehicle GetAvailable(List<TrafficVehicle> pool)
        {
            foreach (var tv in pool) if (!tv.gameObject.activeSelf) return tv;
            return null;
        }

        private void RecycleOutOfRange(List<TrafficVehicle> pool)
        {
            foreach (var tv in pool)
                if (tv.gameObject.activeSelf &&
                    _player.position.z - tv.transform.position.z > despawnDistanceBehind)
                    tv.Deactivate();
        }

        private float GetFurthestActiveFrontZ(List<TrafficVehicle> pool)
        {
            float furthest = float.MinValue;
            foreach (var tv in pool)
            {
                if (!tv.gameObject.activeSelf) continue;
                float frontZ = tv.transform.position.z + carLength * 0.5f;
                if (frontZ > furthest) furthest = frontZ;
            }
            return furthest;
        }
    }
}
