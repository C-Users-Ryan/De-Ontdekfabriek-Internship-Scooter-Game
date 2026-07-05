using NUnit.Framework;
using UnityEngine;
using KenyaScooter.Settings;

namespace KenyaScooter.SettingsTests
{
    /// <summary>
    /// EditMode tests for <see cref="FacilitatorLock"/> — the access code that keeps students out of the menu.
    /// These verify the PIN validation, change, reset and enable flags. They run against the project's real
    /// PlayerPrefs, so SetUp/TearDown back up and restore the two lock keys: a developer's own code is never
    /// clobbered by running the tests.
    /// </summary>
    public sealed class FacilitatorLockTests
    {
        // The keys FacilitatorLock owns (documented in that class); duplicated here only to save/restore them.
        private const string EnabledKey = "ksg.lock.on";
        private const string PinKey = "ksg.lock.pin";

        private bool _hadEnabled, _hadPin;
        private int _enabled;
        private string _pin;

        [SetUp]
        public void Backup()
        {
            _hadEnabled = PlayerPrefs.HasKey(EnabledKey);
            _enabled = PlayerPrefs.GetInt(EnabledKey, 1);
            _hadPin = PlayerPrefs.HasKey(PinKey);
            _pin = PlayerPrefs.GetString(PinKey, "");
        }

        [TearDown]
        public void Restore()
        {
            if (_hadEnabled) PlayerPrefs.SetInt(EnabledKey, _enabled); else PlayerPrefs.DeleteKey(EnabledKey);
            if (_hadPin) PlayerPrefs.SetString(PinKey, _pin); else PlayerPrefs.DeleteKey(PinKey);
            PlayerPrefs.Save();
        }

        [Test]
        public void IsValidPin_AcceptsFourDigits_RejectsTheRest()
        {
            Assert.IsTrue(FacilitatorLock.IsValidPin("0000"));
            Assert.IsTrue(FacilitatorLock.IsValidPin("3683"));
            Assert.IsFalse(FacilitatorLock.IsValidPin("123"), "too short");
            Assert.IsFalse(FacilitatorLock.IsValidPin("12345"), "too long");
            Assert.IsFalse(FacilitatorLock.IsValidPin("12a4"), "non-digit");
            Assert.IsFalse(FacilitatorLock.IsValidPin(""), "empty");
            Assert.IsFalse(FacilitatorLock.IsValidPin(null), "null");
        }

        [Test]
        public void Default_VerifiesAgainstDefaultPin()
        {
            FacilitatorLock.ResetToDefault();
            Assert.AreEqual(FacilitatorLock.DefaultPin, FacilitatorLock.CurrentPin);
            Assert.IsTrue(FacilitatorLock.Verify(FacilitatorLock.DefaultPin));
            Assert.IsFalse(FacilitatorLock.Verify("0000"));
        }

        [Test]
        public void SetPin_StoresAValidCode_AndVerifies()
        {
            Assert.IsTrue(FacilitatorLock.SetPin("2468"));
            Assert.AreEqual("2468", FacilitatorLock.CurrentPin);
            Assert.IsTrue(FacilitatorLock.Verify("2468"));
            Assert.IsFalse(FacilitatorLock.Verify(FacilitatorLock.DefaultPin));
        }

        [Test]
        public void SetPin_RejectsInvalid_AndLeavesTheOldCode()
        {
            FacilitatorLock.SetPin("2468");
            Assert.IsFalse(FacilitatorLock.SetPin("12"), "invalid code is refused");
            Assert.AreEqual("2468", FacilitatorLock.CurrentPin, "a half-typed code can never lock staff out");
        }

        [Test]
        public void ResetToDefault_RestoresTheDefaultCode()
        {
            FacilitatorLock.SetPin("2468");
            FacilitatorLock.ResetToDefault();
            Assert.AreEqual(FacilitatorLock.DefaultPin, FacilitatorLock.CurrentPin);
        }

        [Test]
        public void Enabled_RoundTrips()
        {
            FacilitatorLock.Enabled = false;
            Assert.IsFalse(FacilitatorLock.Enabled);
            FacilitatorLock.Enabled = true;
            Assert.IsTrue(FacilitatorLock.Enabled);
        }
    }
}
