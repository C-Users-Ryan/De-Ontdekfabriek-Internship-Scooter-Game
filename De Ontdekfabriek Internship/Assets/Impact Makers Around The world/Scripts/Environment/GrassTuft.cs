using UnityEngine;

namespace KenyaScooter.Environment
{
    /// <summary>
    /// Marker for a swaying prop (FX Design Spec "Levend Kenia" §4.4, 3 Jul 2026): put it on a grass-tuft
    /// or tree prefab and (optionally) assign the child that leans. Registers with the <see cref="GrassSway"/>
    /// manager while active, so pooled props join and leave the travelling wind wave automatically — no
    /// per-tuft Update. Scale/lag let one manager serve verge grass (full lean, instant) and acacia canopies
    /// (smaller lean, half-second lag — big things answer slowly) alike. In its own file so Unity can
    /// serialize it onto the factory's prefabs.
    /// </summary>
    public sealed class GrassTuft : MonoBehaviour
    {
        [Tooltip("The child transform that leans. Empty = this prop's first child.")]
        [SerializeField] private Transform body;
        [Tooltip("Amplitude relative to the manager's max lean (grass 1, a heavy canopy ~0.4).")]
        [SerializeField] private float leanScale = 1f;
        [Tooltip("Seconds the lean trails the wind. 0 = instant (grass); big things answer slowly (~0.5).")]
        [SerializeField] private float lagSeconds = 0f;

        private Transform Resolved => body != null ? body : (transform.childCount > 0 ? transform.GetChild(0) : null);

        private void OnEnable() => GrassSway.Register(Resolved, leanScale, lagSeconds);
        private void OnDisable() => GrassSway.Unregister(Resolved);
    }
}
