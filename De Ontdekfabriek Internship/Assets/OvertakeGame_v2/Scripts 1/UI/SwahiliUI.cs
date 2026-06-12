using UnityEngine;
using TMPro;

namespace OvertakeGame
{
    /// <summary>
    /// Holds all Swahili / English UI strings in one place.
    /// Attach to a persistent GameObject and call SwahiliUI.I.Get(key)
    /// from any UI script to get the localised string.
    ///
    /// To toggle language at runtime, call SwahiliUI.I.SetLanguage(false/true).
    ///
    /// FULL TRANSLATION TABLE:
    ///   Key              English               Swahili
    ///   "start"          START                 ANZA
    ///   "gameover"       GAME OVER             MCHEZO UMEKWISHA
    ///   "score"          SCORE                 POINTI
    ///   "distance"       DISTANCE              UMBALI
    ///   "lives"          LIVES                 MAISHA
    ///   "playagain"      PLAY AGAIN            CHEZA TENA
    ///   "best"           BEST                  BORA
    ///   "pause"          PAUSE                 SIMAMA
    ///   "newrecord"      NEW RECORD!           REKODI MPYA!
    ///   "morning"        MORNING               ASUBUHI
    ///   "midday"         MIDDAY                MCHANA
    ///   "sunset"         SUNSET                JIONI
    ///   "tagline"        Ride the boda boda — dodge the jam!
    ///                                          Panda boda boda — epuka msongamano!
    /// </summary>
    public class SwahiliUI : MonoBehaviour
    {
        public static SwahiliUI I { get; private set; }

        [Tooltip("When true, all UI text is in Swahili. When false, English.")]
        public bool useSwahili = false;

        void Awake()
        {
            if (I != null && I != this) { Destroy(this); return; }
            I = this;
            DontDestroyOnLoad(gameObject);
        }

        private static readonly System.Collections.Generic.Dictionary<string, (string en, string sw)> _strings
            = new System.Collections.Generic.Dictionary<string, (string, string)>
        {
            { "start",      ("START",           "ANZA") },
            { "gameover",   ("GAME OVER",        "MCHEZO UMEKWISHA") },
            { "score",      ("SCORE",            "POINTI") },
            { "distance",   ("DISTANCE",         "UMBALI") },
            { "lives",      ("LIVES",            "MAISHA") },
            { "playagain",  ("PLAY AGAIN",       "CHEZA TENA") },
            { "best",       ("BEST",             "BORA") },
            { "pause",      ("PAUSE",            "SIMAMA") },
            { "newrecord",  ("NEW RECORD!",      "REKODI MPYA!") },
            { "morning",    ("MORNING",          "ASUBUHI") },
            { "midday",     ("MIDDAY",           "MCHANA") },
            { "sunset",     ("SUNSET",           "JIONI") },
            { "tagline",    ("Ride the boda boda — dodge the jam!",
                             "Panda boda boda — epuka msongamano!") },
            { "welcome",    ("WELCOME",          "KARIBU") },
            { "danger",     ("DANGER!",          "HATARI!") },
            { "slowdown",   ("SLOW DOWN",        "POLE POLE") },
        };

        /// <summary>Returns the string for the given key in the active language.</summary>
        public string Get(string key)
        {
            if (_strings.TryGetValue(key.ToLower(), out var pair))
                return useSwahili ? pair.sw : pair.en;
            Debug.LogWarning($"[SwahiliUI] Unknown key: {key}");
            return key.ToUpper();
        }

        /// <summary>Switch language. Pass true for Swahili, false for English.</summary>
        public void SetLanguage(bool swahili)
        {
            useSwahili = swahili;
        }

        /// <summary>Toggle between English and Swahili.</summary>
        public void Toggle()
        {
            useSwahili = !useSwahili;
        }
    }
}