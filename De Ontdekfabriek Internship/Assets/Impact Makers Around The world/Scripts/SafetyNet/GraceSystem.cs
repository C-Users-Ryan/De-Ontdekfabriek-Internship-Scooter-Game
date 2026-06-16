using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;

namespace KenyaScooter.SafetyNet
{
    /// <summary>
    /// Soaks up light hits (M16, Req §8.1): a light hit spends a charge instead of punishing the player,
    /// so a first small bump never ends anything. Charges come back after a stretch of clean driving;
    /// any collision, hazard hit or lane violation resets that timer. Hard hits always pass straight through.
    /// </summary>
    public sealed class GraceSystem : MonoBehaviour
    {
        public static GraceSystem Instance { get; private set; }

        [SerializeField] private SafetyNetConfig config;

        public int Charges { get; private set; }

        private float cleanTimer;

        private void Awake()
        {
            Instance = this;
            Charges = config.graceCharges;
        }

        private void OnEnable()
        {
            GameEvents.SessionReset += HandleSessionReset;
            GameEvents.CollisionOccurred += HandleDirtyCollision;
            GameEvents.HazardHit += HandleDirtyHazard;
            GameEvents.WrongLaneTick += RestartCleanTimer;
        }

        private void OnDisable()
        {
            GameEvents.SessionReset -= HandleSessionReset;
            GameEvents.CollisionOccurred -= HandleDirtyCollision;
            GameEvents.HazardHit -= HandleDirtyHazard;
            GameEvents.WrongLaneTick -= RestartCleanTimer;
        }

        private void Update()
        {
            if (GameManager.State != GameState.Playing || Charges >= config.graceCharges)
                return;

            cleanTimer += Time.deltaTime;
            if (cleanTimer >= config.graceRechargeSeconds)
            {
                cleanTimer = 0f;
                Charges++;
                GameEvents.RaiseGraceRecharged();
            }
        }

        /// <summary>
        /// Tries to absorb a hit (M16). Light hits are always eligible; medium hits
        /// only when configured (D17 interpretation of "context-dependent"); hard
        /// hits never. Returns true when a charge was consumed.
        /// </summary>
        public bool TryAbsorb(CollisionSeverity severity)
        {
            if (severity == CollisionSeverity.Hard)
                return false;
            if (severity == CollisionSeverity.Medium && !config.mediumHitsUseGrace)
                return false;
            if (Charges <= 0)
                return false;

            Charges--;
            cleanTimer = 0f;
            GameEvents.RaiseGraceAbsorbed();
            return true;
        }

        private void HandleDirtyCollision(CollisionSeverity severity, float relativeKmh, Vector3 position, bool absorbed)
            => RestartCleanTimer();

        private void HandleDirtyHazard(HazardSpawnConfig definition, float playerKmh, Vector3 position)
            => RestartCleanTimer();

        private void RestartCleanTimer() => cleanTimer = 0f;

        private void HandleSessionReset()
        {
            Charges = config.graceCharges;
            cleanTimer = 0f;
        }
    }
}
