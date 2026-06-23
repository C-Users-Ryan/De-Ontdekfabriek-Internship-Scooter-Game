using UnityEngine;

namespace CapsuleCity
{
    /// <summary>
    /// A floating arrow that hovers above the player and points the way:
    ///   • nothing carried  → the nearest uncollected capsule
    ///   • carrying capsules → the drop-off point
    ///
    /// Put this on a small arrow object (or an empty with an arrow mesh as a child)
    /// and assign the player. The arrow's local +Z (blue axis) is treated as the
    /// "pointing" direction — orient your mesh that way, or drop a tilted mesh under
    /// an <c>arrowVisual</c> child if you want it lying flat.
    /// </summary>
    public sealed class NavigationArrow : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CapsulePlayer player;
        [Tooltip("The mesh to rotate. Leave empty to rotate this object itself.")]
        [SerializeField] private Transform arrowVisual;

        [Header("Placement")]
        [Tooltip("How far above the player the arrow floats.")]
        [SerializeField] private float hoverHeight = 2.5f;
        [Tooltip("How quickly the arrow swings round to the new heading (deg/sec).")]
        [SerializeField] private float turnSpeed = 720f;
        [Tooltip("Hide the arrow when there's no target left (all delivered).")]
        [SerializeField] private bool hideWhenNoTarget = true;

        private Transform Pointer => arrowVisual != null ? arrowVisual : transform;
        private Renderer[] renderers;

        private void Awake()
        {
            // Toggle renderers to show/hide (never the GameObject — that would
            // stop this script from running and the arrow could never return).
            renderers = Pointer.GetComponentsInChildren<Renderer>(true);
        }

        private void LateUpdate()
        {
            if (player == null) return;

            // Stick to the player.
            transform.position = player.transform.position + Vector3.up * hoverHeight;

            Transform target = GetTarget();
            if (target == null)
            {
                if (hideWhenNoTarget) SetVisible(false);
                return;
            }
            SetVisible(true);

            // Point at the target along the ground plane only (ignore height).
            Vector3 dir = target.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;

            Quaternion look = Quaternion.LookRotation(dir, Vector3.up);
            Pointer.rotation = Quaternion.RotateTowards(Pointer.rotation, look, turnSpeed * Time.deltaTime);
        }

        /// <summary>Drop-off while carrying, otherwise the closest available capsule.</summary>
        private Transform GetTarget()
        {
            if (player.IsCarrying)
                return DropOffPoint.Instance != null ? DropOffPoint.Instance.transform : null;
            return NearestPickup();
        }

        private Transform NearestPickup()
        {
            Transform best = null;
            float bestSqr = float.MaxValue;
            Vector3 from = player.transform.position;

            // Cheap nearest-search over the live registry; fine for a small city.
            var all = CapsulePickup.Active;
            for (int i = 0; i < all.Count; i++)
            {
                CapsulePickup p = all[i];
                if (p == null) continue;
                float sqr = (p.transform.position - from).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = p.transform;
                }
            }
            return best;
        }

        private void SetVisible(bool visible)
        {
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].enabled = visible;
        }
    }
}
