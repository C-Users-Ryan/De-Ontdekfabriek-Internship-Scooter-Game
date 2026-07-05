using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;
using KenyaScooter.Hazards;
using KenyaScooter.SafetyNet;
using KenyaScooter.Traffic;

namespace KenyaScooter.Player
{
    /// <summary>
    /// Grades collisions and decides what happens next (M15, Req §7.4). The relative speed at impact decides
    /// everything: oncoming = the two speeds added, same direction = the difference, a static obstacle or
    /// animal = just the player's speed. Light hits go to grace (M16); medium hits use grace if there is any
    /// and otherwise count as Hard (the D17 reading of "context-dependent"); hard hits ask GameManager for
    /// the rewind / game-over / recover decision. It catches both trigger and solid contacts, with a short
    /// cooldown so one bump can't fire twice and a brief invulnerability after every recovery.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class PlayerCollisionHandler : MonoBehaviour
    {
        [SerializeField] private SafetyNetConfig config;

        private float cooldownUntil;
        private float invulnerableUntil;

        private void OnEnable()
        {
            GameEvents.RewindCompleted += GrantRecoveryWindow;
            GameEvents.SessionReset += HandleSessionReset;
        }

        private void OnDisable()
        {
            GameEvents.RewindCompleted -= GrantRecoveryWindow;
            GameEvents.SessionReset -= HandleSessionReset;
        }

        // Route both trigger and solid contacts through one handler, so a vehicle registers
        // whether its collider is marked Is Trigger or not. The cooldown dedupes if both fire.
        private void OnTriggerEnter(Collider other) => HandleContact(other);
        private void OnCollisionEnter(Collision collision) => HandleContact(collision.collider);

        private void HandleContact(Collider other)
        {
            if (GameManager.State != GameState.Playing)
                return;
            if (Time.time < cooldownUntil || Time.time < invulnerableUntil || RoadDirection.IsTurning)
                return; // no hits mid-turn (M3)
            if (other.GetComponentInParent<Hazard>() != null)
                return; // hazards report themselves (M21/M22)

            float playerKmh = WorldSpeed.Instance.CurrentKmh;
            float relativeKmh;

            TrafficVehicle vehicle = other.GetComponentInParent<TrafficVehicle>();
            if (vehicle != null)
            {
                if (!vehicle.PassDone)
                    vehicle.PassInvalidated = true; // a crash-pass earns nothing (D17)
                vehicle.WasHitByPlayer = true;

                float vehicleKmh = vehicle.CurrentSpeed * WorldSpeed.MsToKmh;
                relativeKmh = vehicle.isStaticObstacle ? playerKmh
                    : vehicle.Direction == LaneDirection.Oncoming ? playerKmh + vehicleKmh
                    : Mathf.Abs(playerKmh - vehicleKmh);
            }
            else
            {
                return;
            }

            cooldownUntil = Time.time + config.collisionCooldown;
            Vector3 position = other.bounds.center;
            CollisionSeverity severity = Classify(relativeKmh);

            if (severity != CollisionSeverity.Hard)
            {
                bool absorbed = GraceSystem.Instance != null && GraceSystem.Instance.TryAbsorb(severity);
                if (!absorbed && severity == CollisionSeverity.Medium)
                {
                    severity = CollisionSeverity.Hard; // grace empty — medium escalates (D17)
                }
                else
                {
                    GameEvents.RaiseCollisionOccurred(severity, relativeKmh, position, absorbed);
                    return;
                }
            }

            GameEvents.RaiseCollisionOccurred(CollisionSeverity.Hard, relativeKmh, position, false);
            if (GameManager.Instance.HandleHardCrash() == CrashOutcome.Recovered)
                GrantRecoveryWindow();
        }

        private CollisionSeverity Classify(float relativeKmh)
        {
            if (relativeKmh < config.lightHitMaxKmh)
                return CollisionSeverity.Light;
            if (relativeKmh < config.hardHitMinKmh)
                return config.mediumHitsUseGrace ? CollisionSeverity.Medium : CollisionSeverity.Hard;
            return CollisionSeverity.Hard;
        }

        private void GrantRecoveryWindow() => invulnerableUntil = Time.time + config.postRecoveryInvulnerability;

        private void HandleSessionReset()
        {
            cooldownUntil = 0f;
            invulnerableUntil = 0f;
        }
    }
}
