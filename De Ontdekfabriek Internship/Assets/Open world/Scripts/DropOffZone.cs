using UnityEngine;

/// <summary>
/// DropOffZone — Place on each destination landmark in Nairobi.
/// Requires a Trigger Collider on the same GameObject.
///
/// When the scooter enters while carrying a passenger AND this is the active
/// drop-off zone, it completes the delivery and awards points + time.
///
/// SETUP:
///   1. Create an empty GameObject at each landmark (e.g. "Westgate Mall").
///   2. Add a BoxCollider — set isTrigger = true.
///   3. Add this script.
///   4. Optionally assign destinationMarker to show a floating marker when active.
///   5. Add all drop-off GameObjects to PassengerManager.dropOffPoints[].
/// </summary>
[RequireComponent(typeof(Collider))]
public class DropOffZone : MonoBehaviour
{
    // ── Landmark Info ─────────────────────────────────────────────
    [Header("Landmark")]
    [Tooltip("Name shown on the HUD when this is the active destination.")]
    public string landmarkName = "Destination";

    [Tooltip("Optional world-space marker shown above the zone when it is active.")]
    public GameObject destinationMarker;

    // ── Static Pickup Timer ───────────────────────────────────────
    // Shared across all zones — only one trip active at a time.
    private static float _pickupTimestamp = 0f;

    // ─────────────────────────────────────────────────────────────
    #region Unity Lifecycle

    void Start()
    {
        GetComponent<Collider>().isTrigger = true;
        SetMarkerVisible(false);
    }

    void OnTriggerEnter(Collider other)
    {
        // Only react to the player scooter
        if (!other.CompareTag("Scooter")) return;

        // Only during active gameplay
        if (!GameManager.Instance.IsPlaying()) return;

        // Only if a passenger is on board
        if (!PassengerManager.Instance.HasPassenger) return;

        // Only if THIS zone is the currently assigned drop-off
        if (PassengerManager.Instance.ActiveDropOff != transform) return;

        float secondsTaken = Time.time - _pickupTimestamp;
        PassengerManager.Instance.DeliverPassenger(secondsTaken);
        SetMarkerVisible(false);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Public API

    /// <summary>
    /// Call this the moment a passenger is picked up to start the trip timer.
    /// PassengerManager calls this in PickUp().
    /// </summary>
    public static void RecordPickup()
    {
        _pickupTimestamp = Time.time;
    }

    /// <summary>Show or hide the destination marker floating above this zone.</summary>
    public void SetMarkerVisible(bool visible)
    {
        if (destinationMarker != null)
            destinationMarker.SetActive(visible);
    }

    #endregion
}
