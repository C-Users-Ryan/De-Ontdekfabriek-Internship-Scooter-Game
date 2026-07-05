using System;

namespace KenyaScooter.Settings
{
    /// <summary>The widget the facilitator UI draws for a setting.</summary>
    public enum SettingWidget { Slider, Toggle, Stepper }

    /// <summary>The plain-language groups the settings are bucketed into on the menu.</summary>
    public enum SettingCategory
    {
        Profiles,     // Profielen (one-tap whole-game presets — shown first, the easy way in)
        Difficulty,   // Moeilijkheid
        TrafficWorld, // Verkeer en weg (what is on the road, and which side)
        Environment,  // Omgeving (time of day, atmosphere)
        SpeedFeel,    // Snelheid en gevoel
        SafetyNet,    // Vangnet
        Controls,     // Besturing
        Audio,        // Geluid
        Scoring,      // Punten
        Session,      // Speelduur
        Management,   // Beheer (admin actions, e.g. clear the leaderboard)
        Overview      // Overzicht (a read-only summary of what differs from default — no settings of its own)
    }

    /// <summary>
    /// One tunable, described as DATA rather than as hand-built UI. The settings menu and the
    /// override store are both driven entirely off a list of these, so adding a setting is a single
    /// entry in <see cref="SettingsCatalog"/> (a small data change), never new UI code. This is the
    /// extension point a future intern uses to expose more knobs (see the note in SettingsCatalog).
    ///
    /// A definition does not know WHICH ScriptableObject it edits. Instead it carries a getter/setter
    /// pair that reads and writes the live config field, so one definition can drive any field on any
    /// config. The override store calls the setter to push a saved value back onto the live SO at
    /// startup; the menu calls the getter to show the current value.
    /// </summary>
    public sealed class SettingDefinition
    {
        /// <summary>Stable id used as the PlayerPrefs sub-key. Never change it once shipped (it would orphan saved overrides).</summary>
        public string Key { get; }
        /// <summary>Plain-language label shown on the row, e.g. "Aantal hindernissen".</summary>
        public string Label { get; }
        /// <summary>One-line description shown under the label.</summary>
        public string Description { get; }
        public SettingCategory Category { get; }
        public SettingWidget Widget { get; }

        public float Min { get; }
        public float Max { get; }
        /// <summary>Slider/stepper step. 0 = continuous slider.</summary>
        public float Step { get; }

        /// <summary>Reads the current value off the live config (as a float; toggles are 0/1).</summary>
        public Func<float> Get { get; }
        /// <summary>Writes a value onto the live config. Called on apply and when the facilitator edits.</summary>
        public Action<float> Set { get; }

        /// <summary>
        /// Optional pretty-printer for the value chip (e.g. "120 s", "AAN"/"UIT"). Null = plain number.
        /// Keeps the catalog declarative without baking formatting into the UI.
        /// </summary>
        public Func<float, string> Format { get; }

        public SettingDefinition(
            string key, string label, string description,
            SettingCategory category, SettingWidget widget,
            float min, float max, float step,
            Func<float> get, Action<float> set, Func<float, string> format = null)
        {
            Key = key;
            Label = label;
            Description = description;
            Category = category;
            Widget = widget;
            Min = min;
            Max = max;
            Step = step;
            Get = get;
            Set = set;
            Format = format;
        }

        public bool IsToggle => Widget == SettingWidget.Toggle;

        /// <summary>Clamp + snap a raw value to this setting's range and step.</summary>
        public float Coerce(float raw)
        {
            if (IsToggle)
                return raw >= 0.5f ? 1f : 0f;
            float v = UnityEngine.Mathf.Clamp(raw, Min, Max);
            if (Step > 0f)
            {
                v = Min + UnityEngine.Mathf.Round((v - Min) / Step) * Step;
                v = UnityEngine.Mathf.Clamp(v, Min, Max); // snapping can overshoot Max when the range isn't a whole multiple of Step
            }
            return v;
        }

        public string Display(float value)
            => Format != null ? Format(value) : (Step >= 1f ? UnityEngine.Mathf.RoundToInt(value).ToString() : value.ToString("0.0"));
    }
}
