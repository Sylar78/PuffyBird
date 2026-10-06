using System.Collections.Generic;
using NUnit.Framework;
using PuffyBird.Core;

namespace PuffyBird.Tests
{
    /// <summary>Oiseaux à débloquer : médailles du meilleur score, série de jours et achats cosmétiques.</summary>
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
        public void EverySkinHasAnUppercaseNameInEveryLanguage()
        {
            // La police du jeu n'a que des capitales : un nom en minuscules serait rendu en capitales
            // par VoxelFont, mais un nom vide ou manquant ferait un bouton sans titre.
            for (int i = 0; i < Skins.Count; i++)
            {
                foreach (Language language in System.Enum.GetValues(typeof(Language)))
                {
                    string name = Skins.Get(i).NameIn(language);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(name), $"{Skins.Get(i).Id} / {language}");
                    Assert.AreEqual(name.ToUpperInvariant(), name, $"{Skins.Get(i).Id} / {language}");
                    Assert.LessOrEqual(name.Length, 9, $"{Skins.Get(i).Id} / {language} : trop long pour le menu");
                }
            }
        }

        [Test]
        public void FirePhoenixIsTheOnlyFreeBird()
        {
            // Aucun oiseau choisi (préférence vide) : le phénix de feu, seul oiseau offert.
            Assert.AreEqual("phoenix", Skins.Get(Skins.IndexOf("")).Id);
            Assert.IsNull(Skins.Get(0).ProductId);
            for (int i = 1; i < Skins.Count; i++) Assert.AreNotEqual(SkinUnlock.Free, Skins.Get(i).Unlock, Skins.Get(i).Id);
        }

        [Test]
        public void FiveElementsWithEveryUnlockKind()
        {
            Assert.AreEqual(5, Skins.Count);
            int medals = 0, streak = 0, paid = 0;
            var used = new HashSet<Medal>();
            for (int i = 0; i < Skins.Count; i++)
            {
                var skin = Skins.Get(i);
                if (skin.Unlock == SkinUnlock.Medal) { medals++; Assert.IsTrue(used.Add(skin.Medal), skin.Id); }
                if (skin.Unlock == SkinUnlock.Streak) streak++;
                if (skin.Unlock == SkinUnlock.Purchase) paid++;
            }
            Assert.AreEqual(2, medals);
            Assert.AreEqual(1, streak, "un oiseau pour la série de jours");
            Assert.AreEqual(1, paid);
        }

        [Test]
        public void MedalSkinsFollowBestScore()
        {
            var water = Skins.Get(Skins.IndexOf("blue"));
            var wind = Skins.Get(Skins.IndexOf("mint"));
            Assert.IsFalse(Skins.IsUnlocked(water, 9, _cfg, false));
            Assert.IsTrue(Skins.IsUnlocked(water, 10, _cfg, false));
            Assert.IsFalse(Skins.IsUnlocked(wind, 29, _cfg, false));
            Assert.IsTrue(Skins.IsUnlocked(wind, 30, _cfg, false));
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
            Assert.AreEqual(1, Skins.NewlyUnlocked(9, 25, _cfg));
            Assert.AreEqual(0, Skins.NewlyUnlocked(25, 22, _cfg));
            Assert.AreEqual(2, Skins.NewlyUnlocked(0, 40, _cfg));
        }
    }
}
