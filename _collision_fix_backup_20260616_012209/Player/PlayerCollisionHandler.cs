using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;
using KenyaScooter.Hazards;
using KenyaScooter.Roads;
using KenyaScooter.SafetyNet;
using KenyaScooter.Traffic;

namespace KenyaScooter.Player
{
    /// <summary>
    /// Collision classification and consequence routing (M15, Req §7.4). Relative
    /// speed at impact decides everything: oncoming = speed sum, same direction =
    /// speed difference, static obstacle or animal = player speed. Light hits go to
    /// grace (M16); medium hits use grace when available and escalate to Hard when
    /// not (the D17 reading of "context-dependent"); hard hits ask GameManager for
    /// the rewind / game-over / fallback-recovery decision. A trigger cooldown stops
    /// one contact double-firing, and a short invulnerability follows every recovery.
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

        private void OnTriggerEnter(Collider other)
        {
            if (GameManager.State != GameState.Playing)
                return;
            if (Time.time < cooldownUntil || Time.time < invulnerableUntil)
                return;
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
            else if (other.GetComponentInParent<WildlifeAnimal>() != null)
            {
                relativeKmh = playerKmh;
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
                bool absorbed = GraceSystem.Instance.TryAbsorb(severity);
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
