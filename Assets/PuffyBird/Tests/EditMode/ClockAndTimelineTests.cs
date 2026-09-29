using NUnit.Framework;
using PuffyBird.Core;

namespace PuffyBird.Tests
{
    public class ClockAndTimelineTests
    {
        GameConfig _cfg;

        [SetUp]
        public void SetUp() => _cfg = GameConfig.CreateDefault();

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        [TestCase(144)]
        public void ClockGives60StepsPerSecondAtAnyFrameRate(int hz)
        {
            var clock = new FixedStepClock(_cfg.Step, _cfg.MaxFrameDelta);
            int steps = 0;
            for (int i = 0; i < hz * 10; i++) steps += clock.Advance(1.0 / hz);
            Assert.That(steps, Is.InRange(599, 601));
        }

        [Test]
        public void ClockClampsLongFrames()
        {
            var clock = new FixedStepClock(_cfg.Step, _cfg.MaxFrameDelta);
            Assert.AreEqual(15, clock.Advance(10.0));
        }

        [Test]
        public void OverPanelCountsScoreAt30PerSecond()
        {
            Assert.AreEqual(0, OverScreenTimeline.ShownScore(0.5f, 20, _cfg));
            Assert.AreEqual(15, OverScreenTimeline.ShownScore(1.1f, 20, _cfg));
            Assert.AreEqual(20, OverScreenTimeline.ShownScore(5f, 20, _cfg));
            Assert.IsFalse(OverScreenTimeline.NewBadgeVisible(1.1f, 20, true, _cfg));
            Assert.IsTrue(OverScreenTimeline.NewBadgeVisible(1.4f, 20, true, _cfg));
            Assert.IsFalse(OverScreenTimeline.NewBadgeVisible(5f, 20, false, _cfg));
        }

        [Test]
        public void OverTitleAndPanelSlideIntoPlace()
        {
            Assert.AreEqual(60f, OverScreenTimeline.TitleY(0f));
            Assert.AreEqual(110f, OverScreenTimeline.TitleY(1f));
            Assert.IsFalse(OverScreenTimeline.PanelVisible(0.2f));
            Assert.AreEqual(180f, OverScreenTimeline.PanelY(2f, _cfg));
            Assert.IsFalse(OverScreenTimeline.ButtonsVisible(0.8f, _cfg));
            Assert.IsTrue(OverScreenTimeline.ButtonsVisible(0.81f, _cfg));
        }

        [Test]
        public void SoundsAreGeneratedWithinRange()
        {
            foreach (SoundId id in System.Enum.GetValues(typeof(SoundId)))
            {
                var samples = SfxRecipes.Generate(id, 44100);
                Assert.Greater(samples.Length, 44100 / 20, id.ToString());
                float peak = 0f;
                foreach (var s in samples)
                {
                    Assert.That(s, Is.InRange(-1f, 1f));
                    peak = System.Math.Max(peak, System.Math.Abs(s));
                }
                Assert.Greater(peak, 0.01f, id + " ne doit pas être silencieux");
            }
        }
    }
}
