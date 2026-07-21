using System.Collections.Generic;

namespace KenyaScooter.Settings
{
    /// <summary>
    /// A one-tap PROFILE: a named bundle of setting values that restructures the whole game for a scenario
    /// (a young/calm group, an older/challenging group, the authentic Kenya look, the Dutch comparison, a
    /// demo for an open day...). This is the "structure the full game to their whim, without reading every
    /// row" surface — a facilitator picks a profile and is ready, then fine-tunes individual rows if they want.
    ///
    /// Like settings and actions, profiles are pure DATA in <see cref="SettingsCatalog"/> and rendered by the
    /// menu, so adding one is a single catalog entry, never new UI code.
    ///
    /// A profile is ADDITIVE: applying it writes only the keys it lists (through <see cref="GameSettings"/>,
    /// so it persists and applies live), and leaves everything else untouched. That is deliberate — it lets a
    /// facilitator stack profiles ("Jonge kinderen" + "Kenia-modus") and keep their audio/accessibility tweaks.
    /// The one exception is the profile created with <see cref="ResetToDefault"/>, which clears every override.
    /// </summary>
    public sealed class SettingsPreset
    {
        /// <summary>Stable id (used for the card name). Profiles are applied, not stored, so this is cosmetic.</summary>
        public string Key { get; }
        /// <summary>Plain-language name shown big on the card, e.g. "Jonge kinderen — rustig".</summary>
        public string Label { get; }
        /// <summary>One-line "who is this for / what does it feel like" under the name.</summary>
        public string Description { get; }
        /// <summary>A short plain-language summary of WHAT it changes, shown as a chip line, e.g. "Rustig · weinig verkeer · vergevingsgezind".</summary>
        public string Summary { get; }
        /// <summary>When true this profile FIRST clears every override, then writes <see cref="Values"/> on the
        /// clean slate — so a "Standaard" profile can both reset everything AND pin its few signature values
        /// (e.g. right-side driving) regardless of what the device captured as its factory baseline.</summary>
        public bool ResetToDefault { get; }
        /// <summary>The setting-key → value pairs this profile writes. Keys not in the catalog are ignored.</summary>
        public IReadOnlyDictionary<string, float> Values { get; }
        /// <summary>True for the two MODE presets (Standaard, Eindeloze modus). The Profielen page renders these
        /// in a separate "MODUS" section from the non-primary DIFFICULTY presets (Rustig/Normaal/Uitdagend).</summary>
        public bool Primary { get; }

        public SettingsPreset(string key, string label, string description, string summary,
                              Dictionary<string, float> values, bool resetToDefault = false, bool primary = false)
        {
            Key = key;
            Label = label;
            Description = description;
            Summary = summary;
            Values = values ?? new Dictionary<string, float>();
            ResetToDefault = resetToDefault;
            Primary = primary;
        }

        /// <summary>Applies this profile: the reset profile clears every override first, then any profile writes
        /// its listed values through the override store (persisted + applied to the live game immediately).
        /// Unknown keys are skipped.</summary>
        public void Apply()
        {
            if (ResetToDefault)
                GameSettings.ResetAll();
            foreach (var pair in Values)
            {
                var def = SettingsCatalog.ById(pair.Key);
                if (def != null)
                    GameSettings.Set(def, pair.Value);
            }
        }
    }
}
