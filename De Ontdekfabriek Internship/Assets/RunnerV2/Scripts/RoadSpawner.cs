using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// RoadSpawner — Scrolls the world toward the player by moving road segments.
/// Recycles segments from behind the player and spawns them ahead.
/// Also spawns hazard prefabs (speed bumps, potholes) on segments.
///
/// HOW IT WORKS:
///   The player stays at a fixed Z position.
///   Road segments move toward the player each frame at worldScrollSpeed.
///   When a segment passes behind the player it is moved to the front of the queue.
///   This creates the illusion of infinite forward movement.
///
/// STUDENT TIP — changing the setting:
///   Replace the roadSegmentPrefabs in RunnerGameConfig.
///   The spawner will automatically use whatever prefabs are in that list.
///   Each prefab should be a flat road tile, length matching config.roadSegmentLength.
/// </summary>
public class RoadSpawner : MonoBehaviour
{
    public static RoadSpawner Instance { get; private set; }

    [Header("Config")]
    public RunnerGameConfig config;

    // ── Public State ──────────────────────────────────────────────
    /// <summary>Current world scroll speed, driven by difficulty ramp.</summary>
    public float WorldScrollSpeed { get; private set; }

    // ── Private ───────────────────────────────────────────────────
    private readonly List<GameObject> _activeSegments = new();
    private float _nextSegmentZ;
    private float _elapsedPlayTime;
    private bool  _running;

    // ─────────────────────────────────────────────────────────────
    #region Unity Lifecycle

    void Awake()
    {
        Instance = this;
    }

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
        if (!_running || config == null) return;

        UpdateDifficulty();
        ScrollWorld();
        RecycleSegments();
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Difficulty Ramp

    void UpdateDifficulty()
    {
        if (!RunnerGameManager.Instance.IsPlaying()) return;

        _elapsedPlayTime += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsedPlayTime / config.difficultyRampDuration);
        WorldScrollSpeed = Mathf.Lerp(config.initialScrollSpeed, config.maxScrollSpeed, t);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Road Scrolling

    void ScrollWorld()
    {
        float scroll = WorldScrollSpeed * Time.deltaTime;

        foreach (var seg in _activeSegments)
            if (seg != null)
                seg.transform.position -= new Vector3(0f, 0f, scroll);
    }

    void RecycleSegments()
    {
        if (_activeSegments.Count == 0) return;

        GameObject first = _activeSegments[0];
        if (first == null) { _activeSegments.RemoveAt(0); return; }

        // If the back edge has passed behind the player (z < -despawn threshold)
        float backEdge = first.transform.position.z + config.roadSegmentLength;
        if (backEdge < -config.vehicleDespawnDistance)
        {
            _activeSegments.RemoveAt(0);
            MoveSegmentToFront(first);
        }
    }

    void MoveSegmentToFront(GameObject segment)
    {
        float frontZ = GetFrontZ();
        segment.transform.position = new Vector3(0f, 0f, frontZ);
        SpawnHazardsOn(segment);
        _activeSegments.Add(segment);
        _nextSegmentZ = frontZ + config.roadSegmentLength;
    }

    float GetFrontZ()
    {
        if (_activeSegments.Count == 0) return 0f;
        var last = _activeSegments[_activeSegments.Count - 1];
        return last != null
            ? last.transform.position.z + config.roadSegmentLength
            : _nextSegmentZ;
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Hazard Spawning

    void SpawnHazardsOn(GameObject segment)
    {
        if (config.hazardPrefabs == null || config.hazardPrefabs.Length == 0) return;
        if (Random.value > config.hazardSpawnChance) return;

        GameObject prefab = config.hazardPrefabs[Random.Range(0, config.hazardPrefabs.Length)];
        if (prefab == null) return;

        // Place randomly within road bounds, slightly offset from centre line
        float halfRoad = config.roadWidth * 0.45f;
        float hazardX  = Random.Range(config.centreLine - halfRoad,
                                      config.centreLine + halfRoad);
        float hazardZ  = segment.transform.position.z + config.roadSegmentLength * 0.5f;

        GameObject hazard = Instantiate(prefab,
            new Vector3(hazardX, 0f, hazardZ), Quaternion.identity, segment.transform);

        Debug.Log($"[RoadSpawner] Hazard spawned: {prefab.name}");
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Setup & Events

    void OnGameStart()
    {
        _running         = true;
        _elapsedPlayTime = 0f;
        WorldScrollSpeed = config.initialScrollSpeed;
        BuildInitialRoad();
    }

    void OnGameOver()
    {
        _running = false;
    }

    void OnChargingBegin()
    {
        // Slow to a stop during charging
        _running = false;
    }

    void OnChargingEnd()
    {
        _running = true;
    }

    void BuildInitialRoad()
    {
        // Clear existing
        foreach (var s in _activeSegments)
            if (s != null) Destroy(s);
        _activeSegments.Clear();

        if (config.roadSegmentPrefabs == null || config.roadSegmentPrefabs.Length == 0)
        {
            Debug.LogError("[RoadSpawner] No road segment prefabs assigned in config!");
            return;
        }

        _nextSegmentZ = 0f;
        int total = config.roadSegmentsAhead + 2; // a few extra behind start

        for (int i = 0; i < total; i++)
        {
            GameObject prefab = config.roadSegmentPrefabs[
                Random.Range(0, config.roadSegmentPrefabs.Length)];

            GameObject seg = Instantiate(prefab,
                new Vector3(0f, 0f, _nextSegmentZ - config.roadSegmentLength * 2),
                Quaternion.identity);

            _activeSegments.Add(seg);
            _nextSegmentZ += config.roadSegmentLength;
        }

        Debug.Log($"[RoadSpawner] Built initial road: {total} segments.");
    }

    #endregion
}
