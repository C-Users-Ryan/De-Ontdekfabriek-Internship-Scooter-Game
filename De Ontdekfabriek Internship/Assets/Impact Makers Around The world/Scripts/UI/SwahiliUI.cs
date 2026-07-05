using System;
using System.Collections.Generic;
using UnityEngine;

namespace KenyaScooter.UI
{
    /// <summary>
    /// Dutch/English/Swahili UI dictionary (Req §12.3). The game ships in DUTCH — that is the default
    /// language and the fallback, so nothing ever shows in English by accident. A few callouts stay in the
    /// game's established voice on purpose (INGEHAALD!, KUUKUA!) and the Swahili column carries the Kenya
    /// flavour for the language toggle. Where no verified Swahili exists, the English string is reused
    /// rather than inventing wrong Swahili — flagged for native-speaker review in the architecture doc
    /// (SC4: respectful representation beats fake localisation).
    /// </summary>
    public static class SwahiliUI
    {
        public enum Language { English, Swahili, Dutch }

        // Dutch is the shipped language (kid-facing, Dutch classrooms). English/Swahili remain reachable via the toggle.
        public static Language Current { get; private set; } = Language.Dutch;

        public static event Action LanguageChanged;

        // Optional editable strings asset (English/Dutch/Swahili). When loaded, Get consults it first and only
        // falls back to the built-in table below. Null = the game runs purely on the built-ins (current behaviour).
        private static UIStringsConfig _config;

        // Each row is (English, Dutch, Swahili). Dutch ships; English/Swahili back the language toggle. A blank
        // Dutch cell falls back to English in Get(). Kenya-flavour callouts (INGEHAALD!, KUUKUA!, the Swahili
        // time-of-day labels elsewhere) are intentionally kept, per the locked UI direction.
        private static readonly Dictionary<string, (string en, string nl, string sw)> Table =
            new Dictionary<string, (string, string, string)>
            {
                // HUD
                { "HUD_SCORE",      ("SCORE", "SCORE", "ALAMA") },
                { "HUD_TIME",       ("TIME", "TIJD", "MUDA") },
                { "START_PROMPT",   ("TAP TO START", "TIK OM TE STARTEN", "ANZA!") },

                // Popups (M19) — INGEHAALD/KUUKUA are the game's established voice (Req §7.5), kept in Dutch too
                { "POPUP_OVERTAKE", ("INGEHAALD!", "INGEHAALD!", "INGEHAALD!") },
                { "POPUP_DEDUCT",   ("KUUKUA!", "KUUKUA!", "KUUKUA!") },
                { "POPUP_GRACE",    ("CLOSE ONE!", "OP HET NIPPERTJE!", "CLOSE ONE!") }, // sw TODO: native review
                { "POPUP_REWIND",   ("REWIND", "TERUGSPOELEN", "REWIND") },
                { "POPUP_YIELD",    ("GOED GEWACHT!", "GOED GEWACHT!", "GOED GEWACHT!") }, // M28 pedestrian yield reward; sw TODO: native review

                // Warnings (Req §12.1)
                { "WARN_COLLISION", ("COLLISION!", "BOTSING!", "COLLISION!") },          // sw TODO: native review
                { "WARN_WRONGLANE", ("WRONG LANE!", "VERKEERDE WEGHELFT!", "WRONG LANE!") }, // sw TODO: native review
                { "WARN_SPEEDING",  ("TOO FAST!", "TE SNEL!", "POLE POLE!") },
                { "WARN_POTHOLE",   ("POTHOLE!", "KUIL!", "POTHOLE!") },                  // sw TODO: native review
                { "WARN_ROCK",      ("ROCKS!", "STENEN!", "ROCKS!") },                    // sw TODO: native review
                { "WARN_BUMP",      ("SPEED BUMP!", "DREMPEL!", "SPEED BUMP!") },         // sw TODO: native review
                { "WARN_CROSSING_AHEAD", ("VOETGANGERS · REM AF", "VOETGANGERS · REM AF", "VOETGANGERS · REM AF") }, // M28 crossing telegraph; sw TODO: native review
                { "WARN_TURN_LEFT",  ("BEND  ·  ← LEFT",  "BOCHT  ·  ← LINKS",  "BOCHT  ·  ← LINKS") },  // turn telegraph (2026-07-05); "  ·  " splits the HUD banner into two lines
                { "WARN_TURN_RIGHT", ("BEND  ·  RIGHT →", "BOCHT  ·  RECHTS →", "BOCHT  ·  RECHTS →") }, // turn telegraph (2026-07-05); sw TODO: native review
                { "WARN_PEDESTRIAN", ("LAAT VOETGANGERS VOORGAAN", "LAAT VOETGANGERS VOORGAAN", "LAAT VOETGANGERS VOORGAAN") }, // M28 pedestrian hit; sw TODO: native review

                // Screens (Req §12.2, §13)
                { "GAME_OVER",      ("GAME OVER", "SPEL VOORBIJ", "GAME OVER") },
                { "FINISH_TITLE",   ("JOURNEY COMPLETE", "REIS VOLTOOID", "MWISHO WA SAFARI") },
                { "CHECKPOINT_TITLE", ("CHARGE STATION", "LAADSTATION", "CHARGE STATION") }, // sw TODO: native review
                { "NEW_RECORD",     ("NEW RECORD!", "NIEUW RECORD!", "REKODI MPYA!") },
                { "NEXT_PLAYER",    ("NEXT PLAYER", "VOLGENDE SPELER", "NEXT PLAYER") },  // sw TODO: native review
                { "GROUP_TOTAL",    ("CLASS TOTAL", "KLAS TOTAAL", "CLASS TOTAL") },      // sw TODO: native review
                { "RANK_ANNOUNCE",  ("JULLIE STAAN OP PLEK {0}!", "JULLIE STAAN OP PLEK {0}!", "JULLIE STAAN OP PLEK {0}!") }
            };

