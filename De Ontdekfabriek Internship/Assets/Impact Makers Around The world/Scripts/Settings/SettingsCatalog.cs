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

        // The profile lineup. Two INDEPENDENT axes the facilitator page presents as two labelled sections
        // (2026-07-14, Ryan): a MODE (Primary presets — how you play: Standaard = the class relay, or Eindeloze
        // modus = the solo lives run) and a DIFFICULTY (non-primary — Rustig / Normaal / Uitdagend). The three
        // difficulty presets only touch intensity settings, never session.endless, so any of them stacks cleanly
        // on EITHER mode. Normaal is the way BACK to normal after Rustig/Uitdagend. The old
        // Kenia/Nederland/Demo/Prikkelarm/Jonge-kinderen profiles were cut (they added nothing — Ryan); the calm
        // tuning lives on in Rustig and the drive-side is a quick toggle.
        private static List<SettingsPreset> BuildPresets()
        {
            var list = new List<SettingsPreset>();

            // MAIN MODE 1 — the class relay, as shipped. Clears every override, THEN pins its two signature
            // values so "standard" is the same on every device: right-side driving (as in the Netherlands) and
            // a 79 km/h top speed (just under the 80 zone limit — full throttle can never speed).
            list.Add(new SettingsPreset(
                "preset.standard", "Standaard",
                "De klassenrelay zoals bedoeld: rechts rijden (zoals in Nederland), topsnelheid 79 km/u en alle andere instellingen op standaard.",
                "Klassenrelay · rechts rijden · 79 km/u",
                new Dictionary<string, float>
                {
                    ["world.driveLeft"] = 0f,
                    ["feel.maxSpeed"] = 22f, // 22 m/s = 79 km/h
                },
                resetToDefault: true, primary: true));

            // MAIN MODE 2 — Eindeloze modus (endless): the solo lives mode. It only flips the mode (session.endless),
            // leaving the other settings as they are; "Standaard" turns it back off. This — and the toggle under
            // SPEELDUUR — is the ONLY way into it: there is no title button, so a child can never reach it.
            list.Add(new SettingsPreset(
                "preset.endless", "Eindeloze modus",
                "Zet het spel in de solomodus: levens in plaats van de klassenrelay, geen teamkeuze, telt niet mee voor de klas. Kies 'Standaard' om terug te gaan naar de klassenrelay.",
                "Solo · levens · geen teamkeuze",
                new Dictionary<string, float>
                {
                    ["session.endless"] = 1f,
                },
                primary: true));

            // DIFFICULTY — Rustig (easy): less going on for the player. Only intensity settings, so it works on top
            // of either mode (in the eindeloze modus it also grants extra lives).
            list.Add(new SettingsPreset(
                "preset.rustig", "Rustig",
                "Minder om op te letten: rustig tempo, weinig verkeer, geen tegenliggers, minder gevaren en meer hulp bij fouten. Werkt in beide modi.",
                "Langzaam · weinig verkeer · vergevingsgezind",
                new Dictionary<string, float>
                {
                    ["feel.baseSpeed"] = 8f,
                    ["feel.maxSpeed"] = 18f,
                    ["feel.acceleration"] = 11f,
                    ["diff.hazardDensity"] = 0.5f,
                    ["diff.trafficAmount"] = 3f,
                    ["world.oncoming"] = 0f,
                    ["diff.wrongLaneGrace"] = 8f,
                    ["diff.speedingGrace"] = 6f,
                    ["safe.rewind"] = 3f,
                    ["safe.graceCharges"] = 3f,
                    ["ctrl.motion"] = 0.85f,
                    ["session.endlessLives"] = 5f, // in the eindeloze modus: more room for mistakes (inert in the relay)
                }));

            // DIFFICULTY — Normaal: the balanced middle, and the way BACK to normal after Rustig or Uitdagend.
            // Writes the same keys as those two, at their neutral middle values, so tapping it undoes either.
            list.Add(new SettingsPreset(
                "preset.normal", "Normaal",
                "De gewone, gebalanceerde moeilijkheid: normaal tempo en verkeer, gewone hoeveelheid hulp. Gebruik dit om terug te gaan naar normaal na Rustig of Uitdagend.",
                "Gebalanceerd · normaal verkeer · 3 levens",
                new Dictionary<string, float>
                {
                    ["feel.baseSpeed"] = 10f,
                    ["feel.maxSpeed"] = 22f,
                    ["feel.acceleration"] = 15f,
                    ["diff.hazardDensity"] = 1f,
                    ["diff.trafficAmount"] = 6f,
                    ["world.oncoming"] = 4f,
                    ["diff.wrongLaneGrace"] = 5f,
                    ["diff.speedingGrace"] = 4f,
                    ["safe.rewind"] = 2f,
                    ["safe.graceCharges"] = 2f,
                    ["ctrl.motion"] = 1f,
                    ["session.endlessLives"] = 3f,
                }));

            // DIFFICULTY — Uitdagend (hard): ups the challenge. Faster (108 km/h — managing your speed IS the
            // challenge), busy road, strict on mistakes, little safety net. Works in both modes (fewer lives in the
            // eindeloze modus). Deliberately does NOT touch session.length — duration is the facilitator's own call.
            list.Add(new SettingsPreset(
                "preset.challenge", "Uitdagend",
                "Meer uitdaging: sneller (tot 108 km/u — zelf je snelheid bewaken hoort erbij), druk verkeer met tegenliggers, meer gevaren, strenger op fouten en minder vangnet. Werkt in beide modi.",
                "Sneller · druk verkeer · streng",
                new Dictionary<string, float>
                {
                    ["feel.baseSpeed"] = 12f,
                    ["feel.maxSpeed"] = 30f,
                    ["feel.acceleration"] = 18f,
                    ["diff.hazardDensity"] = 1.4f,
                    ["diff.trafficAmount"] = 9f,
                    ["world.oncoming"] = 7f,
                    ["diff.wrongLaneGrace"] = 3f,
                    ["diff.speedingGrace"] = 2f,
                    ["safe.rewind"] = 1f,
                    ["safe.graceCharges"] = 0f,
                    ["session.endlessLives"] = 2f, // in the eindeloze modus: fewer lives (inert in the relay)
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
