using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace OvertakeGame
{
    /// <summary>
    /// Manages traffic spawning and adapts behaviour per RoadSequence via TrafficBehaviourProfile.
    /// Object pool — no Instantiate/Destroy during play.
    /// Same-dir: moveSpeed = WorldSpeed – ownSpeed. Oncoming: moveSpeed = WorldSpeed + ownSpeed.
    /// Spawn positions use RoadDirection.SteerpAxis so lanes stay correct after turns.
    /// </summary>
    public class TrafficManager : MonoBehaviour
    {
        [Header("Lane Configuration")]
        public RoadSideConfig roadConfig;
        [Tooltip("Lateral offset from road centre to lane centre (metres).")]
        public float laneOffset = 1.5f;

        [Header("Spawn Y")]
        public float spawnY = 0f;

        [Header("Same-Direction Spawning")]
        public float prewarmStartDistance  = 15f;
        public float prewarmEndDistance    = 200f;
        public float sameDirectionSpawnMin = 3f;
        public float sameDirectionSpawnMax = 6f;

        [Header("Oncoming Spawning")]
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

        [Header("Minimum Gap (Same-Direction)")]
        public float minimumCarGap = 12f;
        public float carLength     =  4f;

        [Header("Pool Settings")]
        public int poolSizePerLane = 16;

        // ── Private pools ─────────────────────────────────────────────────────
        private Transform             _player;
        private List<TrafficVehicle>  _samePool     = new();
        private List<TrafficVehicle>  _oncomingPool = new();

        private bool _spawning;

        // ── Active profile (updated per sequence) ─────────────────────────────
        private TrafficBehaviourProfile _profile;
        private Coroutine _sameRoutine;
        private Coroutine _oncomingRoutine;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        void Start()
        {
            _player = FindFirstObjectByType<PlayerController>()?.transform;
            TrafficVehicle.SetSharedPlayer(_player);

            InitVehiclePool(sameDirectionPrefabs, _samePool,     poolSizePerLane, false);
            InitVehiclePool(oncomingPrefabs,      _oncomingPool, poolSizePerLane, true);

            RoadSequencer.OnSequenceStarted += OnSequenceChanged;

            _spawning = true;
            PrewarmSameLane();
            StartSpawnRoutines();
        }

        void OnDestroy() => RoadSequencer.OnSequenceStarted -= OnSequenceChanged;

        void Update()
        {
            if (_player == null) return;
            RecycleVehicles(_samePool);
            RecycleVehicles(_oncomingPool);
        }

        // ── Public control ────────────────────────────────────────────────────

        public void StopSpawning()
        {
            _spawning = false;
            if (_sameRoutine     != null) StopCoroutine(_sameRoutine);
            if (_oncomingRoutine != null) StopCoroutine(_oncomingRoutine);
        }

        public void ResumeSpawning()
        {
            if (_spawning) return;
            _spawning = true;
            PrewarmSameLane();
            StartSpawnRoutines();
        }

        // ── Sequence profile switching ────────────────────────────────────────

        private void OnSequenceChanged(RoadSequence seq)
        {
            if (seq?.trafficProfile == null) return;
            ApplyProfile(seq.trafficProfile);
        }

        private void ApplyProfile(TrafficBehaviourProfile profile)
        {
            _profile = profile;
            StopSpawnRoutines();
            if (!_spawning) return;

            if (profile.trafficJamMode)
                PlaceJam(profile);
            else
                StartSpawnRoutines();
        }

        // ── Prewarm ───────────────────────────────────────────────────────────

        private void PrewarmSameLane()
        {
            if (_player == null) return;
            float nextZ  = prewarmStartDistance;
            float limitZ = prewarmEndDistance;
            float speed  = GetSameSpeed();

            while (nextZ < limitZ)
            {
                var tv = GetAvailable(_samePool);
                if (tv == null) break;
                tv.Activate(MakeLanePos(false, nextZ), speed, false, PickPersonality());
                nextZ += carLength + minimumCarGap + Random.Range(0f, 5f);
            }
        }

        // ── Traffic jam spawning ──────────────────────────────────────────────

        private void PlaceJam(TrafficBehaviourProfile profile)
        {
            foreach (var tv in _samePool) if (tv.gameObject.activeSelf) tv.Deactivate();

            float ahead = 30f;
            for (int i = 0; i < profile.jamVehicleCount; i++)
            {
                var tv = GetAvailable(_samePool);
                if (tv == null) break;
                tv.Activate(MakeLanePos(false, ahead), profile.jamBaseSpeed, false, DriverPersonality.Normal);
                ahead += carLength + profile.jamGap;
            }

            _oncomingRoutine = StartCoroutine(OncomingRoutine());
        }

        // ── Coroutine spawning ────────────────────────────────────────────────

        private void StartSpawnRoutines()
        {
            StopSpawnRoutines();
            _sameRoutine     = StartCoroutine(SameLaneRoutine());
            _oncomingRoutine = StartCoroutine(OncomingRoutine());
        }

        private void StopSpawnRoutines()
        {
            if (_sameRoutine     != null) { StopCoroutine(_sameRoutine);     _sameRoutine = null; }
            if (_oncomingRoutine != null) { StopCoroutine(_oncomingRoutine); _oncomingRoutine = null; }
        }

        private IEnumerator SameLaneRoutine()
        {
            while (true)
            {
                float mult = _profile?.sameDirectionIntervalMultiplier ?? 1f;
                yield return new WaitForSeconds(
                    Random.Range(sameDirectionSpawnMin, sameDirectionSpawnMax) * mult);

                if (!_spawning) yield break;
                if (!IsPlaying()) continue;

                var tv = GetAvailable(_samePool);
                if (tv == null) continue;

                float desiredAhead = prewarmEndDistance * 0.6f;
                float frontAhead   = GetFurthestAhead(_samePool);
                if (frontAhead > float.MinValue)
                {
                    float earliest = frontAhead + minimumCarGap + carLength;
                    if (desiredAhead < earliest) desiredAhead = earliest;
                }

                tv.Activate(MakeLanePos(false, desiredAhead), GetSameSpeed(), false, PickPersonality());
            }
        }

        private IEnumerator OncomingRoutine()
        {
            while (true)
            {
                float mult = _profile?.oncomingIntervalMultiplier ?? 1f;
                if (mult <= 0f) { yield return new WaitForSeconds(5f); continue; }

                yield return new WaitForSeconds(
                    Random.Range(oncomingSpawnMin, oncomingSpawnMax) * mult);

                if (!_spawning) yield break;
                if (!IsPlaying()) continue;

                var tv = GetAvailable(_oncomingPool);
                if (tv == null) continue;

                tv.Activate(MakeLanePos(true, oncomingSpawnDistance), GetOncomingSpeed(), true, PickPersonality());
            }
        }

        // ── Position helpers ──────────────────────────────────────────────────

        private Vector3 MakeLanePos(bool oncoming, float ahead)
        {
            if (_player == null) return Vector3.zero;

            Vector3 forwardDir = -RoadDirection.Current;
            float sign = oncoming
                ? (roadConfig != null ? (roadConfig.driveOnRight ? -1f :  1f) : -1f)
                : (roadConfig != null ? (roadConfig.driveOnRight ?  1f : -1f) :  1f);

            Vector3 pos = _player.position
                        + forwardDir               * ahead
                        + RoadDirection.SteerpAxis * (sign * laneOffset);
            pos.y = spawnY;
            return pos;
        }

        // ── Speed helpers ─────────────────────────────────────────────────────

        private float GetSameSpeed()
        {
            float mult = _profile?.sameDirectionSpeedMultiplier ?? 1f;
            return Random.Range(sameDirectionSpeedMin, sameDirectionSpeedMax) * mult;
        }

        private float GetOncomingSpeed()
        {
            float mult = _profile?.oncomingSpeedMultiplier ?? 1f;
            return Random.Range(oncomingSpeedMin, oncomingSpeedMax) * mult;
        }

        private DriverPersonality PickPersonality() =>
            _profile != null ? _profile.PickPersonality() : DriverPersonality.Normal;

        // ── Pool helpers ──────────────────────────────────────────────────────

        private void InitVehiclePool(List<GameObject> prefabs, List<TrafficVehicle> pool,
                                     int count, bool oncoming)
        {
            if (prefabs == null || prefabs.Count == 0)
            {
                Debug.LogWarning($"[TrafficManager] No prefabs for {(oncoming ? "oncoming" : "same-dir")} lane.");
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

        private TrafficVehicle GetAvailable(List<TrafficVehicle> pool)
        {
            for (int i = 0; i < pool.Count; i++)
                if (!pool[i].gameObject.activeSelf) return pool[i];
            return null;
        }

        private void RecycleVehicles(List<TrafficVehicle> pool)
        {
            if (_player == null) return;
            Vector3 forwardDir  = -RoadDirection.Current;
            float   playerAhead = Vector3.Dot(_player.position, forwardDir);

            foreach (var tv in pool)
            {
                if (!tv.gameObject.activeSelf) continue;
                float vehicleAhead = Vector3.Dot(tv.transform.position, forwardDir);
                if (playerAhead - vehicleAhead > despawnDistanceBehind)
                    tv.Deactivate();
            }
        }

        private float GetFurthestAhead(List<TrafficVehicle> pool)
        {
            Vector3 forwardDir = -RoadDirection.Current;
            float   max        = float.MinValue;
            foreach (var tv in pool)
            {
                if (!tv.gameObject.activeSelf) continue;
                float v = Vector3.Dot(tv.transform.position, forwardDir) + carLength * 0.5f;
                if (v > max) max = v;
            }
            return max;
        }

        private bool IsPlaying() =>
            GameManager.Instance?.CurrentState == GameManager.GameState.Playing;
    }
}
