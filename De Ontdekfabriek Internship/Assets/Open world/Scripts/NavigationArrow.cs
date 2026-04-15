using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// NavigationArrow — Rotates a UI arrow to point toward the active drop-off target.
/// Also moves a minimap destination pin to the correct minimap position.
///
/// SETUP:
///   - Attach to a UI Image in your Canvas (the arrow sprite).
///   - Assign minimapPin (another UI Image, the destination dot on the minimap).
///   - Assign minimapCamera (the overhead orthographic camera for the minimap).
/// </summary>
public class NavigationArrow : MonoBehaviour
{
    public static NavigationArrow Instance { get; private set; }

    // ── References ────────────────────────────────────────────────
    [Header("Arrow")]
    [Tooltip("The UI RectTransform of the arrow image in world-space HUD.")]
    public RectTransform arrowRect;

    [Header("Minimap")]
    public RectTransform  minimapPin;
    public Camera         minimapCamera;
    public RectTransform  minimapRect;

    // ── Private ───────────────────────────────────────────────────
    private Transform _target;
    private Transform _scooter;
    private bool      _hasTarget;

    // ─────────────────────────────────────────────────────────────
    #region Unity Lifecycle

    void Awake()
    {
        Instance = this;
        SetArrowVisible(false);
    }

    void Start()
    {
        if (ScooterController.Instance != null)
            _scooter = ScooterController.Instance.transform;
    }

    void LateUpdate()
    {
        if (!_hasTarget || _scooter == null) return;

        UpdateArrow();
        UpdateMinimapPin();
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Public API

    public void SetTarget(Transform target)
    {
        _target    = target;
        _hasTarget = target != null;
        SetArrowVisible(_hasTarget);
        if (minimapPin != null) minimapPin.gameObject.SetActive(_hasTarget);
    }

    public void ClearTarget()
    {
        _target    = null;
        _hasTarget = false;
        SetArrowVisible(false);
        if (minimapPin != null) minimapPin.gameObject.SetActive(false);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Internal

    void UpdateArrow()
    {
        if (arrowRect == null) return;

        // Direction from scooter to target in world space, projected onto XZ plane
        Vector3 toTarget = _target.position - _scooter.position;
        toTarget.y = 0f;

        if (toTarget == Vector3.zero) return;

        // Angle between scooter's forward and the direction to target
        float angle = Vector3.SignedAngle(Vector3.forward, toTarget, Vector3.up)
                    - _scooter.eulerAngles.y;

        arrowRect.localEulerAngles = new Vector3(0f, 0f, -angle);
    }

    void UpdateMinimapPin()
    {
        if (minimapPin == null || minimapCamera == null || minimapRect == null) return;

        // Convert world position to minimap viewport position
        Vector3 viewportPos = minimapCamera.WorldToViewportPoint(_target.position);

        // Map viewport (0-1) to minimap RectTransform local space
        Vector2 minimapSize = minimapRect.sizeDelta;
        minimapPin.anchoredPosition = new Vector2(
            (viewportPos.x - 0.5f) * minimapSize.x,
            (viewportPos.y - 0.5f) * minimapSize.y);
    }

    void SetArrowVisible(bool visible)
    {
        if (arrowRect != null)
            arrowRect.gameObject.SetActive(visible);
    }

    #endregion
}
