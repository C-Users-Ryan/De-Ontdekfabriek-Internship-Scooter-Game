using UnityEngine;

/// <summary>
/// RunnerScoringSystem — Awards points per second, multiplied by the lane safety streak.
///
/// FORMULA:
///   points per frame = config.pointsPerSecond * laneMultiplier * Time.deltaTime
///
/// The lane multiplier comes from LaneSafetyMonitor:
///   Correct side, long streak → up to config.maxStreakMultiplier (e.g. x3)
///   Wrong side                → config.wrongSidePenaltyMultiplier (e.g. x0.25)
/// </summary>
public class RunnerScoringSystem : MonoBehaviour
{
    public static RunnerScoringSystem Instance { get; private set; }

    [Header("Config")]
    public RunnerGameConfig config;

    // ── Public State ──────────────────────────────────────────────
    public int   TotalPoints     { get; private set; }
    public float SurvivalSeconds { get; private set; }

    // ─────────────────────────────────────────────────────────────

    void Awake() => Instance = this;

    void Start()
    {
        RunnerGameManager.Instance.OnGameStart.AddListener(ResetScore);
        RunnerGameManager.Instance.OnChargingEnd.AddListener(AwardCleanStopBonus);
    }

    void OnDestroy()
    {
        if (RunnerGameManager.Instance == null) return;
        RunnerGameManager.Instance.OnGameStart.RemoveListener(ResetScore);
        RunnerGameManager.Instance.OnChargingEnd.RemoveListener(AwardCleanStopBonus);
    }

    void Update()
    {
        if (!RunnerGameManager.Instance.IsPlaying()) return;
        if (config == null) return;

        SurvivalSeconds += Time.deltaTime;

        float multiplier = LaneSafetyMonitor.Instance != null
            ? LaneSafetyMonitor.Instance.CurrentMultiplier
            : 1f;

        float earned = config.pointsPerSecond * multiplier * Time.deltaTime;
        TotalPoints  += Mathf.RoundToInt(earned);
    }

    void AwardCleanStopBonus()
    {
        // Bonus for reaching a charging stop without recent collision
        TotalPoints += config.cleanStopBonus;
        Debug.Log($"[RunnerScoring] Clean stop bonus! +{config.cleanStopBonus} pts. Total: {TotalPoints}");
    }

    void ResetScore()
    {
        TotalPoints     = 0;
        SurvivalSeconds = 0f;
    }
}
