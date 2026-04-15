using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// SessionManager — Controls the player handoff experience.
/// Shows the score screen after game over, then a break/attract screen
/// for the next player. The next player presses Start (or any key) to begin.
///
/// REQUIRED UI ELEMENTS (assign in Inspector):
///   - scorePanel     : shown immediately after game over
///   - handoffPanel   : shown after scorePanel, waits for next player
///   - attractPanel   : shown on very first launch
///   - finalScoreText : displays the final points tally
///   - handoffTimerText : counts down remaining break time
/// </summary>
public class SessionManager : MonoBehaviour
{
    public static SessionManager Instance { get; private set; }

    // ── UI References ─────────────────────────────────────────────
    [Header("UI Panels")]
    public GameObject attractPanel;
    public GameObject scorePanel;
    public GameObject handoffPanel;

    [Header("UI Text")]
    public TextMeshProUGUI finalScoreText;
    public TextMeshProUGUI handoffTimerText;
    public TextMeshProUGUI highScoreText;

    // ── Session Tracking ──────────────────────────────────────────
    [Header("Session")]
    public int sessionCount = 0;
    private int _highScore  = 0;

    // ── Private ───────────────────────────────────────────────────
    private float _handoffCountdown;
    private bool  _inHandoff;

    // ─────────────────────────────────────────────────────────────
    #region Unity Lifecycle

    void Awake()
    {
        Instance = this;
    }

    void OnEnable()
    {
        GameManager.Instance.OnAttractBegin.AddListener(ShowAttract);
        GameManager.Instance.OnGameStart.AddListener(HideAllPanels);
        GameManager.Instance.OnGameOver.AddListener(ShowScorePanel);
        GameManager.Instance.OnHandoffBegin.AddListener(ShowHandoffPanel);
    }

    void OnDisable()
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.OnAttractBegin.RemoveListener(ShowAttract);
        GameManager.Instance.OnGameStart.RemoveListener(HideAllPanels);
        GameManager.Instance.OnGameOver.RemoveListener(ShowScorePanel);
        GameManager.Instance.OnHandoffBegin.RemoveListener(ShowHandoffPanel);
    }

    void Update()
    {
        // Handoff countdown display
        if (_inHandoff)
        {
            _handoffCountdown -= Time.deltaTime;
            if (handoffTimerText != null)
                handoffTimerText.text = $"Next player in {Mathf.CeilToInt(_handoffCountdown)}s";

            // Any key / button press skips countdown and starts immediately
            if (Input.anyKeyDown)
                GameManager.Instance.StartGame();
        }

        // Attract screen: any key starts
        if (GameManager.Instance.CurrentState == GameManager.GameState.Attract)
        {
            if (Input.anyKeyDown)
                GameManager.Instance.StartGame();
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Panel Control

    void ShowAttract()
    {
        _inHandoff = false;
        SetPanels(attract: true, score: false, handoff: false);
    }

    void ShowScorePanel()
    {
        _inHandoff = false;
        int score = ScoringSystem.Instance != null ? ScoringSystem.Instance.TotalPoints : 0;

        if (score > _highScore)
        {
            _highScore = score;
        }

        if (finalScoreText != null)
            finalScoreText.text = $"{score:N0} pts";

        if (highScoreText != null)
            highScoreText.text = $"Best: {_highScore:N0} pts";

        sessionCount++;
        SetPanels(attract: false, score: true, handoff: false);
    }

    void ShowHandoffPanel()
    {
        _inHandoff        = true;
        _handoffCountdown = GameManager.Instance.handoffDuration;
        SetPanels(attract: false, score: false, handoff: true);
    }

    void HideAllPanels()
    {
        _inHandoff = false;
        SetPanels(attract: false, score: false, handoff: false);
    }

    void SetPanels(bool attract, bool score, bool handoff)
    {
        if (attractPanel  != null) attractPanel.SetActive(attract);
        if (scorePanel    != null) scorePanel.SetActive(score);
        if (handoffPanel  != null) handoffPanel.SetActive(handoff);
    }

    #endregion
}
