using UnityEngine;

namespace CapsuleCity
{
    /// <summary>
    /// The player capsule. Drives around a flat city on the XZ plane with WASD /
    /// arrow keys and carries pickup capsules to the drop-off point.
    ///
    /// Movement uses Rigidbody.MovePosition on a non-kinematic body so the capsule
    /// still sweeps against (and stops at) buildings, while sidestepping the
    /// velocity → linearVelocity API rename so it compiles on any Unity version.
    /// Pickup / drop-off is handled here via trigger overlaps, so the capsules and
    /// the drop-off zone only need a trigger collider and their marker script.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class CapsulePlayer : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("Metres per second.")]
        [SerializeField] private float moveSpeed = 8f;
        [Tooltip("How quickly the capsule yaws to face its travel direction (deg/sec).")]
        [SerializeField] private float turnSpeed = 540f;
        [Tooltip("Move relative to the main camera's facing instead of world axes. Handy for a rotating follow camera.")]
        [SerializeField] private bool cameraRelative = false;

        [Header("Carrying")]
        [Tooltip("How many capsules the player can hold before they must drop off.")]
        [SerializeField] private int carryCapacity = 1;

        /// <summary>How many capsules are currently being carried.</summary>
        public int Carried { get; private set; }
        /// <summary>Total capsules delivered to the drop-off so far.</summary>
        public int Delivered { get; private set; }
        public bool IsCarrying => Carried > 0;
        public bool IsFull => Carried >= carryCapacity;

        private Rigidbody body;
        private Vector3 moveInput;

        private void Reset()
        {
            // Make a sensible body the moment the script is added in the editor.
            var rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.constraints = RigidbodyConstraints.FreezeRotation; // stay upright
            body.interpolation = RigidbodyInterpolation.Interpolate;
        }

        private void Update()
        {
            float h = Input.GetAxisRaw("Horizontal"); // A/D, ←/→
            float v = Input.GetAxisRaw("Vertical");    // W/S, ↑/↓
            moveInput = new Vector3(h, 0f, v);
            if (moveInput.sqrMagnitude > 1f) moveInput.Normalize();

            if (cameraRelative && Camera.main != null)
            {
                // Flatten the camera's facing onto the ground and steer relative to it.
                Vector3 fwd = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;
                Vector3 right = Vector3.ProjectOnPlane(Camera.main.transform.right, Vector3.up).normalized;
                moveInput = right * moveInput.x + fwd * moveInput.z;
            }
        }

        private void FixedUpdate()
        {
            // Move on the XZ plane; keep the current height so the capsule sits on the ground.
            Vector3 delta = moveInput * moveSpeed * Time.fixedDeltaTime;
            body.MovePosition(body.position + delta);

            // Face the travel direction.
            if (moveInput.sqrMagnitude > 0.001f)
            {
                Quaternion target = Quaternion.LookRotation(moveInput, Vector3.up);
                body.MoveRotation(Quaternion.RotateTowards(body.rotation, target, turnSpeed * Time.fixedDeltaTime));
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // Pick up a capsule if there's room.
            if (!IsFull && other.TryGetComponent(out CapsulePickup pickup))
            {
                pickup.Collect();
                Carried++;
                return;
            }

            // Drop everything off at the zone.
            if (IsCarrying && other.TryGetComponent(out DropOffPoint _))
            {
                Delivered += Carried;
                Carried = 0;
            }
        }
    }
}
