using System;
using NUnit.Framework;
using PuffyBird.Core;

namespace PuffyBird.Tests
{
    /// <summary>Sensation de jeu (frôlements, paliers de médaille) et série de jours.</summary>
    public class FeelAndStreakTests
    {
        GameConfig _cfg;

        [SetUp]
        public void SetUp() => _cfg = GameConfig.CreateDefault();

        GameSimulation NewSim(uint seed = 1)
        {
            var sim = new GameSimulation(_cfg, new MemoryScoreStorage(), seed, startOnTitle: false);
            sim.Press();
            return sim;
        }

        /// <summary>Le bot joue 40 s ; renvoie le nombre de frôlements signalés.</summary>
        int CountNearMisses(GameSimulation sim, out int pairsPassed)
        {
            int nearMisses = 0;
            for (int i = 0; i < (int)(40f / _cfg.Step) && (sim.State == GameState.Ready || sim.State == GameState.Playing); i++)
            {
                if (AutoPilot.ShouldFlap(sim)) sim.Press();
                sim.Step();
                if ((sim.ConsumeEvents() & GameEvents.NearMiss) != 0) nearMisses++;
            }
            pairsPassed = sim.Score;
            return nearMisses;
        }

        [Test]
        public void PipeGapMeasuresDistanceToTheNearestPipe()
        {
            // Ouverture de 100 px : oiseau centré, 39 px de chaque côté (50 − rayon 11).
            Assert.AreEqual(39f, Collision.PipeGap(100f, 150f, 11f, 100f, 90f, _cfg), 1e-3f);
            // Plus bas : le tuyau du bas est à 200 − (170 + 11) = 19 px.
            Assert.AreEqual(19f, Collision.PipeGap(100f, 170f, 11f, 100f, 90f, _cfg), 1e-3f);
            // En contact : 0, jamais négatif.
            Assert.AreEqual(0f, Collision.PipeGap(100f, 195f, 11f, 100f, 90f, _cfg));
        }

        [Test]
        public void EveryPairIsGrazedWhenTheThresholdIsWide()
        {
            // Le milieu de l'ouverture est à 39 px des tuyaux : un seuil de 45 px frôle à chaque paire.
            _cfg.NearMissDistance = 45f;
            var sim = NewSim();
            int nearMisses = CountNearMisses(sim, out int pairs);
            Assert.GreaterOrEqual(pairs, 20);
            // L'événement part quand l'oiseau a dépassé le tuyau, un peu après le point : au plus une paire d'écart.
            Assert.That(nearMisses, Is.InRange(pairs - 1, pairs));
        }

        [Test]
        public void NoNearMissWithAZeroThresholdAndNoCollision()
        {
            _cfg.NearMissDistance = 0f;
            var sim = NewSim();
            Assert.AreEqual(0, CountNearMisses(sim, out int pairs));
            Assert.GreaterOrEqual(pairs, 20);
        }

        [Test]
        public void NearMissIsReportedOncePerPair()
        {
            _cfg.NearMissDistance = 45f;
            var sim = NewSim();
            sim.Press();
            int total = 0;
            var seen = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < (int)(20f / _cfg.Step) && sim.State == GameState.Playing; i++)
            {
                if (AutoPilot.ShouldFlap(sim)) sim.Press();
                sim.Step();
                if ((sim.ConsumeEvents() & GameEvents.NearMiss) != 0) total++;
                for (int p = 0; p < sim.Pipes.Count; p++)
                    if (sim.Pipes[p].GrazeReported) seen.Add(sim.Pipes[p].Id);
            }
            Assert.AreEqual(total, seen.Count);
        }

