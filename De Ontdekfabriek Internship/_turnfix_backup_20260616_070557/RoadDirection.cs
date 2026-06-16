using System;
using UnityEngine;

namespace KenyaScooter.Core
{
    /// <summary>
    /// The single source of truth for which way the road runs and which way is sideways (M3, Req §2).
    /// Every spatial calculation projects onto these two axes instead of hardcoded X/Z, so it all
    /// keeps working after a 90° turn — the axes rotate, the maths stays the same.
    /// </summary>
    public static class RoadDirection
    {
        /// <summary>World-space direction of travel. World objects scroll along -Current (M1).</summary>
        public static Vector3 Current { get; private set; } = Vector3.forward;

        /// <summary>Lateral axis, positive to the player's right. Always Cross(up, Current).</summary>
        public static Vector3 SteerAxis { get; private set; } = Vector3.right;

        /// <summary>(old travel axis, new travel axis). Raised when a TurnTrigger fires.</summary>
        public static event Action<Vector3, Vector3> DirectionChanged;

        /// <summary>Signed distance of a world position along the travel axis.</summary>
        public static float Longitudinal(Vector3 worldPosition) => Vector3.Dot(worldPosition, Current);

        /// <summary>Signed distance of a world position along the steer axis.</summary>
        public static float Lateral(Vector3 worldPosition) => Vector3.Dot(worldPosition, SteerAxis);

        /// <summary>
        /// Rotates the travel axis around world up and snaps to the nearest cardinal
        /// direction, so axes stay exact after repeated 90° turns (M3).
        /// </summary>
        public static void Turn(float signedDegrees)
        {
            Vector3 previous = Current;
            Vector3 rotated = Quaternion.AngleAxis(signedDegrees, Vector3.up) * Current;
            Current = SnapToCardinal(rotated);
            SteerAxis = Vector3.Cross(Vector3.up, Current);
            DirectionChanged?.Invoke(previous, Current);
        }

        /// <summary>Returns to +Z at the start of every turn. Does not raise DirectionChanged — systems re-read axes on SessionReset.</summary>
        public static void ResetToDefault()
        {
            Current = Vector3.forward;
            SteerAxis = Vector3.right;
        }

        private static Vector3 SnapToCardinal(Vector3 direction)
        {
            return Mathf.Abs(direction.x) > Mathf.Abs(direction.z)
                ? new Vector3(Mathf.Sign(direction.x), 0f, 0f)
                : new Vector3(0f, 0f, Mathf.Sign(direction.z));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Current = Vector3.forward;
            SteerAxis = Vector3.right;
            DirectionChanged = null;
        }
    }
}
