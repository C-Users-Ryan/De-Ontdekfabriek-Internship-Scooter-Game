using UnityEngine;

namespace CapsuleCity
{
    /// <summary>
    /// The spot where carried capsules are delivered. Mark one object in the scene
    /// with this and a trigger collider. The navigation arrow points here whenever
    /// the player is carrying something.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class DropOffPoint : MonoBehaviour
    {
        /// <summary>The active drop-off in the scene (assumes a single one).</summary>
        public static DropOffPoint Instance { get; private set; }

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void Awake() => Instance = this;
        private void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
