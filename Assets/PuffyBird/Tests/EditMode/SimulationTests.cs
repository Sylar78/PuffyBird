using System.Collections.Generic;
using NUnit.Framework;
using PuffyBird.Core;

namespace PuffyBird.Tests
{
    /// <summary>Critères d'acceptation A1 à A13 (§23.1) vérifiables sans affichage.</summary>
    public class SimulationTests
    {
        GameConfig _cfg;

        [SetUp]
        public void SetUp() => _cfg = GameConfig.CreateDefault();

        GameSimulation NewReadySim(MemoryScoreStorage storage = null, uint seed = 1)
        {
            return new GameSimulation(_cfg, storage ?? new MemoryScoreStorage(), seed, startOnTitle: false);
        }

        void Run(GameSimulation sim, float seconds)
        {
            int steps = (int)(seconds / _cfg.Step + 0.5f);
            for (int i = 0; i < steps; i++) sim.Step();
        }

        [Test]
        public void StartsOnTitleThenGoesToReadyAfterFade()
        {
            var sim = new GameSimulation(_cfg, new MemoryScoreStorage(), 1);
            Assert.AreEqual(GameState.Title, sim.State);
            sim.Press();
            sim.Step();
            Assert.IsTrue(sim.IsFadingOut);
            Run(sim, _cfg.FadeTime + 0.05f);
            Assert.AreEqual(GameState.Ready, sim.State);
        }

        [Test]
        public void QuitToTitleFromPauseFadesToTitleWithoutSavingScore()
        {
            var storage = new MemoryScoreStorage();
            var sim = NewReadySim(storage);
            sim.Press();
            sim.Step();
            Assert.AreEqual(GameState.Playing, sim.State);
            sim.Pause();
            int run = sim.RunId;
            sim.QuitToTitle();
            sim.Step();
            Assert.IsTrue(sim.IsFadingOut);
            Assert.AreEqual(GameState.Paused, sim.State);
            // Les taps sont ignorés pendant le fondu.
            sim.Press();
            Run(sim, _cfg.FadeTime + 0.05f);
            Assert.AreEqual(GameState.Title, sim.State);
            Assert.AreEqual(run + 1, sim.RunId);
            Assert.AreEqual(0, sim.Score);
            Assert.AreEqual(0, storage.Writes);
            // L'écran titre relance ensuite normalement une partie.
            sim.Press();
            Run(sim, _cfg.FadeTime + 0.05f);
            Assert.AreEqual(GameState.Ready, sim.State);
        }

        [Test]
        public void QuitToTitleWhilePlayingFreezesTheBird()
        {
            var sim = NewReadySim();
            sim.Press();
            sim.Step();
            sim.QuitToTitle();
            sim.Step();
            float y = sim.Bird.Y;
            Run(sim, _cfg.FadeTime * 0.5f);
            Assert.AreEqual(y, sim.Bird.Y);
            Run(sim, _cfg.FadeTime);
            Assert.AreEqual(GameState.Title, sim.State);
        }

        [Test]
        public void QuitToTitleOnTitleDoesNothing()
        {
            var sim = new GameSimulation(_cfg, new MemoryScoreStorage(), 1);
            sim.QuitToTitle();
            sim.Step();
            Assert.IsFalse(sim.IsFadingOut);
            Assert.AreEqual(GameState.Title, sim.State);
        }

        static void DieOnGround(GameSimulation sim, GameConfig cfg)
        {
            int guard = 0;
            while (sim.State != GameState.Over && guard++ < 2000) sim.Step();
        }

