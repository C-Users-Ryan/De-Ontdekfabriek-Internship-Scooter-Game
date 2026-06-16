using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Controls;

namespace KenyaScooter.Player
{
    /// <summary>
    /// Visual lean of the scooter model (M6). Reads RAW steering input — not the
    /// accumulated lateral velocity — so the lean answers the player's hand. The steer
    /// lean is eased toward its target with SmoothDampAngle, so the tilt rolls in and out
    /// naturally instead of snapping at a constant rate. Hazard wobble (M21) is added on
    /// top un-smoothed, so a hit still reads as a sharp jolt rather than being damped away.
    /// </summary>
    public sealed class ScooterLean : MonoBehaviour
    {
        [SerializeField] private ScooterConfig config;
        [Tooltip("The scooter model child this script rotates. The root stays upright for physics.")]
        [SerializeField] private Transform visual;
        [SerializeField] private ScooterWobble wobble;
        [Tooltip("Seconds for the steering lean to ease toward its target. Higher = smoother/lazier, lower = snappier. ~0.18 is a calm ride.")]
        [SerializeField] private float leanSmoothTime = 0.18f;

        private float steerRoll;
        private float rollVelocity;

        private void Update()
        {
            float target = -ScooterInputRouter.Instance.Lateral * config.maxLeanAngle;
            steerRoll = Mathf.SmoothDampAngle(steerRoll, target, ref rollVelocity, Mathf.Max(0.0001f, leanSmoothTime));

            float roll = steerRoll + (wobble != null ? wobble.CurrentRoll : 0f);
            visual.localRotation = Quaternion.Euler(0f, 0f, roll);
        }
    }
}
