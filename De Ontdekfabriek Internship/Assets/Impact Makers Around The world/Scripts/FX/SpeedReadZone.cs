using UnityEngine;
using KenyaScooter.Core;
using KenyaScooter.Roads;

namespace KenyaScooter.FX
{
    /// <summary>
    /// Which speed-read flavour fits the CURRENT zone (2026-07-05, design direction): clean white SPEED
    /// LINES in the city, warm DUST rushing past on dirt roads and in the wild. Resolved once per zone
    /// change from the road grammar (<see cref="GameEvents.SequenceChanged"/>): a sequence tagged "City"
    /// (or whose zone name contains city/stad) reads as city; every other zone — savannah, dirt,
    /// vegetation — is wild. <see cref="SpeedLines"/> and <see cref="SlipstreamDust"/> each ease toward
    /// their share, so the hand-over is a crossfade at the zone border, never a pop.
    /// </summary>
    public static class SpeedReadZone
    {
        private static readonly string[] CityTags = { "city", "City", "CITY", "stad", "Stad" };

        /// <summary>True while the road grammar is riding a city zone (the speed lines carry the speed
        /// read there; the slipstream dust carries it everywhere else).</summary>
        public static bool InCity { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Hook()
        {
            GameEvents.SequenceChanged -= OnZoneChanged; // re-entry safety across domain reloads
            GameEvents.SequenceChanged += OnZoneChanged;
            if (RoadSequencer.Instance != null)
                OnZoneChanged(RoadSequencer.Instance.CurrentSequence); // the zone we spawned into
        }

        private static void OnZoneChanged(RoadSequence zone)
        {
            if (zone == null)
            {
                InCity = false;
                return;
            }
            string label = ((zone.zoneName ?? "") + " " + zone.name).ToLowerInvariant();
            InCity = zone.HasAnyTag(CityTags) || label.Contains("city") || label.Contains("stad");
        }
    }
}
