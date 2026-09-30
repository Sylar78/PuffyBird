using NUnit.Framework;
using PuffyBird.Core;

namespace PuffyBird.Tests
{
    /// <summary>Extensions : tuyaux mobiles à partir de 15 points, étoiles de vitesse.</summary>
    public class ExtensionTests
    {
        GameConfig _cfg;

        [SetUp]
        public void SetUp() => _cfg = GameConfig.CreateDefault();

        GameSimulation NewPlayingSim(uint seed = 1)
        {
            var sim = new GameSimulation(_cfg, new MemoryScoreStorage(), seed, startOnTitle: false);
            sim.ForcePlaying();
            return sim;
        }

        /// <summary>Le bot joue <paramref name="seconds"/> secondes ; renvoie le nombre d'étoiles prises.</summary>
        int PlayWithBot(GameSimulation sim, float seconds, System.Action<GameSimulation> eachStep = null)
        {
            int stars = 0;
            int steps = (int)(seconds / _cfg.Step);
            for (int i = 0; i < steps && (sim.State == GameState.Ready || sim.State == GameState.Playing); i++)
            {
                if (AutoPilot.ShouldFlap(sim)) sim.Press();
                sim.Step();
                if ((sim.ConsumeEvents() & GameEvents.Star) != 0) stars++;
                eachStep?.Invoke(sim);
            }
            return stars;
        }

        [TestCase(1u)]
        [TestCase(2u)]
        [TestCase(3u)]
        public void PipesMoveOnlyFromTheSixteenthPair(uint seed)
        {
            var sim = new GameSimulation(_cfg, new MemoryScoreStorage(), seed, startOnTitle: false);
            sim.Press();
            PlayWithBot(sim, 40f, s =>
            {
                for (int i = 0; i < s.Pipes.Count; i++)
                {
                    ref var p = ref s.Pipes[i];
                    bool moving = p.Id >= _cfg.MovingPipesFromScore;
                    Assert.AreEqual(moving ? _cfg.PipeMoveAmplitude : 0f, p.MoveAmplitude, $"paire {p.Id}");
                    Assert.LessOrEqual(System.Math.Abs(p.Shift), _cfg.PipeMoveAmplitude + 1e-3f);
                }
            });
            Assert.GreaterOrEqual(sim.Score, 25);
        }

        [Test]
        public void MovingPairKeepsItsOpeningHeight()
        {
            var sim = NewPlayingSim();
            sim.Pipes.Spawn(_cfg.BirdCenterX - _cfg.PipeWidth * 0.5f, 150);
            ref var p = ref sim.Pipes.Last;
            p.MoveAmplitude = _cfg.PipeMoveAmplitude;
            p.Shift = 25f;
            float x = p.X;
            float cx = _cfg.BirdCenterX;
            float r = _cfg.BirdRadius;
            // L'ouverture est décalée de 25 px, les deux tuyaux ensemble : elle mesure toujours 100 px.
            Assert.IsFalse(Collision.HitsPipe(cx, 150f + 25f + r + 1f, r, p.OpeningTop, x, _cfg));
            Assert.IsFalse(Collision.HitsPipe(cx, 150f + 25f + _cfg.PipeGap - r - 1f, r, p.OpeningTop, x, _cfg));
            Assert.IsTrue(Collision.HitsPipe(cx, 150f + r + 1f, r, p.OpeningTop, x, _cfg));
            Assert.IsTrue(Collision.HitsPipe(cx, 150f + 25f + _cfg.PipeGap + 1f, r, p.OpeningTop, x, _cfg));
        }

