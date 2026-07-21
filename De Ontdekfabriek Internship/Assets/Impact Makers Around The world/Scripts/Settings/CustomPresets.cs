using UnityEngine;

namespace KenyaScooter.Settings
{
    /// <summary>
    /// Facilitator-defined profiles: a few SAVE SLOTS that capture the whole current configuration so a host can
    /// store their own perfect setup for a group and re-apply it any day, on top of the built-in profiles. This is
    /// the "structure the game to YOUR whim and keep it" piece — the built-in <see cref="SettingsPreset"/> list is
    /// curated by us; these slots are the facilitator's own.
    ///
    /// Tablet-friendly by design: slots are NUMBERED (Mijn profiel 1/2/3), so saving never needs a hardware keyboard
    /// or on-screen text entry — the host just taps a slot. Like everything else here it is pure PlayerPrefs scalars
    /// (no JSON), consistent with <see cref="GameSettings"/>: a slot snapshots every catalog setting's CURRENT value,
    /// so applying it restores that exact full configuration (deterministic, not additive).
    ///
    /// Keys: "ksg.slot.{n}.on" (1 = filled) and "ksg.slot.{n}.{settingKey}" (one float per setting).
    /// </summary>
    public static class CustomPresets
    {
        public const int SlotCount = 3;
        public const int MaxLabelLength = 24;

        private static string FilledKey(int slot) => "ksg.slot." + slot + ".on";
        private static string ValueKey(int slot, string settingKey) => "ksg.slot." + slot + "." + settingKey;
        private static string NameKey(int slot) => "ksg.slot." + slot + ".name";

        /// <summary>Plain-language slot name shown on the card: the facilitator's own name if they set one
        /// (<see cref="SetLabel"/>), else the numbered default.</summary>
        public static string Label(int slot)
        {
            string custom = PlayerPrefs.GetString(NameKey(slot), "");
            return string.IsNullOrEmpty(custom) ? "Mijn profiel " + slot : custom;
        }

        /// <summary>Facilitator renames a slot. Trimmed and capped; empty/whitespace restores the numbered default.
        /// The name is label-only — it never touches the saved values, so renaming a filled slot is always safe.</summary>
        public static void SetLabel(int slot, string name)
        {
            name = name != null ? name.Trim() : "";
            if (name.Length > MaxLabelLength) name = name.Substring(0, MaxLabelLength);
            if (string.IsNullOrEmpty(name)) PlayerPrefs.DeleteKey(NameKey(slot));
            else PlayerPrefs.SetString(NameKey(slot), name);
            PlayerPrefs.Save();
        }

        public static bool IsFilled(int slot) => PlayerPrefs.GetInt(FilledKey(slot), 0) == 1;

        // A profile is about the GAME experience, not the kiosk's security. So slots deliberately skip the
        // Management category — saving and re-applying a profile must never quietly flip the access code or the
        // lock on/off. (Those live only in their own keys via FacilitatorLock.)
        private static bool IsProfileSetting(SettingDefinition def) => def.Category != SettingCategory.Management;

        /// <summary>Snapshots every game (non-Management) setting's current value into the slot.</summary>
        public static void Save(int slot)
        {
            foreach (var def in SettingsCatalog.All)
            {
                if (!IsProfileSetting(def)) continue;
                try { PlayerPrefs.SetFloat(ValueKey(slot, def.Key), GameSettings.CurrentValue(def)); }
                catch (System.Exception e) { Debug.LogException(e); }
            }
            PlayerPrefs.SetInt(FilledKey(slot), 1);
            PlayerPrefs.Save();
        }

        /// <summary>Applies a saved slot to the live game (and persists it through the override store). No-op if empty.</summary>
        public static bool Apply(int slot)
        {
            if (!IsFilled(slot)) return false;
            foreach (var def in SettingsCatalog.All)
            {
                if (!IsProfileSetting(def)) continue;
                string k = ValueKey(slot, def.Key);
                if (PlayerPrefs.HasKey(k))
                    GameSettings.Set(def, PlayerPrefs.GetFloat(k));
            }
            return true;
        }

        /// <summary>Empties a slot (the saved values, not the live game). The custom name goes with it — an
        /// emptied slot reading "Nog leeg" under someone's old label would look like it still held that setup.</summary>
        public static void Clear(int slot)
        {
            foreach (var def in SettingsCatalog.All)
                PlayerPrefs.DeleteKey(ValueKey(slot, def.Key));
            PlayerPrefs.DeleteKey(FilledKey(slot));
            PlayerPrefs.DeleteKey(NameKey(slot));
            PlayerPrefs.Save();
        }
    }
}
