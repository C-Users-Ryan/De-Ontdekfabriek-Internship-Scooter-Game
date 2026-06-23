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

        [Tooltip("Optional. If set, this tile is the relay checkpoint tile (Req §9.2): the world brakes to a " +
                 "stop when this marker reaches the player. Author it as a child empty at the charge-station spot.")]
        [SerializeField] private Transform stopMarker;

        [System.NonSerialized] public ObjectPool<RoadTile> SourcePool;

        // Road-space identity, written by RoadSequencer when the tile is appended to the chain.
        // The sequencer lays the road out in a stable "road space" (head-to-tail, bending through
        // curveAngle) and renders it each frame relative to the player, so a turn bends the road
        // around the player instead of rotating the world's compass. These survive pooling.
        [System.NonSerialized] public Vector3 RoadPosition;     // start position of this tile in road space
        [System.NonSerialized] public Quaternion RoadRotation;  // start heading of this tile in road space
        [System.NonSerialized] public float StartArc;           // cumulative road length at this tile's start

        // The bend this tile is actually laid out with, set by RoadSequencer at spawn. Normally equals
        // the authored curveAngle, but the sequencer's sharp-curve overlap guard may soften it to 0 when
        // another sharp bend is too close behind (so the road can't fold over itself within the draw
        // distance). All road geometry reads this; the authored curveAngle stays the untouched design intent.
        [System.NonSerialized] public float EffectiveCurveAngle;

        public Vector3 ExitPosition =>
            exitAnchor != null ? exitAnchor.position : transform.position + transform.forward * length;

        public Quaternion ExitRotation =>
            exitAnchor != null ? exitAnchor.rotation : transform.rotation;

        /// <summary>True when this tile carries a checkpoint stop marker (the charge-station tile).</summary>
        public bool IsCheckpoint => stopMarker != null;

        /// <summary>World point the player should come to rest at — the charge station on a checkpoint tile.</summary>
        public Vector3 StopPosition => stopMarker != null ? stopMarker.position : ExitPosition;

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
