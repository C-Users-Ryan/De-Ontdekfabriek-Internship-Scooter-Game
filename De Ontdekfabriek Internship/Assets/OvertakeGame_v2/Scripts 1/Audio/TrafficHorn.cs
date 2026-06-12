using UnityEngine;

namespace OvertakeGame
{
    /// <summary>
    /// Attach to each traffic vehicle prefab alongside its trigger collider.
    /// When the player enters the vehicle's proximity trigger, a horn plays.
    ///
    /// SETUP:
    ///   1. Add a second (larger) Sphere or Box Collider to the traffic prefab.
    ///      Set IsTrigger = true. Make it 2–3x the size of the vehicle.
    ///      This is the "proximity zone" — separate from the collision collider.
    ///   2. Attach this script to the prefab root.
    ///   3. Make sure the collider is on a layer that detects the Player layer.
    /// </summary>
    public class TrafficHorn : MonoBehaviour
    {
        [Tooltip("Tag of the player object.")]
        public string playerTag = "Player";

        [Tooltip("Seconds between horn honks from the same vehicle. Prevents spam.")]
        public float hornCooldown = 3f;

        private float _lastHornTime = -99f;

        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(playerTag)) return;
            if (Time.time - _lastHornTime < hornCooldown) return;
            _lastHornTime = Time.time;
            AudioManager.I?.PlayHorn();
        }
    }
}