using System.Collections.Generic;
using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Controls;
using KenyaScooter.Scoring;
using KenyaScooter.Core;
using KenyaScooter.Cameras;
using KenyaScooter.Roads;

namespace KenyaScooter.Settings
{
    /// <summary>
    /// The CURATED, non-technical list of what a facilitator may change, as data. This is the ONLY file
    /// you touch to add or remove a setting — the store and the menu read this list and need no changes.
    ///
    /// Design rules followed here:
    ///  - Plain Dutch labels and one-line descriptions, never raw field names.
    ///  - Sensible min/max so a child-safe range can't break the game (e.g. speed can't go to zero).
    ///  - Each setting maps to the RIGHT live config field via a getter/setter, so applying a saved
    ///    override mutates the shared SO instance and every system picks it up (see ConfigLocator).
    ///  - A few "feel" knobs intentionally drive more than one field (e.g. "hazard density" scales every
    ///    HazardSpawnConfig at once) so the facilitator turns one dial, not ten.
    ///
    /// EXTENSION POINT (future interns / next team):
    ///  - To expose a new numeric/toggle tunable: add one new SettingDefinition entry below. Done.
    ///  - To add a whole new category: add a value to SettingCategory and a label in CategoryLabel().
    ///  - TILE / HAZARD CONTENT editing (which hazards appear, road order, new locations) is deliberately
    ///    OUT of scope here — it is structural data, not a scalar, so it needs a different editor (a list
    ///    editor over the HazardSpawnConfig[] / RoadSequence assets, ideally writing JSON to
    ///    Application.persistentDataPath and rebuilding the spawner). The override-store + apply-on-startup
    ///    plumbing in GameSettings is reusable for it; only a new UI surface is needed. Documented in the
    ///    Sprint 8 design note.
    /// </summary>
    // NOTE: this class is split across two files for readability (2026-06-30, no behaviour change):
    //   SettingsCatalog.cs             — the query API: InCategory/ById, actions, presets, labels, value formatters.
    //   SettingsCatalog.Definitions.cs — Build(): the curated list of every facilitator setting, grouped by category.
    public static partial class SettingsCatalog
    {
        private static List<SettingDefinition> _all;

        /// <summary>The full ordered list, built once. Safe to call before any config is loaded — getters resolve lazily.</summary>
        public static IReadOnlyList<SettingDefinition> All
        {
            get
            {
                if (_all == null)
                    _all = Build();
                return _all;
            }
        }

        public static IEnumerable<SettingDefinition> InCategory(SettingCategory category)
        {
            foreach (var s in All)
                if (s.Category == category)
                    yield return s;
        }

        /// <summary>Finds a setting by its stable key, or null. Lets another surface (e.g. a front-of-house mode
        /// toggle on the title screen) drive the very same setting the facilitator menu does, so the two stay in
        /// sync and persist identically through <see cref="GameSettings"/>.</summary>
        public static SettingDefinition ById(string key)
        {
            foreach (var s in All)
                if (s.Key == key)
                    return s;
            return null;
        }

        private static List<SettingAction> _actions;
        /// <summary>The facilitator action buttons (recalibrate, clear leaderboard...). Built once, like the settings list.</summary>
        public static IReadOnlyList<SettingAction> Actions
        {
            get
            {
                if (_actions == null)
                    _actions = BuildActions();
                return _actions;
            }
        }

        public static IEnumerable<SettingAction> ActionsInCategory(SettingCategory category)
        {
            foreach (var a in Actions)
                if (a.Category == category)
                    yield return a;
        }

