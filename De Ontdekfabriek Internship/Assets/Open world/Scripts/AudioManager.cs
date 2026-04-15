using UnityEngine;

/// <summary>
/// AudioManager — Handles all in-game audio for the electric scooter.
///
/// Electric scooter sound design:
///   • motorSource   — high-pitched electric whine, pitch & volume scale with speed
///   • windSource    — wind rush loop, volume scales with speed
///   • ambienceSource — Nairobi city background loop (matatus, street noise)
///   • pickupClip    — short sting when passenger is collected
///   • deliveryClip  — sting on successful delivery
///   • warningClip   — alarm when timer is low
///
/// SETUP:
///   Add 3 AudioSource components to this GameObject.
///   Assign each in the Inspector.
///   Assign your AudioClips.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    // ── Audio Sources ─────────────────────────────────────────────
    [Header("Audio Sources")]
    public AudioSource motorSource;
    public AudioSource windSource;
    public AudioSource ambienceSource;
    public AudioSource sfxSource;   // For one-shot SFX

    // ── Clips ─────────────────────────────────────────────────────
    [Header("Clips")]
    public AudioClip motorLoop;
    public AudioClip windLoop;
    public AudioClip ambienceLoop;
    public AudioClip pickupClip;
    public AudioClip deliveryGreatClip;
    public AudioClip deliveryBadClip;
    public AudioClip timerWarningClip;

    // ── Motor Tuning ──────────────────────────────────────────────
    [Header("Motor Tuning")]
    public float motorMinPitch  = 0.8f;
    public float motorMaxPitch  = 2.0f;
    public float motorMinVolume = 0.05f;
    public float motorMaxVolume = 0.5f;

    // ── Wind Tuning ───────────────────────────────────────────────
    [Header("Wind Tuning")]
    [Tooltip("Speed (km/h) at which wind sound reaches full volume.")]
    public float windFullSpeedKmh = 40f;
    public float windMaxVolume    = 0.6f;

    // ── Private ───────────────────────────────────────────────────
    private bool _warningPlayed;

    // ─────────────────────────────────────────────────────────────
    #region Unity Lifecycle

    void Awake() => Instance = this;

    void OnEnable()
    {
        GameManager.Instance.OnGameStart.AddListener(OnGameStart);
        GameManager.Instance.OnGameOver.AddListener(OnGameOver);

        if (TimerSystem.Instance != null)
            TimerSystem.Instance.OnTimerWarning.AddListener(OnTimerWarning);

        if (ScoringSystem.Instance != null)
            ScoringSystem.Instance.OnDelivery.AddListener(OnDelivery);

        if (PassengerManager.Instance != null)
        {
            // PassengerManager fires pickup via DeliverPassenger, 
            // but pickup sound is triggered separately — hook via a direct call 
            // or add a UnityEvent to PassengerManager if you prefer.
        }
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
        if (ScooterController.Instance == null) return;

        UpdateMotor();
        UpdateWind();
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Motor & Wind

    void UpdateMotor()
    {
        if (motorSource == null) return;

        float normSpeed = Mathf.Clamp01(
            ScooterController.Instance.SpeedKmh /
            ScooterController.Instance.AccelBrake.maxSpeedKmh);

        motorSource.pitch  = Mathf.Lerp(motorMinPitch,  motorMaxPitch,  normSpeed);
        motorSource.volume = Mathf.Lerp(motorMinVolume, motorMaxVolume, normSpeed);
    }

    void UpdateWind()
    {
        if (windSource == null) return;

        float normSpeed    = Mathf.Clamp01(
            ScooterController.Instance.SpeedKmh / windFullSpeedKmh);
        windSource.volume = normSpeed * windMaxVolume;
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Game Events

    void OnGameStart()
    {
        _warningPlayed = false;

        PlayLoop(motorSource,    motorLoop);
        PlayLoop(windSource,     windLoop);
        PlayLoop(ambienceSource, ambienceLoop);
    }

    void OnGameOver()
    {
        if (motorSource    != null) motorSource.Stop();
        if (windSource     != null) windSource.Stop();
        // Keep ambience running on score screen for atmosphere
    }

    void OnTimerWarning()
    {
        if (_warningPlayed) return;
        _warningPlayed = true;
        PlaySFX(timerWarningClip);
    }

    void OnDelivery(ScoringSystem.TipRating rating, int _)
    {
        AudioClip clip = rating == ScoringSystem.TipRating.Bad
            ? deliveryBadClip
            : deliveryGreatClip;
        PlaySFX(clip);
    }

    /// <summary>Call this from PassengerManager when a passenger is picked up.</summary>
    public void PlayPickupSound()
    {
        PlaySFX(pickupClip);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Helpers

    void PlayLoop(AudioSource source, AudioClip clip)
    {
        if (source == null || clip == null) return;
        source.clip   = clip;
        source.loop   = true;
        source.Play();
    }

    void PlaySFX(AudioClip clip)
    {
        if (sfxSource == null || clip == null) return;
        sfxSource.PlayOneShot(clip);
    }

    #endregion
}
