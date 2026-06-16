using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Controls;

namespace KenyaScooter.Player
{
    /// <summary>
    /// Visual lean of the scooter model (M6). Reads RAW steering input — not the
    /// accumulated lateral velocity — so the lean answers the player's hand
    /// immediately, before the position starts moving. That ordering (lean precedes
    /// movement) is what makes tilting feel like riding rather than dragging.
    /// Hazard wobble (M21) is added on top when a ScooterWobble sibling exists.
    /// </summary>
    public sealed class ScooterLean : MonoBehaviour
    {
        [SerializeField] private ScooterConfig config;
        [Tooltip("The scooter model child this script rotates. The root stays upright for physics.")]
        [SerializeField] private Transform visual;
        [SerializeField] private ScooterWobble wobble;

        private float currentRoll;

        private void Update()
        {
            float target = -ScooterInputRouter.Instance.Lateral * config.maxLeanAngle;
            if (wobble != null)
                target += wobble.CurrentRoll;

            currentRoll = Mathf.MoveTowardsAngle(currentRoll, target, config.leanResponse * Time.deltaTime);
            visual.localRotation = Quaternion.Euler(0f, 0f, currentRoll);
        }
    }
}
