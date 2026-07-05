using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Controls;
using KenyaScooter.Core;

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
        /// <summary>The smoothed steering lean, normalised -1..1 (sign matches the visual roll: + leans left).
        /// Exposed statically, mirroring SpeedFeel, so FX (the tyre-scrape dust) can read "how hard is the bike
        /// leaning" without a scene reference. 0 when no lean system is active.</summary>
        public static float Current01 { get; private set; }

        [SerializeField] private ScooterConfig config;
        [Tooltip("The scooter model child this script rotates. The root stays upright for physics.")]
        [SerializeField] private Transform visual;
        [SerializeField] private ScooterWobble wobble;
        [Tooltip("Seconds for the steering lean to ease toward its target. Higher = smoother/lazier, lower = snappier. ~0.18 is a calm ride.")]
        [SerializeField] private float leanSmoothTime = 0.18f;

        [Header("Turn lean (M3)")]
        [Tooltip("Extra degrees the bike leans into a road turn, on top of any steering lean.")]
        [SerializeField] private float turnLeanAngle = 16f;
        [Tooltip("Road bend rate (deg/s) that produces the FULL turn lean. A sharper bend than this still caps at " +
                 "turnLeanAngle. Keep this near the curve rate a real bend actually produces: a 90° tile over its " +
                 "length L ridden at speed v gives CurveRate = (90/L)*v deg/s, e.g. a 90°/200 m tile at 10-20 m/s is " +
                 "only ~4.5-9 deg/s, so a high reference here leaves the lean nearly invisible. ~8 makes a cruise-speed " +
                 "bend lean hard; lower for an even stronger dip.")]
        [SerializeField] private float turnLeanReferenceRate = 8f;

        private float steerRoll;
        private float rollVelocity;
        private float turnLean;
        private float turnLeanVelocity;
        private Quaternion baseRotation = Quaternion.identity;
        private Vector3 basePosition;
        private Vector3 pivotLocal;

        // The dirt-road shake source (2026-07-05). Resolved lazily in LateUpdate rather than Awake because
        // PlayerController auto-adds both components and the Awake order between them is not guaranteed.
        private DirtRumble rumble;
        private bool rumbleSearched;

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
            Current01 = config.maxLeanAngle > 0.01f ? Mathf.Clamp(steerRoll / config.maxLeanAngle, -1f, 1f) : 0f;

            // Turn lean (M3): lean into the road's live bend. CurveRate is signed deg/s (+ bends right),
            // so the bike dips into the corner while the road curves and rights itself on the straight —
            // matching the steer-lean sign. Scaled by how sharp the bend is, capped at turnLeanAngle.
            float curveLeanTarget = -Mathf.Clamp(RoadDirection.CurveRate / Mathf.Max(1f, turnLeanReferenceRate), -1f, 1f) * turnLeanAngle;
            turnLean = Mathf.SmoothDampAngle(turnLean, curveLeanTarget, ref turnLeanVelocity, 0.12f);

            if (!rumbleSearched)
            {
                rumble = GetComponentInParent<DirtRumble>();
                rumbleSearched = true;
            }

            // Dirt-road shake (2026-07-05): added like the hazard wobble — un-smoothed, on top — so the
            // murram chatter stays sharp instead of being damped into the steering lean.
            float rumbleRoll = rumble != null ? rumble.CurrentRoll : 0f;
            float rumbleBob = rumble != null ? rumble.CurrentBob : 0f;

            float roll = steerRoll + turnLean + (wobble != null ? wobble.CurrentRoll : 0f) + rumbleRoll;

            // Roll around the contact pivot: rotate the model, then shift it so the pivot stays put.
            Quaternion lean = Quaternion.Euler(0f, 0f, roll);
            visual.localRotation = lean * baseRotation;
            visual.localPosition = pivotLocal + lean * (basePosition - pivotLocal) + new Vector3(0f, rumbleBob, 0f);
        }
    }
}