        /// <summary>The shipped Dutch/English/Swahili strings, exposed read-only so UIStringsConfig can seed its
        /// editable rows ("Fill with built-in defaults") in every language.</summary>
        public static IReadOnlyDictionary<string, (string en, string nl, string sw)> BuiltInDefaults => Table;

        /// <summary>Point the dictionary at an editable strings asset (English/Dutch/Swahili). Keys it does not
        /// cover fall back to the built-in table, then to the key itself. Pass null to use only the built-ins.</summary>
        public static void Load(UIStringsConfig config) => _config = config;

        public static string Get(string key)
        {
            // An assigned strings asset wins (any facilitator overrides); otherwise the built-in Dutch/English/Swahili table.
            if (_config != null && _config.TryGet(key, Current, out string localized))
                return localized;
            if (Table.TryGetValue(key, out (string en, string nl, string sw) entry))
            {
                string value = Current == Language.Swahili ? entry.sw
                             : Current == Language.Dutch   ? entry.nl
                             : entry.en;
                return string.IsNullOrEmpty(value) ? entry.en : value; // a blank cell falls back to English
            }
            return key;
        }

        // Cycle Dutch → Swahili → English → Dutch, so no language is unreachable now that Dutch is the default.
        public static void Toggle()
        {
            Language next = Current == Language.Dutch   ? Language.Swahili
                          : Current == Language.Swahili ? Language.English
                          : Language.Dutch;
            SetLanguage(next);
        }

        public static void SetLanguage(Language language)
        {
            if (Current == language)
                return;
            Current = language;
            LanguageChanged?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Current = Language.Dutch;
            LanguageChanged = null;
            // Auto-load a strings asset named "UIStringsConfig" from any Resources folder, if one exists. Harmless
            // (no-op) when absent, so the game keeps running on the built-in table until an asset is added.
            _config = Resources.Load<UIStringsConfig>("UIStringsConfig");
        }
    }
}
