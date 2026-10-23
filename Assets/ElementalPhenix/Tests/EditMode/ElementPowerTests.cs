using ElementalPhenix.Core;
using NUnit.Framework;

namespace ElementalPhenix.Tests
{
    /// <summary>Pouvoirs d'élément : jauge, déclenchement et effet de chacun des cinq phénix.</summary>
    public class ElementPowerTests
    {
        GameConfig _cfg;

        [SetUp]
        public void SetUp() => _cfg = GameConfig.CreateDefault();

        GameSimulation StartClassic(uint seed, PhoenixElement element)
        {
            var sim = new GameSimulation(_cfg, new MemoryScoreStorage(), seed, startOnTitle: false) { Element = element };
            sim.Press();
            sim.Step();
            sim.ConsumeEvents();
            return sim;
        }

        /// <summary>
        /// Partie avec une seule paire, juste à portée du pouvoir, dont l'ouverture est tout en haut :
        /// sans pouvoir, le phénix maintenu à sa hauteur de départ la percute.
        /// </summary>
        GameSimulation FacingAWall(PhoenixElement element, bool charged = true, int gapTop = -1)
        {
            var sim = StartClassic(3, element);
            sim.Pipes.Clear();
            sim.Pipes.Spawn(_cfg.BirdCenterX + _cfg.PowerTriggerDistance - 1f, gapTop < 0 ? _cfg.GapTopMin : gapTop);
            if (charged) sim.ForceCharge(_cfg.PowerChargeCost);
            sim.ConsumeEvents();
            return sim;
        }

        /// <summary>Garde le phénix au niveau de départ (sous l'ouverture) sans jamais battre des ailes.</summary>
        static void HoldLow(GameSimulation sim, GameConfig cfg)
        {
            sim.Bird.Y = cfg.BirdStartY;
            sim.Bird.Vy = 0f;
        }

        static GameEvents Run(GameSimulation sim, int steps, System.Action each = null)
        {
            var all = GameEvents.None;
            for (int i = 0; i < steps && sim.State == GameState.Playing; i++)
            {
                each?.Invoke();
                sim.Step();
                all |= sim.ConsumeEvents();
            }
            return all;
        }

        [Test]
        public void EachPhoenixHasItsOwnElement()
        {
            Assert.AreEqual(PhoenixElement.Fire, Skins.Get(Skins.IndexOf("phoenix")).Element);
            Assert.AreEqual(PhoenixElement.Water, Skins.Get(Skins.IndexOf("blue")).Element);
            Assert.AreEqual(PhoenixElement.Wind, Skins.Get(Skins.IndexOf("mint")).Element);
            Assert.AreEqual(PhoenixElement.Light, Skins.Get(Skins.IndexOf("streak")).Element);
            Assert.AreEqual(PhoenixElement.Shadow, Skins.Get(Skins.IndexOf("ninja")).Element);
        }

        [Test]
        public void WithoutElementNothingCharges()
        {
            var sim = StartClassic(2, PhoenixElement.None);
            var events = Run(sim, 60 * 20, () => { if (AutoPilot.ShouldFlap(sim)) sim.Press(); });
            Assert.Greater(sim.Score, 0);
            Assert.AreEqual(0, sim.ElementCharge);
            Assert.IsFalse(sim.PowersActive);
            Assert.AreEqual(GameEvents.None, events & (GameEvents.PowerReady | GameEvents.ElementPower));
        }

        [Test]
        public void PointsAndShardsFillTheGauge()
        {
            var sim = StartClassic(5, PhoenixElement.Fire);
            sim.Pipes.Clear();
            sim.Shards.Spawn(_cfg.BirdCenterX + 2f, sim.Bird.CenterY(_cfg));
            sim.Step();
            Assert.AreEqual(_cfg.PowerChargePerShard, sim.ElementCharge);

            sim.ForceCharge(_cfg.PowerChargeCost - 1);
            sim.Shards.Spawn(_cfg.BirdCenterX + 2f, sim.Bird.CenterY(_cfg));
            sim.ConsumeEvents();
            sim.Step();
            Assert.IsTrue((sim.ConsumeEvents() & GameEvents.PowerReady) != 0);
            Assert.IsTrue(sim.PowerReady);
        }

        [Test]
        public void PowersCanBeKeptForTheJourneyOnly()
        {
            _cfg.ElementPowersInClassic = false;
            var sim = StartClassic(5, PhoenixElement.Fire);
            Assert.IsFalse(sim.PowersActive);
        }

        [Test]
        public void ThePowerWaitsForAnObstacleInRange()
        {
            var sim = StartClassic(3, PhoenixElement.Fire);
            sim.Pipes.Clear();
            sim.Pipes.Spawn(_cfg.BirdCenterX + _cfg.PowerTriggerDistance + 60f, _cfg.GapTopMin);
            sim.ForceCharge(_cfg.PowerChargeCost);
            HoldLow(sim, _cfg);
            sim.Step();
            Assert.IsFalse(sim.Pipes[0].Burned);
            Assert.IsTrue(sim.PowerReady);
            var events = Run(sim, 60, () => HoldLow(sim, _cfg));
            Assert.IsTrue((events & GameEvents.ElementPower) != 0);
            Assert.AreEqual(0, sim.ElementCharge);
        }

