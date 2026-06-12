using UnityEngine;
using System.Collections.Generic;

namespace OvertakeGame
{
    public class OvertakeDetector : MonoBehaviour
    {
        [Header("Overtake Detection")]
        public float overtakeThreshold = 3f;

        [Header("References")]
        public RewardSystem rewardSystem;

        private HashSet<TrafficVehicle> _alreadyOvertaken = new();

        void Update()
        {
            if (GameManager.Instance?.CurrentState != GameManager.GameState.Playing) return;

            // Project positions onto the "ahead" axis — works on any travel axis after turns.
            // -RoadDirection.Current points in the direction the player is "travelling toward".
            Vector3 aheadAxis   = -RoadDirection.Current;
            float   playerAhead = Vector3.Dot(transform.position, aheadAxis);

            // TrafficVehicle.Active is maintained by Activate/Deactivate — no scene scan needed.
            var allVehicles = TrafficVehicle.Active;
            for (int i = 0; i < allVehicles.Count; i++)
            {
                var tv = allVehicles[i];
                if (tv.isOncoming) continue;  // Active list guarantees activeSelf — skip check
                float carAhead = Vector3.Dot(tv.transform.position, aheadAxis);

                if (playerAhead > carAhead + overtakeThreshold)
                {
                    if (_alreadyOvertaken.Add(tv))  // HashSet.Add returns true only when newly inserted
                        rewardSystem?.OnOvertakeCompleted();
                }
                else if (playerAhead < carAhead - overtakeThreshold)
                    _alreadyOvertaken.Remove(tv);
            }
            // Remove any vehicles that were deactivated (and therefore removed from Active)
            _alreadyOvertaken.RemoveWhere(tv => !tv.gameObject.activeSelf);
        }
    }
}