        [Test]
        public void ContinueKeepsScoreAndRestartsFromReadyOnce()
        {
            var sim = NewReadySim();
            sim.Press();
            sim.Step();
            sim.ForceScore(7);
            DieOnGround(sim, _cfg);
            Assert.AreEqual(GameState.Over, sim.State);
            Assert.IsTrue(sim.CanContinue);
            int run = sim.RunId;

            sim.ContinueRun();
            Assert.AreEqual(GameState.Ready, sim.State);
            Assert.AreEqual(7, sim.Score);
            Assert.AreEqual(run, sim.RunId);
            Assert.AreEqual(0, sim.Pipes.Count);
            Assert.AreEqual(_cfg.BirdStartY, sim.Bird.Y);

            // Le tap suivant relance la partie avec une nouvelle paire de tuyaux.
            sim.Press();
            sim.Step();
            Assert.AreEqual(GameState.Playing, sim.State);
            Assert.AreEqual(1, sim.Pipes.Count);
            Assert.AreEqual(7, sim.Score);

            // Une seule seconde chance par partie.
            DieOnGround(sim, _cfg);
            Assert.IsFalse(sim.CanContinue);
            sim.ContinueRun();
            Assert.AreEqual(GameState.Over, sim.State);

            // La partie suivante y a de nouveau droit.
            Run(sim, _cfg.OverInputDelay + 0.05f);
            sim.Press();
            Run(sim, _cfg.FadeTime + 0.05f);
            Assert.AreEqual(GameState.Ready, sim.State);
            Assert.AreEqual(0, sim.Score);
            Assert.IsFalse(sim.Continued);
        }

        [Test]
        public void ContinueIsOnlyPossibleOnTheOverScreen()
        {
            var sim = NewReadySim();
            Assert.IsFalse(sim.CanContinue);
            sim.ContinueRun();
            Assert.AreEqual(GameState.Ready, sim.State);
            Assert.IsFalse(sim.Continued);
        }

        [Test]
        public void ReadyBirdFloatsWithoutFalling()
        {
            var sim = NewReadySim();
            Run(sim, 3f);
            Assert.AreEqual(GameState.Ready, sim.State);
            Assert.That(sim.Bird.Y, Is.InRange(_cfg.BirdStartY - 4.01f, _cfg.BirdStartY + 4.01f));
        }

        [Test]
        public void FirstTapStartsTheGameAndFlaps()
        {
            var sim = NewReadySim();
            sim.Press();
            sim.Step();
            Assert.AreEqual(GameState.Playing, sim.State);
            Assert.Less(sim.Bird.Vy, 0f);
            Assert.AreEqual(1, sim.Pipes.Count);
            Assert.That(sim.Pipes[0].X, Is.EqualTo(_cfg.FirstPipeX - _cfg.ScrollSpeed * _cfg.Step).Within(0.001f));
            Assert.IsTrue((sim.ConsumeEvents() & GameEvents.Flap) != 0);
        }

        [Test]
        public void WithoutTapBirdHitsGroundInUnderOneAndHalfSeconds()
        {
            var sim = NewReadySim();
            sim.ForcePlaying();
            float t = 0f;
            while (sim.State == GameState.Playing && t < 3f)
            {
                sim.Step();
                t += _cfg.Step;
            }
            Assert.AreEqual(GameState.Over, sim.State);
            Assert.Less(t, 1.5f);
            Assert.IsTrue(sim.DiedOnGround);
        }

        [Test]
        public void ScoresOncePerPair()
        {
            var sim = NewReadySim();
            sim.ForcePlaying();
            sim.Pipes.Spawn(48f, 200);
            for (int i = 0; i < 10; i++) sim.Step();
            Assert.AreEqual(1, sim.Score);
            Assert.AreEqual(GameState.Playing, sim.State);
        }

        [Test]
        public void PipeCollisionTriggersDyingThenDieSoundThenOver()
        {
            var sim = NewReadySim();
            sim.ForcePlaying();
            sim.Pipes.Spawn(57f, 300);
            sim.Step();
            Assert.AreEqual(GameState.Dying, sim.State);
            var events = sim.ConsumeEvents();
            Assert.IsTrue((events & GameEvents.Hit) != 0);
            Assert.AreEqual(_cfg.FlashTime, sim.Flash, 1e-5f);

            bool dieHeard = false;
            float t = 0f;
            while (sim.State == GameState.Dying && t < 3f)
            {
                sim.Step();
                t += _cfg.Step;
                var e = sim.ConsumeEvents();
                if ((e & GameEvents.Die) != 0)
                {
                    dieHeard = true;
                    Assert.That(t, Is.EqualTo(_cfg.DieSoundDelay).Within(_cfg.Step * 1.5f));
                }
            }
            Assert.IsTrue(dieHeard);
            Assert.AreEqual(GameState.Over, sim.State);
            Assert.AreEqual(-90f, sim.Bird.DisplayRotation(_cfg));
        }

