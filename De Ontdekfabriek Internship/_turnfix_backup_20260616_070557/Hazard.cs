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
        /// <summary>The spawn-config that created this instance, stamped by HazardSpawner.
        /// Carries the hazard's scoring, warning and category data (data-driven, SC4).</summary>
        [System.NonSerialized] public HazardSpawnConfig definition;

        [System.NonSerialized] public ObjectPool<Hazard> SourcePool;

        /// <summary>The prefab's authored scale, captured once so the spawner can vary the
        /// size around it without clobbering the intended proportions.</summary>
        [System.NonSerialized] public Vector3 baseScale = Vector3.one;

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
        }

        private void OnTriggerEnter(Collider other)
        {
            if (consumed || GameManager.State != GameState.Playing)
                return;
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
