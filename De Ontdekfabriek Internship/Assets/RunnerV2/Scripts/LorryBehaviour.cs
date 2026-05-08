using UnityEngine;

/// <summary>
/// LorryBehaviour — Large slow truck. Can be EITHER lane.
/// When oncoming: a wide wall of metal rushing at the player.
/// When same-dir: player overtakes it easily but it's wide so needs room.
///
/// isOncoming = set by TrafficManager (random)
/// baseSpeed  = low (3-5 m/s)
/// </summary>
public class LorryBehaviour : VehicleBehaviour
{
    [Header("Lorry Settings")]
    [Tooltip("Very slight drift. Lorries don't weave — just minor road imperfections.")]
    [Range(0f, 0.3f)]
    public float driftAmount = 0.08f;

    private float _driftTimer;

    protected override void Start()
    {
        baseSpeed = Random.Range(3f, 5f);
        base.Start();
    }

    protected override void OnBehaviourUpdate()
    {
        _driftTimer += Time.deltaTime * 0.3f;
        float drift = Mathf.Sin(_driftTimer) * driftAmount;

        Vector3 pos = transform.position;
        pos.x = SpawnX + drift;
        transform.position = pos;
    }
}
