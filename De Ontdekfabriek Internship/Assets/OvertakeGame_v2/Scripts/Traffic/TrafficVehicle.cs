using UnityEngine;

namespace OvertakeGame
{
    /// <summary>
    /// Individual traffic car. Activated/deactivated by TrafficManager via object pool.
    /// </summary>
    public class TrafficVehicle : MonoBehaviour
    {
        public bool isOncoming;

        private float _speed;
        private bool  _active;

        public void Activate(Vector3 position, float speed, bool oncoming)
        {
            transform.position = position;
            _speed     = speed;
            isOncoming = oncoming;
            transform.rotation = oncoming
                ? Quaternion.Euler(0f, 180f, 0f)
                : Quaternion.identity;
            gameObject.SetActive(true);
            _active = true;
        }

        public void Deactivate()
        {
            _active = false;
            gameObject.SetActive(false);
        }

        void Update()
        {
            if (!_active) return;
            float dir = isOncoming ? -1f : 1f;
            transform.Translate(Vector3.forward * dir * _speed * Time.deltaTime, Space.World);
        }
    }
}
