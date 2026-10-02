using NUnit.Framework;
using PuffyBird.Core;

namespace PuffyBird.Tests
{
    /// <summary>Défi du jour, succès et mesure de fluidité.</summary>
    public class DailyAchievementPerfTests
    {
        GameConfig _cfg;

        [SetUp]
        public void SetUp() => _cfg = GameConfig.CreateDefault();

        static void RunFade(GameSimulation sim)
        {
            // Un premier pas traite le tap en attente et lance le fondu ; les suivants le terminent.
            sim.Step();
            for (int i = 0; i < 60 && sim.IsFadingOut; i++) sim.Step();
            sim.Step();
        }

        /// <summary>Lance le défi, tape pour jouer et relève les ouvertures des premières paires.</summary>
        int[] DailyOpenings(uint simSeed, uint dailySeed, out Theme theme)
        {
            var sim = new GameSimulation(_cfg, new MemoryScoreStorage(), simSeed);
            sim.StartDaily(dailySeed);
            RunFade(sim);
            Assert.IsTrue(sim.IsDaily);
            theme = sim.Theme;
            sim.Press();
            sim.Step();
            var gaps = new System.Collections.Generic.List<int> { sim.Pipes[0].GapTop };
            for (int i = 0; i < 60 * 12 && gaps.Count < 5 && sim.State == GameState.Playing; i++)
            {
                if (AutoPilot.ShouldFlap(sim)) sim.Press();
                int count = sim.Pipes.Count;
                sim.Step();
                if (sim.Pipes.Count > count || (sim.Pipes.Count == count && sim.Pipes.Last.Id + 1 > gaps.Count && gaps.Count < sim.Pipes.Last.Id + 1))
                    gaps.Add(sim.Pipes.Last.GapTop);
            }
            return gaps.ToArray();
        }

