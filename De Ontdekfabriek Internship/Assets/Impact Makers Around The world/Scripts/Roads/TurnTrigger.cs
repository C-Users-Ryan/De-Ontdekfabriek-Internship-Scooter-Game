using UnityEngine;
using KenyaScooter.Core;
using KenyaScooter.Player;

namespace KenyaScooter.Roads
{
    /// <summary>
    /// Embedded in a turn tile (M3). When the player reaches the turn point, the
    /// world axis changes: RoadDirection.Turn fires, the camera rig rotates over
    /// 0.35 s, and every system that projects onto RoadDirection axes updates
    /// automatically. One-shot per activation; pooling re-arms it via OnEnable.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class TurnTrigger : MonoBehaviour
    {
        [Tooltip("Signed turn in degrees: +90 = right, -90 = left. Should match the parent tile's curveAngle.")]
        [SerializeField] private float turnDegrees = 90f;

        private bool consumed;

        private void OnEnable() => consumed = false;

        private void OnTriggerEnter(Collider other)
        {
            if (consumed || other.GetComponentInParent<PlayerController>() == null)
                return;

            consumed = true;
            RoadDirection.Turn(turnDegrees);
        }
    }
}
