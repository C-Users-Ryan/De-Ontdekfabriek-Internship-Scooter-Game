using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Controls;

namespace KenyaScooter.Player
{
    /// <summary>
    /// Visual lean of the scooter model (M6). Reads RAW steering input — not the
    /// accumulated lateral velocity — so the lean answers the player's hand. The steer
    /// lean is eased toward its target with SmoothDampAngle, so the tilt rolls in and out
    /// naturally instead of snapping at a constant rate. Hazard wobble (M21) is added on
    /// top un-smoothed, so a hit still reads as a sharp jolt rather than being damped away.
    /// </summary>
    public sealed class ScooterLean : MonoBehaviour
    {
        [SerializeField] private ScooterConfig config;
        [Tooltip("The scooter model child this script rotates. The root stays upright for physics.")]
        [SerializeField] private Transform visual;
        [SerializeField] private ScooterWobble wobble;
        [Tooltip("Seconds for the steering lean to ease toward its target. Higher = smoother/lazier, lower = snappier. ~0.18 is a calm ride.")]
        [SerializeField] private float leanSmoothTime = 0.18f;

        private float steerRoll;
        private float rollVelocity;
        private Quaternion baseRotation = Quaternion.identity;
        private Vector3 basePosition;
        private Vector3 pivotLocal;

        private void Awake()
        {
            // Pull the tuning from the sibling PlayerController if it was not assigned directly, so
            // the lean works the moment the component is added (the player adds it automatically).
            if (config == null)
            {
                PlayerController controller = GetComponentInParent<PlayerController>();
                if (controller != null)
                    config = controller.Config;
            }

            // If the model child was left unassigned, find it automatically so the scooter still
            // leans. We take the first child that owns a renderer (the visible model) and never the
            // root, which stays upright for collision.
            if (visual == null)
                visual = FindVisualChild();
            if (visual != null)
            {
                baseRotation = visual.localRotation; // keep the model's authored orientation; the roll is added on top
                basePosition = visual.localPosition;
                pivotLocal = ComputeContactPivot();  // roll around the tyres, not the off-centre transform origin
            }

            if (visual == null)
                Debug.LogWarning("[ScooterLean] No visual assigned and no child mesh found, so the scooter will not lean. Assign the model child in the Inspector.", this);
            if (config == null)
                Debug.LogWarning("[ScooterLean] No ScooterConfig found (assign one, or add a PlayerController that has one), so the scooter will not lean.", this);
        }

        private Transform FindVisualChild()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child.GetComponentInChildren<Renderer>() != null)
                    return child;
            }
            return null;
        }

        /// <summary>
        /// The point the bike rolls around: the bottom-centre of the model's mesh bounds — where the
        /// tyres meet the road — in the parent's local space. Rolling around this instead of the
        /// transform origin makes the scooter tip over its wheels like a real bike, rather than
        /// swinging the whole body around an off-centre pivot (the visual layer is offset on its parent).
        /// </summary>
        private Vector3 ComputeContactPivot()
        {
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0 || visual.parent == null)
                return basePosition;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            Vector3 contactWorld = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            return visual.parent.InverseTransformPoint(contactWorld);
        }

        // LateUpdate, not Update: if the model has an Animator it writes its rotation during the
        // animation pass (after Update), so setting the roll here makes the lean win instead of
        // being silently overwritten.
        private void LateUpdate()
        {
            if (visual == null || config == null)
                return;

            float target = -ScooterInputRouter.Instance.Lateral * config.maxLeanAngle;
            steerRoll = Mathf.SmoothDampAngle(steerRoll, target, ref rollVelocity, Mathf.Max(0.0001f, leanSmoothTime));

            float roll = steerRoll + (wobble != null ? wobble.CurrentRoll : 0f);

            // Roll around the contact pivot: rotate the model, then shift it so the pivot stays put.
            Quaternion lean = Quaternion.Euler(0f, 0f, roll);
            visual.localRotation = lean * baseRotation;
            visual.localPosition = pivotLocal + lean * (basePosition - pivotLocal);
        }
    }
}
