using NUnit.Framework;
using PuffyBird.Core;

namespace PuffyBird.Tests
{
    /// <summary>Tests §23.2 et critères A6, A9 sur les tuyaux et les collisions.</summary>
    public class PipeAndCollisionTests
    {
        GameConfig _cfg;

        [SetUp]
        public void SetUp() => _cfg = GameConfig.CreateDefault();

        [Test]
        public void GapTopStaysWithinBounds()
        {
            var rng = new Rng(42);
            var field = new PipeField(4);
            bool sawMin = false, sawMax = false;
            for (int i = 0; i < 10000; i++)
            {
                field.SpawnRandom(0f, rng, _cfg);
                int gapTop = field.Last.GapTop;
                Assert.That(gapTop, Is.InRange(80, 220));
                sawMin |= gapTop == 80;
                sawMax |= gapTop == 220;
            }
            Assert.IsTrue(sawMin && sawMax, "les deux bornes doivent être atteignables");
        }

        [Test]
        public void SpacingIsExactlyConstant()
        {
            var rng = new Rng(7);
            var field = new PipeField(4);
            field.SpawnRandom(_cfg.FirstPipeX, rng, _cfg);
            int steps = (int)(60f / _cfg.Step);
            for (int s = 0; s < steps; s++)
            {
                field.Advance(_cfg.Step, rng, _cfg);
                for (int i = 1; i < field.Count; i++)
                    Assert.That(field[i].X - field[i - 1].X, Is.EqualTo(150f).Within(0.001f));
                Assert.That(field.Count, Is.LessThanOrEqualTo(3));
            }
        }

        [Test]
        public void WideViewSpawnsAndRemovesPipesOffScreen()
        {
            float margin = _cfg.MaxViewMargin;
            var rng = new Rng(11);
            var field = new PipeField(4);
            field.SpawnRandom(_cfg.FirstPipeX, rng, _cfg);
            int lastId = field.Last.Id;
            int steps = (int)(60f / _cfg.Step);
            for (int s = 0; s < steps; s++)
            {
                float firstRight = field[0].X + _cfg.PipeWidth;
                int firstId = field[0].Id;
                field.Advance(_cfg.Step, rng, _cfg, margin);

                if (field[0].Id != firstId)
                    Assert.That(firstRight - _cfg.ScrollSpeed * _cfg.Step, Is.LessThan(-margin), "disparition dans le champ (ou pool plein)");
                if (field.Last.Id != lastId)
                {
                    Assert.That(field.Last.X, Is.GreaterThan(_cfg.Width + margin), "apparition dans le champ");
                    Assert.AreEqual(lastId + 1, field.Last.Id);
                    lastId = field.Last.Id;
                }
                Assert.That(field.Count, Is.LessThanOrEqualTo(field.Capacity));
                for (int i = 1; i < field.Count; i++)
                    Assert.That(field[i].X - field[i - 1].X, Is.EqualTo(150f).Within(0.001f));
            }
        }

        [Test]
        public void TopPipeIsInfinite()
        {
            float cx = _cfg.BirdCenterX;
            float cy = -24f + _cfg.BirdHeight * 0.5f;
            Assert.IsTrue(Collision.HitsPipe(cx, cy, _cfg.BirdRadius, 150, 57f, _cfg));
        }

        [Test]
        public void BirdInsideGapDoesNotCollide()
        {
            float cx = _cfg.BirdCenterX;
            Assert.IsFalse(Collision.HitsPipe(cx, 200f, _cfg.BirdRadius, 150, 57f, _cfg));
        }

        [Test]
        public void BottomPipeCollides()
        {
            float cx = _cfg.BirdCenterX;
            Assert.IsTrue(Collision.HitsPipe(cx, 255f, _cfg.BirdRadius, 150, 57f, _cfg));
        }

        [Test]
        public void CircleRectTouchesOnlyWithinRadius()
        {
            Assert.IsTrue(Collision.CircleRect(0f, 0f, 11f, 10f, -5f, 10f, 10f));
            Assert.IsFalse(Collision.CircleRect(0f, 0f, 11f, 11f, -5f, 10f, 10f));
        }
    }
}
