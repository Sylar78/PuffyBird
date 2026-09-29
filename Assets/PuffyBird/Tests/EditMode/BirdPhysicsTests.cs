using NUnit.Framework;
using PuffyBird.Core;

namespace PuffyBird.Tests
{
    /// <summary>Tests §23.2 et critères A3 à A5 sur la physique de l'oiseau.</summary>
    public class BirdPhysicsTests
    {
        GameConfig _cfg;

        [SetUp]
        public void SetUp() => _cfg = GameConfig.CreateDefault();

        [Test]
        public void FlapReplacesVelocity()
        {
            var bird = new Bird();
            bird.Reset(_cfg.BirdStartY);
            bird.Vy = 250f;
            bird.Flap(_cfg);
            Assert.AreEqual(-270f, bird.Vy);
            Assert.AreEqual(45f, bird.Rot);
        }

        [Test]
        public void FallSpeedIsCappedAtTerminalVelocity()
        {
            var bird = new Bird();
            bird.Reset(-10000f);
            for (int i = 0; i < 120; i++) bird.Integrate(_cfg.Step, _cfg.RotSpeed, _cfg);
            Assert.AreEqual(300f, bird.Vy);
        }

        [Test]
        public void JumpHeightIsAbout40Pixels()
        {
            var bird = new Bird();
            bird.Reset(200f);
            bird.Flap(_cfg);
            float y0 = bird.Y;
            while (bird.Vy < 0f) bird.Integrate(_cfg.Step, _cfg.RotSpeed, _cfg);
            Assert.That(y0 - bird.Y, Is.EqualTo(40.5f).Within(3f));
        }

        [Test]
        public void CeilingIsSoftAndNotDeadly()
        {
            var bird = new Bird();
            bird.Reset(-20f);
            bird.Vy = -270f;
            for (int i = 0; i < 5; i++) bird.Integrate(_cfg.Step, _cfg.RotSpeed, _cfg);
            Assert.AreEqual(-_cfg.BirdHeight, bird.Y);
        }

        [Test]
        public void DisplayedRotationIsCappedAt20Degrees()
        {
            var bird = new Bird();
            bird.Reset(200f);
            bird.Flap(_cfg);
            Assert.AreEqual(20f, bird.DisplayRotation(_cfg));
            for (int i = 0; i < 600; i++) bird.Integrate(_cfg.Step, _cfg.RotSpeed, _cfg);
            Assert.AreEqual(-90f, bird.DisplayRotation(_cfg));
        }

        [Test]
        public void WingsFollowUpMidDownMidSequence()
        {
            var bird = new Bird();
            bird.Reset(200f);
            var frames = new int[4];
            for (int i = 0; i < 4; i++)
            {
                frames[i] = bird.WingFrame(_cfg);
                for (int s = 0; s < 5; s++) bird.AnimateWings(_cfg.Step, _cfg);
            }
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 1 }, frames);
        }
    }
}
