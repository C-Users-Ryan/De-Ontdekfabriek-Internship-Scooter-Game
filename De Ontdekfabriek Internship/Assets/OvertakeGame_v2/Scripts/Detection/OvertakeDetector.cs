using UnityEngine;

namespace OvertakeGame
{
    /// <summary>
    /// Detects when the player successfully overtakes a same-direction traffic car.
    /// 
    /// An overtake is registered when:
    ///   1. The player's Z moves ahead of a traffic car's Z by overtakeThreshold units.
    ///   2. The player was previously behind that car (tracked per vehicle).
    /// 
    /// Attach to the player car. Wire rewardSystem in the Inspector.
    /// </summary>
    public class OvertakeDetector : MonoBehaviour
    {
        [Header("Overtake Detection")]
        [Tooltip("How many world units ahead of the car's centre the player must be before the overtake counts.")]
        public float overtakeThreshold = 3f;

        [Header("References")]
        public RewardSystem rewardSystem;

        // Tracks which cars the player has already overtaken (so we don't double-count)
        private System.Collections.Generic.HashSet<TrafficVehicle> _alreadyOvertaken
            = new System.Collections.Generic.HashSet<TrafficVehicle>();

        void Update()
        {
            if (GameManager.Instance?.CurrentState != GameManager.GameState.Playing) return;

            // Find all active traffic vehicles (pooled, so we check active ones)
            var allVehicles = FindObjectsByType<TrafficVehicle>(FindObjectsSortMode.None);
            float playerZ   = transform.position.z;

            foreach (var tv in allVehicles)
            {
                if (!tv.gameObject.activeSelf) continue;
                if (tv.isOncoming) continue; // only count same-direction overtakes

                float carZ = tv.transform.position.z;

                if (playerZ > carZ + overtakeThreshold)
                {
                    // Player has passed this car
                    if (!_alreadyOvertaken.Contains(tv))
                    {
                        _alreadyOvertaken.Add(tv);
                        rewardSystem?.OnOvertakeCompleted();
                    }
                }
                else if (playerZ < carZ - overtakeThreshold)
                {
                    // Car has moved ahead of the player again (e.g. player braked) — reset so it can be overtaken again
                    _alreadyOvertaken.Remove(tv);
                }
            }

            // Clean up references to deactivated/recycled vehicles
            _alreadyOvertaken.RemoveWhere(tv => tv == null || !tv.gameObject.activeSelf);
        }
    }
}