        [Test]
        public void StarGivesThirtyPercentBoostThenWearsOff()
        {
            var sim = NewPlayingSim();
            sim.Stars.Spawn(_cfg.BirdCenterX + 2f, sim.Bird.CenterY(_cfg));
            sim.Step();
            Assert.IsTrue((sim.ConsumeEvents() & GameEvents.Star) != 0);
            Assert.AreEqual(0, sim.Stars.ActiveCount, "l'étoile est consommée");

            for (int i = 0; i < (int)(_cfg.StarBoostRamp / _cfg.Step) + 2; i++) sim.Step();
            Assert.AreEqual(_cfg.StarBoostFactor, sim.SpeedFactor, 1e-5f);
            double before = sim.ScrollDistance;
            sim.Step();
            Assert.AreEqual(_cfg.ScrollSpeed * 1.3f * _cfg.Step, sim.ScrollDistance - before, 1e-4);

            for (int i = 0; i < (int)((_cfg.StarBoostDuration + _cfg.StarBoostRamp) / _cfg.Step) + 2; i++)
            {
                if (sim.Bird.Y > 200f) sim.Press();
                sim.Step();
            }
            Assert.AreEqual(1f, sim.SpeedFactor, 1e-5f);
            Assert.AreEqual(0f, sim.BoostTime);
        }

        [Test]
        public void BoostKeepsPipeSpacingExact()
        {
            var sim = new GameSimulation(_cfg, new MemoryScoreStorage(), 7, startOnTitle: false);
            sim.Press();
            sim.ForceBoost(1000f);
            PlayWithBot(sim, 20f, s =>
            {
                for (int i = 1; i < s.Pipes.Count; i++)
                    Assert.AreEqual(_cfg.PipeSpacing, s.Pipes[i].X - s.Pipes[i - 1].X, 1e-3f);
            });
            Assert.AreEqual(GameState.Playing, sim.State);
        }

        [Test]
        public void StarsNeverOverlapAPipe()
        {
            for (uint seed = 1; seed <= 5; seed++)
            {
                var sim = new GameSimulation(_cfg, new MemoryScoreStorage(), seed, startOnTitle: false);
                sim.Press();
                PlayWithBot(sim, 30f, s =>
                {
                    for (int k = 0; k < s.Stars.Capacity; k++)
                    {
                        ref var star = ref s.Stars[k];
                        if (!star.Active) continue;
                        for (int i = 0; i < s.Pipes.Count; i++)
                        {
                            ref var p = ref s.Pipes[i];
                            Assert.IsFalse(star.X + _cfg.StarRadius > p.X && star.X - _cfg.StarRadius < p.X + _cfg.PipeWidth,
                                "une étoile se trouve entre deux paires, jamais dans un tuyau");
                        }
                    }
                });
            }
        }

        /// <summary>
        /// Non-régression de l'équité (§16.2) avec les extensions : le bot franchit les tuyaux
        /// mobiles et survit aux accélérations sur plusieurs graines.
        /// </summary>
        [TestCase(1u)]
        [TestCase(2u)]
        [TestCase(3u)]
        [TestCase(4u)]
        [TestCase(5u)]
        [TestCase(6u)]
        public void AutoPilotSurvivesMovingPipesAndStars(uint seed)
        {
            var sim = new GameSimulation(_cfg, new MemoryScoreStorage(), seed, startOnTitle: false);
            sim.Press();
            int stars = PlayWithBot(sim, 75f);
            Assert.AreEqual(GameState.Playing, sim.State, $"le bot meurt à {sim.Score} points");
            Assert.GreaterOrEqual(sim.Score, 55);
            TestContext.WriteLine($"graine {seed} : {sim.Score} points, {stars} étoiles");
        }

        [Test]
        public void StarsShowUpInARun()
        {
            int total = 0;
            for (uint seed = 1; seed <= 5; seed++)
            {
                var sim = new GameSimulation(_cfg, new MemoryScoreStorage(), seed, startOnTitle: false);
                sim.Press();
                int spawned = 0;
                int lastId = -1;
                PlayWithBot(sim, 40f, s =>
                {
                    for (int k = 0; k < s.Stars.Capacity; k++)
                    {
                        if (s.Stars[k].Active && s.Stars[k].Id > lastId)
                        {
                            lastId = s.Stars[k].Id;
                            spawned++;
                        }
                    }
                });
                total += spawned;
            }
            Assert.Greater(total, 20, "environ 30 % des paires ont une étoile");
        }
    }
}
