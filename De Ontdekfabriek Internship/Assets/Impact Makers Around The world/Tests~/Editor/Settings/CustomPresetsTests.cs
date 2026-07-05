using NUnit.Framework;
using KenyaScooter.Settings;

namespace KenyaScooter.SettingsTests
{
    /// <summary>
    /// EditMode tests for <see cref="CustomPresets"/> — the facilitator's own save-slots. These verify the
    /// empty/filled lifecycle and the empty-slot guard without disturbing real data: the test only ever uses a slot
    /// that is already EMPTY, and returns it to empty, so a developer's saved profiles are never touched. It also
    /// deliberately does not call <see cref="CustomPresets.Apply(int)"/> on a filled slot (that would write live
    /// game overrides); the apply path is covered by the integrity test and manual play.
    /// </summary>
    public sealed class CustomPresetsTests
    {
        private int FindEmptySlot()
        {
            for (int s = 1; s <= CustomPresets.SlotCount; s++)
                if (!CustomPresets.IsFilled(s)) return s;
            return -1;
        }

        [Test]
        public void Apply_OnEmptySlot_ReturnsFalse()
        {
            int slot = FindEmptySlot();
            if (slot < 0) Assert.Ignore("all save-slots are in use on this machine; skipping to avoid touching real data");
            Assert.IsFalse(CustomPresets.Apply(slot), "applying an empty slot must be a no-op");
        }

        [Test]
        public void Save_ThenClear_TogglesFilledState()
        {
            int slot = FindEmptySlot();
            if (slot < 0) Assert.Ignore("all save-slots are in use on this machine; skipping to avoid touching real data");

            Assert.IsFalse(CustomPresets.IsFilled(slot));
            try
            {
                CustomPresets.Save(slot);
                Assert.IsTrue(CustomPresets.IsFilled(slot), "a saved slot reports filled");
            }
            finally
            {
                CustomPresets.Clear(slot); // always return the slot to its original empty state
            }
            Assert.IsFalse(CustomPresets.IsFilled(slot), "a cleared slot reports empty again");
        }

        [Test]
        public void Label_IsStableAndNonEmpty()
        {
            for (int s = 1; s <= CustomPresets.SlotCount; s++)
                Assert.IsFalse(string.IsNullOrEmpty(CustomPresets.Label(s)));
        }
    }
}
