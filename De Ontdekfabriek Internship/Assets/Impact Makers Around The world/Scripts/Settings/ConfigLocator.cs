using UnityEngine;
using KenyaScooter.Config;

namespace KenyaScooter.Settings
{
    /// <summary>
    /// Finds the LIVE ScriptableObject config instances at runtime, without any scene wiring.
    ///
    /// The whole override system rests on one fact: every manager that uses a config holds a
    /// [SerializeField] reference to the SAME shared config asset (one ScooterConfig, one ScoreConfig,
    /// and so on — see SETUP — Quick Start.md). So if we mutate that one asset instance in memory before
    /// gameplay reads it, every manager picks up the change with zero per-system code.
    ///
    /// To reach those instances without a serialized reference we use Resources.FindObjectsOfTypeAll,
    /// which returns every loaded object of a type, including loaded-but-unreferenced assets. Because the
    /// managers reference the configs, the configs are loaded by the time the first scene is up, so they
    /// are found. We pick the first instance of each type (the project ships exactly one of each; if a
    /// future location adds variants, this still edits the active one as long as only one is loaded).
    ///
    /// HazardSpawnConfig is the exception — there are several (pothole, rock, bump). "Hazard density" is
    /// applied across ALL of them as a multiplier, handled in SettingsCatalog rather than here.
    ///
    /// Editing the in-memory asset does NOT write back to the project file in a build (you cannot), and
    /// in the Editor the change is reverted on exit of Play mode, which is exactly what we want: the
    /// shipped asset stays the documented default, and the facilitator's choices live only in the
    /// override store (PlayerPrefs).
    /// </summary>
    public static class ConfigLocator
    {
        /// <summary>Locates the first loaded instance of any config type, without a serialized reference — the same
        /// discovery the typed accessors use. Lets a self-bootstrapping system (e.g. PedestrianCrossingSpawner) find
        /// its config asset with zero scene wiring. Returns null if none is loaded, so the caller can no-op cleanly.</summary>
        public static T Find<T>() where T : ScriptableObject => First<T>();

        private static T First<T>() where T : ScriptableObject
        {
            T[] all = Resources.FindObjectsOfTypeAll<T>();
            for (int i = 0; i < all.Length; i++)
            {
                // Skip any that live in the editor's asset-preview / hidden space; in a build all are fair game.
                if (all[i] != null && (all[i].hideFlags & HideFlags.HideAndDontSave) == 0)
                    return all[i];
            }
            return all.Length > 0 ? all[0] : null;
        }

        public static ScooterConfig Scooter => Cached(ref _scooter);
        public static ScoreConfig Score => Cached(ref _score);
        public static SafetyNetConfig Safety => Cached(ref _safety);
        public static InputConfig Input => Cached(ref _input);
        public static SessionConfig Session => Cached(ref _session);
        public static TrafficConfig Traffic => Cached(ref _traffic);
        public static AudioConfig Audio => Cached(ref _audio);
        public static PedestrianCrossingConfig Pedestrian => Cached(ref _pedestrian);
        public static RoadsidePropConfig RoadsideProps => Cached(ref _roadsideProps);
        public static DayCycleConfig DayCycle => Cached(ref _dayCycle);
        public static WeatherConfig Weather => Cached(ref _weather);
        public static RoadSideConfig RoadSide => RoadSideConfig.Active != null ? RoadSideConfig.Active : Cached(ref _roadSide);

        public static HazardSpawnConfig[] Hazards
        {
            get
            {
                if (_hazards == null)
                    _hazards = Resources.FindObjectsOfTypeAll<HazardSpawnConfig>();
                return _hazards;
            }
        }

        private static ScooterConfig _scooter;
        private static ScoreConfig _score;
        private static SafetyNetConfig _safety;
        private static InputConfig _input;
        private static SessionConfig _session;
        private static TrafficConfig _traffic;
        private static AudioConfig _audio;
        private static PedestrianCrossingConfig _pedestrian;
        private static RoadsidePropConfig _roadsideProps;
        private static DayCycleConfig _dayCycle;
        private static WeatherConfig _weather;
        private static RoadSideConfig _roadSide;
        private static HazardSpawnConfig[] _hazards;

        private static T Cached<T>(ref T slot) where T : ScriptableObject
        {
            if (slot == null)
                slot = First<T>();
            return slot;
        }

        /// <summary>Drops the cache so a fresh scene/Play session re-resolves the live instances.</summary>
        public static void Forget()
        {
            _scooter = null; _score = null; _safety = null; _input = null;
            _session = null; _traffic = null; _audio = null; _pedestrian = null; _roadsideProps = null; _dayCycle = null; _weather = null; _roadSide = null; _hazards = null;
        }
    }
}
