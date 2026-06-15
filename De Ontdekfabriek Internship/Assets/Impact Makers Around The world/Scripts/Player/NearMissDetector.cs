using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;
using KenyaScooter.Traffic;

namespace KenyaScooter.Player
{
    /// <summary>
    /// Near-miss detection (M18): the frame an oncoming vehicle passes the player's
    /// plane within nearMissDistance laterally — and nothing was hit — the vehicle
    /// flashes and a camera shake fires. Deliberately no score change: the near miss
    /// is purely experiential, the game's strongest single sensation event (MDA A1/D5).
    /// Iterates TrafficVehicle.Active only (Req §17); once per vehicle per activation.
    /// </summary>
    public sealed class NearMissDetector : MonoBehaviour
    {
        [SerializeField] private TrafficConfig config;

        private void Update()
        {
            if (GameManager.State != GameState.Playing)
                return;

            Vector3 playerPosition = transform.position;
            float playerLong = RoadDirection.Longitudinal(playerPosition);
            float playerLat = RoadDirection.Lateral(playerPosition);

            var vehicles = TrafficVehicle.Active;
            for (int i = 0; i < vehicles.Count; i++)
            {
                TrafficVehicle vehicle = vehicles[i];
                if (vehicle.Direction != LaneDirection.Oncoming || vehicle.NearMissDone || vehicle.WasHitByPlayer)
                    continue;

                float delta = RoadDirection.Longitudinal(vehicle.transform.position) - playerLong;

                // Crossing the player's plane this frame, from ahead to behind.
                if (vehicle.NearMissPrevDelta != float.MaxValue && vehicle.NearMissPrevDelta > 0f && delta <= 0f
                    && Mathf.Abs(RoadDirection.Lateral(vehicle.transform.position) - playerLat) < config.nearMissDistance)
                {
                    vehicle.NearMissDone = true;
                    vehicle.FlashHighlight();
                    GameEvents.RaiseNearMiss(vehicle);
                }

                vehicle.NearMissPrevDelta = delta;
            }
        }
    }
}
