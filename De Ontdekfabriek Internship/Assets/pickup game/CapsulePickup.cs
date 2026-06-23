using System.Collections.Generic;
using UnityEngine;

namespace CapsuleCity
{
    /// <summary>
    /// A capsule lying in the city for the player to collect. Each one registers
    /// itself in a shared <see cref="Active"/> list while it's available, so the
    /// navigation arrow can find the nearest one without any manager wiring.
    /// Just drop this on a capsule with a trigger collider.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class CapsulePickup : MonoBehaviour
    {
        /// <summary>Every pickup that's currently still on the ground.</summary>
        public static readonly List<CapsulePickup> Active = new List<CapsulePickup>();

        private void Reset()
        {
            // Pickups are detected by overlap, so the collider must be a trigger.
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);

        /// <summary>Called by the player when this capsule is picked up.</summary>
        public void Collect()
        {
            // Deactivating removes it from Active via OnDisable. Swap this for a
            // pooling / Destroy call if you prefer.
            gameObject.SetActive(false);
        }
    }
}
