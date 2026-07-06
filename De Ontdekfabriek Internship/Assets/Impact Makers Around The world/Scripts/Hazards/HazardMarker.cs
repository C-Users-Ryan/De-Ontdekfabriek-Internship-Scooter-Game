using UnityEngine;
using KenyaScooter.Core;
using KenyaScooter.SafetyNet;

namespace KenyaScooter.Hazards
{
    /// <summary>
    /// A purely cosmetic road-space companion for a hazard cluster — a warning cone (or branch, sign, ...) set
    /// just up-road of the potholes so the player spots them coming: the physical twin of the config's warnKey
    /// text (SC4). It carries NO gameplay — HazardSpawner switches off its colliders when the pool is built, so
    /// it never fires HazardHit, never scores and never blocks. Like a Hazard it lives in road space (arc +
    /// lateral) and HazardSpawner re-derives its world pose every frame, so it rides the curve with the road,
    /// and it grounds once at spawn. It is rewindable exactly like a Hazard, so a rewind puts the cones back.
    ///
    /// The spawner adds this component to the assigned prefab automatically (via the pool's onCreated), so a
    /// facilitator can drop ANY plain prefab into HazardSpawnConfig.warningPrefab with no setup.
    /// </summary>
    public sealed class HazardMarker : MonoBehaviour, IRewindable
    {
        /// <summary>The Transform pool that owns this instance — release back to it when the marker despawns.</summary>
        [System.NonSerialized] public ObjectPool<Transform> SourcePool;

        /// <summary>Where this marker sits on the road, in road space: arc-length along the centreline plus a
        /// lateral offset. HazardSpawner stamps these at spawn and re-derives the world pose from them every
        /// frame (via RoadSequencer) so the marker rides the curve. They survive pooling, like SourcePool.</summary>
        [System.NonSerialized] public float RoadArc;
        [System.NonSerialized] public float RoadLateral;

        /// <summary>World-space vertical lift that rests this marker on the (flat) road surface, measured once at
        /// spawn from its renderer and reused every frame — so the per-frame re-place stays allocation-free.</summary>
        [System.NonSerialized] public float GroundOffset;

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
