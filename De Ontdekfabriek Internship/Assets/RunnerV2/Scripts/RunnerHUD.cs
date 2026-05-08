using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// RunnerHUD — Updates all in-game UI elements every frame.
///
/// ELEMENTS:
///   scoreText        — current total points
///   multiplierText   — current score multiplier (e.g. "x2.4")
///   streakBar        — fill image showing streak progress toward max multiplier
///   wrongSideWarning — red warning panel shown when on the wrong side of road
///   survivalTimeText — MM:SS elapsed time
///   speedText        — current world scroll speed
/// </summary>
public class RunnerHUD : MonoBehaviour
{
    public static RunnerHUD Instance { get; private set; }

    [Header("Score")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI multiplierText;
    public Image           streakBar;

    [Header("Wrong side warning")]
    public GameObject      wrongSideWarning;

    [Header("Survival")]
    public TextMeshProUGUI survivalTimeText;
    public TextMeshProUGUI speedText;

    // ─────────────────────────────────────────────────────────────

    void Awake() => Instance = this;

    void Start()
    {
        RunnerGameManager.Instance.OnGameStart.AddListener(OnGameStart);
        RunnerGameManager.Instance.OnGameOver.AddListener(OnGameOver);

        if (LaneSafetyMonitor.Instance != null)
            LaneSafetyMonitor.Instance.OnSideChanged.AddListener(OnSideChanged);

        if (wrongSideWarning != null) wrongSideWarning.SetActive(false);
    }

    void OnDestroy()
    {
        if (RunnerGameManager.Instance == null) return;
        RunnerGameManager.Instance.OnGameStart.RemoveListener(OnGameStart);
        RunnerGameManager.Instance.OnGameOver.RemoveListener(OnGameOver);
    }

    void Update()
    {
        if (!RunnerGameManager.Instance.IsActive()) return;

        UpdateScore();
        UpdateMultiplier();
        UpdateSurvivalTime();
        UpdateSpeed();
    }

    // ─────────────────────────────────────────────────────────────

    void UpdateScore()
    {
        if (scoreText == null || RunnerScoringSystem.Instance == null) return;
        scoreText.text = $"{RunnerScoringSystem.Instance.TotalPoints:N0}";
    }

    void UpdateMultiplier()
    {
        if (LaneSafetyMonitor.Instance == null) return;

        float mult = LaneSafetyMonitor.Instance.CurrentMultiplier;

        if (multiplierText != null)
            multiplierText.text = $"x{mult:F1}";

        if (streakBar != null)
        {
            // Map multiplier between 1 and maxStreak to 0..1
            float config = RunnerGameManager.Instance.config != null
                ? RunnerGameManager.Instance.config.maxStreakMultiplier : 3f;
            streakBar.fillAmount = Mathf.Clamp01((mult - 1f) / (config - 1f));
        }
    }

    void UpdateSurvivalTime()
    {
        if (survivalTimeText == null || RunnerScoringSystem.Instance == null) return;
        float t = RunnerScoringSystem.Instance.SurvivalSeconds;
        survivalTimeText.text = $"{Mathf.FloorToInt(t / 60f):00}:{Mathf.FloorToInt(t % 60f):00}";
    }

    void UpdateSpeed()
    {
        if (speedText == null || RoadSpawner.Instance == null) return;
        speedText.text = $"{RoadSpawner.Instance.WorldScrollSpeed * 3.6f:F0} km/h";
    }

    void OnSideChanged(bool isOnWrongSide)
    {
        if (wrongSideWarning != null)
            wrongSideWarning.SetActive(isOnWrongSide);
    }

    void OnGameStart() => gameObject.SetActive(true);
    void OnGameOver()  => gameObject.SetActive(false);
}