        [Test]
        public void WithoutPowerTheWallKills()
        {
            var sim = FacingAWall(PhoenixElement.Fire, charged: false);
            Run(sim, 120, () => HoldLow(sim, _cfg));
            Assert.AreNotEqual(GameState.Playing, sim.State);
        }

        [Test]
        public void FireBurnsTheObstacle()
        {
            var sim = FacingAWall(PhoenixElement.Fire);
            var events = Run(sim, 80, () => HoldLow(sim, _cfg));
            Assert.AreEqual(GameState.Playing, sim.State);
            Assert.IsTrue((events & GameEvents.ElementPower) != 0);
            Assert.IsTrue(sim.Pipes[0].Burned);
            Assert.AreEqual(1, sim.Score);
        }

        [Test]
        public void ShadowStepsToTheMiddleOfTheOpening()
        {
            var sim = FacingAWall(PhoenixElement.Shadow);
            var events = GameEvents.None;
            bool held = true;
            // Le phénix est maintenu bas jusqu'au pas de l'ombre, puis vole librement.
            for (int i = 0; i < 80 && sim.State == GameState.Playing; i++)
            {
                if (held) HoldLow(sim, _cfg);
                sim.Step();
                var e = sim.ConsumeEvents();
                events |= e;
                if ((e & GameEvents.ShadowStep) != 0)
                {
                    held = false;
                    float middle = _cfg.GapTopMin + _cfg.PipeGap * 0.5f;
                    Assert.AreEqual(middle, sim.Bird.CenterY(_cfg), 0.01f);
                    Assert.AreEqual(0f, sim.Bird.Vy);
                }
            }
            Assert.IsTrue((events & GameEvents.ShadowStep) != 0);
            Assert.AreEqual(GameState.Playing, sim.State);
            Assert.AreEqual(1, sim.Score);
        }

        [Test]
        public void ShadowIsSpentWithoutTeleportIfNoContact()
        {
            var sim = FacingAWall(PhoenixElement.Shadow);
            // Le phénix passe au milieu de l'ouverture : le pouvoir se déclenche mais ne sert pas.
            float middle = _cfg.GapTopMin + _cfg.PipeGap * 0.5f;
            var events = Run(sim, 80, () =>
            {
                sim.Bird.Y = middle - _cfg.BirdHeight * 0.5f;
                sim.Bird.Vy = 0f;
            });
            Assert.IsTrue((events & GameEvents.ElementPower) != 0);
            Assert.AreEqual(GameEvents.None, events & GameEvents.ShadowStep);
            Assert.AreEqual(1, sim.Score);
        }

        [Test]
        public void WindCarriesThePhoenixThroughTheOpening()
        {
            var sim = FacingAWall(PhoenixElement.Wind);
            HoldLow(sim, _cfg);
            // Aucun tap : le souffle seul l'amène dans l'ouverture, 160 px plus haut.
            var events = Run(sim, 80);
            Assert.IsTrue((events & GameEvents.ElementPower) != 0);
            Assert.AreEqual(GameState.Playing, sim.State);
            Assert.AreEqual(1, sim.Score);
        }

        [Test]
        public void WindStopsOnceTheOpeningIsPassed()
        {
            var sim = FacingAWall(PhoenixElement.Wind);
            HoldLow(sim, _cfg);
            Run(sim, 85);
            Assert.IsFalse(sim.Guiding);
            Assert.AreEqual(-1, sim.GuideTarget);
        }

        [Test]
        public void LightWidensTheNextOpenings()
        {
            var sim = StartClassic(3, PhoenixElement.Light);
            sim.Pipes.Clear();
            sim.Pipes.Spawn(_cfg.BirdCenterX + 40f, 170);
            sim.Pipes.Spawn(_cfg.BirdCenterX + 40f + _cfg.PipeSpacing, 170);
            sim.ForceCharge(_cfg.PowerChargeCost);
            sim.Step();
            Assert.IsTrue((sim.ConsumeEvents() & GameEvents.ElementPower) != 0);
            for (int i = 0; i < 30; i++)
            {
                HoldLow(sim, _cfg);
                sim.Step();
            }
            for (int k = 0; k < 2; k++)
            {
                ref var p = ref sim.Pipes[k];
                Assert.AreEqual(_cfg.LightWiden, p.Widen, 0.001f);
                Assert.AreEqual(170f - _cfg.LightWiden * 0.5f, p.OpeningTop, 0.001f);
                Assert.AreEqual(_cfg.PipeGap + _cfg.LightWiden, p.Gap(_cfg), 0.001f);
            }
        }

