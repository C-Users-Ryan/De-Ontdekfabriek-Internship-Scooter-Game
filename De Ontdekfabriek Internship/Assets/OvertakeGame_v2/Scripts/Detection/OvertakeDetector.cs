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

            // Measure progress along the travel axis so overtake detection survives turns.
            Vector3 travelAxis = -RoadDirection.Current;
            float playerProgress = Vector3.Dot(transform.position, travelAxis);

            for (int i = 0; i < TrafficVehicle.Active.Count; i++)
            {
                var tv = TrafficVehicle.Active[i];
                if (tv == null || tv.isOncoming) continue;
                float carProgress = Vector3.Dot(tv.transform.position, travelAxis);

                if (playerProgress > carProgress + overtakeThreshold)
                {
                    if (!_alreadyOvertaken.Contains(tv))
                    {
                        _alreadyOvertaken.Add(tv);
                        rewardSystem?.OnOvertakeCompleted();
                    }
                }
                else if (playerProgress < carProgress - overtakeThreshold)
                    _alreadyOvertaken.Remove(tv);
            }
            _alreadyOvertaken.RemoveWhere(tv => tv == null || !tv.gameObject.activeSelf);
        }
    }
}
