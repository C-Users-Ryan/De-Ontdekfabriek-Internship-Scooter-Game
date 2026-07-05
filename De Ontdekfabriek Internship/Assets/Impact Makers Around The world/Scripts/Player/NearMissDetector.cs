using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;
using KenyaScooter.Roads;
using KenyaScooter.Traffic;

namespace KenyaScooter.Player
{
    /// <summary>
    /// Spots near misses (M18): the frame an oncoming vehicle slips past the player within nearMissDistance
    /// sideways — without actually hitting — the vehicle flashes and the camera shakes. On purpose, there is
    /// no score change: a near miss is pure feel, the game's biggest single hit of sensation (MDA A1/D5). It
    /// only walks TrafficVehicle.Active (Req §17), and fires once per vehicle per spawn.
    /// </summary>
    public sealed class NearMissDetector : MonoBehaviour
    {
        [SerializeField] private TrafficConfig config;

        private void Update()
        {
            if (GameManager.State != GameState.Playing)
                return;

            Vector3 playerPosition = transform.position;
            // Road space, so "the vehicle crossing the player's plane" is measured along the road through a bend.
            float playerArc = RoadSequencer.Instance != null
                ? RoadSequencer.Instance.PlayerArc
                : RoadDirection.Longitudinal(playerPosition);
            float playerLat = RoadDirection.Lateral(playerPosition);

            var vehicles = TrafficVehicle.Active;
            for (int i = 0; i < vehicles.Count; i++)
            {
                TrafficVehicle vehicle = vehicles[i];
                if (vehicle.Direction != LaneDirection.Oncoming || vehicle.NearMissDone || vehicle.WasHitByPlayer)
                    continue;

                float delta = vehicle.RoadArc - playerArc;

                // Crossing the player's plane this frame, from ahead to behind.
                if (vehicle.NearMissPrevDelta != float.MaxValue && vehicle.NearMissPrevDelta > 0f && delta <= 0f
                    && Mathf.Abs(vehicle.RoadLateral - playerLat) < config.nearMissDistance)
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
