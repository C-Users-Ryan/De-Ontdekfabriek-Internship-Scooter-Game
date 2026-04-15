using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// CollisionHandler — Detects collisions with traffic, walls, and obstacles.
/// On impact:
///   • Kills a percentage of current velocity (speed penalty)
///   • Fires OnCrash event (used by AudioManager, HUD screen shake etc.)
///
/// SETUP:
///   Attach to the same GameObject as ScooterController (which has the Rigidbody).
///   Assign the scooter tag as "Scooter" so DropOffZone can detect it.
/// </summary>
public class CollisionHandler : MonoBehaviour
{
    // ── Settings ──────────────────────────────────────────────────
    [Header("Collision Settings")]
    [Tooltip("Layers counted as crashable obstacles (vehicles, walls, buildings).")]
    public LayerMask crashLayers;

    [Tooltip("Minimum impact speed (km/h) to trigger a crash response.")]
    public float minCrashSpeedKmh = 5f;

    [Tooltip("What fraction of velocity is lost on crash. 0.5 = lose half your speed.")]
    [Range(0f, 1f)]
    public float velocityLossFraction = 0.5f;

    [Tooltip("Cooldown in seconds between crash events (prevents rapid repeated triggers).")]
    public float crashCooldown = 0.5f;

    // ── Events ────────────────────────────────────────────────────
    [Header("Events")]
    public UnityEvent OnCrash;   // Subscribe: AudioManager, CameraShake, HUD flash

    // ── Private ───────────────────────────────────────────────────
    private Rigidbody _rb;
    private float     _lastCrashTime = -999f;

    // ─────────────────────────────────────────────────────────────

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!GameManager.Instance.IsPlaying()) return;

        // Cooldown check
        if (Time.time - _lastCrashTime < crashCooldown) return;

        // Layer check
        if ((crashLayers.value & (1 << collision.gameObject.layer)) == 0) return;

        // Speed check — only react to meaningful impacts
        float impactSpeedKmh = _rb.linearVelocity.magnitude * 3.6f;
        if (impactSpeedKmh < minCrashSpeedKmh) return;

        ApplySpeedPenalty(impactSpeedKmh);
        _lastCrashTime = Time.time;
        OnCrash?.Invoke();

        Debug.Log($"[CollisionHandler] Crash at {impactSpeedKmh:F1} km/h");
    }

    void ApplySpeedPenalty(float impactSpeedKmh)
    {
        // Scale penalty with impact severity (bigger crash = more speed lost)
        float speedFraction = Mathf.Clamp01(impactSpeedKmh / 60f);
        float actualLoss    = velocityLossFraction * speedFraction;

        _rb.linearVelocity *= (1f - actualLoss);
    }
}
