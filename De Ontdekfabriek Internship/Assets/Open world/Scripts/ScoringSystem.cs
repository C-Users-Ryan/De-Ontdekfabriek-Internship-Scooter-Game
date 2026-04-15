using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// ScoringSystem — Tracks total points, calculates speed bonuses and tip ratings.
///
/// POINT FORMULA:
///   Base points  = distance (metres) * distancePointsPerMetre
///   Speed bonus  = base * speedMultiplier (if delivered before idealTime)
///   Total        += base + speedBonus
/// </summary>
public class ScoringSystem : MonoBehaviour
{
    public static ScoringSystem Instance { get; private set; }

    // ── Tuning ────────────────────────────────────────────────────
    [Header("Scoring")]
    [Tooltip("Points awarded per metre of trip distance.")]
    public float distancePointsPerMetre = 2f;

    [Tooltip("If delivery time is under idealDeliverySeconds, max speed bonus applies.")]
    public float idealDeliverySeconds = 20f;

    [Tooltip("Maximum speed multiplier on top of base (e.g. 2 = double points for fastest delivery).")]
    public float maxSpeedMultiplier = 2f;

    // ── Tip Thresholds ────────────────────────────────────────────
    [Header("Tip Rating Thresholds")]
    [Tooltip("Delivery time (seconds) under which you get a GREAT rating.")]
    public float greatThreshold = 15f;

    [Tooltip("Delivery time under which you get a GOOD rating (above great).")]
    public float goodThreshold  = 30f;

    // ── Events ────────────────────────────────────────────────────
    public enum TipRating { Great, Good, Bad }

    [System.Serializable]
    public class TipEvent : UnityEvent<TipRating, int> {}

    [Header("Events")]
    [Tooltip("Fires with the tip rating and points earned for that delivery.")]
    public TipEvent OnDelivery;

    // ── Public State ──────────────────────────────────────────────
    public int TotalPoints { get; private set; }

    // ─────────────────────────────────────────────────────────────
    #region Unity Lifecycle

    void Awake() => Instance = this;

    void OnEnable()
    {
        GameManager.Instance.OnGameStart.AddListener(ResetScore);
    }

    void OnDisable()
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.OnGameStart.RemoveListener(ResetScore);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Public API

    /// <summary>
    /// Called by PassengerManager on successful delivery.
    /// </summary>
    /// <param name="distanceMetres">Straight-line distance of the trip.</param>
    /// <param name="secondsTaken">How long since pickup.</param>
    public void RegisterDelivery(float distanceMetres, float secondsTaken)
    {
        // Base
        int basePoints = Mathf.RoundToInt(distanceMetres * distancePointsPerMetre);

        // Speed bonus — falls off linearly from idealDeliverySeconds
        float speedRatio  = Mathf.Clamp01(1f - (secondsTaken / idealDeliverySeconds));
        int speedBonus    = Mathf.RoundToInt(basePoints * speedRatio * (maxSpeedMultiplier - 1f));

        int earned = basePoints + speedBonus;
        TotalPoints += earned;

        // Tip rating
        TipRating rating = secondsTaken <= greatThreshold ? TipRating.Great
                         : secondsTaken <= goodThreshold  ? TipRating.Good
                         : TipRating.Bad;

        OnDelivery?.Invoke(rating, earned);

        Debug.Log($"[ScoringSystem] Delivery! Base:{basePoints} Bonus:{speedBonus} " +
                  $"Rating:{rating} Total:{TotalPoints}");
    }

    #endregion

    // ─────────────────────────────────────────────────────────────

    void ResetScore() => TotalPoints = 0;
}
