using UnityEngine;
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
        public HazardKind kind = HazardKind.Pothole;

        [System.NonSerialized] public ObjectPool<Hazard> SourcePool;

        private Collider hitCollider;
        private bool consumed;

        private void Awake() => hitCollider = GetComponent<Collider>();

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
            GameEvents.RaiseHazardHit(kind, WorldSpeed.Instance.CurrentKmh, transform.position);
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
