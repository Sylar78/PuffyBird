namespace ElementalPhenix.Core
{
    /// <summary>Un sillage : traînée lumineuse derrière le phénix, débloquée avec les éclats d'élément. Purement visuel.</summary>
    public sealed class WakeInfo
    {
        public readonly string Id;
        /// <summary>Noms affichés, dans l'ordre de <see cref="Language"/>.</summary>
        readonly string[] _names;
        /// <summary>Éclats à dépenser pour le débloquer (0 : offert).</summary>
        public readonly int Cost;

        public WakeInfo(string id, string[] names, int cost)
        {
            Id = id;
            _names = names;
            Cost = cost;
        }

        public string NameIn(Language language) => _names[(int)language];
    }

    /// <summary>
    /// Catalogue des sillages. Les éclats ramassés en partie servent à les débloquer ; jamais
    /// d'achat avec de l'argent réel, aucun effet sur le jeu. Le premier (aucun sillage) est offert.
    /// </summary>
    public static class Wakes
    {
        public const string DefaultId = "none";

        static readonly WakeInfo[] All =
        {
            new WakeInfo(DefaultId, Names("AUCUN", "NONE", "NINGUNO", "KEINER", "NENHUM"), 0),
            new WakeInfo("embers", Names("BRAISES", "EMBERS", "BRASAS", "GLUT", "BRASAS"), 25),
            new WakeInfo("foam", Names("ÉCUME", "SEA FOAM", "ESPUMA", "GISCHT", "ESPUMA"), 60),
            new WakeInfo("aurora", Names("AURORE", "AURORA", "AURORA", "POLARLICHT", "AURORA"), 120),
            new WakeInfo("rainbow", Names("ARC-EN-CIEL", "RAINBOW", "ARCOÍRIS", "REGENBOGEN", "ARCO-ÍRIS"), 200),
            new WakeInfo("stars", Names("POUSSIÈRE D'ÉTOILES", "STARDUST", "POLVO DE ESTRELLAS", "STERNENSTAUB", "POEIRA ESTELAR"), 320),
        };

        static string[] Names(string french, string english, string spanish, string german, string portuguese)
            => new[] { french, english, spanish, german, portuguese };

        public static int Count => All.Length;

        public static WakeInfo Get(int index) => All[index >= 0 && index < All.Length ? index : 0];

        /// <summary>Indice du sillage, ou 0 (aucun) si l'identifiant est inconnu.</summary>
        public static int IndexOf(string id)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Id == id) return i;
            }
            return 0;
        }

        /// <param name="owned">Sillages débloqués, un bit par indice du catalogue.</param>
        public static bool IsOwned(int index, int owned) => Get(index).Cost == 0 || (owned & (1 << index)) != 0;

        /// <summary>
        /// Débloque le sillage <paramref name="index"/> si les éclats suffisent : retire son prix de
        /// <paramref name="shards"/> et ajoute son bit à <paramref name="owned"/>. Faux s'il est déjà à
        /// soi ou trop cher (rien ne change alors).
        /// </summary>
        public static bool TryUnlock(int index, ref int shards, ref int owned)
        {
            if (index < 0 || index >= All.Length || IsOwned(index, owned)) return false;
            int cost = All[index].Cost;
            if (shards < cost) return false;
            shards -= cost;
            owned |= 1 << index;
            return true;
        }
    }
}
