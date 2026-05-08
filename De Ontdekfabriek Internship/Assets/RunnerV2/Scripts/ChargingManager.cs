using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ChargingManager — Triggers a charging stop at a configurable interval.
/// During the stop:
///   1. World slows and stops (via RunnerGameManager.BeginCharging)
///   2. A green energy station appears
///   3. A fact card is shown with a Kenya renewable energy fact
///   4. Battery bar fills up
///   5. After chargingDuration seconds, gameplay resumes
///
/// STUDENT TIP — changing the facts:
///   Edit the greenEnergyFacts[] array in RunnerGameConfig.
///
/// STUDENT TIP — changing the station visuals:
///   Replace the prefabs in RunnerGameConfig.chargingStationPrefabs[].
///
/// UI SETUP (assign in Inspector):
///   factCardPanel     — the full-screen or overlay panel shown during stop
///   factText          — TMP label for the energy fact
///   stationTypeText   — TMP label for the station name (e.g. "Solar Station")
///   batteryFillImage  — Image with Fill type, filled during charging
///   countdownText     — TMP label counting down the stop duration
/// </summary>
public class ChargingManager : MonoBehaviour
{
    public static ChargingManager Instance { get; private set; }

    [Header("Config")]
    public RunnerGameConfig config;

    [Header("UI")]
    public GameObject      factCardPanel;
    public TextMeshProUGUI factText;
    public TextMeshProUGUI stationTypeText;
    public Image           batteryFillImage;
    public TextMeshProUGUI countdownText;

    // ── Private ───────────────────────────────────────────────────
    private float  _chargingTimer;
    private float  _stopCountdown;
    private bool   _inStop;
    private int    _factIndex;
    private GameObject _currentStation;

    // ─────────────────────────────────────────────────────────────
    #region Unity Lifecycle

    void Awake() => Instance = this;

    void Start()
    {
        RunnerGameManager.Instance.OnGameStart.AddListener(OnGameStart);
        RunnerGameManager.Instance.OnGameOver.AddListener(OnGameOver);

        if (factCardPanel != null) factCardPanel.SetActive(false);
    }

    void OnDestroy()
    {
        if (RunnerGameManager.Instance == null) return;
        RunnerGameManager.Instance.OnGameStart.RemoveListener(OnGameStart);
        RunnerGameManager.Instance.OnGameOver.RemoveListener(OnGameOver);
    }

    void Update()
    {
        if (!RunnerGameManager.Instance.IsPlaying()) return;
        if (config == null) return;

        _chargingTimer -= Time.deltaTime;
        if (_chargingTimer <= 0f && !_inStop)
            StartCoroutine(DoChargingStop());
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Charging Stop Sequence

    IEnumerator DoChargingStop()
    {
        _inStop = true;
        RunnerGameManager.Instance.BeginCharging();

        // Spawn a charging station ahead of the player
        SpawnStation();

        // Wait a moment for the world to slow — feel the stop
        yield return new WaitForSeconds(1.2f);

        // Show the fact card
        ShowFactCard();

        // Fill the battery bar over the stop duration
        float elapsed = 0f;
        float duration = config.chargingDuration;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            if (batteryFillImage != null)
                batteryFillImage.fillAmount = t;

            if (countdownText != null)
                countdownText.text = $"{Mathf.CeilToInt(duration - elapsed)}s";

            yield return null;
        }

        // Hide card and resume
        HideFactCard();
        RemoveStation();

        // Reset timer for next stop
        _chargingTimer = config.chargingInterval;
        _inStop = false;

        RunnerGameManager.Instance.EndCharging();
    }

    void SpawnStation()
    {
        if (config.chargingStationPrefabs == null ||
            config.chargingStationPrefabs.Length == 0) return;

        GameObject prefab = config.chargingStationPrefabs[
            Random.Range(0, config.chargingStationPrefabs.Length)];

        if (prefab == null) return;

        // Spawn at the side of the road, ahead of the player
        _currentStation = Instantiate(prefab,
            new Vector3(config.centreLine + config.roadWidth * 0.6f, 0f, 25f),
            Quaternion.identity);
    }

    void RemoveStation()
    {
        if (_currentStation != null)
            Destroy(_currentStation);
    }

    void ShowFactCard()
    {
        if (factCardPanel != null) factCardPanel.SetActive(true);

        // Cycle through facts
        if (config.greenEnergyFacts != null && config.greenEnergyFacts.Length > 0)
        {
            string fact = config.greenEnergyFacts[_factIndex % config.greenEnergyFacts.Length];
            _factIndex++;

            if (factText != null) factText.text = fact;
        }

        // Station type label pulled from the spawned prefab name
        if (stationTypeText != null && _currentStation != null)
            stationTypeText.text = _currentStation.name.Replace("(Clone)", "").Trim();
    }

    void HideFactCard()
    {
        if (factCardPanel != null) factCardPanel.SetActive(false);
        if (batteryFillImage != null) batteryFillImage.fillAmount = 0f;
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Events

    void OnGameStart()
    {
        _chargingTimer = config != null ? config.chargingInterval : 120f;
        _inStop        = false;
        _factIndex     = 0;
        HideFactCard();
    }

    void OnGameOver()
    {
        StopAllCoroutines();
        _inStop = false;
        HideFactCard();
        RemoveStation();
    }

    #endregion
}
