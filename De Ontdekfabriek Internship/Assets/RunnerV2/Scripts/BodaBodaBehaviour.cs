using UnityEngine;

/// <summary>
/// BodaBodaBehaviour — Fast motorcycle taxi. The most dangerous vehicle.
/// Always ONCOMING (right lane) — it moves fast and weaves unpredictably,
/// making it hard to predict where it will be when it reaches the player.
///
/// isOncoming = true  (always oncoming, right lane)
/// baseSpeed  = high (8-13 m/s on top of world scroll)
/// </summary>
public class BodaBodaBehaviour : VehicleBehaviour
{
    [Header("Boda Boda Settings")]
    [Tooltip("How often (seconds) the boda boda picks a new lateral position.")]
    public float repositionInterval = 1.2f;

    [Tooltip("How fast it snaps to the new lateral position.")]
    public float lateralSnapSpeed = 7f;

    [Tooltip("Maximum lateral offset from spawn X it can drift to.")]
    public float maxLateralRange = 2f;

    private float _repositionTimer;
    private float _targetX;

    protected override void Start()
    {
        isOncoming = true;  // always oncoming
        baseSpeed  = Random.Range(8f, 13f);
        base.Start();

        _targetX = SpawnX;
    }

    protected override void OnBehaviourUpdate()
    {
        // Periodically pick a new lateral target
        _repositionTimer -= Time.deltaTime;
        if (_repositionTimer <= 0f)
        {
            _repositionTimer = repositionInterval + Random.Range(-0.3f, 0.4f);
            _targetX = SpawnX + Random.Range(-maxLateralRange, maxLateralRange);
        }

        // Move toward lateral target
        Vector3 pos = transform.position;
        pos.x = Mathf.MoveTowards(pos.x, _targetX, lateralSnapSpeed * Time.deltaTime);
        transform.position = pos;
    }
}
