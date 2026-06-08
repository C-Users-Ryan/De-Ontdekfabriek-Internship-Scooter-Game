using UnityEngine;

namespace OvertakeGame
{
    public class CheckpointTrigger : MonoBehaviour
    {
        [HideInInspector] public CheckpointManager manager;
        public string playerTag = "Player";
        private bool _triggered;

        void OnTriggerEnter(Collider other)
        {
            if (_triggered || !other.CompareTag(playerTag)) return;
            _triggered = true;
            manager?.OnPlayerReachedCheckpoint();
        }
    }
}
