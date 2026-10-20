using NUnit.Framework;
using PuffyBird.Core;

namespace PuffyBird.Tests
{
    /// <summary>Mode Voyage des éléments : une étape par décor, une règle chacune, un objectif de score.</summary>
    public class JourneyTests
    {
        GameConfig _cfg;

        [SetUp]
        public void SetUp() => _cfg = GameConfig.CreateDefault();

        /// <summary>Lance l'étape depuis l'écran titre et attend la fin du fondu (état READY).</summary>
        GameSimulation StartStage(int stage, uint seed, IScoreStorage storage = null)
        {
            var sim = new GameSimulation(_cfg, storage ?? new MemoryScoreStorage(), seed);
            sim.StartJourney(stage);
            for (int i = 0; i < 60 && (sim.State != GameState.Ready || sim.IsFadingOut); i++) sim.Step();
            Assert.AreEqual(GameState.Ready, sim.State);
            return sim;
        }

        /// <summary>Le bot joue jusqu'à l'écran de fin (au plus <paramref name="seconds"/> s).</summary>
        static void PlayWithBot(GameSimulation sim, float seconds)
        {
            int steps = (int)(seconds / sim.Config.Step);
            for (int i = 0; i < steps && sim.State != GameState.Over; i++)
            {
                if (AutoPilot.ShouldFlap(sim)) sim.Press();
                sim.Step();
            }
        }

        static void WaitAndTap(GameSimulation sim)
        {
            for (int i = 0; i < 60; i++) sim.Step();
            sim.Press();
            for (int i = 0; i < 40; i++) sim.Step();
        }

        [Test]
        public void EveryThemeHasOneStage()
        {
            Assert.AreEqual(_cfg.ThemeCount, _cfg.Journey.Length);
            for (int t = 0; t < _cfg.ThemeCount; t++)
                Assert.AreEqual(1, System.Array.FindAll(_cfg.Journey, s => s.Theme == (Theme)t).Length, ((Theme)t).ToString());
        }

        [Test]
        public void StageUsesItsThemeAndRules()
        {
            var sim = StartStage(4, 1);
            Assert.IsTrue(sim.IsJourney);
            Assert.AreEqual(4, sim.JourneyStage);
            Assert.AreEqual(_cfg.Journey[4].Theme, sim.Theme);
            Assert.AreEqual(_cfg.Journey[4].Goal, sim.JourneyGoal);
            Assert.AreEqual(_cfg.Journey[4].GustAcceleration, sim.Config.GustAcceleration);
            // Les réglages du mode classique ne sont jamais modifiés.
            Assert.AreEqual(0f, _cfg.GustAcceleration);
            Assert.AreEqual(900f, _cfg.Gravity);
        }

        [Test]
        public void WaterSlowsTheFallAndTheScroll()
        {
            var ocean = _cfg.ForStage(System.Array.FindIndex(_cfg.Journey, s => s.Theme == Theme.Ocean));
            Assert.Less(ocean.Gravity, _cfg.Gravity);
            Assert.Less(ocean.MaxFallSpeed, _cfg.MaxFallSpeed);
            Assert.Less(ocean.ScrollSpeed, _cfg.ScrollSpeed);
            Assert.AreEqual(ocean.FlapVelocity, ocean.MaxRiseSpeed);
        }

        [Test]
        public void GustsPushTheBirdUpAndDown()
        {
            var storm = _cfg.ForStage(System.Array.FindIndex(_cfg.Journey, s => s.Theme == Theme.Storm));
            Assert.Greater(storm.GustAt(storm.GustPeriod * 0.25f), 0f);
            Assert.Less(storm.GustAt(storm.GustPeriod * 0.75f), 0f);
            Assert.AreEqual(0f, _cfg.GustAt(0.3f));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        public void AutoPilotClearsEveryStage(int stage)
        {
            for (uint seed = 1; seed <= 5; seed++)
            {
                var sim = StartStage(stage, seed);
                PlayWithBot(sim, 120f);
                Assert.AreEqual(GameState.Over, sim.State, $"graine {seed}");
                Assert.IsTrue(sim.StageCleared, $"étape {stage}, graine {seed} : mort à {sim.Score} / {sim.JourneyGoal}");
                Assert.AreEqual(sim.JourneyGoal, sim.Score);
                Assert.IsFalse(sim.CanContinue);
                Assert.AreEqual(Medal.None, sim.Medal);
            }
        }

        [Test]
        public void ClearedStageGlidesWithoutPipesThenLeadsToTheNextOne()
        {
            var sim = StartStage(0, 2);
            bool clearedEvent = false;
            int steps = 0;
            while (sim.State != GameState.Over && steps++ < 20000)
            {
                if (AutoPilot.ShouldFlap(sim)) sim.Press();
                sim.Step();
                if ((sim.ConsumeEvents() & GameEvents.StageCleared) != 0)
                {
                    clearedEvent = true;
                    int pipeCount = sim.Pipes.Count;
                    // Pendant le vol de fin, plus aucune paire n'apparaît et les taps sont ignorés.
                    for (int i = 0; i < 30; i++)
                    {
                        sim.Press();
                        sim.Step();
                        Assert.LessOrEqual(sim.Pipes.Count, pipeCount);
                        Assert.AreEqual(GameState.Playing, sim.State);
                    }
                }
            }
            Assert.IsTrue(clearedEvent);
            Assert.IsTrue(sim.StageCleared);
            WaitAndTap(sim);
            Assert.IsTrue(sim.IsJourney);
            Assert.AreEqual(1, sim.JourneyStage);
            Assert.AreEqual(_cfg.Journey[1].Theme, sim.Theme);
            Assert.AreEqual(GameState.Ready, sim.State);
        }

        [Test]
        public void FailedStageIsRetriedAndDoesNotTouchTheRecord()
        {
            var storage = new MemoryScoreStorage();
            storage.WriteBest(3);
            var sim = StartStage(2, 1, storage);
            sim.Press();
            // Sans battement, l'oiseau tombe au sol.
            for (int i = 0; i < 600 && sim.State != GameState.Over; i++) sim.Step();
            Assert.AreEqual(GameState.Over, sim.State);
            Assert.IsFalse(sim.StageCleared);
            WaitAndTap(sim);
            Assert.AreEqual(2, sim.JourneyStage);
            Assert.AreEqual(3, sim.Best);
        }

        [Test]
        public void LastStageLeadsBackToTitleAndClassicRules()
        {
            int last = _cfg.Journey.Length - 1;
            var sim = StartStage(last, 3);
            PlayWithBot(sim, 120f);
            Assert.IsTrue(sim.JourneyComplete);
            WaitAndTap(sim);
            Assert.AreEqual(GameState.Title, sim.State);
            Assert.IsFalse(sim.IsJourney);
            Assert.AreSame(_cfg, sim.Config);
        }

        [Test]
        public void HomeLeavesTheJourney()
        {
            var sim = StartStage(5, 1);
            sim.QuitToTitle();
            for (int i = 0; i < 60; i++) sim.Step();
            Assert.AreEqual(GameState.Title, sim.State);
            Assert.IsFalse(sim.IsJourney);
            Assert.AreSame(_cfg, sim.Config);
        }
    }
}
