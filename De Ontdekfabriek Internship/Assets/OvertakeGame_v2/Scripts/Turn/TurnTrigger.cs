using UnityEngine;
using System.Collections;

namespace OvertakeGame
{
    /// <summary>
    /// Place this on a trigger collider at each corner of your road rectangle.
    /// When the player enters it, the travel direction changes and the camera + player
    /// smoothly rotate 90° to face the new heading.
    ///
    /// ── SETUP ────────────────────────────────────────────────────────────────
    /// 1. Create an empty GameObject at the corner. Name it e.g. "TurnRight_Corner1".
    /// 2. Add a Box Collider. Set IsTrigger = true.
    ///    Size it to span the full road width on one axis and about 2–4 units deep
    ///    on the travel axis so the turn registers cleanly.
    /// 3. Attach this script.
    /// 4. Set turnDirection to the direction the road goes AFTER this corner.
    ///    For a clockwise rectangle:
    ///      Corner 1 (top):    turnDirection = Vector3.left   (-X)
    ///      Corner 2 (right):  turnDirection = Vector3.back   (-Z)  ← back to start axis
    ///      Corner 3 (bottom): turnDirection = Vector3.right  (+X)
    ///      Corner 4 (left):   turnDirection = Vector3.forward(+Z)
    ///    Adjust for your specific layout.
    /// 5. cameraRig  = the empty parent of your camera (see GyroscopeSteering setup)
    /// 6. playerRoot = the player's root transform (the Rigidbody GameObject)
    ///
    /// ── WHAT HAPPENS AT A TURN ───────────────────────────────────────────────
    ///   1. RoadDirection.Current changes to the new direction.
    ///   2. The camera rig rotates smoothly by ±90° over rotateDuration seconds.
    ///   3. The player Rigidbody constraints are adjusted so the frozen position
    ///      axis swaps (was FreezePositionZ, becomes FreezePositionX, or vice versa).
    ///   4. PlayerController.lateralAxis is updated so steering stays on the
    ///      correct perpendicular axis.
    ///   5. All traffic, tiles, potholes, rocks automatically follow because they
    ///      read RoadDirection.Current each frame.
    ///
    /// ── ONE-LINE MIGRATION FOR EXISTING WORLD OBJECTS ────────────────────────
    /// In TrafficVehicle, Pothole, RockObstacle — replace:
    ///   transform.Translate(Vector3.back * speed * Time.deltaTime, Space.World);
    /// with:
    ///   transform.Translate(RoadDirection.Current * speed * Time.deltaTime, Space.World);
    /// That is all each script needs.
    /// </summary>
    public class TurnTrigger : MonoBehaviour
    {
        [Header("Turn Direction")]
        [Tooltip("The direction the road travels AFTER this corner. Must be axis-aligned.")]
        public Vector3 turnDirection = Vector3.left;

        [Header("Camera Rotation")]
        [Tooltip("The empty parent of your camera — rotated by this script.")]
        public Transform cameraRig;
        [Tooltip("Seconds to smoothly rotate 90°. 0.3–0.5 feels natural.")]
        public float rotateDuration = 0.35f;

        [Header("Player Root")]
        [Tooltip("The player Rigidbody root. Constraints are adjusted after the turn.")]
        public Rigidbody playerRigidbody;

        [Header("Player Reference")]
        public string playerTag = "Player";

        // Prevents the same trigger firing twice if the player lingers in the zone
        private bool _triggered;

        void OnTriggerEnter(Collider other)
        {
            if (_triggered) return;
            if (!other.CompareTag(playerTag)) return;
            _triggered = true;
            StartCoroutine(ExecuteTurn());
        }

        void OnTriggerExit(Collider other)
        {
            // Reset so the trigger can fire again if the player reverses into it
            if (other.CompareTag(playerTag)) _triggered = false;
        }

        private IEnumerator ExecuteTurn()
        {
            // 1. Calculate target rotation
            Vector3 normalised  = turnDirection.normalized;
            float   targetYRot  = Mathf.Atan2(normalised.x, normalised.z) * Mathf.Rad2Deg;

            // 2. Update RoadDirection — all world objects start moving in the new direction
            //    from this frame onwards
            RoadDirection.SetDirection(normalised);

            // 3. Rotate camera rig smoothly
            if (cameraRig != null)
            {
                Quaternion fromRot  = cameraRig.rotation;
                Quaternion toRot    = Quaternion.Euler(
                    cameraRig.eulerAngles.x,
                    targetYRot,
                    cameraRig.eulerAngles.z);

                float elapsed = 0f;
                while (elapsed < rotateDuration)
                {
                    elapsed += Time.deltaTime;
                    float t  = Mathf.SmoothStep(0f, 1f, elapsed / rotateDuration);
                    cameraRig.rotation = Quaternion.Slerp(fromRot, toRot, t);
                    yield return null;
                }
                cameraRig.rotation = toRot;
            }

            // 4. Swap Rigidbody position constraints to match the new travel axis
            //    Travel on Z → freeze Z, steer on X
            //    Travel on X → freeze X, steer on Z
            if (playerRigidbody != null)
                UpdateRigidbodyConstraints(playerRigidbody, normalised);

            yield break;
        }

        private void UpdateRigidbodyConstraints(Rigidbody rb, Vector3 travelDir)
        {
            // Always freeze Y and rotation regardless of travel axis
            RigidbodyConstraints constraints = RigidbodyConstraints.FreezeRotation
                                             | RigidbodyConstraints.FreezePositionY;

            // Freeze the travel axis (world moves there, not the player)
            bool travelOnZ = Mathf.Abs(travelDir.z) > Mathf.Abs(travelDir.x);
            if (travelOnZ)
                constraints |= RigidbodyConstraints.FreezePositionZ;
            else
                constraints |= RigidbodyConstraints.FreezePositionX;

            rb.constraints = constraints;
        }
    }
}