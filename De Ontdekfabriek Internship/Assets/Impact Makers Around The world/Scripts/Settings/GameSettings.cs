using System.Collections.Generic;
using UnityEngine;
using KenyaScooter.Config;

namespace KenyaScooter.Settings
{
    /// <summary>
    /// The facilitator OVERRIDES layer and the answer to the core problem: ScriptableObject config assets
    /// are baked into the build, so a value changed at runtime is lost on the next app start and you cannot
    /// write back to the asset. This store keeps the facilitator's chosen values in PlayerPrefs and re-applies
    /// them onto the live config instances at every startup, BEFORE gameplay reads them — so the whole game
    /// runs on the chosen values with no per-system changes, and the choices survive closing the app.
    ///
    /// How it works:
    ///  1. On startup (RuntimeInitializeOnLoadMethod, BEFORE the first scene's Awake) we capture each
    ///     setting's SHIPPED DEFAULT once — read straight off the freshly loaded SO. Defaults are themselves
    ///     cached in PlayerPrefs the first time, so "reset to default" keeps working even after the asset has
    ///     been mutated this session, and even if a later build changes a default (the FIRST build to run
    ///     defines the baseline; documented as a known trade-off).
    ///  2. We then push any SAVED override on top, calling the definition's setter (which writes the live SO).
    ///  3. The menu reads current values via the getters and, on edit, writes the override + re-applies live.
    ///
    /// Persistence is plain PlayerPrefs (the same mechanism LeaderboardManager and the haptics toggle already
    /// use), one float per setting under "ksg.set.&lt;key&gt;", with the captured defaults under "ksg.def.&lt;key&gt;".
    /// No JSON file is needed because every exposed setting is a single scalar.
    /// </summary>
    public static class GameSettings
    {
        private const string OverridePrefix = "ksg.set.";
        private const string DefaultPrefix = "ksg.def.";
        private const string BaseHazardPrefix = "ksg.basehaz."; // per-hazard-asset shipped density
        private const string BaseRewindKey = "ksg.baserewinds";
        private const string AppliedFlag = "ksg.settings.applied"; // diagnostic only

        private static bool _applied;

        // ---- startup apply -------------------------------------------------------------

        /// <summary>
        /// Runs once before the first scene's components Awake, so every config is corrected before any
        /// manager reads it. BeforeSceneLoad guarantees the SO assets referenced by scene components are
        /// already loaded (Unity deserialises the scene's asset references at this point).
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void ApplyOnStartup()
        {
            ConfigLocator.Forget(); // fresh resolve for this run (Editor enter-play safety)
            CleanRetiredKeys();
            CaptureBaselinesIfNeeded();
            ApplyAll();
            _applied = true;
            PlayerPrefs.SetInt(AppliedFlag, 1);
        }

        /// <summary>
        /// Deletes the PlayerPrefs residue of settings REMOVED from the catalog (2026-07-14: Voetgangers,
        /// Leven/Drukte langs de weg, Regio-reis). Their ksg.set./ksg.def. entries are merely stale (ApplyAll
        /// loops the catalog, so unknown keys are never applied), but a setter that wrote a RAW key the game
        /// reads directly — Regio-reis wrote "ksg.regionJourney", read by RoadSequencer every zone pick — would
        /// keep steering the game FOREVER with no UI left to turn it off. Wiping here makes removal complete.
        /// GOTCHA FOR FUTURE REMOVALS: if the setting's set() wrote PlayerPrefs directly (not a config field),
        /// add its raw key here when you delete its SettingDefinition.
        /// </summary>
        private static void CleanRetiredKeys()
        {
            PlayerPrefs.DeleteKey("ksg.regionJourney"); // = RoadSequencer.RegionJourneyPrefKey (raw, game-read)
            string[] retired = { "world.pedestrians", "env.roadsideLife", "env.roadsideDensity", "env.regionJourney" };
            for (int i = 0; i < retired.Length; i++)
            {
                PlayerPrefs.DeleteKey(OverridePrefix + retired[i]);
                PlayerPrefs.DeleteKey(DefaultPrefix + retired[i]);
            }
        }

        /// <summary>Pushes every saved override onto the live configs. Settings with no saved override are left at default.</summary>
        public static void ApplyAll()
        {
            foreach (var def in SettingsCatalog.All)
            {
                if (def.Set == null)
                    continue;
                if (HasOverride(def.Key))
                {
                    try { def.Set(def.Coerce(GetOverride(def.Key, def.Get != null ? def.Get() : 0f))); }
                    catch (System.Exception e) { Debug.LogException(e); } // never let one bad setting stop the rest
                }
            }
        }

        // ---- baselines (shipped defaults) ----------------------------------------------

