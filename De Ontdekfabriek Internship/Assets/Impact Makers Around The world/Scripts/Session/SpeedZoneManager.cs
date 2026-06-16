using UnityEngine;
using KenyaScooter.Core;
using KenyaScooter.Roads;

namespace KenyaScooter.Session
{
    /// <summary>
    /// Holds the speed limit of the active road zone (Req §7.3). The limit comes
    /// from the RoadSequence asset, so townships and school zones tune it per asset
    /// (answer to open question Q2: a thin singleton fed by the sequencer, keeping
    /// SpeedMonitor decoupled from road internals). 0 = unrestricted.
    /// </summary>
    public sealed class SpeedZoneManager : MonoBehaviour
    {
        public static SpeedZoneManager Instance { get; private set; }

        public float CurrentLimitKmh { get; private set; }

        private void Awake() => Instance = this;

        private void OnEnable()
        {
            GameEvents.SequenceChanged += HandleSequenceChanged;
            GameEvents.SessionReset += HandleSessionReset;
        }

        private void OnDisable()
        {
            GameEvents.SequenceChanged -= HandleSequenceChanged;
            GameEvents.SessionReset -= HandleSessionReset;
        }

        private void HandleSequenceChanged(RoadSequence sequence)
            => CurrentLimitKmh = sequence != null ? sequence.speedLimitKmh : 0f;

        private void HandleSessionReset() => CurrentLimitKmh = 0f;
    }
}
