using UnityEngine;

namespace KenyaScooter.Config
{
    /// <summary>
    /// Which side of the road the country drives on, plus the lane geometry (M9, Req §4.5). Moving the
    /// game to a new location is just a new instance of this asset (SC4 — modular location system). All
    /// lane logic reads from RoadSideConfig.Active.
    /// </summary>
    [CreateAssetMenu(menuName = "Kenya Scooter/Road Side Config", fileName = "RoadSideConfig_Kenya")]
    public sealed class RoadSideConfig : ScriptableObject
    {
        /// <summary>Assigned by GameManager on Awake; the one instance every system reads.</summary>
        public static RoadSideConfig Active { get; set; }

        [Header("Side of the road (per-country)")]
        [Tooltip("Kenya drives on the left.")]
        public bool driveOnLeft = true;

        [Header("Geometry (metres)")]
        public float laneWidth = 3.25f;
        public float shoulderWidth = 1.5f;
        [Tooltip("How far the player may steer from the road centre (road edge clamp, Req §3.3).")]
        public float playerLateralLimit = 4.2f;

        [Header("Wrong-lane detection (M14)")]
        [Tooltip("Dead band around the centre line before the oncoming lane counts as entered.")]
        public float centreBuffer = 0.5f;
        [Tooltip("Seconds the player may stay in the oncoming lane before deductions start — long enough to complete a normal overtake (design note in D17).")]
        public float wrongLaneGraceSeconds = 5f;

        public float HalfLane => laneWidth * 0.5f;

        /// <summary>Signed lateral centre of the player's own lane along RoadDirection.SteerAxis.</summary>
        public float OwnLaneCentre => driveOnLeft ? -HalfLane : HalfLane;

        /// <summary>Signed lateral centre of the oncoming lane.</summary>
        public float OncomingLaneCentre => -OwnLaneCentre;

        /// <summary>-1 when the own side is left of centre, +1 when right.</summary>
        public float OwnSide => driveOnLeft ? -1f : 1f;
    }
}
