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

        // Seed the limit from the zone that is actually active at reset, instead of zeroing it. The opening zone is
        // assigned on the sequencer at SessionReset but never broadcasts SequenceChanged (only later zones do), so
        // without this the roundel read "--" for the whole first zone until the road built through it and advanced
        // (~160 m of build cursor ahead of the player — "late into the game"). RoadSequencer runs first
        // (DefaultExecutionOrder -50), so its CurrentSequence is already set when this handler runs.
        private void HandleSessionReset()
        {
            RoadSequence opening = RoadSequencer.Instance != null ? RoadSequencer.Instance.CurrentSequence : null;
            CurrentLimitKmh = opening != null ? opening.speedLimitKmh : 0f;
        }
    }
}
