using NUnit.Framework;
using KenyaScooter.Settings;

namespace KenyaScooter.SettingsTests
{
    /// <summary>
    /// EditMode tests for <see cref="SettingDefinition.Coerce"/> — the single guard that keeps every facilitator
    /// value child-safe: it clamps to the setting's range and snaps to its step, so a stored override or a profile
    /// value can never push a setting out of bounds. Pure maths, no scene. A definition is built with no-op
    /// get/set, since Coerce only uses Min/Max/Step/Widget.
    /// </summary>
    public sealed class SettingCoerceTests
    {
        private const float Tol = 1e-4f;

        private static SettingDefinition Slider(float min, float max, float step)
            => new SettingDefinition("test.slider", "L", "D", SettingCategory.Difficulty, SettingWidget.Slider,
                                     min, max, step, () => 0f, _ => { });

        private static SettingDefinition Toggle()
            => new SettingDefinition("test.toggle", "L", "D", SettingCategory.Difficulty, SettingWidget.Toggle,
                                     0f, 1f, 1f, () => 0f, _ => { });

        [Test]
        public void Clamp_AboveMax_ReturnsMax()
            => Assert.That(Slider(6f, 16f, 0.5f).Coerce(20f), Is.EqualTo(16f).Within(Tol));

        [Test]
        public void Clamp_BelowMin_ReturnsMin()
            => Assert.That(Slider(6f, 16f, 0.5f).Coerce(2f), Is.EqualTo(6f).Within(Tol));

        [Test]
        public void Snap_RoundsToNearestStep()
        {
            var s = Slider(6f, 16f, 0.5f);
            Assert.That(s.Coerce(7.3f), Is.EqualTo(7.5f).Within(Tol), "7.3 snaps up to 7.5");
            Assert.That(s.Coerce(7.2f), Is.EqualTo(7.0f).Within(Tol), "7.2 snaps down to 7.0");
        }

        [Test]
        public void Stepper_WholeNumbers_Round()
            => Assert.That(Slider(0f, 12f, 1f).Coerce(2.6f), Is.EqualTo(3f).Within(Tol));

        [Test]
        public void Toggle_IsAlwaysZeroOrOne()
        {
            var t = Toggle();
            Assert.That(t.Coerce(0.7f), Is.EqualTo(1f).Within(Tol));
            Assert.That(t.Coerce(0.49f), Is.EqualTo(0f).Within(Tol));
            Assert.That(t.Coerce(5f), Is.EqualTo(1f).Within(Tol), "anything at/above 0.5 is on");
            Assert.That(t.Coerce(-3f), Is.EqualTo(0f).Within(Tol));
        }

        [Test]
        public void ContinuousSlider_NoStep_DoesNotSnap()
            => Assert.That(Slider(0f, 1f, 0f).Coerce(0.333f), Is.EqualTo(0.333f).Within(Tol));
    }
}