        private static List<SettingAction> BuildActions()
        {
            var list = new List<SettingAction>();

            // Recalibrate steering: re-zero the tilt to the tablet's current pose. The most likely real
            // facilitator complaint (gyro drift) is fixed by this, with no developer involved.
            list.Add(new SettingAction(
                "act.recalibrate", "Sturen opnieuw kalibreren",
                "Zet de huidige houding van de tablet als 'recht'. Gebruik dit als het sturen scheef aanvoelt: houd de tablet zoals je wilt en tik dan.",
                SettingCategory.Controls, "KALIBREER", false,
                () => { if (ScooterInputRouter.Instance != null) ScooterInputRouter.Instance.Calibrate(); }));

            // Replay the one-time "how to play" card (tilt to steer, right = gas, left = brake) at the start of the
            // next turn. Handy when a fresh group needs the intro the first player already dismissed.
            list.Add(new SettingAction(
                "act.showHowto", "Uitleg opnieuw tonen",
                "Laat de korte uitleg (kantelen om te sturen, rechts = gas, links = rem) opnieuw zien bij de start van de volgende beurt.",
                SettingCategory.Controls, "TOON", false,
                () => KenyaScooter.UI.HowToPlayOverlay.ResetFirstRun()));

            // Change the access code. Opens the on-screen keypad in "set a new code" mode (enter twice to confirm),
            // so staff never need a hardware keyboard. The menu owns that little flow; the action just asks for it.
            list.Add(new SettingAction(
                "act.changePin", "Toegangscode wijzigen",
                "Stel een nieuwe cijfercode in voor dit menu. Je tikt de nieuwe code twee keer in ter bevestiging.",
                SettingCategory.Management, "WIJZIGEN", false,
                () => SettingsMenu.RequestChangePin()));

            // Clear the local leaderboard for a new class / new day. Destructive, so the menu confirms.
            list.Add(new SettingAction(
                "act.resetLeaderboard", "Klassement wissen",
                "Verwijdert alle groepsscores van deze tablet. Doe dit aan het begin van een nieuwe dag of een nieuwe klas. Kan niet ongedaan worden gemaakt.",
                SettingCategory.Management, "WISSEN", true,
                () => { if (LeaderboardManager.Instance != null) LeaderboardManager.Instance.ResetLeaderboard(); }));

            return list;
        }

        // ---- Profiles (one-tap whole-game presets) -----------------------------------------
        private static List<SettingsPreset> _presets;
        /// <summary>The curated one-tap profiles shown on the Profielen page. Built once, like the settings list.</summary>
        public static IReadOnlyList<SettingsPreset> Presets
        {
            get
            {
                if (_presets == null)
                    _presets = BuildPresets();
                return _presets;
            }
        }

        private static List<SettingsPreset> BuildPresets()
        {
            var list = new List<SettingsPreset>();

            // "Standaard" fully clears every override — the safe way back to the shipped, balanced game.
            list.Add(new SettingsPreset(
                "preset.standard", "Standaard",
                "De gebalanceerde standaardinstelling. Zet alles terug zoals het spel bedoeld is.",
                "Alles terug naar standaard",
                null, resetToDefault: true));

            // Young / calm group: slow, forgiving, quiet road, lots of safety net.
            list.Add(new SettingsPreset(
                "preset.young", "Jonge kinderen — rustig",
                "Voor de jongste of voorzichtige kinderen. Rustig tempo, weinig op de weg, veel hulp bij fouten.",
                "Langzaam · weinig verkeer · vergevingsgezind",
                new Dictionary<string, float>
                {
                    ["feel.baseSpeed"] = 8f,
                    ["feel.maxSpeed"] = 18f,
                    ["feel.acceleration"] = 11f,
                    ["diff.hazardDensity"] = 0.5f,
                    ["diff.trafficAmount"] = 3f,
                    ["world.oncoming"] = 0f,
                    ["world.pedestrians"] = 0.3f,
                    ["diff.wrongLaneGrace"] = 8f,
                    ["diff.speedingGrace"] = 6f,
                    ["safe.rewind"] = 3f,
                    ["safe.graceCharges"] = 3f,
                    ["ctrl.motion"] = 0.85f,
                    ["session.length"] = 90f,
                }));

            // Older / confident group: brisk, busy, strict, little safety net.
            list.Add(new SettingsPreset(
                "preset.challenge", "Uitdagend — oudere kinderen",
                "Voor oudere of ervaren kinderen. Vlotter tempo, druk verkeer en strenger op fouten.",
                "Sneller · druk verkeer · streng",
                new Dictionary<string, float>
                {
                    ["feel.baseSpeed"] = 12f,
                    ["feel.maxSpeed"] = 30f,
                    ["feel.acceleration"] = 18f,
                    ["diff.hazardDensity"] = 1.4f,
                    ["diff.trafficAmount"] = 9f,
                    ["world.oncoming"] = 7f,
                    ["world.pedestrians"] = 1.0f,
                    ["diff.wrongLaneGrace"] = 3f,
                    ["diff.speedingGrace"] = 2f,
                    ["safe.rewind"] = 1f,
                    ["safe.graceCharges"] = 0f,
                    ["session.length"] = 150f,
                }));

            // Authentic Kenya look: drive on the left, warm dust, lively roadside, full day cycle.
            list.Add(new SettingsPreset(
                "preset.kenya", "Kenia-modus",
                "De echte Keniaanse rijervaring: links rijden, warme stoffige lucht en een levendige berm.",
                "Links rijden · stof · leven langs de weg",
                new Dictionary<string, float>
                {
                    ["world.driveLeft"] = 1f,
                    ["env.dust"] = 1f,
                    ["env.roadsideLife"] = 1f,
                    ["env.roadsideDensity"] = 9f,
                    ["env.dayCycle"] = 1f,
                    ["env.dirtFeel"] = 2f,
                }));

            // Dutch comparison: drive on the right, clearer air — the "now how is it at home?" setup.
            list.Add(new SettingsPreset(
                "preset.netherlands", "Nederland-modus",
                "Ter vergelijking: rijden zoals in Nederland — rechts rijden en een heldere lucht.",
                "Rechts rijden · heldere lucht",
                new Dictionary<string, float>
                {
                    ["world.driveLeft"] = 0f,
                    ["env.dust"] = 0f,
                    ["env.roadsideDensity"] = 6f,
                }));

            // Open day / demo: short, lively, forgiving and good-looking, so a passer-by has a good first go.
            list.Add(new SettingsPreset(
                "preset.demo", "Demo / open dag",
                "Korte, levendige en vergevingsgezinde beurten die er goed uitzien — ideaal voor een open dag of demo.",
                "Korte beurt · levendig · vergevingsgezind",
                new Dictionary<string, float>
                {
                    ["session.length"] = 75f,
                    ["feel.baseSpeed"] = 11f,
                    ["diff.hazardDensity"] = 0.8f,
                    ["diff.trafficAmount"] = 6f,
                    ["world.oncoming"] = 4f,
                    ["env.roadsideLife"] = 1f,
                    ["env.roadsideDensity"] = 11f,
                    ["env.dust"] = 1f,
                    ["env.dayCycle"] = 1f,
                    ["safe.rewind"] = 3f,
                    ["safe.graceCharges"] = 2f,
                    ["session.cinematicRelay"] = 1f,
                }));

            // Sensory-friendly / calm: low motion, no dust, quiet road, no haptics, steady light.
            list.Add(new SettingsPreset(
                "preset.calm", "Prikkelarm — rustig & kalm",
                "Voor kinderen die snel overprikkeld raken. Minder beweging, geen stof, rustige weg en stille tablet.",
                "Weinig beweging · geen stof · stil",
                new Dictionary<string, float>
                {
                    ["ctrl.motion"] = 0.5f,
                    ["ctrl.smoothing"] = 16f,
                    ["ctrl.realRider"] = 0f,
                    ["env.dust"] = 0f,
                    ["env.dayCycle"] = 0f,
                    ["env.fixedPhase"] = 1f,
                    ["env.roadsideDensity"] = 3f,
                    ["env.dirtFeel"] = 0f,
                    ["diff.trafficAmount"] = 2f,
                    ["world.oncoming"] = 0f,
                    ["world.pedestrians"] = 0.2f,
                    ["feel.baseSpeed"] = 8f,
                    ["feel.maxSpeed"] = 16f,
                    ["audio.haptics"] = 0f,
                }));

            return list;
        }