        [Test]
        public void SameDailySeedGivesSameThemeAndOpeningsWhateverTheGameSeed()
        {
            var a = DailyOpenings(1u, 777u, out var themeA);
            var b = DailyOpenings(999u, 777u, out var themeB);
            Assert.AreEqual(themeA, themeB);
            Assert.GreaterOrEqual(a.Length, 3);
            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void DifferentDailySeedsGiveDifferentOpenings()
        {
            var a = DailyOpenings(1u, 777u, out _);
            var b = DailyOpenings(1u, 778u, out _);
            CollectionAssert.AreNotEqual(a, b);
        }

        [Test]
        public void RetryInDailyKeepsTheSameSeedUntilGoingHome()
        {
            var sim = new GameSimulation(_cfg, new MemoryScoreStorage(), 5u);
            sim.StartDaily(4242u);
            RunFade(sim);
            Assert.IsTrue(sim.IsDaily);
            var theme = sim.Theme;
            sim.Press();
            for (int i = 0; i < 600 && sim.State != GameState.Over; i++) sim.Step();
            Assert.AreEqual(GameState.Over, sim.State);
            for (int i = 0; i < 60; i++) sim.Step();
            sim.Press();
            RunFade(sim);
            Assert.IsTrue(sim.IsDaily);
            Assert.AreEqual(theme, sim.Theme);

            sim.Press();
            sim.Step();
            sim.QuitToTitle();
            RunFade(sim);
            Assert.AreEqual(GameState.Title, sim.State);
            Assert.IsFalse(sim.IsDaily);
        }

        [Test]
        public void DailyOnlyStartsFromTheTitle()
        {
            var sim = new GameSimulation(_cfg, new MemoryScoreStorage(), 1u, startOnTitle: false);
            sim.StartDaily(1u);
            RunFade(sim);
            Assert.IsFalse(sim.IsDaily);
        }

        [Test]
        public void DailySeedIsStableNonZeroAndDiffersByDay()
        {
            Assert.AreEqual(DailyChallenge.SeedForDay(9400), DailyChallenge.SeedForDay(9400));
            Assert.AreNotEqual(DailyChallenge.SeedForDay(9400), DailyChallenge.SeedForDay(9401));
            for (int d = 0; d < 5000; d++) Assert.AreNotEqual(0u, DailyChallenge.SeedForDay(d));
        }

        [Test]
        public void DailyBestIsOnlyKeptForTheSameDay()
        {
            Assert.AreEqual(12, DailyChallenge.BestFor(9400, 12, 9400));
            Assert.AreEqual(0, DailyChallenge.BestFor(9399, 12, 9400));
            Assert.AreEqual(0, DailyChallenge.BestFor(0, 0, 9400));
        }

        [Test]
        public void AchievementsUnlockOnceAndAreReportedOnce()
        {
            var t = new AchievementTracker();
            Assert.IsNull(t.NextToReport());
            Assert.IsTrue(t.Unlock(Achievement.FirstStar));
            Assert.IsFalse(t.Unlock(Achievement.FirstStar));
            Assert.AreEqual(Achievement.FirstStar, t.NextToReport());
            t.MarkReported(Achievement.FirstStar);
            Assert.IsNull(t.NextToReport());
        }

        [Test]
        public void AchievementsSurviveBeingSavedAndReloaded()
        {
            var t = new AchievementTracker();
            t.Unlock(Achievement.Score10);
            t.Unlock(Achievement.Streak7);
            t.MarkReported(Achievement.Score10);
            var loaded = new AchievementTracker(t.Unlocked, t.Reported);
            Assert.IsTrue(loaded.IsUnlocked(Achievement.Streak7));
            Assert.AreEqual(Achievement.Streak7, loaded.NextToReport());
        }

        [Test]
        public void ScoreAndStreakAchievementsFollowTheirThresholds()
        {
            var t = new AchievementTracker();
            t.UnlockForScore(9, _cfg);
            Assert.AreEqual(0, t.Unlocked);
            t.UnlockForScore(25, _cfg);
            Assert.IsTrue(t.IsUnlocked(Achievement.Score10));
            Assert.IsTrue(t.IsUnlocked(Achievement.Score20));
            Assert.IsFalse(t.IsUnlocked(Achievement.Score30));
            t.UnlockForStreak(6, _cfg);
            Assert.IsFalse(t.IsUnlocked(Achievement.Streak7));
            t.UnlockForStreak(30, _cfg);
            Assert.IsTrue(t.IsUnlocked(Achievement.Streak7));
            Assert.IsTrue(t.IsUnlocked(Achievement.Streak30));
        }

        [Test]
        public void AchievementKeysAreUniqueAndFitTheBitMask()
        {
            Assert.LessOrEqual(AchievementTracker.Count, 31);
            var keys = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < AchievementTracker.Count; i++) Assert.IsTrue(keys.Add(AchievementTracker.Key((Achievement)i)));
        }

        [Test]
        public void SlowFramesTriggerADropOnlyAfterTwoBadWindows()
        {
            var m = new PerformanceMonitor(60f);
            bool dropped = false;
            int frames = 0;
            // 30 images par seconde : deux fenêtres de 3 s.
            for (; frames < 30 * 3 && !dropped; frames++) dropped = m.Record(1f / 30f);
            Assert.IsFalse(dropped, "une seule fenêtre ne suffit pas");
            for (; frames < 30 * 7 && !dropped; frames++) dropped = m.Record(1f / 30f);
            Assert.IsTrue(dropped);
            Assert.AreEqual(30f, m.LastFps, 1f);
        }

        [Test]
        public void SmoothPlayNeverTriggersADrop()
        {
            var m = new PerformanceMonitor(60f);
            for (int i = 0; i < 60 * 60; i++) Assert.IsFalse(m.Record(1f / 60f));
        }

        [Test]
        public void OneBadWindowFollowedByAGoodOneResetsTheCount()
        {
            var m = new PerformanceMonitor(60f);
            for (int i = 0; i < 90; i++) m.Record(1f / 30f);
            for (int i = 0; i < 180; i++) m.Record(1f / 60f);
            for (int i = 0; i < 90; i++) Assert.IsFalse(m.Record(1f / 30f));
        }

        [Test]
        public void HugeFramesAreIgnored()
        {
            var m = new PerformanceMonitor(60f);
            for (int i = 0; i < 100; i++) Assert.IsFalse(m.Record(2f));
            Assert.AreEqual(0f, m.LastFps);
        }
    }
}
