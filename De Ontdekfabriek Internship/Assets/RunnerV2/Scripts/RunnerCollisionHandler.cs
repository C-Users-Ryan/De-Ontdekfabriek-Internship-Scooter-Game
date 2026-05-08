using UnityEngine;

/// <summary>
/// RunnerCollisionHandler — Detects when the player hits a vehicle and ends the run.
///
/// SETUP:
///   Attach to the player scooter.
///   The scooter needs a Collider (set as Trigger).
///   All traffic vehicles must be on a Layer included in vehicleLayer mask.
///   Tag vehicles as "Vehicle" OR use a LayerMask — both are supported.
///
/// HAZARD HITS (speed bumps, potholes):
///   These don't end the run — they slow the player briefly.
///   Tag hazard objects as "Hazard".
/// </summary>
public class RunnerCollisionHandler : MonoBehaviour
{
    [Header("Layers")]
    [Tooltip("Physics layer(s) that count as a vehicle collision (ends the run).")]
    public LayerMask vehicleLayer;

    [Tooltip("Physics layer(s) that count as a hazard (slows player briefly).")]
    public LayerMask hazardLayer;

    [Header("Hazard Settings")]
    [Tooltip("Speed multiplier applied briefly after hitting a hazard.")]
    [Range(0f, 1f)]
    public float hazardSlowMultiplier = 0.5f;

    [Tooltip("How long the hazard slowdown lasts.")]
    public float hazardSlowDuration = 1.5f;

    // ── Private ───────────────────────────────────────────────────
    private float _hazardSlowTimer;

    // ─────────────────────────────────────────────────────────────

    void OnTriggerEnter(Collider other)
    {
        if (!RunnerGameManager.Instance.IsPlaying()) return;

        // Vehicle hit — end the run
        if (IsInLayerMask(other.gameObject, vehicleLayer))
        {
            Debug.Log($"[CollisionHandler] Hit vehicle: {other.gameObject.name}");
            RunnerGameManager.Instance.TriggerGameOver();
            return;
        }

        // Hazard hit — brief slowdown only
        if (IsInLayerMask(other.gameObject, hazardLayer))
        {
            Debug.Log($"[CollisionHandler] Hit hazard: {other.gameObject.name}");
            _hazardSlowTimer = hazardSlowDuration;
        }
    }

    void Update()
    {
        if (_hazardSlowTimer > 0f)
            _hazardSlowTimer -= Time.deltaTime;
    }

    /// <summary>Returns 0..1 slow factor. 1 = full speed, less = slowed.</summary>
    public float GetHazardSlowFactor()
    {
        if (_hazardSlowTimer <= 0f) return 1f;
        return hazardSlowMultiplier;
    }

    static bool IsInLayerMask(GameObject obj, LayerMask mask)
    {
        return (mask.value & (1 << obj.layer)) != 0;
    }
}
