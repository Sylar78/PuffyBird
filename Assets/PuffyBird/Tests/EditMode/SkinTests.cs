using System.Collections.Generic;
using NUnit.Framework;
using PuffyBird.Core;

namespace PuffyBird.Tests
{
    /// <summary>Oiseaux à débloquer : médailles du meilleur score et achats cosmétiques.</summary>
    public class SkinTests
    {
        GameConfig _cfg;

        [SetUp]
        public void SetUp() => _cfg = GameConfig.CreateDefault();

        [Test]
        public void IdsAreUniqueAndDefaultIsFirst()
        {
            var ids = new HashSet<string>();
            for (int i = 0; i < Skins.Count; i++) Assert.IsTrue(ids.Add(Skins.Get(i).Id), Skins.Get(i).Id);
            Assert.AreEqual(Skins.DefaultId, Skins.Get(0).Id);
            Assert.AreEqual(SkinUnlock.Free, Skins.Get(0).Unlock);
            Assert.AreEqual(0, Skins.IndexOf("inconnu"));
        }

        [Test]
        public void OneSkinPerMedal()
        {
            foreach (Medal medal in new[] { Medal.Bronze, Medal.Silver, Medal.Gold, Medal.Platinum })
            {
                int count = 0;
                for (int i = 0; i < Skins.Count; i++)
                {
                    var skin = Skins.Get(i);
                    if (skin.Unlock == SkinUnlock.Medal && skin.Medal == medal) count++;
                }
                Assert.AreEqual(1, count, medal.ToString());
            }
        }

        [Test]
        public void MedalSkinsFollowBestScore()
        {
            var bronze = Skins.Get(Skins.IndexOf("cherry"));
            var platinum = Skins.Get(Skins.IndexOf("pearl"));
            Assert.IsFalse(Skins.IsUnlocked(bronze, 9, _cfg, false));
            Assert.IsTrue(Skins.IsUnlocked(bronze, 10, _cfg, false));
            Assert.IsFalse(Skins.IsUnlocked(platinum, 39, _cfg, false));
            Assert.IsTrue(Skins.IsUnlocked(platinum, 40, _cfg, false));
        }

        [Test]
        public void PaidSkinsNeedTheirPurchaseOnly()
        {
            var ninja = Skins.Get(Skins.IndexOf("ninja"));
            Assert.AreEqual("fr.puffybird.app.skin.ninja", ninja.ProductId);
            Assert.IsFalse(Skins.IsUnlocked(ninja, 999, _cfg, false));
            Assert.IsTrue(Skins.IsUnlocked(ninja, 0, _cfg, true));
            Assert.IsNull(Skins.Get(0).ProductId);
        }

        [Test]
        public void NewlyUnlockedCountsCrossedThresholds()
        {
            Assert.AreEqual(0, Skins.NewlyUnlocked(5, 9, _cfg));
            Assert.AreEqual(1, Skins.NewlyUnlocked(5, 10, _cfg));
            Assert.AreEqual(2, Skins.NewlyUnlocked(9, 25, _cfg));
            Assert.AreEqual(0, Skins.NewlyUnlocked(25, 22, _cfg));
            Assert.AreEqual(4, Skins.NewlyUnlocked(0, 40, _cfg));
        }
    }
}