        [Test]
        public void ScrollingFreezesOnDeath()
        {
            var sim = NewReadySim();
            sim.ForcePlaying();
            sim.Pipes.Spawn(57f, 300);
            sim.Step();
            double scroll = sim.ScrollDistance;
            float pipeX = sim.Pipes[0].X;
            Run(sim, 0.5f);
            Assert.AreEqual(scroll, sim.ScrollDistance);
            Assert.AreEqual(pipeX, sim.Pipes[0].X);
        }

        [Test]
        public void GroundDeathHasNoDieSound()
        {
            var sim = NewReadySim();
            sim.ForcePlaying();
            var all = GameEvents.None;
            for (int i = 0; i < 200; i++)
            {
                sim.Step();
                all |= sim.ConsumeEvents();
            }
            Assert.AreEqual(GameState.Over, sim.State);
            Assert.IsTrue((all & GameEvents.Hit) != 0);
            Assert.IsTrue((all & GameEvents.Swoosh) != 0);
            Assert.IsTrue((all & GameEvents.Die) == 0);
        }

        [Test]
        public void OverIgnoresInputDuringFirst800Milliseconds()
        {
            var sim = NewReadySim();
            sim.ForcePlaying();
            while (sim.State != GameState.Over) sim.Step();
            Run(sim, 0.5f);
            sim.Press();
            sim.Step();
            Assert.IsFalse(sim.IsFadingOut);
            Run(sim, 0.4f);
            int run = sim.RunId;
            sim.Press();
            Run(sim, _cfg.FadeTime + 0.05f);
            Assert.AreEqual(GameState.Ready, sim.State);
            Assert.AreEqual(run + 1, sim.RunId);
        }

        [Test]
        public void BestScoreIsSavedOnlyWhenBeatenStrictly()
        {
            var storage = new MemoryScoreStorage { Best = 1 };
            var sim = NewReadySim(storage);
            Assert.AreEqual(1, sim.Best);
            sim.ForcePlaying();
            sim.Pipes.Spawn(48f, 200);
            while (sim.State == GameState.Playing) sim.Step();
            Assert.AreEqual(1, sim.Score);
            Assert.IsFalse(sim.NewBest);
            Assert.AreEqual(0, storage.Writes);
        }

        [Test]
        public void NewBestIsWritten()
        {
            var storage = new MemoryScoreStorage();
            var sim = NewReadySim(storage);
            sim.ForcePlaying();
            sim.Pipes.Spawn(48f, 200);
            while (sim.State == GameState.Playing) sim.Step();
            Assert.IsTrue(sim.NewBest);
            Assert.AreEqual(1, storage.Best);
            Assert.AreEqual(1, storage.Writes);
        }

        [Test]
        public void UnreadableBestIsNeverOverwritten()
        {
            var storage = new MemoryScoreStorage { Best = 999, Readable = false };
            var sim = NewReadySim(storage);
            Assert.AreEqual(0, sim.Best);
            sim.ForcePlaying();
            sim.Pipes.Spawn(48f, 200);
            while (sim.State == GameState.Playing) sim.Step();
            Assert.AreEqual(0, storage.Writes);
            Assert.AreEqual(999, storage.Best);
        }

        [Test]
        public void PauseFreezesThenTapResumesWithoutFlap()
        {
            var sim = NewReadySim();
            sim.Press();
            sim.Step();
            sim.Pause();
            Assert.AreEqual(GameState.Paused, sim.State);
            float y = sim.Bird.Y;
            Run(sim, 1f);
            Assert.AreEqual(y, sim.Bird.Y);
            sim.ConsumeEvents();
            sim.Press();
            sim.Step();
            Assert.AreEqual(GameState.Playing, sim.State);
            Assert.IsTrue((sim.ConsumeEvents() & GameEvents.Flap) == 0);
        }