        [Test]
        public void LightWidensAPairThatAppearsLater()
        {
            var sim = StartClassic(3, PhoenixElement.Light);
            sim.Pipes.Clear();
            // Assez loin pour que la paire suivante n'existe pas encore, assez près pour le pouvoir.
            sim.Pipes.Spawn(_cfg.BirdCenterX + 80f, 170);
            sim.ForceCharge(_cfg.PowerChargeCost);
            HoldLow(sim, _cfg);
            sim.Step();
            Assert.IsTrue((sim.ConsumeEvents() & GameEvents.ElementPower) != 0);
            Assert.AreEqual(1, sim.Pipes.Count);
            Run(sim, 10, () => HoldLow(sim, _cfg));
            Assert.AreEqual(2, sim.Pipes.Count);
            Assert.AreEqual(_cfg.LightWiden, sim.Pipes[1].WidenTarget);
        }

        [Test]
        public void TheWidenedGapIsTheCollisionGap()
        {
            float x = _cfg.BirdCenterX - _cfg.PipeWidth * 0.5f;
            // Juste sous le bas d'une ouverture normale : touché sans élargissement, libre avec.
            float cy = 150f + _cfg.PipeGap + 2f;
            Assert.IsTrue(Collision.HitsPipe(_cfg.BirdCenterX, cy, _cfg.BirdRadius, 150f, x, _cfg));
            Assert.IsFalse(Collision.HitsPipe(_cfg.BirdCenterX, cy, _cfg.BirdRadius, 150f - 18f, x, _cfg, PipeKind.Pair, _cfg.PipeGap + 36f));
        }

        [Test]
        public void WaterSlowsTheWholeGameThenEnds()
        {
            var sim = FacingAWall(PhoenixElement.Water);
            HoldLow(sim, _cfg);
            sim.Step();
            Assert.IsTrue((sim.ConsumeEvents() & GameEvents.ElementPower) != 0);
            // Plus d'obstacle : seul le ralenti compte. Après sa montée, le défilement d'un pas vaut
            // WaterTimeScale fois la normale.
            sim.Pipes.Clear();
            for (int i = 0; i < 30; i++)
            {
                HoldLow(sim, _cfg);
                sim.Step();
            }
            Assert.AreEqual(_cfg.WaterTimeScale, sim.TimeScale, 0.0001f);
            double before = sim.ScrollDistance;
            sim.Step();
            Assert.AreEqual(_cfg.ScrollSpeed * _cfg.Step * _cfg.WaterTimeScale, sim.ScrollDistance - before, 0.001);
            // 3 s de temps réel plus tard, tout est revenu à la normale.
            for (int i = 0; i < (int)(_cfg.WaterSlowDuration * 60f); i++)
            {
                HoldLow(sim, _cfg);
                sim.Step();
            }
            Assert.AreEqual(1f, sim.TimeScale);
        }

        [Test]
        public void WaterKeepsTheSameJumpHeight()
        {
            // Le ralenti étire le temps, pas la trajectoire : le saut monte toujours de ≈ 40,5 px.
            var sim = FacingAWall(PhoenixElement.Water);
            HoldLow(sim, _cfg);
            sim.Step();
            sim.Pipes.Clear();
            for (int i = 0; i < 25; i++)
            {
                HoldLow(sim, _cfg);
                sim.Step();
            }
            Assert.AreEqual(_cfg.WaterTimeScale, sim.TimeScale, 0.0001f);
            sim.Bird.Y = 300f;
            sim.Press();
            float top = 300f;
            for (int i = 0; i < 120; i++)
            {
                sim.Step();
                top = System.Math.Min(top, sim.Bird.Y);
            }
            Assert.AreEqual(40.5f, 300f - top, 1.5f);
        }

        [Test]
        public void DeathStopsThePowers()
        {
            var sim = FacingAWall(PhoenixElement.Water);
            HoldLow(sim, _cfg);
            sim.Step();
            Assert.Greater(sim.SlowTime, 0f);
            sim.Bird.Y = _cfg.GroundY;
            sim.Step();
            Assert.AreEqual(0f, sim.SlowTime);
            Assert.AreEqual(1f, sim.TimeScale);
        }

        [TestCase(PhoenixElement.Fire)]
        [TestCase(PhoenixElement.Water)]
        [TestCase(PhoenixElement.Wind)]
        [TestCase(PhoenixElement.Light)]
        [TestCase(PhoenixElement.Shadow)]
        public void AutoPilotSurvivesWithEveryPower(PhoenixElement element)
        {
            int powers = 0;
            for (uint seed = 1; seed <= 2; seed++)
            {
                var sim = StartClassic(seed, element);
                for (int i = 0; i < 60 * 30; i++)
                {
                    if (AutoPilot.ShouldFlap(sim)) sim.Press();
                    sim.Step();
                    if ((sim.ConsumeEvents() & GameEvents.ElementPower) != 0) powers++;
                    Assert.AreNotEqual(GameState.Dying, sim.State, $"graine {seed}, score {sim.Score}");
                    Assert.AreNotEqual(GameState.Over, sim.State, $"graine {seed}, score {sim.Score}");
                }
                Assert.Greater(sim.Score, 12);
            }
            Assert.Greater(powers, 1);
        }
    }
}
