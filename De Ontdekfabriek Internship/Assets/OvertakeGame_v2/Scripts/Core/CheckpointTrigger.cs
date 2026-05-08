using UnityEngine;

namespace OvertakeGame
{
    /// <summary>
    /// Placed on the checkpoint prefab. Notifies CheckpointManager when the player enters.
    /// The trigger collider on the prefab should be set to IsTrigger = true.
    /// </summary>
    public class CheckpointTrigger : MonoBehaviour
    {
        [HideInInspector] public CheckpointManager manager;

        [Tooltip("Tag of the player GameObject.")]
        public string playerTag = "Player";

        private bool _triggered;

        void OnTriggerEnter(Collider other)
        {
            if (_triggered) return;
            if (!other.CompareTag(playerTag)) return;
            _triggered = true;
            manager?.OnPlayerReachedCheckpoint();
        }
    }
}
