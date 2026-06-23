using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;
using KenyaScooter.Player;

namespace KenyaScooter.Traffic
{
    /// <summary>
    /// Overtake detection (M13, Req §7.1). Pure dot-product comparison along
    /// RoadDirection.Current — correct after 90° turns. A pass goes through three
    /// stages stored on the vehicle: PassStarted (player was behind), PassPending
    /// (player is ahead), PassDone (player returned to their own lane — the
    /// confirmation Requirements §7.1 adds on top of the MDA threshold). A collision
    /// with the vehicle invalidates the pass (PlayerCollisionHandler sets the flag).
    /// Iterates TrafficVehicle.Active only — no scene scans (Req §17).
    /// </summary>
    public sealed class OvertakeDetector : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [Tooltip("Extra clearance beyond the vehicle's half length before behind/ahead flips.")]
        [SerializeField] private float passMargin = 1.5f;

        private void Update()
        {
            if (GameManager.State != GameState.Playing)
                return;

            float playerLong = RoadDirection.Longitudinal(player.position);
            bool inOwnLane = WrongLaneDetector.Instance == null || WrongLaneDetector.Instance.IsInOwnLane;

            var vehicles = TrafficVehicle.Active;
            for (int i = 0; i < vehicles.Count; i++)
            {
                TrafficVehicle vehicle = vehicles[i];
                if (vehicle.Direction != LaneDirection.SameDirection || vehicle.PassDone || vehicle.PassInvalidated)
                    continue;

                float threshold = vehicle.length * 0.5f + passMargin;
                float vehicleLong = RoadDirection.Longitudinal(vehicle.transform.position);

                if (!vehicle.PassStarted)
                {
                    if (playerLong < vehicleLong - threshold)
                        vehicle.PassStarted = true;
                    continue;
                }

                if (!vehicle.PassPending && playerLong > vehicleLong + threshold)
                {
                    vehicle.PassPending = true;
                    // A legal overtake is made toward the oncoming side (the opposite of the
                    // country's own side): for Kenya (drive-on-left) that means passing on the right.
                    float playerLat = RoadDirection.Lateral(player.position);
                    float vehicleLat = RoadDirection.Lateral(vehicle.transform.position);
                    float ownSide = RoadSideConfig.Active != null ? RoadSideConfig.Active.OwnSide : -1f;
                    vehicle.PassOnCorrectSide = (playerLat - vehicleLat) * (-ownSide) > 0f;
                }

                if (vehicle.PassPending && inOwnLane)
                {
                    vehicle.PassDone = true;
                    if (vehicle.PassOnCorrectSide)
                        GameEvents.RaiseOvertakeCompleted(vehicle);   // points + counts as a clean overtake
                    else
                        GameEvents.RaiseIllegalOvertake(vehicle);     // no points, corrective warning only
                }
            }
        }
    }
}
