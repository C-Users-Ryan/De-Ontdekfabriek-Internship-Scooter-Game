using UnityEngine;
using KenyaScooter.Core;
using KenyaScooter.SafetyNet;

namespace KenyaScooter.Roads
{
    /// <summary>Tile difficulty metadata (Req §4.1) — used by designers when authoring sequences.</summary>
    public enum TileDifficulty { Clear, Low, Medium, High }

    /// <summary>
    /// One pooled road tile. Tiles chain geometrically: the sequencer snaps each new
    /// tile to the previous tile's exit anchor, which is how 90° turn tiles bend the
    /// road (M3) without any special-case spawn math. Scrolling is done centrally by
    /// RoadSequencer — tiles have no Update of their own.
    /// </summary>
    public sealed class RoadTile : MonoBehaviour, IRewindable
    {
        [Tooltip("Playable length in metres along the tile's forward axis.")]
        public float length = 30f;
        public TileDifficulty difficulty = TileDifficulty.Clear;
        [Tooltip("Signed degrees of road turn on this tile, 0 for straight (M3). Curved tiles must author an exit anchor.")]
        public float curveAngle = 0f;

        [Tooltip("Where the next tile attaches. Optional for straight tiles (computed from length); required for turns.")]
        [SerializeField] private Transform exitAnchor;

        [System.NonSerialized] public ObjectPool<RoadTile> SourcePool;

        public Vector3 ExitPosition =>
            exitAnchor != null ? exitAnchor.position : transform.position + transform.forward * length;

        public Quaternion ExitRotation =>
            exitAnchor != null ? exitAnchor.rotation : transform.rotation;

        public void CaptureSample(ref RewindSample sample)
        {
            sample.Position = transform.position;
            sample.Rotation = transform.rotation;
            sample.Aux = 0f;
            sample.Active = gameObject.activeInHierarchy;
        }

        public void ApplySample(in RewindSample sample)
        {
            transform.SetPositionAndRotation(sample.Position, sample.Rotation);
            if (gameObject.activeSelf != sample.Active)
                gameObject.SetActive(sample.Active);
        }
    }
}
