using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// RunnerTrafficManager — Spawns traffic on the correct side of the road.
///
/// ROAD LAYOUT (centre line at x = 0):
///
///   x < 0  →  LEFT lane  — player's correct side (Kenya drives left)
///                           same-direction vehicles spawn here
///                           player overtakes these
///
///   x > 0  →  RIGHT lane — oncoming side
///                           oncoming vehicles spawn here
///                           these rush toward the player
///
/// SPAWN RULES:
///   Same-dir vehicles:  x = Random.Range(-roadHalf, -0.5f)   always negative
///   Oncoming vehicles:  x = Random.Range( 0.5f,  roadHalf)   always positive
///   0.5f margin keeps vehicles away from the very centre line.
///
/// OVERLAP PREVENTION:
///   Before spawning, checks all active vehicles.
///   If any is within minSpawnSeparation metres, skips this tick.
/// </summary>
public class RunnerTrafficManager : MonoBehaviour
{
    public static RunnerTrafficManager Instance { get; private set; }

    [Header("Config")]
    public RunnerGameConfig config;

    [Header("Spawn Settings")]
    [Tooltip("Minimum distance between any two vehicles at the moment of spawn.")]
    public float minSpawnSeparation = 6f;

    [Tooltip("How many positions to try before giving up this tick.")]
    public int maxSpawnAttempts = 5;

    [Tooltip("Seconds between each spawn attempt.")]
    public float spawnInterval = 1.2f;

    [Tooltip("Minimum margin from the centre line (x=0). Keeps cars off the white line.")]
    public float centrLineMargin = 0.8f;

    // ── Private ───────────────────────────────────────────────────
    private readonly List<VehicleBehaviour> _activeVehicles = new();
    private float _spawnTimer;
    private float _elapsedTime;
    private bool _running;

    // ─────────────────────────────────────────────────────────────
    #region Unity Lifecycle

    void Awake() => Instance = this;

    void Start()
    {
        RunnerGameManager.Instance.OnGameStart.AddListener(OnGameStart);
        RunnerGameManager.Instance.OnGameOver.AddListener(OnGameOver);
        RunnerGameManager.Instance.OnChargingBegin.AddListener(OnChargingBegin);
        RunnerGameManager.Instance.OnChargingEnd.AddListener(OnChargingEnd);
    }

    void OnDestroy()
    {
        if (RunnerGameManager.Instance == null) return;
        RunnerGameManager.Instance.OnGameStart.RemoveListener(OnGameStart);
        RunnerGameManager.Instance.OnGameOver.RemoveListener(OnGameOver);
    }

    void Update()
    {
        if (!_running) return;
        if (!RunnerGameManager.Instance.IsPlaying()) return;

        _elapsedTime += Time.deltaTime;
        CleanNullVehicles();

        int target = TargetVehicleCount();
        if (_activeVehicles.Count < target)
        {
            _spawnTimer -= Time.deltaTime;
            if (_spawnTimer <= 0f)
            {
                TrySpawnVehicle();
                _spawnTimer = spawnInterval;
            }
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Spawning

    void TrySpawnVehicle()
    {
        if (config == null || config.vehiclePrefabs == null ||
            config.vehiclePrefabs.Length == 0)
        {
            Debug.LogWarning("[TrafficManager] No vehicle prefabs assigned in config.");
            return;
        }

        // 50/50 split between oncoming and same-direction each spawn
        bool oncoming = Random.value > 0.5f;

        GameObject prefab = config.vehiclePrefabs[
            Random.Range(0, config.vehiclePrefabs.Length)];
        if (prefab == null) return;

        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            Vector3 pos = PickSpawnPosition(oncoming);

            if (IsPositionClear(pos))
            {
                SpawnAt(prefab, pos, oncoming);
                return;
            }
        }

        Debug.Log("[TrafficManager] Spawn skipped — no clear position found.");
    }

    /// <summary>
    /// Returns a spawn position strictly on the correct side of the road.
    ///
    /// Road centre = x 0.
    /// Left  side (player's side, same-dir): x is NEGATIVE  (-roadHalf to -margin)
    /// Right side (oncoming):                x is POSITIVE   (+margin  to +roadHalf)
    /// </summary>
    Vector3 PickSpawnPosition(bool oncoming)
    {
        float roadHalf = config.roadWidth * 0.5f;
        float margin = centrLineMargin;
        float x;

        if (oncoming)
        {
            // RIGHT side — strictly positive x
            x = Random.Range(margin, roadHalf - 0.3f);
        }
        else
        {
            // LEFT side — strictly negative x
            x = Random.Range(-(roadHalf - 0.3f), -margin);
        }

        float z = config.vehicleSpawnDistance + Random.Range(0f, 12f);

        return new Vector3(x, 0f, z);
    }

    bool IsPositionClear(Vector3 pos)
    {
        foreach (var v in _activeVehicles)
        {
            if (v == null) continue;

            // Check XZ distance only (height doesn't matter)
            float dist = new Vector2(
                pos.x - v.transform.position.x,
                pos.z - v.transform.position.z).magnitude;

            if (dist < minSpawnSeparation) return false;
        }
        return true;
    }

    void SpawnAt(GameObject prefab, Vector3 pos, bool oncoming)
    {
        GameObject go = Instantiate(prefab, pos, Quaternion.identity);

        VehicleBehaviour vb = go.GetComponent<VehicleBehaviour>();
        if (vb == null)
        {
            Debug.LogError($"[TrafficManager] '{prefab.name}' has no VehicleBehaviour!");
            Destroy(go);
            return;
        }

        vb.isOncoming = oncoming;
        _activeVehicles.Add(vb);

        Debug.Log($"[TrafficManager] Spawned {prefab.name} | " +
                  $"{(oncoming ? "ONCOMING  x=" : "SAME-DIR  x=")}{pos.x:F2}");
    }

    int TargetVehicleCount()
    {
        if (config == null) return 4;
        float t = Mathf.Clamp01(_elapsedTime / config.difficultyRampDuration);
        return Mathf.RoundToInt(
            Mathf.Lerp(config.initialVehicleCount, config.maxVehicleCount, t));
    }

    void CleanNullVehicles() =>
        _activeVehicles.RemoveAll(v => v == null);

    void ClearAll()
    {
        foreach (var v in _activeVehicles)
            if (v != null) Destroy(v.gameObject);
        _activeVehicles.Clear();
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Events

    void OnGameStart()
    {
        _running = true;
        _elapsedTime = 0f;
        _spawnTimer = 0.5f;
        ClearAll();
    }

    void OnGameOver() { _running = false; }
    void OnChargingBegin() { _running = false; }
    void OnChargingEnd() { _running = true; }

    #endregion
}