using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// HUDController — Drives all in-game HUD elements.
///   • Speedometer needle + digital readout
///   • Countdown timer (flashes red when warning)
///   • Tip rating popup ("GREAT!" / "GOOD!" / "BAD!")
///   • Points display
///
/// ASSIGN IN INSPECTOR:
///   speedometerNeedle  — RectTransform of the needle image
///   speedText          — TMP label for km/h number
///   timerText          — TMP label for MM:SS countdown
///   pointsText         — TMP label for current points
///   tipPopup           — parent GameObject of the tip popup
///   tipRatingText      — TMP label inside tipPopup
/// </summary>
public class HUDController : MonoBehaviour
{
    public static HUDController Instance { get; private set; }

    // ── Speedometer ───────────────────────────────────────────────
    [Header("Speedometer")]
    public RectTransform    speedometerNeedle;
    public TextMeshProUGUI  speedText;
    [Tooltip("Needle angle at 0 km/h (degrees, e.g. -130).")]
    public float needleMinAngle = -130f;
    [Tooltip("Needle angle at max speed (degrees, e.g. 130).")]
    public float needleMaxAngle =  130f;

    // ── Timer ─────────────────────────────────────────────────────
    [Header("Timer")]
    public TextMeshProUGUI timerText;
    public Color           normalTimerColor  = Color.white;
    public Color           warningTimerColor = Color.red;

    // ── Points ────────────────────────────────────────────────────
    [Header("Points")]
    public TextMeshProUGUI pointsText;

    // ── Tip Popup ─────────────────────────────────────────────────
    [Header("Tip Popup")]
    public GameObject       tipPopup;
    public TextMeshProUGUI  tipRatingText;
    public TextMeshProUGUI  tipPointsText;
    [Tooltip("How long the popup stays on screen.")]
    public float tipPopupDuration = 2f;

    // ── Private ───────────────────────────────────────────────────
    private Coroutine _tipCoroutine;

    // ─────────────────────────────────────────────────────────────
    #region Unity Lifecycle

    void Awake()
    {
        Instance = this;
        if (tipPopup != null) tipPopup.SetActive(false);
    }

    void OnEnable()
    {
        GameManager.Instance.OnGameStart.AddListener(OnGameStart);
        GameManager.Instance.OnGameOver.AddListener(OnGameOver);
        if (ScoringSystem.Instance != null)
            ScoringSystem.Instance.OnDelivery.AddListener(ShowTipPopup);
    }

    void OnDisable()
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.OnGameStart.RemoveListener(OnGameStart);
        GameManager.Instance.OnGameOver.RemoveListener(OnGameOver);
        if (ScoringSystem.Instance != null)
            ScoringSystem.Instance.OnDelivery.RemoveListener(ShowTipPopup);
    }

    void Update()
    {
        if (!GameManager.Instance.IsPlaying()) return;

        UpdateSpeedometer();
        UpdateTimer();
        UpdatePoints();
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region HUD Updates

    void UpdateSpeedometer()
    {
        if (ScooterController.Instance == null) return;

        float speedKmh   = ScooterController.Instance.SpeedKmh;
        float maxSpeedKmh = ScooterController.Instance.AccelBrake.maxSpeedKmh;
        float t          = Mathf.Clamp01(speedKmh / maxSpeedKmh);

        // Needle rotation
        if (speedometerNeedle != null)
        {
            float angle = Mathf.Lerp(needleMinAngle, needleMaxAngle, t);
            speedometerNeedle.localEulerAngles = new Vector3(0f, 0f, -angle);
        }

        if (speedText != null)
            speedText.text = $"{Mathf.RoundToInt(speedKmh)} km/h";
    }

    void UpdateTimer()
    {
        if (TimerSystem.Instance == null || timerText == null) return;

        float t = TimerSystem.Instance.timeRemaining;
        int   m = Mathf.FloorToInt(t / 60f);
        int   s = Mathf.FloorToInt(t % 60f);

        timerText.text  = $"{m:00}:{s:00}";
        timerText.color = TimerSystem.Instance.IsWarning ? warningTimerColor : normalTimerColor;
    }

    void UpdatePoints()
    {
        if (ScoringSystem.Instance == null || pointsText == null) return;
        pointsText.text = $"{ScoringSystem.Instance.TotalPoints:N0} pts";
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Tip Popup

    void ShowTipPopup(ScoringSystem.TipRating rating, int points)
    {
        if (tipPopup == null) return;

        if (_tipCoroutine != null)
            StopCoroutine(_tipCoroutine);

        string label = rating switch
        {
            ScoringSystem.TipRating.Great => "GREAT!",
            ScoringSystem.TipRating.Good  => "GOOD!",
            _                             => "BAD!"
        };

        if (tipRatingText != null) tipRatingText.text = label;
        if (tipPointsText != null) tipPointsText.text = $"+{points:N0}";

        _tipCoroutine = StartCoroutine(ShowThenHide());
    }

    IEnumerator ShowThenHide()
    {
        tipPopup.SetActive(true);
        yield return new WaitForSeconds(tipPopupDuration);
        tipPopup.SetActive(false);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Game Events

    void OnGameStart()  => gameObject.SetActive(true);
    void OnGameOver()   => gameObject.SetActive(false);

    #endregion
}
