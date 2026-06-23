using System.Collections.Generic;
using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;
using KenyaScooter.Player;
using KenyaScooter.SafetyNet;

namespace KenyaScooter.Hazards
{
    /// <summary>
    /// One pooled static road hazard (M21, M22, Req §6): pothole, rock or speed bump.
    /// On player contact it fires HazardHit exactly once (the collider disarms until
    /// recycled) — ScoreManager applies the speed-scaled deduction, ScooterWobble and
    /// CameraShake react, all via the event. Scrolling is centralised in HazardSpawner.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class Hazard : MonoBehaviour, IRewindable
    {
        /// <summary>Every live hazard, so traffic can steer around them (the only lookup path, no scene scans).</summary>
        public static readonly List<Hazard> Active = new List<Hazard>(32);

        /// <summary>The spawn-config that created this instance, stamped by HazardSpawner.
        /// Carries the hazard's scoring, warning and category data (data-driven, SC4).</summary>
        [System.NonSerialized] public HazardSpawnConfig definition;

        [System.NonSerialized] public ObjectPool<Hazard> SourcePool;

        /// <summary>The prefab's authored scale, captured once so the spawner can vary the
        /// size around it without clobbering the intended proportions.</summary>
        [System.NonSerialized] public Vector3 baseScale = Vector3.one;

        /// <summary>Where this hazard sits on the road, in road space: arc-length along the centreline plus a
        /// lateral offset from it. HazardSpawner stamps these at spawn and re-derives the world position from
        /// them every frame (via RoadSequencer) so the hazard rides the curve. They survive pooling, like SourcePool.</summary>
        [System.NonSerialized] public float RoadArc;
        [System.NonSerialized] public float RoadLateral;

        /// <summary>World-space vertical lift that rests this hazard on the (flat) road surface, measured once at
        /// spawn from its renderer and reused every frame — so the per-frame re-place stays allocation-free.</summary>
        [System.NonSerialized] public float GroundOffset;

        private Collider hitCollider;
        private bool consumed;

        private void Awake()
        {
            hitCollider = GetComponent<Collider>();
            baseScale = transform.localScale;
        }

        private void OnEnable()
        {
            consumed = false;
            hitCollider.enabled = true;
            Active.Add(this);
        }

        private void OnDisable() => Active.Remove(this);

        private void OnTriggerEnter(Collider other)
        {
            if (consumed || GameManager.State != GameState.Playing)
                return;
            if (RoadDirection.IsTurning)
                return; // no hits mid-turn (M3) — stay armed so it can still hit once the turn settles
            if (other.GetComponentInParent<PlayerController>() == null)
                return;

            consumed = true;
            hitCollider.enabled = false;
            GameEvents.RaiseHazardHit(definition, WorldSpeed.Instance.CurrentKmh, transform.position);
        }

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
            // A rewound hazard is hittable again — the hit it absorbed has been undone.
            consumed = false;
            hitCollider.enabled = true;
            if (gameObject.activeSelf != sample.Active)
                gameObject.SetActive(sample.Active);
        }
    }
}
