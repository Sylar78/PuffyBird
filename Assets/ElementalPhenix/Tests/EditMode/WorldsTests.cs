using ElementalPhenix.Core;
using NUnit.Framework;

namespace ElementalPhenix.Tests
{
    /// <summary>Éclats d'élément, sillages, courants, gardiens et renaissance des cendres.</summary>
    public class WorldsTests
    {
        GameConfig _cfg;

        [SetUp]
        public void SetUp() => _cfg = GameConfig.CreateDefault();

        GameSimulation StartClassic(uint seed)
        {
            var sim = new GameSimulation(_cfg, new MemoryScoreStorage(), seed, startOnTitle: false);
            sim.Press();
            sim.Step();
            sim.ConsumeEvents();
            return sim;
        }

        GameSimulation StartStage(int stage, uint seed)
        {
            var sim = new GameSimulation(_cfg, new MemoryScoreStorage(), seed);
            sim.StartJourney(stage);
            for (int i = 0; i < 60 && sim.State != GameState.Ready; i++) sim.Step();
            sim.Press();
            sim.Step();
            sim.ConsumeEvents();
            return sim;
        }

        static GameEvents Play(GameSimulation sim, float seconds, bool bot = true)
        {
            var all = GameEvents.None;
            int steps = (int)(seconds * 60f);
            for (int i = 0; i < steps && sim.State != GameState.Over; i++)
            {
                if (bot && AutoPilot.ShouldFlap(sim)) sim.Press();
                sim.Step();
                all |= sim.ConsumeEvents();
            }
            return all;
        }

        [Test]
        public void ShardsAppearBetweenPairsInsideTheScreen()
        {
            var sim = StartClassic(4);
            int seen = 0;
            int steps = 60 * 30;
            for (int i = 0; i < steps; i++)
            {
                if (AutoPilot.ShouldFlap(sim)) sim.Press();
                sim.Step();
                for (int k = 0; k < sim.Shards.Capacity; k++)
                {
                    ref var s = ref sim.Shards[k];
                    if (!s.Active) continue;
                    seen++;
                    Assert.GreaterOrEqual(s.Y, _cfg.ShardMarginTop);
                    Assert.LessOrEqual(s.Y, _cfg.GroundY - _cfg.ShardMarginGround);
                }
            }
            Assert.Greater(seen, 0);
        }

        [Test]
        public void TouchingAShardCountsItAndFillsTheEmbers()
        {
            var sim = StartClassic(1);
            sim.Shards.Spawn(_cfg.BirdCenterX + 2f, sim.Bird.CenterY(_cfg));
            sim.Step();
            Assert.IsTrue((sim.ConsumeEvents() & GameEvents.Shard) != 0);
            Assert.AreEqual(1, sim.ShardsThisRun);
            Assert.AreEqual(_cfg.EmbersPerShard, sim.Embers);
            Assert.AreEqual(0, sim.Shards.ActiveCount);
        }

        [Test]
        public void ExtensionLayoutIgnoresShards()
        {
            // Les éclats ont leur propre générateur : étoiles, tuyaux seuls et pauses restent les mêmes.
            _cfg.ShardChance = 0f;
            var a = StartClassic(9);
            var cfgB = GameConfig.CreateDefault();
            var b = new GameSimulation(cfgB, new MemoryScoreStorage(), 9, startOnTitle: false);
            b.Press();
            b.Step();
            for (int i = 0; i < 60 * 25; i++)
            {
                if (AutoPilot.ShouldFlap(a)) a.Press();
                if (AutoPilot.ShouldFlap(b)) b.Press();
                a.Step();
                b.Step();
            }
            Assert.AreEqual(a.Pipes.Count, b.Pipes.Count);
            for (int i = 0; i < a.Pipes.Count; i++)
            {
                Assert.AreEqual(a.Pipes[i].X, b.Pipes[i].X);
                Assert.AreEqual(a.Pipes[i].BaseTop, b.Pipes[i].BaseTop);
            }
        }

        [Test]
        public void WakesUnlockWithShardsOnlyOnce()
        {
            int shards = 30;
            int owned = 0;
            Assert.IsTrue(Wakes.IsOwned(0, owned));
            Assert.IsFalse(Wakes.TryUnlock(0, ref shards, ref owned));
            Assert.IsTrue(Wakes.TryUnlock(1, ref shards, ref owned));
            Assert.AreEqual(30 - Wakes.Get(1).Cost, shards);
            Assert.IsTrue(Wakes.IsOwned(1, owned));
            Assert.IsFalse(Wakes.TryUnlock(1, ref shards, ref owned));
            int before = shards;
            Assert.IsFalse(Wakes.TryUnlock(Wakes.Count - 1, ref shards, ref owned));
            Assert.AreEqual(before, shards);
        }

