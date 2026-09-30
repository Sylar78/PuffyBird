using NUnit.Framework;
using PuffyBird.Core;

namespace PuffyBird.Tests
{
    /// <summary>La bannière n'apparaît jamais pendant une partie.</summary>
    public class AdPolicyTests
    {
        GameConfig _cfg;

        [SetUp]
        public void SetUp() => _cfg = GameConfig.CreateDefault();

        void Run(GameSimulation sim, float seconds)
        {
            for (int i = 0; i < (int)(seconds / _cfg.Step + 0.5f); i++) sim.Step();
        }

        [Test]
        public void BannerOnTitleOnlyUntilTheTap()
        {
            var sim = new GameSimulation(_cfg, new MemoryScoreStorage(), 1);
            Assert.IsTrue(AdPolicy.BannerVisible(sim, adsRemoved: false));
            Assert.IsFalse(AdPolicy.BannerVisible(sim, adsRemoved: true), "achat « Sans pub »");
            sim.Press();
            sim.Step();
            Assert.IsFalse(AdPolicy.BannerVisible(sim, false), "masquée dès le tap");
            Run(sim, _cfg.FadeTime + 0.05f);
            Assert.AreEqual(GameState.Ready, sim.State);
            Assert.IsFalse(AdPolicy.BannerVisible(sim, false));
        }

        [Test]
        public void NeverDuringPlayDyingOrPause()
        {
            var sim = new GameSimulation(_cfg, new MemoryScoreStorage(), 1, startOnTitle: false);
            sim.Press();
            sim.Step();
            Assert.AreEqual(GameState.Playing, sim.State);
            Assert.IsFalse(AdPolicy.BannerVisible(sim, false));
            sim.Pause();
            Assert.IsFalse(AdPolicy.BannerVisible(sim, false));
            sim.Resume();

            sim.Pipes.Spawn(_cfg.BirdCenterX - _cfg.PipeWidth * 0.5f, 300);
            sim.Step();
            Assert.AreEqual(GameState.Dying, sim.State);
            Assert.IsFalse(AdPolicy.BannerVisible(sim, false));
        }

        [Test]
        public void OnGameOverOnlyOnceTheScoreIsShown()
        {
            var sim = new GameSimulation(_cfg, new MemoryScoreStorage(), 1, startOnTitle: false);
            sim.Press();
            while (sim.State != GameState.Over) sim.Step();
            Assert.IsFalse(AdPolicy.BannerVisible(sim, false), "pas pendant la chute ni l'arrivée du panneau");
            Run(sim, _cfg.OverInputDelay + 0.05f);
            Assert.IsTrue(AdPolicy.BannerVisible(sim, false));
            sim.Press();
            sim.Step();
            Assert.IsFalse(AdPolicy.BannerVisible(sim, false), "masquée dès le tap qui relance");
        }
    }
}
