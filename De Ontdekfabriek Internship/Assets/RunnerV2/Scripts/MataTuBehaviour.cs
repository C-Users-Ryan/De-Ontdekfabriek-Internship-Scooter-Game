using UnityEngine;

/// <summary>
/// MataTuBehaviour — Kenyan minibus. Same-direction vehicle (left lane).
/// Randomly slows down to simulate picking up passengers, then speeds back up.
/// Player must time their overtake around these unpredictable slow-downs.
///
/// isOncoming = false  (same direction as player, left lane)
/// baseSpeed  = 3-4    (slow enough that player always overtakes)
/// </summary>
public class MataTuBehaviour : VehicleBehaviour
{
    [Header("Matatu Settings")]
    [Tooltip("How often the matatu checks whether to brake (seconds).")]
    public float brakeCheckInterval = 4f;

    [Tooltip("How long the matatu slows for when picking up a passenger.")]
    public float brakeDuration = 2.5f;

    [Tooltip("Speed multiplier while braking. 0.1 = nearly stopped.")]
    [Range(0f, 0.5f)]
    public float brakingSpeedMult = 0.15f;

    [Tooltip("Slight lateral wobble to make it feel alive.")]
    public float wobbleAmount = 0.25f;

    private float _brakeCheckTimer;
    private float _brakeCountdown;
    private bool  _isBraking;
    private float _wobbleTimer;

    protected override void Start()
    {
        isOncoming = false;    // always same-direction
        baseSpeed  = Random.Range(3f, 4.5f);
        base.Start();

        _brakeCheckTimer = brakeCheckInterval;
    }

    protected override void OnBehaviourUpdate()
    {
        HandleBraking();
        HandleWobble();
    }

    void HandleBraking()
    {
        if (_isBraking)
        {
            CurrentSpeed    = baseSpeed * brakingSpeedMult;
            _brakeCountdown -= Time.deltaTime;
            if (_brakeCountdown <= 0f)
            {
                _isBraking   = false;
                CurrentSpeed = baseSpeed;
            }
        }
        else
        {
            _brakeCheckTimer -= Time.deltaTime;
            if (_brakeCheckTimer <= 0f)
            {
                _brakeCheckTimer = brakeCheckInterval + Random.Range(-1f, 1.5f);

                // 40% chance to actually brake when the timer fires
                if (Random.value < 0.4f)
                {
                    _isBraking      = true;
                    _brakeCountdown = brakeDuration;
                }
            }
        }
    }

    void HandleWobble()
    {
        _wobbleTimer += Time.deltaTime * 1.5f;
        float wobbleX = Mathf.Sin(_wobbleTimer) * wobbleAmount;

        Vector3 pos = transform.position;
        pos.x = SpawnX + wobbleX;
        transform.position = pos;
    }
}
