using UnityEngine;

namespace KenyaScooter.Config
{
    /// <summary>
    /// Spawn distances and gap guarantees for both lanes (M9, M10, M11). The oncoming
    /// minimum gap is the design contract that every overtake opportunity is physically
    /// completable — a player who fails, failed on judgement, not unfairness (MDA Chain 1).
    /// </summary>
    [CreateAssetMenu(menuName = "Kenya Scooter/Traffic Config", fileName = "TrafficConfig")]
    public sealed class TrafficConfig : ScriptableObject
    {
        [Header("Spawn distances (metres ahead of the player)")]
        public float sameDirectionSpawnDistance = 110f;
        [Tooltip("Oncoming vehicles appear 120 units out — about 4 seconds of warning at base speed (M11).")]
        public float oncomingSpawnDistance = 120f;
        [Tooltip("Vehicles this far behind the player return to the pool.")]
        public float despawnBehindDistance = 30f;

        [Header("Same-direction lane (M10, M11)")]
        [Tooltip("Minimum clear road between queued vehicles, excluding vehicle length.")]
        public float minimumCarGap = 12f;
        public float sameDirectionGapJitter = 8f;
        [Tooltip("The session starts with this band already populated — the player is immediately behind a queue (M10).")]
        public float prewarmStartDistance = 12f;
        public float prewarmEndDistance = 90f;
        public float prewarmGap = 16f;
        public float prewarmGapJitter = 4f;
        public int maxSameDirection = 8;

        [Header("Oncoming lane (M11)")]
        [Tooltip("Minimum headway between oncoming vehicles. 70 m at combined closing speed ≈ a 3+ second overtake window.")]
        public float oncomingMinGap = 70f;
        public float oncomingGapJitter = 70f;
        public int maxOncoming = 8;

        [Header("Vehicle interaction (M12)")]
        [Tooltip("Same-direction vehicles yield when the player is within this distance behind them (Req §5.1).")]
        public float yieldDistance = 10f;
        [Tooltip("Oncoming vehicles swerve away when the player is within this longitudinal distance.")]
        public float swerveDistance = 14f;
        [Tooltip("Lateral distance under which surviving an oncoming pass counts as a near miss (M18).")]
        public float nearMissDistance = 1.7f;
        [Tooltip("How fast vehicles converge laterally onto their lane target (m/s).")]
        public float laneConvergeRate = 2.5f;
        [Tooltip("Degrees per second a vehicle turns to face its travel direction after a road turn.")]
        public float headingTurnRate = 360f;

        [Header("Boda boda swarms (Req §5.3 T03)")]
        public int bodaSwarmMin = 3;
        public int bodaSwarmMax = 5;
        public float bodaSwarmSpacing = 5f;
        public float bodaSwarmLateralJitter = 0.9f;

        [Header("Pools")]
        [Tooltip("Instances created per vehicle prefab at startup.")]
        public int poolSizePerPrefab = 6;
    }
}