        [TestCase(1u)]
        [TestCase(2u)]
        [TestCase(3u)]
        [TestCase(4u)]
        [TestCase(5u)]
        public void AutoPilotScores14PointsIn20Seconds(uint seed)
        {
            var sim = NewReadySim(seed: seed);
            sim.Press();
            int steps = (int)(20f / _cfg.Step);
            for (int i = 0; i < steps; i++)
            {
                if (AutoPilot.ShouldFlap(sim)) sim.Press();
                sim.Step();
            }
            Assert.AreEqual(GameState.Playing, sim.State, "le bot ne doit pas mourir");
            Assert.GreaterOrEqual(sim.Score, 14);
        }

        [Test]
        public void BehaviourIsIdenticalAt30And60And144Hz()
        {
            var t30 = Trace(30);
            var t60 = Trace(60);
            var t144 = Trace(144);
            AssertSamePrefix(t60, t30);
            AssertSamePrefix(t60, t144);
        }

        List<(int score, float y)> Trace(int hz)
        {
            var sim = NewReadySim(seed: 99);
            var clock = new FixedStepClock(_cfg.Step, _cfg.MaxFrameDelta);
            var trace = new List<(int, float)>();
            sim.Press();
            for (int frame = 0; frame < hz * 15; frame++)
            {
                int steps = clock.Advance(1.0 / hz);
                for (int s = 0; s < steps; s++)
                {
                    if (AutoPilot.ShouldFlap(sim)) sim.Press();
                    sim.Step();
                    trace.Add((sim.Score, sim.Bird.Y));
                }
            }
            return trace;
        }

        static void AssertSamePrefix(List<(int score, float y)> a, List<(int score, float y)> b)
        {
            Assert.That(System.Math.Abs(a.Count - b.Count), Is.LessThanOrEqualTo(1));
            int n = System.Math.Min(a.Count, b.Count);
            for (int i = 0; i < n; i++) Assert.AreEqual(a[i], b[i], $"écart au pas {i}");
        }

        [Test]
        public void ViewMarginDoesNotChangeGameplay()
        {
            var narrow = NewReadySim(seed: 21);
            var wide = NewReadySim(seed: 21);
            wide.ViewMargin = 1000f;
            Assert.AreEqual(_cfg.MaxViewMargin, wide.ViewMargin, "marge bornée");
            narrow.Press();
            wide.Press();
            int steps = (int)(20f / _cfg.Step);
            for (int i = 0; i < steps; i++)
            {
                if (AutoPilot.ShouldFlap(narrow))
                {
                    narrow.Press();
                    wide.Press();
                }
                narrow.Step();
                wide.Step();
                Assert.AreEqual(narrow.Bird.Y, wide.Bird.Y, $"pas {i}");
                Assert.AreEqual(narrow.Score, wide.Score, $"pas {i}");
                Assert.AreEqual(narrow.State, wide.State, $"pas {i}");
            }
            Assert.GreaterOrEqual(narrow.Score, 14);
        }

        [Test]
        public void MedalThresholds()
        {
            Assert.AreEqual(Medal.None, Medals.For(9, _cfg));
            Assert.AreEqual(Medal.Bronze, Medals.For(10, _cfg));
            Assert.AreEqual(Medal.Silver, Medals.For(29, _cfg));
            Assert.AreEqual(Medal.Gold, Medals.For(30, _cfg));
            Assert.AreEqual(Medal.Platinum, Medals.For(40, _cfg));
            Assert.AreEqual(Medal.Platinum, Medals.For(999, _cfg));
        }

        [Test]
        public void SameSeedGivesSameGaps()
        {
            var a = NewReadySim(seed: 5);
            var b = NewReadySim(seed: 5);
            a.Press();
            b.Press();
            for (int i = 0; i < 400; i++)
            {
                a.Step();
                b.Step();
            }
            Assert.AreEqual(a.Pipes.Count, b.Pipes.Count);
            for (int i = 0; i < a.Pipes.Count; i++) Assert.AreEqual(a.Pipes[i].GapTop, b.Pipes[i].GapTop);
        }
    }
}
