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

            float playerZ = transform.position.z;
            var allVehicles = FindObjectsByType<TrafficVehicle>(FindObjectsSortMode.None);

            foreach (var tv in allVehicles)
            {
                if (!tv.gameObject.activeSelf || tv.isOncoming) continue;
                float carZ = tv.transform.position.z;

                if (playerZ > carZ + overtakeThreshold)
                {
                    if (!_alreadyOvertaken.Contains(tv))
                    {
                        _alreadyOvertaken.Add(tv);
                        rewardSystem?.OnOvertakeCompleted();
                    }
                }
                else if (playerZ < carZ - overtakeThreshold)
                    _alreadyOvertaken.Remove(tv);
            }
            _alreadyOvertaken.RemoveWhere(tv => tv == null || !tv.gameObject.activeSelf);
        }
    }
}
