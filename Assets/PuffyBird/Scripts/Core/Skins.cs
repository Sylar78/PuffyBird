namespace PuffyBird.Core
{
    /// <summary>Comment un oiseau se débloque.</summary>
    public enum SkinUnlock
    {
        /// <summary>Oiseau de départ.</summary>
        Free,
        /// <summary>Meilleur score atteignant le seuil d'une médaille.</summary>
        Medal,
        /// <summary>Achat intégré (cosmétique seulement).</summary>
        Purchase,
        /// <summary>Série de jours consécutifs (<see cref="GameConfig.StreakDaysForSkin"/>).</summary>
        Streak,
    }

    /// <summary>Un oiseau du catalogue : apparence seulement, aucune influence sur le jeu.</summary>
    public sealed class SkinInfo
    {
        public readonly string Id;
        /// <summary>Noms affichés, dans l'ordre de <see cref="Language"/>.</summary>
        readonly string[] _names;
        public readonly SkinUnlock Unlock;
        /// <summary>Médaille à obtenir (meilleur score) si <see cref="Unlock"/> vaut <see cref="SkinUnlock.Medal"/>.</summary>
        public readonly Medal Medal;

        public SkinInfo(string id, string[] names, SkinUnlock unlock, Medal medal = Medal.None)
        {
            Id = id;
            _names = names;
            Unlock = unlock;
            Medal = medal;
        }

        /// <summary>Nom de l'oiseau dans la langue donnée.</summary>
        public string NameIn(Language language) => _names[(int)language];

        /// <summary>Identifiant du produit dans les boutiques, ou null si l'oiseau ne s'achète pas.</summary>
        public string ProductId => Unlock == SkinUnlock.Purchase ? Products.SkinPrefix + Id : null;
    }

    /// <summary>Identifiants des achats intégrés, identiques sur l'App Store et Google Play.</summary>
    public static class Products
    {
        /// <summary>Retrait des bannières publicitaires (non consommable).</summary>
        public const string NoAds = "fr.puffybird.app.noads";
        public const string SkinPrefix = "fr.puffybird.app.skin.";
    }

    /// <summary>
    /// Catalogue des oiseaux, tous des phénix de plumages différents : deux gratuits (le phénix de départ et l'azur), un par médaille (débloqué dès que le meilleur score
    /// atteint son seuil, pour toujours) et quelques oiseaux payants.
    /// </summary>
    public static class Skins
    {
        /// <summary>Oiseau de départ : le phénix. Un joueur qui avait choisi un autre oiseau le garde.</summary>
        public const string DefaultId = "phoenix";

        // Les identifiants datent des premiers oiseaux (bleu, cerise, ninja…) : ils sont gardés pour
        // les choix enregistrés et les produits des boutiques, seuls les noms affichés ont changé.

        static readonly SkinInfo[] All =
        {
            new SkinInfo(DefaultId, Names("PHÉNIX", "PHOENIX", "FÉNIX", "PHÖNIX", "FÊNIX"), SkinUnlock.Free),
            new SkinInfo("blue", Names("AZUR", "AZURE", "AZUL", "AZUR", "AZUL"), SkinUnlock.Free),
            new SkinInfo("cherry", Names("BRAISE", "EMBER", "ASCUA", "GLUT", "BRASA"), SkinUnlock.Medal, Medal.Bronze),
            new SkinInfo("mint", Names("JADE", "JADE", "JADE", "JADE", "JADE"), SkinUnlock.Medal, Medal.Silver),
            new SkinInfo("cool", Names("AURORE", "DAWN", "AURORA", "MORGENROT", "AURORA"), SkinUnlock.Medal, Medal.Gold),
            new SkinInfo("pearl", Names("NACRE", "PEARL", "NÁCAR", "PERLMUTT", "PÉROLA"), SkinUnlock.Medal, Medal.Platinum),
            new SkinInfo("streak", Names("ÉCLIPSE", "ECLIPSE", "ECLIPSE", "ECLIPSE", "ECLIPSE"), SkinUnlock.Streak),
            new SkinInfo("ninja", Names("OMBRE", "SHADOW", "SOMBRA", "SCHATTEN", "SOMBRA"), SkinUnlock.Purchase),
            new SkinInfo("robot", Names("CHROME", "CHROME", "CROMO", "CHROM", "CROMO"), SkinUnlock.Purchase),
            new SkinInfo("galaxy", Names("COSMOS", "COSMOS", "COSMOS", "KOSMOS", "COSMOS"), SkinUnlock.Purchase),
        };

        /// <summary>Noms dans l'ordre de <see cref="Language"/> : français, anglais, espagnol, allemand, portugais.</summary>
        static string[] Names(string french, string english, string spanish, string german, string portuguese)
            => new[] { french, english, spanish, german, portuguese };

        public static int Count => All.Length;

        public static SkinInfo Get(int index) => All[index >= 0 && index < All.Length ? index : 0];

        /// <summary>Indice de l'oiseau, ou 0 (oiseau de départ) si l'identifiant est inconnu.</summary>
        public static int IndexOf(string id)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Id == id) return i;
            }
            return 0;
        }

        /// <summary>Meilleur score à atteindre pour une médaille.</summary>
        public static int RequiredScore(Medal medal, GameConfig cfg)
        {
            switch (medal)
            {
                case Medal.Bronze: return cfg.MedalBronze;
                case Medal.Silver: return cfg.MedalSilver;
                case Medal.Gold: return cfg.MedalGold;
                case Medal.Platinum: return cfg.MedalPlatinum;
                default: return 0;
            }
        }

        /// <param name="purchased">Le produit de l'oiseau a été acheté (sans effet sur les autres oiseaux).</param>
        /// <param name="longestStreak">Plus longue série de jours consécutifs jamais atteinte.</param>
        public static bool IsUnlocked(SkinInfo skin, int best, GameConfig cfg, bool purchased, int longestStreak = 0)
        {
            switch (skin.Unlock)
            {
                case SkinUnlock.Free: return true;
                case SkinUnlock.Medal: return best >= RequiredScore(skin.Medal, cfg);
                case SkinUnlock.Streak: return longestStreak >= cfg.StreakDaysForSkin;
                default: return purchased;
            }
        }

        /// <summary>
        /// Nombre d'oiseaux à médaille que le score <paramref name="score"/> débloque alors que
        /// l'ancien meilleur score <paramref name="previousBest"/> ne les débloquait pas.
        /// </summary>
        public static int NewlyUnlocked(int previousBest, int score, GameConfig cfg)
        {
            int count = 0;
            foreach (var skin in All)
            {
                if (skin.Unlock != SkinUnlock.Medal) continue;
                int required = RequiredScore(skin.Medal, cfg);
                if (previousBest < required && score >= required) count++;
            }
            return count;
        }
    }
}