        public static string CategoryLabel(SettingCategory c)
        {
            switch (c)
            {
                case SettingCategory.Profiles:   return "PROFIELEN";
                case SettingCategory.Difficulty: return "MOEILIJKHEID";
                case SettingCategory.TrafficWorld: return "VERKEER EN WEG";
                case SettingCategory.Environment: return "OMGEVING";
                case SettingCategory.SpeedFeel:  return "SNELHEID EN GEVOEL";
                case SettingCategory.SafetyNet:  return "VANGNET";
                case SettingCategory.Controls:   return "BESTURING";
                case SettingCategory.Audio:      return "GELUID";
                case SettingCategory.Scoring:    return "PUNTEN";
                case SettingCategory.Session:    return "SPEELDUUR";
                case SettingCategory.Management:  return "BEHEER";
                case SettingCategory.Overview:    return "OVERZICHT";
                default: return c.ToString().ToUpper();
            }
        }

        /// <summary>A one-line, plain-language "what does this category control" line, shown at the top of each
        /// category page so a facilitator understands the group before reading any single row.</summary>
        public static string CategoryBlurb(SettingCategory c)
        {
            switch (c)
            {
                case SettingCategory.Profiles:    return "Kies in één tik een complete opstelling voor de groep.";
                case SettingCategory.Difficulty:  return "Hoe moeilijk of vergevingsgezind het spel is.";
                case SettingCategory.TrafficWorld: return "Wat er op de weg gebeurt en aan welke kant je rijdt.";
                case SettingCategory.Environment: return "Hoe de wereld eruitziet: tijd van de dag, stof en de berm.";
                case SettingCategory.SpeedFeel:   return "Hoe snel en hoe pittig de scooter aanvoelt.";
                case SettingCategory.SafetyNet:   return "Hoeveel hulp een kind krijgt bij een botsing.";
                case SettingCategory.Controls:    return "Hoe het sturen en de knoppen werken.";
                case SettingCategory.Audio:       return "Het volume van de verschillende geluiden.";
                case SettingCategory.Scoring:     return "Waarvoor punten worden gegeven of afgetrokken.";
                case SettingCategory.Session:     return "Hoe lang een beurt duurt en wat er tussen spelers gebeurt.";
                case SettingCategory.Management:  return "De toegangscode en het wissen van scores.";
                case SettingCategory.Overview:    return "Alles wat je hebt veranderd ten opzichte van de standaard, op één scherm.";
                default: return "";
            }
        }

