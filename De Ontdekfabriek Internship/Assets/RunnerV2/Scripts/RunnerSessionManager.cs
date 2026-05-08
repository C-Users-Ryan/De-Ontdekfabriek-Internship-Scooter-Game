using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// RunnerSessionManager — Manages the attract screen, score screen,
/// and handoff between players. Mirrors the main game's SessionManager
/// but wired to RunnerGameManager events.
/// </summary>
public class RunnerSessionManager : MonoBehaviour
{
    public static RunnerSessionManager Instance { get; private set; }

    [Header("UI Panels")]
    public GameObject attractPanel;
    public GameObject scorePanel;
    public GameObject handoffPanel;

    [Header("Score Screen")]
    public TextMeshProUGUI finalScoreText;
    public TextMeshProUGUI survivalTimeText;
    public TextMeshProUGUI highScoreText;
    public TextMeshProUGUI handoffTimerText;

    // ── Private ───────────────────────────────────────────────────
    private int   _highScore;
    private float _handoffCountdown;
    private bool  _inAttract;
    private bool  _inHandoff;

    // ─────────────────────────────────────────────────────────────

    void Awake() => Instance = this;

    void Start()
    {
        RunnerGameManager.Instance.OnAttractBegin.AddListener(ShowAttract);
        RunnerGameManager.Instance.OnGameStart.AddListener(HideAll);
        RunnerGameManager.Instance.OnGameOver.AddListener(ShowScore);
        RunnerGameManager.Instance.OnHandoffBegin.AddListener(ShowHandoff);
    }

    void OnDestroy()
    {
        if (RunnerGameManager.Instance == null) return;
        RunnerGameManager.Instance.OnAttractBegin.RemoveListener(ShowAttract);
        RunnerGameManager.Instance.OnGameStart.RemoveListener(HideAll);
        RunnerGameManager.Instance.OnGameOver.RemoveListener(ShowScore);
        RunnerGameManager.Instance.OnHandoffBegin.RemoveListener(ShowHandoff);
    }

    void Update()
    {
        if (_inHandoff)
        {
            _handoffCountdown -= Time.deltaTime;
            if (handoffTimerText != null)
                handoffTimerText.text = $"Next player in {Mathf.CeilToInt(_handoffCountdown)}s";
            if (AnyButtonPressed()) RunnerGameManager.Instance.StartGame();
        }

        if (_inAttract && AnyButtonPressed())
            RunnerGameManager.Instance.StartGame();
    }

    // ─────────────────────────────────────────────────────────────

    bool AnyButtonPressed()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.anyKey.wasPressedThisFrame) return true;
        var gp = Gamepad.current;
        if (gp != null && (gp.buttonSouth.wasPressedThisFrame ||
                           gp.startButton.wasPressedThisFrame)) return true;
        return false;
    }

    void ShowAttract()
    {
        _inAttract = true; _inHandoff = false;
        SetPanels(true, false, false);
    }

    void ShowScore()
    {
        _inAttract = false; _inHandoff = false;

        int score = RunnerScoringSystem.Instance != null
            ? RunnerScoringSystem.Instance.TotalPoints : 0;

        float time = RunnerScoringSystem.Instance != null
            ? RunnerScoringSystem.Instance.SurvivalSeconds : 0f;

        if (score > _highScore) _highScore = score;

        if (finalScoreText  != null) finalScoreText.text  = $"{score:N0} pts";
        if (highScoreText   != null) highScoreText.text   = $"Best: {_highScore:N0} pts";
        if (survivalTimeText != null)
            survivalTimeText.text =
                $"{Mathf.FloorToInt(time / 60f):00}:{Mathf.FloorToInt(time % 60f):00}";

        SetPanels(false, true, false);
    }

    void ShowHandoff()
    {
        _inAttract        = false;
        _inHandoff        = true;
        _handoffCountdown = RunnerGameManager.Instance.config != null
            ? RunnerGameManager.Instance.config.handoffDuration : 15f;
        SetPanels(false, false, true);
    }

    void HideAll()
    {
        _inAttract = false; _inHandoff = false;
        SetPanels(false, false, false);
    }

    void SetPanels(bool attract, bool score, bool handoff)
    {
        if (attractPanel != null) attractPanel.SetActive(attract);
        if (scorePanel   != null) scorePanel.SetActive(score);
        if (handoffPanel != null) handoffPanel.SetActive(handoff);
    }
}
