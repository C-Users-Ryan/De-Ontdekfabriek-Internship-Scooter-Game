using NUnit.Framework;
using KenyaScooter.Roads;

namespace KenyaScooter.RoadsTests
{
    /// <summary>
    /// EditMode tests for the shared dirt-road signal (2026-07-05): the blend eases toward the published
    /// surface (never snaps), grabs faster onto dirt than it settles back onto tarmac, resets cleanly for a
    /// new group's turn, and DustBoost maps the blend onto a layer's emission multiplier.
    /// </summary>
    public sealed class RoadSurfaceFeelTests
    {
        private const float Dt = 1f / 60f;

        private int savedIntensity;

        [SetUp]
        public void SetUp()
        {
            RoadSurfaceFeel.Reset();
            // Pin the facilitator "Onverharde wegen" step to VOL so the boost assertions are deterministic
            // regardless of what this editor's PlayerPrefs happen to hold; restored in TearDown.
            savedIntensity = RoadSurfaceFeel.IntensityStep;
            RoadSurfaceFeel.IntensityStep = 2;
        }

        [TearDown]
        public void TearDown()
        {
            RoadSurfaceFeel.IntensityStep = savedIntensity;
            RoadSurfaceFeel.Reset();
        }

        private static void Ride(RoadSurfaceType surface, float seconds)
        {
            for (float t = 0f; t < seconds; t += Dt)
                RoadSurfaceFeel.Publish(surface, Dt);
        }

        [Test]
        public void Blend_EasesInsteadOfSnapping()
        {
            RoadSurfaceFeel.Publish(RoadSurfaceType.Dirt, Dt);
            Assert.That(RoadSurfaceFeel.Current, Is.EqualTo(RoadSurfaceType.Dirt), "raw surface publishes immediately");
            Assert.That(RoadSurfaceFeel.DirtBlend01, Is.GreaterThan(0f).And.LessThan(0.2f),
                "one frame onto dirt moves the blend, but nowhere near a snap to 1");
        }

        [Test]
        public void Blend_ReachesFullDirt_AndReturnsToTarmac()
        {
            Ride(RoadSurfaceType.Dirt, 2f);
            Assert.That(RoadSurfaceFeel.DirtBlend01, Is.EqualTo(1f).Within(1e-4f), "settled on dirt");

            Ride(RoadSurfaceType.Paved, 2f);
            Assert.That(RoadSurfaceFeel.DirtBlend01, Is.EqualTo(0f).Within(1e-4f), "settled back on tarmac");
        }

        [Test]
        public void Blend_GrabsInFasterThanItSettlesOut()
        {
            Ride(RoadSurfaceType.Dirt, 0.3f);
            float grabbed = RoadSurfaceFeel.DirtBlend01;

            RoadSurfaceFeel.Reset();
            Ride(RoadSurfaceType.Dirt, 5f); // fully on dirt
            Ride(RoadSurfaceType.Paved, 0.3f);
            float settled = 1f - RoadSurfaceFeel.DirtBlend01;

            Assert.That(grabbed, Is.GreaterThan(settled),
                "the same window moves the blend further onto dirt than off it (grab fast, settle gently)");
        }

        [Test]
        public void Reset_ClearsForTheNextGroup()
        {
            Ride(RoadSurfaceType.Dirt, 5f);
            RoadSurfaceFeel.Reset();
            Assert.That(RoadSurfaceFeel.DirtBlend01, Is.Zero);
            Assert.That(RoadSurfaceFeel.Current, Is.EqualTo(RoadSurfaceType.Paved));
        }

        [Test]
        public void DustBoost_IsOneOnTarmac_AndTheMultiplierOnDirt()
        {
            Assert.That(RoadSurfaceFeel.DustBoost(2.5f), Is.EqualTo(1f).Within(1e-5f), "tarmac: no boost");

            Ride(RoadSurfaceType.Dirt, 5f);
            Assert.That(RoadSurfaceFeel.DustBoost(2.5f), Is.EqualTo(2.5f).Within(1e-4f), "full dirt: the full multiplier");
        }

        [Test]
        public void IntensitySetting_ScalesTheBoost_AndUitDisablesIt()
        {
            Ride(RoadSurfaceType.Dirt, 5f); // fully on dirt

            RoadSurfaceFeel.IntensityStep = 1; // SUBTIEL: halfway between tarmac and the full multiplier
            Assert.That(RoadSurfaceFeel.DustBoost(2.5f), Is.EqualTo(1.75f).Within(1e-4f));

            RoadSurfaceFeel.IntensityStep = 0; // UIT: dirt rides like asphalt
            Assert.That(RoadSurfaceFeel.DustBoost(2.5f), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(RoadSurfaceFeel.IntensityScale, Is.Zero, "the rumble reads this same scale, so UIT silences it too");
        }

        [Test]
        public void IntensityStep_ClampsAndPersistsItsRange()
        {
            RoadSurfaceFeel.IntensityStep = 7;
            Assert.That(RoadSurfaceFeel.IntensityStep, Is.EqualTo(2), "clamped to VOL");
            RoadSurfaceFeel.IntensityStep = -3;
            Assert.That(RoadSurfaceFeel.IntensityStep, Is.EqualTo(0), "clamped to UIT");
        }
    }
}