        // The settings shown only after "Meer opties" — the finer tuning, kept out of the way so each category
        // leads with the few knobs a facilitator reaches for most. Membership lives here (one list) rather than on
        // every entry, so it is easy to see and adjust the basic/advanced split in one place.
        private static readonly HashSet<string> _advanced = new HashSet<string>
        {
            "diff.wrongLaneGrace", "diff.speedingGrace",
            "env.fixedPhase", "env.skyShifts",
            "feel.acceleration", "feel.lean", "feel.brake", "feel.agility",
            "safe.graceRecharge",
            "ctrl.smoothing", "ctrl.deadzone",
            "audio.music", "audio.ambient", "audio.engine", "audio.sfx",
            "score.deductPotholes", "score.deductRocks", "score.deductBumps",
            "score.trickle", "score.correctLaneBonus", "score.cleanZoneBonus", "score.collisionDeduction",
            "session.handoffWait", "session.endWait",
        };

        /// <summary>True for a finer-tuning setting tucked behind "Meer opties" on its category page.</summary>
        public static bool IsAdvanced(string key) => _advanced.Contains(key);

        // Pretty-printers ------------------------------------------------------------------
        private static string OnOff(float v) => v >= 0.5f ? "AAN" : "UIT";
        private static string Seconds(float v) => Mathf.RoundToInt(v) + " s";
        private static string Kmh(float v) => Mathf.RoundToInt(v * 3.6f) + " km/u"; // configs store m/s
        private static string Percent(float v) => Mathf.RoundToInt(v * 100f) + "%";
        private static string Points(float v) => Mathf.RoundToInt(v) + " ptn";
        private static string PedestrianRate(float v) => v <= 0.001f ? "UIT" : v < 0.5f ? "WEINIG" : v < 1.0f ? "NORMAAL" : "VEEL";
        private static string RoadsideBusyness(float v) => v <= 0.001f ? "LEEG" : v < 4f ? "RUSTIG" : v < 9f ? "NORMAAL" : "DRUK";
        private static string LeftRight(float v) => v >= 0.5f ? "LINKS" : "RECHTS";
        private static string HandheldMode(float v)
        {
            switch (Mathf.RoundToInt(v)) { case 1: return "ALTIJD"; case 2: return "NOOIT"; default: return "AUTOMATISCH"; }
        }
        private static string Rewinds(float v) => v <= 0.001f ? "UIT" : Mathf.RoundToInt(v) + "x";

        // Turns a bare number into a plain-language word by where it sits in its [min,max] range, so a facilitator
        // reads "NORMAAL" or "SNEL" instead of "15" for the abstract feel knobs that have no natural unit.
        private static string Band(float v, float min, float max, params string[] words)
        {
            if (words.Length == 0) return "";
            if (max <= min) return words[0];
            float f = Mathf.Clamp01((v - min) / (max - min));
            int i = Mathf.Clamp(Mathf.FloorToInt(f * words.Length), 0, words.Length - 1);
            return words[i];
        }
        private static string PhaseName(float v)
        {
            switch (Mathf.RoundToInt(v))
            {
                case 0: return "OCHTEND";
                case 1: return "MIDDAG";
                case 2: return "NAMIDDAG";
                default: return "AVOND";
            }
        }
        private static string DirtFeel(float v)
        {
            switch (Mathf.RoundToInt(v)) { case 0: return "UIT"; case 1: return "SUBTIEL"; default: return "VOL"; }
        }
        // 0 = let the journey choose; 1..N = a specific zone, named live from the road sequencer so the labels always
        // match the shipped sequences. Falls back to "ZONE n" if the scene's sequencer is not up yet (e.g. on a menu
        // shown before play), so the picker is never blank.
        private static string ZoneName(float v)
        {
            int i = Mathf.RoundToInt(v);
            if (i <= 0) return "AUTOMATISCH";
            RoadSequencer seq = RoadSequencer.Instance;
            if (seq != null)
            {
                string n = seq.SelectableZoneName(i);
                if (!string.IsNullOrEmpty(n)) return n.ToUpper();
            }
            return "ZONE " + i;
        }

    }
}
