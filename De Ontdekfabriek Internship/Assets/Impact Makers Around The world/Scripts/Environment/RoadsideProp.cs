using UnityEngine;
using KenyaScooter.Core;
using KenyaScooter.SafetyNet;

namespace KenyaScooter.Environment
{
    /// <summary>
    /// One pooled piece of roadside scenery (Oplevering insight 2 — make the roadside feel alive): a market
    /// stall, a person, a windmill, a solar panel, a Big-5 animal, a curb bollard. Modelled on
    /// <see cref="KenyaScooter.Hazards.Hazard"/> but with NO collider, hit or scoring — it is purely decorative.
    ///
    /// Like a hazard it lives at a fixed point on the road (arc-length along the centreline + a lateral offset
    /// onto the shoulder), and <see cref="RoadsidePropSpawner"/> re-derives its world pose every frame through
    /// RoadSequencer's curve mapping, so it rides a bend with the road instead of floating off the straight +Z
    /// line. It is <see cref="IRewindable"/> exactly like the tiles and hazards, so during a rewind it scrolls
    /// backward with the rest of the world rather than freezing and sliding against the road.
    ///
    /// Animated behaviours (a turning windmill, a waving person) live on SEPARATE components on the prefab
    /// (<see cref="WindmillRotor"/>, <see cref="RoadsideWaver"/>) which animate CHILD transforms, so they never
    /// fight the spawner that owns this root's pose.
    /// </summary>
    public sealed class RoadsideProp : MonoBehaviour, IRewindable
    {
        [System.NonSerialized] public ObjectPool<RoadsideProp> SourcePool;

        /// <summary>Where this prop sits on the road centreline (arc-length, metres). Stamped at spawn; the spawner
        /// re-derives the world pose from it every frame so the prop rides the curve. Survives pooling.</summary>
        [System.NonSerialized] public float RoadArc;

        /// <summary>Lateral offset from the centreline (metres, +X = the player's right). Set once at spawn — a prop
        /// is static, so unlike a pedestrian this never animates.</summary>
        [System.NonSerialized] public float RoadLateral;

        /// <summary>Random yaw (deg) around the road-facing direction, applied every frame on top of the road pose
        /// so the prop keeps a consistent facing relative to the road as the road bends. Set once at spawn.</summary>
        [System.NonSerialized] public float YawOffset;

        /// <summary>World-space vertical lift that rests the prop's base on the (flat) road, reused every frame so the
        /// per-frame re-place stays allocation-free.</summary>
        [System.NonSerialized] public float GroundOffset;

        /// <summary>Pivot-to-bottom distance at scale 1, measured ONCE per pooled instance from the COMBINED bounds of
        /// every child renderer (a multi-part prop must rest on its lowest part, not whichever renderer is first), then
        /// scaled per spawn. -1 = not yet measured. Caching it means the bounds walk happens once, not every spawn.</summary>
        [System.NonSerialized] public float UnscaledLift = -1f;

        /// <summary>The prefab's authored scale, captured by the spawner at pool creation so per-spawn size variation
        /// multiplies around the intended proportions instead of clobbering them.</summary>
        [System.NonSerialized] public Vector3 BaseScale = Vector3.one;

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
