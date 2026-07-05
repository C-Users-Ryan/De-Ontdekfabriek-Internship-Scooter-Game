using UnityEngine;

namespace KenyaScooter.Environment
{
    /// <summary>
    /// Gives a roadside figure LIFE by moving it — a gentle local wander/pace with a walking bob, facing its
    /// direction, with the odd stop-and-look. People mill about the verge, goats and cattle amble and graze, instead
    /// of standing frozen. The companion to <see cref="RoadsideWaver"/> (a person's arm) and <see cref="WindmillRotor"/>
    /// (blades): like them it animates a CHILD transform (the figure's body) and never the root, because
    /// <see cref="RoadsidePropSpawner"/> owns the root's road pose — so it composes with the spawner and the other
    /// behaviours, and rides the curve + the rewind for free.
    ///
    /// Movement is LOCAL (the figure stays within a small radius of its spot on the shoulder). That reads as "alive"
    /// without any navigation, and the world scrolling past carries the figure by the player. Null-safe: no body
    /// assigned = it does nothing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoadsideWalker : MonoBehaviour
    {
        [Tooltip("The body to move (a CHILD of the prop root). Auto-uses the first child if left empty.")]
        [SerializeField] private Transform body;

        [Header("Wander")]
        [Tooltip("How far the figure strays from its spot on the shoulder (metres). Small = paces on the spot; larger = ambles about.")]
        [SerializeField] private float radius = 1.4f;
        [Tooltip("Walk speed (m/s). People ~0.7; a grazing goat ~0.4.")]
        [SerializeField] private float speed = 0.7f;
        [Tooltip("How fast it turns to face where it's walking (deg/s).")]
        [SerializeField] private float turnRate = 220f;
        [Range(0f, 1f)]
        [Tooltip("Chance, on reaching a spot, to pause and look around rather than move straight on.")]
        [SerializeField] private float pauseChance = 0.4f;
        [Tooltip("How long a pause lasts (seconds, random in this range).")]
        [SerializeField] private Vector2 pauseSeconds = new Vector2(0.8f, 2.5f);

        [Header("Gait")]
        [Tooltip("Vertical bob while walking (metres) — the step. 0 = glide.")]
        [SerializeField] private float bobHeight = 0.05f;
        [Tooltip("Steps per second at the walk speed.")]
        [SerializeField] private float bobFrequency = 4f;

        private Vector3 home;      // the body's authored local position (its spot)
        private Vector3 target;    // current local wander target (flat)
        private float pauseTimer;
        private float phase;       // per-instance bob offset

        private void Awake()
        {
            if (body == null && transform.childCount > 0)
                body = transform.GetChild(0);
            if (body != null)
                home = body.localPosition;
        }

        private void OnEnable()
        {
            phase = Random.value * 10f;
            pauseTimer = 0f;
            if (body != null)
            {
                body.localPosition = home;
                target = PickTarget();
            }
        }

        private Vector3 PickTarget()
        {
            Vector2 r = Random.insideUnitCircle * radius;
            return home + new Vector3(r.x, 0f, r.y);
        }

        private void Update()
        {
            if (body == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (pauseTimer > 0f) { pauseTimer -= dt; return; } // standing, looking around

            Vector3 pos = body.localPosition;
            Vector3 flat = new Vector3(pos.x, home.y, pos.z);
            Vector3 to = target - flat;
            float dist = to.magnitude;

            if (dist < 0.05f)
            {
                // Arrived: pause and look, or pick a fresh spot.
                if (Random.value < pauseChance)
                    pauseTimer = Random.Range(pauseSeconds.x, pauseSeconds.y);
                target = PickTarget();
                return;
            }

            Vector3 dir = to / dist;
            Vector3 next = flat + dir * Mathf.Min(speed * dt, dist);
            float bob = bobHeight > 0f ? Mathf.Abs(Mathf.Sin((Time.time + phase) * bobFrequency * Mathf.PI)) * bobHeight : 0f;
            body.localPosition = new Vector3(next.x, home.y + bob, next.z);

            // Face the walk direction (in the root's local frame, so it stays correct as the road bends).
            Quaternion face = Quaternion.LookRotation(dir, Vector3.up);
            body.localRotation = Quaternion.RotateTowards(body.localRotation, face, turnRate * dt);
        }
    }
}
