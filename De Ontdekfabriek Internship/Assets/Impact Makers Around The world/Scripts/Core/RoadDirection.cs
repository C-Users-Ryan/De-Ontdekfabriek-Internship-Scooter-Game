using UnityEngine;

namespace KenyaScooter.Core
{
    /// <summary>
    /// The travel frame the whole game projects onto (M3, Req §2). Rebuilt 2026-06-17:
    /// the frame is now CONSTANT — forward is always +Z and right always +X. The road no
    /// longer rotates a global compass on a turn; instead RoadSequencer bends the road
    /// around the stationary player, so the player always rides "forward" while the world
    /// curves. Because this frame never flips, every system that projects onto these axes
    /// (traffic, hazards, the overtake/near-miss/wrong-lane detectors, the player) stays
    /// stable across turns, and the rewind no longer has a moving compass to desync.
    /// </summary>
    public static class RoadDirection
    {
        /// <summary>World-space direction of travel. Constant +Z — world objects scroll along -Current (M1).</summary>
        public static Vector3 Current => Vector3.forward;

        /// <summary>Lateral axis, positive to the player's right. Constant +X (= Cross(up, forward)).</summary>
        public static Vector3 SteerAxis => Vector3.right;

        /// <summary>
        /// Signed rate the road is bending right now, in degrees/second (set each frame by
        /// RoadSequencer from the curve tile under the player). + bends right, - bends left.
        /// The camera bank and the scooter's turn-lean read this so a curve reads on screen.
        /// </summary>
        public static float CurveRate { get; private set; }

        /// <summary>True while the road is bending hard enough to count as "in a turn" (M3).
        /// Collisions are skipped while this holds — the corner is a chaotic moment, so being
        /// hit mid-turn would be unfair. Driven by the live CurveRate, not a fixed timer.</summary>
        public static bool IsTurning => Mathf.Abs(CurveRate) > TurnCurveThreshold;

        // deg/s. A turn produces CurveRate = (degrees / turn span)*speed, so a 90° turn over 200 m at
        // 10-20 m/s is only ~4.5-9 deg/s — the old 15 threshold never tripped on the authored geometry, so the
        // mid-turn collision grace never fired. 4 trips for a genuine bend at cruise speed but stays clear of a
        // gentle sub-45° bend or steering noise.
        private const float TurnCurveThreshold = 4f;

        /// <summary>Signed distance of a world position along the travel axis (forward = +Z).</summary>
        public static float Longitudinal(Vector3 worldPosition) => worldPosition.z;

        /// <summary>Signed distance of a world position along the steer axis (right = +X).</summary>
        public static float Lateral(Vector3 worldPosition) => worldPosition.x;

        /// <summary>Called by RoadSequencer every frame with the road's current bend rate (deg/s).</summary>
        public static void SetCurveRate(float degreesPerSecond) => CurveRate = degreesPerSecond;

        /// <summary>Kept for the session-flow call sites (GameManager). The frame is constant now,
        /// so this only clears the live bend rate at the start of a turn.</summary>
        public static void ResetToDefault() => CurveRate = 0f;

        // Static state survives an editor play session when domain reload is disabled.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => CurveRate = 0f;
    }
}
