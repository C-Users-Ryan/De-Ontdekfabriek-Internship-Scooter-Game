using System;
using System.Collections.Generic;
using UnityEngine;

namespace KenyaScooter.UI
{
    /// <summary>
    /// English/Swahili UI dictionary (Req §12.3). Strings the Requirements document
    /// specifies are used verbatim (INGEHAALD!, KUUKUA!, REKODI MPYA!, "JULLIE STAAN
    /// OP PLEK {0}!"). Where no verified Swahili exists, the English string is reused
    /// rather than inventing wrong Swahili — flagged for native-speaker review in the
    /// architecture doc (SC4: respectful representation beats fake localisation).
    /// </summary>
    public static class SwahiliUI
    {
        public enum Language { English, Swahili }

        public static Language Current { get; private set; } = Language.English;

        public static event Action LanguageChanged;

        private static readonly Dictionary<string, (string en, string sw)> Table =
            new Dictionary<string, (string, string)>
            {
                // HUD
                { "HUD_SCORE",      ("SCORE", "ALAMA") },
                { "HUD_TIME",       ("TIME", "MUDA") },
                { "START_PROMPT",   ("TAP TO START", "ANZA!") },

                // Popups (M19) — INGEHAALD/KUUKUA are the game's established voice (Req §7.5)
                { "POPUP_OVERTAKE", ("INGEHAALD!", "INGEHAALD!") },
                { "POPUP_DEDUCT",   ("KUUKUA!", "KUUKUA!") },
                { "POPUP_GRACE",    ("CLOSE ONE!", "CLOSE ONE!") },      // TODO: native review
                { "POPUP_REWIND",   ("REWIND", "REWIND") },

                // Warnings (Req §12.1)
                { "WARN_COLLISION", ("COLLISION!", "COLLISION!") },      // TODO: native review
                { "WARN_WRONGLANE", ("WRONG LANE!", "WRONG LANE!") },    // TODO: native review
                { "WARN_SPEEDING",  ("TOO FAST!", "POLE POLE!") },
                { "WARN_POTHOLE",   ("POTHOLE!", "POTHOLE!") },          // TODO: native review
                { "WARN_ROCK",      ("ROCKS!", "ROCKS!") },              // TODO: native review
                { "WARN_BUMP",      ("SPEED BUMP!", "SPEED BUMP!") },    // TODO: native review

                // Screens (Req §12.2, §13)
                { "GAME_OVER",      ("GAME OVER", "GAME OVER") },
                { "FINISH_TITLE",   ("JOURNEY COMPLETE", "MWISHO WA SAFARI") },
                { "CHECKPOINT_TITLE", ("CHARGE STATION", "CHARGE STATION") },
                { "NEW_RECORD",     ("NEW RECORD!", "REKODI MPYA!") },
                { "NEXT_PLAYER",    ("NEXT PLAYER", "NEXT PLAYER") },
                { "GROUP_TOTAL",    ("CLASS TOTAL", "CLASS TOTAL") },    // TODO: native review
                { "RANK_ANNOUNCE",  ("JULLIE STAAN OP PLEK {0}!", "JULLIE STAAN OP PLEK {0}!") }
            };

        public static string Get(string key)
        {
            if (Table.TryGetValue(key, out (string en, string sw) entry))
                return Current == Language.Swahili ? entry.sw : entry.en;
            return key;
        }

        public static void Toggle()
            => SetLanguage(Current == Language.English ? Language.Swahili : Language.English);

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
            Current = Language.English;
            LanguageChanged = null;
        }
    }
}
