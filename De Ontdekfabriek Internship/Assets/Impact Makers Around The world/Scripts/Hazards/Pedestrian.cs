using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;
using KenyaScooter.Player;
using KenyaScooter.Roads;
using KenyaScooter.SafetyNet;

namespace KenyaScooter.Hazards
{
    /// <summary>
    /// One pooled pedestrian crossing the road (M28 — yield to vulnerable road users). Modelled on
    /// <see cref="Hazard"/>: it lives at a fixed point on the road (arc-length along the centreline) and
    /// PedestrianCrossingSpawner re-derives its world pose every frame through RoadSequencer's curve mapping,
    /// so it rides a bend with the road. The difference from a static hazard is that its LATERAL position
    /// animates here — the person walks across the lanes over a few seconds — and it both REWARDS a clean
    /// yield and routes a hit through the hazard pipeline.
    ///
    /// Hit: on player contact it fires HazardHit (carrying the config's hitConfig) exactly once, exactly like
    /// a Hazard, so ScoreManager / WorldSpeed / ScooterWobble / CameraShake / HapticFeedback / WarningSystem /
    /// DiegeticHud all react with no new wiring. A hit cancels the pending yield reward.
    ///
    /// Yield: while the pedestrian sits in the danger window ahead of the player AND the player has slowed to
    /// the configured ratio, the crossing is marked yielded and PedestrianYielded is raised once — ScoreManager
    /// awards the bonus with the current streak multiplier. The emphasis is the reward (design: reward safe
    /// behaviour, do not over-punish). No collision is registered mid-turn (the existing M3 grace).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class Pedestrian : MonoBehaviour, IRewindable
    {
        [System.NonSerialized] public PedestrianCrossingConfig config;
        [System.NonSerialized] public ObjectPool<Pedestrian> SourcePool;

        /// <summary>Where this crossing sits on the road centreline (arc-length, metres). The spawner stamps it
        /// at spawn and re-derives the world pose from it every frame so the crossing rides the curve. Survives pooling.</summary>
        [System.NonSerialized] public float RoadArc;

        /// <summary>Current lateral offset from the centreline (metres). Animates from one shoulder to the other as the person walks.</summary>
        [System.NonSerialized] public float RoadLateral;

        /// <summary>World-space vertical lift that rests the feet on the (flat) road, measured once at spawn.</summary>
        [System.NonSerialized] public float GroundOffset;

        private Collider hitCollider;
        private bool consumed;     // hit already registered this life (collider disarms until recycled)
        private bool yielded;      // yield reward already awarded this crossing
        private float startLateral, endLateral;
        private float crossProgress; // 0..1 across the road
        private float lingerTimer;

        private void Awake() => hitCollider = GetComponent<Collider>();

        private void OnEnable()
        {
            consumed = false;
            yielded = false;
            crossProgress = 0f;
            lingerTimer = 0f;
            hitCollider.enabled = true;
        }

        /// <summary>Spawner sets the walk's start/end lateral (which shoulder the person comes from) at spawn.</summary>
        public void BeginWalk(float fromLateral, float toLateral)
        {
            startLateral = fromLateral;
            endLateral = toLateral;
            RoadLateral = fromLateral;
            crossProgress = 0f;
            lingerTimer = 0f;
        }

        /// <summary>True once the person has finished walking and lingered, so the spawner can recycle it.</summary>
        public bool ReadyToDespawn => crossProgress >= 1f && lingerTimer >= (config != null ? config.despawnAfterCrossSeconds : 1.5f);

        /// <summary>Advances the walk and the yield check. Driven by the spawner each frame (one place owns the
        /// player-relative logic, like HazardSpawner owns hazard scrolling). dt is already gated to Playing/AtCheckpoint.</summary>
        public void Tick(float dt, float playerArc)
        {
            if (config == null)
                return;

            // Walk across the road at the configured speed, in road-lateral metres.
            float span = Mathf.Abs(endLateral - startLateral);
            if (crossProgress < 1f && span > 0.001f)
            {
                crossProgress = Mathf.Clamp01(crossProgress + (config.walkSpeed * dt) / span);
                RoadLateral = Mathf.Lerp(startLateral, endLateral, crossProgress);
            }
            else
            {
                crossProgress = 1f;
                lingerTimer += dt;
            }

            TryAwardYield(playerArc);
        }

        /// <summary>
        /// The teaching reward. While this crossing is still ahead of the player and within the danger window,
        /// if the player has eased off to the yield speed ratio we award the bonus once. A hit (consumed) cancels
        /// it — you do not get the yield reward for a crossing you ran into. Awarded only while the person is still
        /// a live obstacle (not yet fully past), so it rewards an ACTUAL yield, not coasting up to an empty crossing.
        /// </summary>
        private void TryAwardYield(float playerArc)
        {
            if (yielded || consumed || config.yieldReward <= 0)
                return;

            float ahead = RoadArc - playerArc;          // + = crossing still in front of the player
            if (ahead < -config.dangerWindow || ahead > config.dangerWindow)
                return;                                  // only while we are in this crossing's danger window
            if (crossProgress >= 1f)
                return;                                  // person already across — no live hazard left to yield to

            WorldSpeed speed = WorldSpeed.Instance;
            if (speed == null)
                return;
            // SpeedRatio is Current/MaxSpeed; the yield threshold is expressed against BASE speed, so a player
            // who has clearly eased off below cruising counts as yielding (a full stop is not required).
            float ratioVsBase = speed.BaseSpeed > 0f ? speed.Current / speed.BaseSpeed : 0f;
            if (ratioVsBase > config.yieldSpeedRatio)
                return;

            yielded = true;
            string popupKey = string.IsNullOrEmpty(config.yieldPopupKey) ? null : config.yieldPopupKey;
            GameEvents.RaisePedestrianYielded(config.yieldReward, popupKey, transform.position);
        }

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
            // Reuse the hazard pipeline: the hitConfig carries the deduction, speed scrub, shake/wobble strength
            // and the warn/popup keys, so the whole feedback stack fires exactly as it does for a hazard (SC4).
            if (config != null && config.hitConfig != null)
                GameEvents.RaiseHazardHit(config.hitConfig, WorldSpeed.Instance.CurrentKmh, transform.position);
        }

        public void CaptureSample(ref RewindSample sample)
        {
            sample.Position = transform.position;
            sample.Rotation = transform.rotation;
            // Pack the walk state so a rewind across a crossing resumes mid-walk instead of snapping.
            sample.Aux = crossProgress;
            sample.Active = gameObject.activeInHierarchy;
        }

        public void ApplySample(in RewindSample sample)
        {
            transform.SetPositionAndRotation(sample.Position, sample.Rotation);
            crossProgress = sample.Aux;
            RoadLateral = Mathf.Lerp(startLateral, endLateral, crossProgress);
            // A rewound crossing is hittable and yieldable again — the hit/yield it produced has been undone.
            consumed = false;
            yielded = false;
            hitCollider.enabled = true;
            if (gameObject.activeSelf != sample.Active)
                gameObject.SetActive(sample.Active);
        }
    }
}
