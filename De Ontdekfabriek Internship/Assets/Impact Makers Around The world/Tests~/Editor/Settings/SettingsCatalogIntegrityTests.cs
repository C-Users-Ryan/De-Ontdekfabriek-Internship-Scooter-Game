using System.Collections.Generic;
using NUnit.Framework;
using KenyaScooter.Settings;

namespace KenyaScooter.SettingsTests
{
    /// <summary>
    /// EditMode tests that lock the INTEGRITY of the facilitator settings data: stable keys are unique, and every
    /// one-tap profile (built-in and the reset profile) refers to a real setting and stays inside that setting's
    /// allowed range. These are pure data checks — they build the catalog and the preset list and never touch a
    /// scene, a config asset or PlayerPrefs — so they run anywhere and fail loudly if a profile is ever given a
    /// typo'd key or an out-of-range value (e.g. a session length past its max), which the UI alone would hide.
    /// </summary>
    public sealed class SettingsCatalogIntegrityTests
    {
        [Test]
        public void SettingKeys_AreUnique()
        {
            var seen = new HashSet<string>();
            foreach (var def in SettingsCatalog.All)
                Assert.IsTrue(seen.Add(def.Key), $"duplicate setting key '{def.Key}' (keys are PlayerPrefs sub-keys and must be unique)");
        }

        [Test]
        public void ActionKeys_AreUnique()
        {
            var seen = new HashSet<string>();
            foreach (var a in SettingsCatalog.Actions)
                Assert.IsTrue(seen.Add(a.Key), $"duplicate action key '{a.Key}'");
        }

        [Test]
        public void ById_FindsEverySetting_AndNullForUnknown()
        {
            foreach (var def in SettingsCatalog.All)
                Assert.AreSame(def, SettingsCatalog.ById(def.Key), $"ById should round-trip '{def.Key}'");
            Assert.IsNull(SettingsCatalog.ById("does.not.exist"));
        }

        [Test]
        public void Presets_Exist_AndHaveKeysAndLabels()
        {
            Assert.Greater(SettingsCatalog.Presets.Count, 0, "there should be at least one profile");
            var seen = new HashSet<string>();
            foreach (var p in SettingsCatalog.Presets)
            {
                Assert.IsFalse(string.IsNullOrEmpty(p.Key), "every profile needs a key");
                Assert.IsFalse(string.IsNullOrEmpty(p.Label), $"profile '{p.Key}' needs a label");
                Assert.IsTrue(seen.Add(p.Key), $"duplicate profile key '{p.Key}'");
            }
        }

        [Test]
        public void EveryPresetValue_RefersToARealSetting_AndIsInRange()
        {
            foreach (var preset in SettingsCatalog.Presets)
            {
                // (2026-07-14: a ResetToDefault profile may now ALSO pin values — it clears every override and
                // THEN writes its list, which is how "Standaard" guarantees right-side driving + 79 km/u. So its
                // values go through the same range checks as everyone else's instead of being asserted empty.)
                foreach (var pair in preset.Values)
                {
                    var def = SettingsCatalog.ById(pair.Key);
                    Assert.IsNotNull(def, $"profile '{preset.Key}' refers to unknown setting '{pair.Key}'");

                    if (def.IsToggle)
                    {
                        Assert.IsTrue(pair.Value == 0f || pair.Value == 1f,
                            $"profile '{preset.Key}' sets toggle '{pair.Key}' to {pair.Value}; toggles must be 0 or 1");
                    }
                    else
                    {
                        Assert.GreaterOrEqual(pair.Value, def.Min,
                            $"profile '{preset.Key}' sets '{pair.Key}' to {pair.Value}, below its minimum {def.Min}");
                        Assert.LessOrEqual(pair.Value, def.Max,
                            $"profile '{preset.Key}' sets '{pair.Key}' to {pair.Value}, above its maximum {def.Max}");
                    }
                }
            }
        }

        [Test]
        public void EveryUsedCategory_HasALabelAndAPlainLanguageBlurb()
        {
            foreach (SettingCategory c in System.Enum.GetValues(typeof(SettingCategory)))
            {
                bool used = c == SettingCategory.Profiles || c == SettingCategory.Overview; // always-shown views
                if (!used) foreach (var _ in SettingsCatalog.InCategory(c)) { used = true; break; }
                if (!used) foreach (var _ in SettingsCatalog.ActionsInCategory(c)) { used = true; break; }
                if (!used) continue;

                Assert.IsFalse(string.IsNullOrEmpty(SettingsCatalog.CategoryLabel(c)), $"{c} needs a label");
                Assert.IsFalse(string.IsNullOrEmpty(SettingsCatalog.CategoryBlurb(c)), $"{c} needs a plain-language blurb");
            }
        }

        [Test]
        public void ProfilesCategory_IsTheFirstCategory_SoTheMenuOpensThere()
        {
            // The menu shows categories in enum order and lands on the first; Profielen must lead.
            var values = (SettingCategory[])System.Enum.GetValues(typeof(SettingCategory));
            Assert.AreEqual(SettingCategory.Profiles, values[0], "Profielen should be the first category");
        }
    }
}
