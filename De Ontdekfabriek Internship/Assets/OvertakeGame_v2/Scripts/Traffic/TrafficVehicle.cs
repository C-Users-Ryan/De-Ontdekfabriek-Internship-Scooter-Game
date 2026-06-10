using UnityEngine;
using System.Collections.Generic;

namespace OvertakeGame
{
    /// <summary>
    /// A traffic vehicle in the world-moves model.
    /// Same-direction vehicles move at (WorldSpeed - ownSpeed) toward the player,
    /// so they appear slower than the player.
    /// Oncoming vehicles move at (WorldSpeed + ownSpeed) toward the player,
    /// so they appear much faster.
    /// </summary>
    public class TrafficVehicle : MonoBehaviour
    {
        /// <summary>All currently active TrafficVehicles. Updated by Activate/Deactivate.</summary>
        public static readonly List<TrafficVehicle> Active = new();

        /// <summary>This vehicle's own speed relative to world speed (m/s).</summary>
        public float ownSpeed;
        public bool isOncoming;

        private bool _active;

        public void Activate(Vector3 position, float speed, bool oncoming)
        {
            transform.position = position;
            ownSpeed = speed;
            isOncoming = oncoming;
            transform.rotation = oncoming
                ? Quaternion.Euler(0f, 180f, 0f)
                : Quaternion.identity;
            gameObject.SetActive(true);
            _active = true;
            if (!Active.Contains(this)) Active.Add(this);
        }

        public void Deactivate()
        {
            _active = false;
            gameObject.SetActive(false);
            Active.Remove(this);
        }

        void OnDisable() => Active.Remove(this);

        void Update()
        {
            if (!_active) return;

            float worldSpd = WorldSpeed.Instance != null ? WorldSpeed.Instance.Current : 10f;

            // How fast this vehicle moves toward the player along RoadDirection.Current
            float moveSpeed = isOncoming
                ? worldSpd + ownSpeed   // oncoming: world speed + their speed
                : worldSpd - ownSpeed;  // same-dir: world speed - their speed (slower = overtakeable)

            transform.Translate(RoadDirection.Current * moveSpeed * Time.deltaTime, Space.World);
        }
    }
}