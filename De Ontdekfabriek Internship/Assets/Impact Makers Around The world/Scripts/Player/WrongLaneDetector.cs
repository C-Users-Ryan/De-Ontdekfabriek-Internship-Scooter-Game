using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;

namespace KenyaScooter.Player
{
    /// <summary>
    /// Oncoming-lane detection (M14). Projects the player onto RoadDirection.SteerAxis
    /// and compares against the centre line from RoadSideConfig — axis-agnostic and
    /// country-agnostic. Crossing into the oncoming lane is NOT the violation
    /// (overtaking requires it); staying there beyond the grace window is. The first
    /// tick fires the moment the window closes, then once per second; ScoreManager
    /// turns ticks into deductions and a streak reset (design note in D17).
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
            if (wrongLaneTime < road.wrongLaneGraceSeconds)
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
