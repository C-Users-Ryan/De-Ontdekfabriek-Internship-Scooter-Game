using UnityEngine;

public class TurnTrigger : MonoBehaviour
{
    [SerializeField] private PlayerRigTurnController rig;

    [Header("Turn Settings")]
    [SerializeField] private float turnDegrees = 90f;
    [SerializeField] private float turnDuration = 2f;

    private bool triggered;

    private void OnTriggerEnter(Collider other)
    {
        if (triggered) return;
        if (!other.CompareTag("Player")) return;

        triggered = true;

        rig.TurnDegrees(turnDegrees, turnDuration);
    }
}