        [TestCase(9, true)]
        [TestCase(19, true)]
        [TestCase(29, true)]
        [TestCase(39, true)]
        [TestCase(0, false)]
        [TestCase(10, false)]
        [TestCase(25, false)]
        public void MilestoneFiresOnlyWhenTheScoreReachesAMedalThreshold(int scoreBefore, bool expected)
        {
            var sim = new GameSimulation(_cfg, new MemoryScoreStorage(), 1u, startOnTitle: false);
            sim.ForcePlaying();
            sim.ForceScore(scoreBefore);
            sim.Pipes.Spawn(_cfg.BirdCenterX - _cfg.PipeWidth * 0.5f - 1f, 200);
            for (int i = 0; i < 5; i++) sim.Step();
            Assert.AreEqual(scoreBefore + 1, sim.Score);
            Assert.AreEqual(expected, (sim.ConsumeEvents() & GameEvents.Milestone) != 0);
        }

        [Test]
        public void FirstTapStartsTheRunOnce()
        {
            var sim = new GameSimulation(_cfg, new MemoryScoreStorage(), 1u, startOnTitle: false);
            sim.Press();
            sim.Step();
            Assert.IsTrue((sim.ConsumeEvents() & GameEvents.RunStarted) != 0);
            sim.Press();
            sim.Step();
            Assert.IsFalse((sim.ConsumeEvents() & GameEvents.RunStarted) != 0);
        }

        [Test]
        public void StreakStartsAtOneThenGrowsDayAfterDay()
        {
            Assert.AreEqual(1, DailyStreak.Advance(0, 0, 8000));
            Assert.AreEqual(2, DailyStreak.Advance(8000, 1, 8001));
            Assert.AreEqual(30, DailyStreak.Advance(8028, 29, 8029));
        }

        [Test]
        public void StreakIsUnchangedWithinTheSameDay()
        {
            Assert.AreEqual(5, DailyStreak.Advance(8000, 5, 8000));
        }

        [Test]
        public void StreakResetsAfterAMissedDay()
        {
            Assert.AreEqual(1, DailyStreak.Advance(8000, 12, 8002));
            Assert.AreEqual(1, DailyStreak.Advance(8000, 12, 9000));
        }

        [Test]
        public void StreakSurvivesAClockMovedBack()
        {
            Assert.AreEqual(7, DailyStreak.Advance(8010, 7, 8005));
            Assert.AreEqual(8010, DailyStreak.LastDayAfter(8010, 8005));
            Assert.AreEqual(8011, DailyStreak.LastDayAfter(8010, 8011));
        }

        [Test]
        public void DisplayedStreakFallsToZeroOnceADayIsMissed()
        {
            Assert.AreEqual(0, DailyStreak.Current(0, 0, 8000));
            Assert.AreEqual(6, DailyStreak.Current(8000, 6, 8000));
            Assert.AreEqual(6, DailyStreak.Current(8000, 6, 8001));
            Assert.AreEqual(0, DailyStreak.Current(8000, 6, 8002));
        }

        [Test]
        public void DayNumbersAreConsecutiveAcrossMonthsAndYears()
        {
            Assert.AreEqual(1, DailyStreak.DayNumber(new DateTime(2000, 1, 2)) - DailyStreak.DayNumber(new DateTime(2000, 1, 1)));
            Assert.AreEqual(1, DailyStreak.DayNumber(new DateTime(2027, 1, 1)) - DailyStreak.DayNumber(new DateTime(2026, 12, 31)));
            Assert.AreEqual(DailyStreak.DayNumber(new DateTime(2026, 10, 2, 0, 0, 1)), DailyStreak.DayNumber(new DateTime(2026, 10, 2, 23, 59, 59)));
        }

        [Test]
        public void StreakBirdUnlocksAfterThirtyConsecutiveDays()
        {
            var skin = Skins.Get(Skins.IndexOf("streak"));
            Assert.AreEqual(SkinUnlock.Streak, skin.Unlock);
            Assert.AreEqual(30, _cfg.StreakDaysForSkin);
            Assert.IsFalse(Skins.IsUnlocked(skin, 999, _cfg, true, 29));
            Assert.IsTrue(Skins.IsUnlocked(skin, 0, _cfg, false, 30));
            Assert.IsNull(skin.ProductId);
        }
    }
}