        [Test]
        public void RebirthOnceWhenTheEmbersAreFull()
        {
            var sim = StartClassic(2);
            sim.ForceScore(7);
            sim.ForceEmbers(_cfg.RebirthCost);
            Assert.IsTrue(sim.RebirthReady);
            // Sans battement, l'oiseau finit au sol : il renaît au lieu de s'éteindre.
            var events = Play(sim, 4f, bot: false);
            Assert.IsTrue((events & GameEvents.Rebirth) != 0);
            Assert.AreEqual(GameState.Ready, sim.State);
            Assert.AreEqual(7, sim.Score);
            Assert.IsTrue(sim.Reborn);
            Assert.AreEqual(0, sim.Embers);
            Assert.AreEqual(0, sim.Pipes.Count);

            // Une seule renaissance par partie : la chute suivante éteint la flamme.
            sim.ForceEmbers(_cfg.RebirthCost);
            Assert.IsFalse(sim.RebirthReady);
            sim.Press();
            Play(sim, 4f, bot: false);
            Assert.AreEqual(GameState.Over, sim.State);
            Assert.AreEqual(7, sim.Best);
        }

        [Test]
        public void PointsFillTheEmbersUpToTheCost()
        {
            var sim = StartClassic(3);
            var events = Play(sim, 40f);
            Assert.GreaterOrEqual(sim.Score, _cfg.RebirthCost);
            Assert.AreEqual(_cfg.RebirthCost, sim.Embers);
            Assert.IsTrue((events & GameEvents.EmbersFull) != 0);
        }

        [Test]
        public void NoEmbersNorRebirthInTheJourney()
        {
            var sim = StartStage(0, 1);
            Play(sim, 20f);
            Assert.AreEqual(0, sim.Embers);
            Assert.IsFalse(sim.RebirthReady);
        }

        [TestCase(0)]
        [TestCase(2)]
        [TestCase(5)]
        public void GuardianThrowsTheLastPairsOfAStage(int stage)
        {
            var sim = StartStage(stage, 2);
            var events = GameEvents.None;
            var stageCfg = sim.Config;
            float[] tops = new float[stageCfg.GuardianPairs];
            int seen = 0;
            int lastId = -1;
            for (int i = 0; i < 60 * 120 && sim.State != GameState.Over; i++)
            {
                if (AutoPilot.ShouldFlap(sim)) sim.Press();
                sim.Step();
                events |= sim.ConsumeEvents();
                if (sim.Pipes.Count == 0) continue;
                ref var last = ref sim.Pipes.Last;
                if (last.Id == lastId || !sim.GuardianActive) continue;
                lastId = last.Id;
                if (seen < tops.Length)
                {
                    Assert.AreEqual(PipeKind.Pair, last.Kind);
                    Assert.AreEqual(0f, last.MoveAmplitude);
                    tops[seen++] = last.BaseTop;
                }
            }
            Assert.IsTrue((events & GameEvents.Guardian) != 0);
            Assert.IsTrue(sim.StageCleared);
            Assert.IsFalse(sim.GuardianActive);
            Assert.AreEqual(tops.Length, seen);
            foreach (float top in tops) Assert.That(top, Is.InRange((float)stageCfg.GapTopMin, (float)stageCfg.GapTopMax));
            switch (stageCfg.GuardianPattern)
            {
                case GuardianPattern.Climb: Assert.Greater(tops[0], tops[2]); break;
                case GuardianPattern.Dive: Assert.Less(tops[0], tops[2]); break;
                default: Assert.AreNotEqual(tops[0], tops[1]); break;
            }
        }

        [Test]
        public void CurrentsLiftTheBirdOnlyUnderwater()
        {
            int ocean = System.Array.FindIndex(_cfg.Journey, s => s.Rule == StageRule.SlowSwim);
            var oceanCfg = _cfg.ForStage(ocean);
            Assert.Greater(oceanCfg.CurrentChance, 0f);
            Assert.AreEqual(0f, _cfg.CurrentChance);

            var field = new StarField(4);
            field.Spawn(_cfg.BirdCenterX + 10f, 0f);
            Assert.AreEqual(oceanCfg.CurrentAcceleration, GameSimulation.CurrentAt(oceanCfg.BirdCenterX, field, 0f, oceanCfg));
            Assert.AreEqual(0f, GameSimulation.CurrentAt(oceanCfg.BirdCenterX, field, 60f, oceanCfg));
            // Dans un courant, l'oiseau finit par monter malgré la gravité de l'eau.
            Assert.Less(oceanCfg.Gravity + oceanCfg.CurrentAcceleration, 0f);

            var sim = StartStage(ocean, 1);
            int seen = 0;
            for (int i = 0; i < 60 * 40 && sim.State == GameState.Playing; i++)
            {
                if (AutoPilot.ShouldFlap(sim)) sim.Press();
                sim.Step();
                seen += sim.Currents.ActiveCount;
            }
            Assert.Greater(seen, 0);
        }
    }
}
