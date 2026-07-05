using UnityEngine;
using KenyaScooter.Core;

namespace KenyaScooter.Environment
{
    /// <summary>
    /// Spins a windmill's blades so a roadside windmill reads as ALIVE, not dead (Oplevering insight 2:
    /// "a static windmill reads as dead"). Self-contained: drop it on a windmill prop prefab and assign the
    /// blades child. It only rotates a CHILD transform in local space, so it never fights the
    /// <see cref="RoadsidePropSpawner"/> that owns the prop root's road pose, and it adds no per-frame
    /// world-axis code (the spin is purely local). Survives pooling (no state to reset).
    ///
    /// Works equally for any always-turning roadside element (a fan, a wheel). For a flag or cooking smoke
    /// use a small oscillation instead — out of scope here, but the same "animate a child" pattern applies.
    /// </summary>
    public sealed class WindmillRotor : MonoBehaviour
    {
        [Tooltip("The transform that spins (the blades / rotor). If left empty this component's own transform " +
                 "is spun, but prefer a dedicated child so the spawner-owned root stays put.")]
        [SerializeField] private Transform blades;

        [Tooltip("Local axis the blades turn around. Default +Z (face of the windmill); use +Y for a flat fan.")]
        [SerializeField] private Vector3 spinAxis = Vector3.forward;

        [Tooltip("Turn speed in degrees per second. A gentle Kenyan-breeze windmill is ~40-90.")]
        [SerializeField] private float degreesPerSecond = 60f;

        [Tooltip("Random +/- variation (deg/s) added once at start, so a row of windmills does not turn in lockstep.")]
        [SerializeField] private float speedJitter = 15f;

        [Tooltip("If on, the blades only turn while the game is actually playing (frozen on menus). Off = always turn, " +
                 "so the windmill looks alive on the start/relay screens too.")]
        [SerializeField] private bool onlyWhilePlaying = false;

        private float speed;

        private void Awake() => speed = degreesPerSecond + Random.Range(-speedJitter, speedJitter);

        private void OnEnable()
        {
            // Re-roll the per-instance speed each time it leaves the pool, so reused windmills still vary.
            speed = degreesPerSecond + Random.Range(-speedJitter, speedJitter);
        }

        private void Update()
        {
            if (onlyWhilePlaying)
            {
                GameState state = GameManager.State;
                if (state != GameState.Playing && state != GameState.AtCheckpoint)
                    return;
            }

            Transform t = blades != null ? blades : transform;
            t.Rotate(spinAxis, speed * Time.deltaTime, Space.Self);
        }
    }
}
