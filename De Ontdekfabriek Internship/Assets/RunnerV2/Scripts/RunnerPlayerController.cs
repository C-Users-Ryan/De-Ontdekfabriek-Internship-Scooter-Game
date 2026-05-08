using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// RunnerPlayerController — Handles the player's lateral movement across the road.
///
/// The world scrolls toward the player (handled by RoadSpawner + TrafficManager).
/// The player moves LEFT and RIGHT freely across the full road width.
/// There are NO fixed lanes — the player can be anywhere on the road.
///
/// FEATURES:
///   • Free-roam left/right movement within road bounds
///   • Visual lean when moving sideways (via a child VisualRoot transform)
///   • Slows and stops smoothly during charging state
///   • Road bounds enforced — can't go off the edge
///
/// SETUP:
///   Attach to your scooter GameObject.
///   Assign config (same RunnerGameConfig as the manager).
///   Create a child called VisualRoot holding the mesh — assign to visualRoot.
/// </summary>
public class RunnerPlayerController : MonoBehaviour
{
    public static RunnerPlayerController Instance { get; private set; }

    [Header("Config")]
    public RunnerGameConfig config;

    [Header("References")]
    [Tooltip("Child GameObject holding the scooter mesh — this is what leans.")]
    public Transform visualRoot;

    // ── State ─────────────────────────────────────────────────────
    public bool IsAlive { get; private set; } = true;

    // ── Private ───────────────────────────────────────────────────
    private float _currentLean;
    private float _lateralInput;
    private bool  _chargingSlowdown;
    private float _chargingSpeedMult = 1f;

    // ─────────────────────────────────────────────────────────────
    #region Unity Lifecycle

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        RunnerGameManager.Instance.OnGameOver.AddListener(OnGameOver);
        RunnerGameManager.Instance.OnChargingBegin.AddListener(OnChargingBegin);
        RunnerGameManager.Instance.OnChargingEnd.AddListener(OnChargingEnd);
        RunnerGameManager.Instance.OnGameStart.AddListener(OnGameStart);
    }

    void OnDestroy()
    {
        if (RunnerGameManager.Instance == null) return;
        RunnerGameManager.Instance.OnGameOver.RemoveListener(OnGameOver);
        RunnerGameManager.Instance.OnChargingBegin.RemoveListener(OnChargingBegin);
        RunnerGameManager.Instance.OnChargingEnd.RemoveListener(OnChargingEnd);
        RunnerGameManager.Instance.OnGameStart.RemoveListener(OnGameStart);
    }

    void Update()
    {
        if (!RunnerGameManager.Instance.IsActive()) return;
        if (!IsAlive) return;

        ReadInput();
        MovePlayer();
        AnimateLean();
        HandleChargingSlowdown();
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Input

    void ReadInput()
    {
        _lateralInput = 0f;

        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  _lateralInput -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) _lateralInput += 1f;
        }

        var gp = Gamepad.current;
        if (gp != null)
        {
            float stickX = gp.leftStick.x.ReadValue();
            if (Mathf.Abs(stickX) > 0.1f)
                _lateralInput += stickX;
        }

        _lateralInput = Mathf.Clamp(_lateralInput, -1f, 1f);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Movement

    void MovePlayer()
    {
        if (config == null) return;

        float speed = config.lateralSpeed * _chargingSpeedMult;
        float newX  = transform.position.x + _lateralInput * speed * Time.deltaTime;

        // Clamp to road bounds
        float halfRoad = config.roadWidth * 0.5f;
        newX = Mathf.Clamp(newX, config.centreLine - halfRoad, config.centreLine + halfRoad);

        transform.position = new Vector3(newX, transform.position.y, transform.position.z);
    }

    void AnimateLean()
    {
        if (visualRoot == null || config == null) return;

        float targetLean   = -_lateralInput * config.lateralLeanAngle;
        _currentLean       = Mathf.Lerp(_currentLean, targetLean,
                                        config.leanSmoothing * Time.deltaTime);

        Vector3 euler = visualRoot.localEulerAngles;
        euler.z = _currentLean;
        visualRoot.localEulerAngles = euler;
    }

    void HandleChargingSlowdown()
    {
        if (!_chargingSlowdown) return;
        _chargingSpeedMult = Mathf.MoveTowards(_chargingSpeedMult, 0f,
                                               Time.deltaTime * 2f);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Game Events

    void OnGameStart()
    {
        IsAlive            = true;
        _chargingSlowdown  = false;
        _chargingSpeedMult = 1f;
    }

    void OnGameOver()
    {
        IsAlive = false;
        _lateralInput = 0f;
    }

    void OnChargingBegin()
    {
        _chargingSlowdown = true;
    }

    void OnChargingEnd()
    {
        _chargingSlowdown  = false;
        _chargingSpeedMult = 1f;
    }

    #endregion

    // ─────────────────────────────────────────────────────────────

    /// <summary>Is the player on the correct (left) side of the road?</summary>
    public bool IsOnCorrectSide()
    {
        if (config == null) return true;
        // In Kenya, drive on the LEFT — left of centre line is correct
        return transform.position.x < config.centreLine;
    }
}
