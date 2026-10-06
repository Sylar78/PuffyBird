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
        public void PipesMoveOnlyFromTheSixteenthPairAndNotAlways(uint seed)
        {
            var sim = new GameSimulation(_cfg, new MemoryScoreStorage(), seed, startOnTitle: false);
            sim.Press();
            int moving = 0, still = 0, lastId = -1;
            PlayWithBot(sim, 60f, s =>
            {
                for (int i = 0; i < s.Pipes.Count; i++)
                {
                    ref var p = ref s.Pipes[i];
                    if (p.Id < _cfg.MovingPipesFromScore || p.Kind != PipeKind.Pair) Assert.AreEqual(0f, p.MoveAmplitude, $"paire {p.Id}");
                    else Assert.That(p.MoveAmplitude == 0f || p.MoveAmplitude == _cfg.PipeMoveAmplitude, $"paire {p.Id}");
                    Assert.LessOrEqual(System.Math.Abs(p.Shift), _cfg.PipeMoveAmplitude + 1e-3f);
                }
                ref var last = ref s.Pipes.Last;
                if (last.Id > lastId && last.Id >= _cfg.MovingPipesFromScore && last.Kind == PipeKind.Pair)
                {
                    if (last.MoveAmplitude > 0f) moving++;
                    else still++;
                }
                lastId = last.Id;
            });
            Assert.GreaterOrEqual(sim.Score, 35);
            Assert.Greater(moving, 3, "des paires bougent");
            Assert.Greater(still, 3, "d'autres restent fixes");
        }

        [Test]
        public void SinglePipeLeavesTheGapAgainstTheScreenEdge()
        {
            float cx = _cfg.BirdCenterX;
            float r = _cfg.BirdRadius;
            float x = cx - _cfg.PipeWidth * 0.5f;
            // Tuyau du bas seul : ouverture de 100 px sous le haut de l'écran, rien au-dessus.
            Assert.IsFalse(Collision.HitsPipe(cx, -40f, r, 0f, x, _cfg, PipeKind.BottomOnly));
            Assert.IsFalse(Collision.HitsPipe(cx, _cfg.PipeGap - r - 1f, r, 0f, x, _cfg, PipeKind.BottomOnly));
            Assert.IsTrue(Collision.HitsPipe(cx, _cfg.PipeGap + 1f, r, 0f, x, _cfg, PipeKind.BottomOnly));
            Assert.IsTrue(Collision.HitsPipe(cx, 380f, r, 0f, x, _cfg, PipeKind.BottomOnly));
            // Tuyau du haut seul : ouverture de 100 px au-dessus du sol, le tuyau du haut reste infini.
            float top = _cfg.GroundY - _cfg.PipeGap;
            Assert.IsFalse(Collision.HitsPipe(cx, top + r + 1f, r, top, x, _cfg, PipeKind.TopOnly));
            Assert.IsFalse(Collision.HitsPipe(cx, _cfg.GroundY - r - 1f, r, top, x, _cfg, PipeKind.TopOnly));
            Assert.IsTrue(Collision.HitsPipe(cx, top - 1f, r, top, x, _cfg, PipeKind.TopOnly));
            Assert.IsTrue(Collision.HitsPipe(cx, -500f, r, top, x, _cfg, PipeKind.TopOnly));
        }

        /// <summary>Relève chaque paire une fois, à son apparition, sur plusieurs graines.</summary>
        System.Collections.Generic.List<PipePair> SpawnedPairs(uint seeds, float seconds)
        {
            var pairs = new System.Collections.Generic.List<PipePair>();
            for (uint seed = 1; seed <= seeds; seed++)
            {
                var sim = new GameSimulation(_cfg, new MemoryScoreStorage(), seed, startOnTitle: false);
                sim.Press();
                int lastId = -1;
                PlayWithBot(sim, seconds, s =>
                {
                    if (s.Pipes.Last.Id == lastId) return;
                    lastId = s.Pipes.Last.Id;
                    pairs.Add(s.Pipes.Last);
                });
                Assert.AreEqual(GameState.Playing, sim.State, $"graine {seed}");
            }
            return pairs;
        }

        [Test]
        public void SinglePipesAndBreathersShowUp()
        {
            int singles = 0, breathers = 0, regular = 0;
            PipeKind previous = PipeKind.Pair;
            foreach (var p in SpawnedPairs(4, 60f))
            {
                if (p.Kind == PipeKind.BottomOnly) Assert.AreEqual(0f, p.BaseTop);
                if (p.Kind == PipeKind.TopOnly) Assert.AreEqual(_cfg.GroundY - _cfg.PipeGap, p.BaseTop);
                if (p.Kind == PipeKind.Pair) Assert.AreEqual(p.GapTop, p.BaseTop);
                if (p.Kind != PipeKind.Pair)
                {
                    singles++;
                    Assert.GreaterOrEqual(p.Id, _cfg.SinglePipeFromPair);
                    Assert.AreEqual(PipeKind.Pair, previous, "jamais deux tuyaux seuls de suite");
                }
                if (previous != PipeKind.Pair) Assert.GreaterOrEqual(p.SpacingBefore, _cfg.AfterSinglePipeSpacing, "plus de place après un tuyau seul");
                if (p.SpacingBefore == _cfg.BreatherSpacing) breathers++;
                if (p.SpacingBefore == _cfg.PipeSpacing) regular++;
                previous = p.Kind;
            }
            Assert.Greater(singles, 3, "des tuyaux seuls");
            Assert.Greater(breathers, 3, "des pauses sans tuyau");
            Assert.Greater(regular, breathers * 4, "l'espacement normal reste la règle");
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
            Assert.AreEqual(_cfg.ScrollSpeed * 1.15f * _cfg.Step, sim.ScrollDistance - before, 1e-4);

            // 5 s après la prise (montée et retour compris), la vitesse est redevenue normale.
            int elapsed = (int)(_cfg.StarBoostRamp / _cfg.Step) + 3;
            int total = (int)System.Math.Round(_cfg.StarBoostDuration / _cfg.Step);
            for (int i = elapsed; i < total - (int)(_cfg.StarBoostRamp / _cfg.Step) - 1; i++)
            {
                if (sim.Bird.Y > 200f) sim.Press();
                sim.Step();
            }
            Assert.AreEqual(_cfg.StarBoostFactor, sim.SpeedFactor, 1e-5f, "pleine vitesse jusqu'au retour");
            for (int i = total - (int)(_cfg.StarBoostRamp / _cfg.Step) - 1; i < total; i++)
            {
                if (sim.Bird.Y > 200f) sim.Press();
                sim.Step();
            }
            Assert.AreEqual(1f, sim.SpeedFactor, 1e-5f, "vitesse normale 5 s après la prise");
            Assert.AreEqual(0f, sim.BoostTime, 1e-4f);
        }

        [Test]
        public void BirdIsBlueAndEveryThemeShowsUp()
        {
            Assert.AreEqual(System.Enum.GetValues(typeof(Theme)).Length, _cfg.ThemeCount, "tous les décors sont tirés");
            var sim = new GameSimulation(_cfg, new MemoryScoreStorage(), 11, startOnTitle: false);
            var seen = new bool[_cfg.ThemeCount];
            for (int run = 0; run < 200; run++)
            {
                Assert.AreEqual(BirdColor.Blue, sim.BirdColor);
                Assert.Less((int)sim.Theme, _cfg.ThemeCount);
                seen[(int)sim.Theme] = true;
                sim.ResetRun();
            }
            CollectionAssert.DoesNotContain(seen, false, "chaque décor finit par sortir");
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
                {
                    float spacing = s.Pipes[i].SpacingBefore;
                    Assert.That(spacing == _cfg.PipeSpacing || spacing == _cfg.AfterSinglePipeSpacing || spacing == _cfg.BreatherSpacing, $"espacement {spacing}");
                    Assert.AreEqual(spacing, s.Pipes[i].X - s.Pipes[i - 1].X, 1e-3f);
                }
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
        /// mobiles et les tuyaux seuls, et survit aux accélérations sur plusieurs graines.
        /// </summary>
        [TestCase(1u)]
        [TestCase(2u)]
        [TestCase(3u)]
        [TestCase(4u)]
        [TestCase(5u)]
        [TestCase(6u)]
        [TestCase(7u)]
        [TestCase(8u)]
        [TestCase(9u)]
        [TestCase(10u)]
        [TestCase(11u)]
        [TestCase(12u)]
        public void AutoPilotSurvivesMovingPipesAndStars(uint seed)
        {
            var sim = new GameSimulation(_cfg, new MemoryScoreStorage(), seed, startOnTitle: false);
            sim.Press();
            int stars = PlayWithBot(sim, 75f);
            Assert.AreEqual(GameState.Playing, sim.State, $"le bot meurt à {sim.Score} points");
            Assert.GreaterOrEqual(sim.Score, 45);
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
            Assert.Greater(total, 8, "environ 15 % des paires ont une étoile");
            Assert.Less(total, 35, "moitié moins d'étoiles qu'avant (30 %)");
        }
    }
}
