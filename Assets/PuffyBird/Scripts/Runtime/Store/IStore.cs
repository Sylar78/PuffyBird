namespace PuffyBird.Store
{
    /// <summary>
    /// Achats intégrés (cosmétiques et retrait des pubs uniquement). L'intégration Unity IAP
    /// (<c>Assets/PuffyBird/Monetization</c>, hors assembly) s'inscrit dans <see cref="StoreServices"/> ;
    /// sans elle, rien n'est à vendre.
    /// </summary>
    public interface IStore
    {
        /// <summary>Boutique joignable et produits chargés : on peut acheter.</summary>
        bool Ready { get; }

        /// <summary>Augmente à chaque changement (achat, restauration, prix reçus) : les vues se mettent à jour.</summary>
        int Version { get; }

        bool Owns(string productId);

        /// <summary>Prix localisé fourni par la boutique (« 0,99 € »), ou null s'il n'est pas connu.</summary>
        string Price(string productId);

        void Buy(string productId);

        /// <summary>Restaure les achats (bouton exigé par Apple ; automatique sur Android).</summary>
        void Restore();
    }

    /// <summary>Pas de boutique : rien à vendre, rien d'acheté.</summary>
    public sealed class NoStore : IStore
    {
        public bool Ready => false;
        public int Version => 0;
        public bool Owns(string productId) => false;
        public string Price(string productId) => null;
        public void Buy(string productId) { }
        public void Restore() { }
    }

    public static class StoreServices
    {
        public static System.Func<IStore> Factory;

        public static IStore Create() => Factory?.Invoke() ?? new NoStore();
    }
}
