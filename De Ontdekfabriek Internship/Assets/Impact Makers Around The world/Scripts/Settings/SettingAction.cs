using System;

namespace KenyaScooter.Settings
{
    /// <summary>
    /// A one-shot facilitator BUTTON in the settings menu (as opposed to a <see cref="SettingDefinition"/>,
    /// which is a persistent value). Some things a facilitator needs are actions, not stored settings, e.g.
    /// "recalibrate the steering now" or "clear the leaderboard for a new class". Like the settings, these are
    /// described as DATA in <see cref="SettingsCatalog"/> and rendered by the menu, so adding one is a single
    /// catalog entry, never new UI code.
    /// </summary>
    public sealed class SettingAction
    {
        /// <summary>Stable id (used for the row name). Actions are not persisted, so this is cosmetic.</summary>
        public string Key { get; }
        /// <summary>Plain-language label shown on the row, e.g. "Klassement wissen".</summary>
        public string Label { get; }
        /// <summary>One-line description shown under the label.</summary>
        public string Description { get; }
        public SettingCategory Category { get; }
        /// <summary>Short text on the button itself, e.g. "WISSEN".</summary>
        public string ButtonText { get; }
        /// <summary>If true, the button asks for a second tap to confirm before it runs (for destructive actions).</summary>
        public bool Destructive { get; }
        /// <summary>What the button does. Wrapped in try/catch by the menu so one bad action can't break the panel.</summary>
        public Action Invoke { get; }

        public SettingAction(string key, string label, string description, SettingCategory category,
                             string buttonText, bool destructive, Action invoke)
        {
            Key = key;
            Label = label;
            Description = description;
            Category = category;
            ButtonText = buttonText;
            Destructive = destructive;
            Invoke = invoke;
        }
    }
}
