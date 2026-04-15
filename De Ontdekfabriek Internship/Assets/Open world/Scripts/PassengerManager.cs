using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PassengerManager — Spawns passenger NPCs at defined points around Nairobi.
/// Handles pickup detection, destination assignment, and delivery.
///
/// SETUP:
///   1. Create empty GameObjects around the map for spawn points and drop-off zones.
///   2. Assign them to spawnPoints and dropOffPoints arrays in the Inspector.
///   3. Create a Passenger prefab with a visible mesh. Tag it "Passenger".
///   4. Add DropOffZone script + trigger collider to each drop-off point object.
/// </summary>
public class PassengerManager : MonoBehaviour
{
    public static PassengerManager Instance { get; private set; }

    // ── Setup ─────────────────────────────────────────────────────
    [Header("Prefabs & Points")]
    public GameObject passengerPrefab;
    public Transform[] spawnPoints;

    [Tooltip("Each entry must have a DropOffZone component attached.")]
    public DropOffZone[] dropOffZones;

    [Header("Passenger Settings")]
    [Tooltip("How many passengers can wait in the world at once.")]
    public int maxWaitingPassengers = 3;

    [Tooltip("Seconds a passenger waits before giving up.")]
    public float passengerPatienceSeconds = 30f;

    [Tooltip("Pickup detection radius around the scooter (metres).")]
    public float pickupRadius = 4f;

    [Tooltip("Seconds added to timer just for picking up a passenger.")]
    public float pickupTimeBonus = 5f;

    // ── State ─────────────────────────────────────────────────────
    public bool HasPassenger         { get; private set; }
    public Transform ActiveDropOff   { get; private set; }
    public float ActiveTripDistance  { get; private set; }

    private readonly List<GameObject> _waitingPassengers = new();
    private DropOffZone _activeDropOffZone;
    private float _pickupCheckTimer;

    // ─────────────────────────────────────────────────────────────
    #region Unity Lifecycle

    void Awake() => Instance = this;

    void OnEnable()
    {
        GameManager.Instance.OnGameStart.AddListener(OnGameStart);
        GameManager.Instance.OnGameOver.AddListener(OnGameOver);
    }

    void OnDisable()
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.OnGameStart.RemoveListener(OnGameStart);
        GameManager.Instance.OnGameOver.RemoveListener(OnGameOver);
    }

    void Update()
    {
        if (!GameManager.Instance.IsPlaying()) return;

        _pickupCheckTimer -= Time.deltaTime;
        if (_pickupCheckTimer <= 0f)
        {
            _pickupCheckTimer = 0.15f;
            if (!HasPassenger) CheckPickup();
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Game Events

    void OnGameStart()
    {
        HasPassenger      = false;
        ActiveDropOff     = null;
        _activeDropOffZone = null;
        StartCoroutine(SpawnLoop());
    }

    void OnGameOver()
    {
        StopAllCoroutines();
        ClearPassengers();
        if (_activeDropOffZone != null)
            _activeDropOffZone.SetMarkerVisible(false);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Spawning

    IEnumerator SpawnLoop()
    {
        while (GameManager.Instance.IsPlaying())
        {
            if (_waitingPassengers.Count < maxWaitingPassengers && spawnPoints.Length > 0)
                SpawnPassenger();
            yield return new WaitForSeconds(3f);
        }
    }

    void SpawnPassenger()
    {
        Transform sp = spawnPoints[Random.Range(0, spawnPoints.Length)];
        GameObject p = Instantiate(passengerPrefab, sp.position, Quaternion.identity);
        _waitingPassengers.Add(p);
        StartCoroutine(DespawnAfter(p, passengerPatienceSeconds));
    }

    IEnumerator DespawnAfter(GameObject passenger, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (passenger != null && _waitingPassengers.Contains(passenger))
        {
            _waitingPassengers.Remove(passenger);
            Destroy(passenger);
        }
    }

    void ClearPassengers()
    {
        foreach (var p in _waitingPassengers)
            if (p != null) Destroy(p);
        _waitingPassengers.Clear();
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Pickup & Delivery

    void CheckPickup()
    {
        if (dropOffZones == null || dropOffZones.Length == 0) return;
        Vector3 scooterPos = ScooterController.Instance.transform.position;

        for (int i = _waitingPassengers.Count - 1; i >= 0; i--)
        {
            GameObject p = _waitingPassengers[i];
            if (p == null) { _waitingPassengers.RemoveAt(i); continue; }

            if (Vector3.Distance(scooterPos, p.transform.position) <= pickupRadius)
            {
                PickUp(p, i);
                return;
            }
        }
    }

    void PickUp(GameObject passenger, int index)
    {
        _waitingPassengers.RemoveAt(index);
        Destroy(passenger);
        HasPassenger = true;

        // Pick a random drop-off zone
        _activeDropOffZone = dropOffZones[Random.Range(0, dropOffZones.Length)];
        ActiveDropOff      = _activeDropOffZone.transform;

        // Measure rough trip distance
        ActiveTripDistance = Vector3.Distance(
            ScooterController.Instance.transform.position,
            ActiveDropOff.position);

        // Start trip timer
        DropOffZone.RecordPickup();

        // Show marker above the destination
        _activeDropOffZone.SetMarkerVisible(true);

        // Small time bonus for pickup
        TimerSystem.Instance.AddTime(pickupTimeBonus);

        // Point nav arrow at target
        NavigationArrow.Instance?.SetTarget(ActiveDropOff);

        // Play pickup sound
        AudioManager.Instance?.PlayPickupSound();

        Debug.Log($"[PassengerManager] Picked up! Destination: " +
                  $"{_activeDropOffZone.landmarkName} ({ActiveTripDistance:F0}m)");
    }

    /// <summary>Called by DropOffZone when the scooter enters the correct zone.</summary>
    public void DeliverPassenger(float secondsTaken)
    {
        if (!HasPassenger) return;

        HasPassenger       = false;
        _activeDropOffZone = null;
        ActiveDropOff      = null;

        ScoringSystem.Instance?.RegisterDelivery(ActiveTripDistance, secondsTaken);
        TimerSystem.Instance?.AddTime(TimerSystem.Instance.timeExtensionOnDelivery);
        NavigationArrow.Instance?.ClearTarget();

        Debug.Log($"[PassengerManager] Delivered in {secondsTaken:F1}s!");
    }

    #endregion
}
