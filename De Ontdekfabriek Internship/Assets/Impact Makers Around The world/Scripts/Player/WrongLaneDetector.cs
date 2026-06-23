using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;

namespace KenyaScooter.Player
{
    /// <summary>
    /// Detects driving in the oncoming lane (M14). It projects the player onto RoadDirection.SteerAxis and
    /// compares against the centre line from RoadSideConfig, so it works on any axis and either side of the
    /// road. Crossing into the oncoming lane is fine on its own — overtaking needs it; the violation is
    /// staying there past the grace window. The first tick fires the moment that window closes, then once a
    /// second, and ScoreManager turns those ticks into deductions and a streak reset (design note in D17).
    /// </summary>
    public sealed class WrongLaneDetector : MonoBehaviour
    {
        public static WrongLaneDetector Instance { get; private set; }

        /// <summary>True when the player is properly on their own side — the overtake confirmation gate (Req §7.1).</summary>
        public bool IsInOwnLane { get; private set; } = true;
        /// <summary>True while the player is across the centre line in the oncoming lane.</summary>
        public bool IsInWrongLane { get; private set; }

        private float wrongLaneTime;
        private float tickAccumulator;
        private bool overstayed;

        private void Awake() => Instance = this;

        private void OnEnable() => GameEvents.SessionReset += HandleSessionReset;
        private void OnDisable() => GameEvents.SessionReset -= HandleSessionReset;

        private void Update()
        {
            if (GameManager.State != GameState.Playing)
                return;

            RoadSideConfig road = RoadSideConfig.Active;
            float ownSide = RoadDirection.Lateral(transform.position) * road.OwnSide;

            IsInOwnLane = ownSide > road.centreBuffer;
            bool wrong = ownSide < -road.centreBuffer;

            if (wrong != IsInWrongLane)
            {
                IsInWrongLane = wrong;
                GameEvents.RaiseWrongLaneChanged(wrong);
                if (!wrong)
                {
                    wrongLaneTime = 0f;
                    overstayed = false;
                }
            }

            if (!IsInWrongLane)
                return;

            wrongLaneTime += Time.deltaTime;
            // Hugging the far edge of the oncoming lane is egregious (not a normal overtake) — warn at once,
            // bypassing the overtake grace. Anything closer to the centre line still gets the grace window.
            bool deep = -ownSide > road.playerLateralLimit * 0.7f;
            if (!deep && wrongLaneTime < road.wrongLaneGraceSeconds)
                return;

            if (!overstayed)
            {
                overstayed = true;
                tickAccumulator = 0f;
                GameEvents.RaiseWrongLaneTick();
                return;
            }

            tickAccumulator += Time.deltaTime;
            if (tickAccumulator >= 1f)
            {
                tickAccumulator -= 1f;
                GameEvents.RaiseWrongLaneTick();
            }
        }

        private void HandleSessionReset()
        {
            IsInOwnLane = true;
            IsInWrongLane = false;
            wrongLaneTime = 0f;
            tickAccumulator = 0f;
            overstayed = false;
        }
    }
}