        /// <summary>
        /// On the very first run we record each setting's current value as the shipped default, plus the two
        /// special bases (per-hazard density and rewind count) the catalog needs for its relative dials.
        /// Subsequent runs read the stored defaults so a previously-applied override doesn't pollute them.
        /// </summary>
        private static void CaptureBaselinesIfNeeded()
        {
            // Special bases first — the hazard-density and rewind getters depend on these.
            var hazards = ConfigLocator.Hazards;
            if (hazards != null)
            {
                for (int i = 0; i < hazards.Length; i++)
                {
                    if (hazards[i] == null) continue;
                    string k = BaseHazardPrefix + hazards[i].name;
                    if (!PlayerPrefs.HasKey(k))
                        PlayerPrefs.SetFloat(k, hazards[i].clustersPer100m);
                }
            }
            var safety = ConfigLocator.Safety;
            if (safety != null && !PlayerPrefs.HasKey(BaseRewindKey))
                PlayerPrefs.SetInt(BaseRewindKey, Mathf.Max(1, safety.rewindsPerTurn));

            // Per-setting defaults: capture the live (pre-override) value once.
            foreach (var def in SettingsCatalog.All)
            {
                string k = DefaultPrefix + def.Key;
                if (!PlayerPrefs.HasKey(k) && def.Get != null)
                {
                    try { PlayerPrefs.SetFloat(k, def.Get()); }
                    catch (System.Exception e) { Debug.LogException(e); }
                }
            }
            PlayerPrefs.Save();
        }

        /// <summary>Shipped density of a specific hazard asset (used by the density multiplier in the catalog).</summary>
        public static float BaseHazardDensity(Object hazardAsset)
        {
            if (hazardAsset == null) return 3f;
            string k = BaseHazardPrefix + hazardAsset.name;
            if (PlayerPrefs.HasKey(k)) return PlayerPrefs.GetFloat(k);
            // Not captured yet (catalog read before startup capture): fall back to the live value.
            var hsc = hazardAsset as HazardSpawnConfig;
            return hsc != null ? hsc.clustersPer100m : 3f;
        }

        /// <summary>Shipped per-turn rewind count, so the rewind toggle can restore "ON" to the right number.</summary>
        public static float BaseRewindsPerTurn(Object safetyAsset)
            => PlayerPrefs.HasKey(BaseRewindKey) ? PlayerPrefs.GetInt(BaseRewindKey) : 2f;

        /// <summary>The shipped default for a setting (for the per-row reset and to detect "is at default").</summary>
        public static float DefaultOf(SettingDefinition def)
        {
            string k = DefaultPrefix + def.Key;
            if (PlayerPrefs.HasKey(k)) return PlayerPrefs.GetFloat(k);
            return def.Get != null ? def.Get() : 0f;
        }

        // ---- read / write a single setting ---------------------------------------------

        public static bool HasOverride(string key) => PlayerPrefs.HasKey(OverridePrefix + key);
        private static float GetOverride(string key, float fallback) => PlayerPrefs.GetFloat(OverridePrefix + key, fallback);

        /// <summary>The value to SHOW for a setting: live value if no override, else the saved override (coerced).</summary>
        public static float CurrentValue(SettingDefinition def)
        {
            if (HasOverride(def.Key))
                return def.Coerce(GetOverride(def.Key, def.Get != null ? def.Get() : 0f));
            return def.Get != null ? def.Get() : 0f;
        }

        /// <summary>Facilitator edited a setting: coerce, save as override, and apply live immediately.</summary>
        public static void Set(SettingDefinition def, float rawValue)
        {
            float v = def.Coerce(rawValue);
            PlayerPrefs.SetFloat(OverridePrefix + def.Key, v);
            PlayerPrefs.Save();
            if (def.Set != null)
            {
                try { def.Set(v); } catch (System.Exception e) { Debug.LogException(e); }
            }
        }

        /// <summary>Reset one setting to its shipped default (clears the override, applies the default live).</summary>
        public static void ResetOne(SettingDefinition def)
        {
            PlayerPrefs.DeleteKey(OverridePrefix + def.Key);
            PlayerPrefs.Save();
            float d = DefaultOf(def);
            if (def.Set != null)
            {
                try { def.Set(def.Coerce(d)); } catch (System.Exception e) { Debug.LogException(e); }
            }
        }

        /// <summary>Reset EVERYTHING to the shipped defaults.</summary>
        public static void ResetAll()
        {
            foreach (var def in SettingsCatalog.All)
                ResetOne(def);
        }

        public static bool IsAtDefault(SettingDefinition def)
            => !HasOverride(def.Key) || Mathf.Approximately(def.Coerce(GetOverride(def.Key, 0f)), def.Coerce(DefaultOf(def)));

        /// <summary>True once the startup apply has run this session (diagnostic, e.g. for the menu header).</summary>
        public static bool Applied => _applied;
    }
}
