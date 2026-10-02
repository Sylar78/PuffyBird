#if PUFFYBIRD_IAP
using System;
using System.Collections.Generic;
using PuffyBird.Core;
using PuffyBird.Store;
using UnityEngine;
using UnityEngine.Purchasing;

namespace PuffyBird.Monetization
{
    /// <summary>
    /// Achats intégrés par Unity IAP 5 (App Store et Google Play) : « Sans pub » et oiseaux
    /// payants, tous non consommables. Compilé seulement si le package est installé (symbole
    /// <c>PUFFYBIRD_IAP</c>, posé par <c>Editor/PackageDefines</c>) ; s'inscrit dans
    /// <see cref="StoreServices"/> avant le chargement de la scène.
    /// Achat : la boutique annonce une commande en attente, le jeu l'enregistre (PlayerPrefs
    /// <c>puffybird.owned</c>, pour jouer hors ligne) puis seulement la confirme ; une commande non
    /// confirmée revient au lancement suivant. Les achats déjà faits reviennent par FetchPurchases
    /// (et RESTORE sur iOS) ; un remboursement Apple les retire.
    /// </summary>
    sealed class UnityIapStore : IStore
    {
        const string OwnedKey = "puffybird.owned";

        static UnityIapStore _instance;

        readonly HashSet<string> _owned = new HashSet<string>();
        readonly Dictionary<string, string> _prices = new Dictionary<string, string>();
        StoreController _store;
        bool _ready;
        int _version;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            _instance = new UnityIapStore();
            StoreServices.Factory = () => _instance;
            _instance.Start();
        }

        UnityIapStore()
        {
            foreach (string id in PlayerPrefs.GetString(OwnedKey, "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                _owned.Add(id);
            }
        }

        public bool Ready => _ready;
        public int Version => _version;
        public bool Owns(string productId) => productId != null && _owned.Contains(productId);
        public string Price(string productId) => productId != null && _prices.TryGetValue(productId, out var price) ? price : null;

        public void Buy(string productId)
        {
            if (!_ready || Owns(productId)) return;
            _store.PurchaseProduct(productId);
        }

        public void Restore()
        {
            if (_store == null) return;
            _store.RestoreTransactions((success, error) =>
            {
                if (!success) Debug.LogWarning($"PuffyBird : restauration des achats impossible ({error}).");
            });
        }

        static List<ProductDefinition> Catalog()
        {
            var products = new List<ProductDefinition> { new ProductDefinition(Products.NoAds, ProductType.NonConsumable) };
            for (int i = 0; i < Skins.Count; i++)
            {
                string id = Skins.Get(i).ProductId;
                if (id != null) products.Add(new ProductDefinition(id, ProductType.NonConsumable));
            }
            return products;
        }

        async void Start()
        {
            try
            {
                _store = UnityIAPServices.StoreController();

                // Tous les événements avant Connect : une commande en attente peut arriver aussitôt.
                _store.OnPurchasePending += OnPurchasePending;
                _store.OnPurchaseConfirmed += OnPurchaseConfirmed;
                _store.OnPurchaseFailed += failed => Debug.Log($"PuffyBird : achat non abouti ({failed.FailureReason} : {failed.Details}).");
                _store.OnPurchaseDeferred += deferred => Debug.Log("PuffyBird : achat en attente d'approbation.");
                _store.OnStoreConnected += OnStoreConnected;
                _store.OnStoreDisconnected += failure =>
                {
                    _ready = false;
                    _version++;
                    Debug.LogWarning($"PuffyBird : boutique indisponible ({failure.Message}).");
                };
                _store.OnProductsFetched += OnProductsFetched;
                _store.OnProductsFetchFailed += failure => Debug.LogWarning($"PuffyBird : produits indisponibles ({failure.FailureReason}).");
                _store.OnPurchasesFetched += OnPurchasesFetched;
                _store.OnPurchasesFetchFailed += failure => Debug.LogWarning($"PuffyBird : achats passés illisibles ({failure.Message}).");
                if (_store.AppleStoreExtendedPurchaseService != null)
                {
                    _store.AppleStoreExtendedPurchaseService.OnEntitlementRevoked += OnEntitlementRevoked;
                }

                await _store.Connect();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"PuffyBird : boutique indisponible ({e.Message}).");
            }
        }

        void OnStoreConnected()
        {
            _store.FetchProducts(Catalog());
            _store.FetchPurchases();
        }

        void OnProductsFetched(List<Product> products)
        {
            foreach (var product in products)
            {
                string price = product.metadata?.localizedPriceString;
                if (!string.IsNullOrEmpty(price)) _prices[product.definition.id] = price;
            }
            _ready = true;
            _version++;
        }

        void OnPurchasesFetched(Orders orders)
        {
            foreach (var order in orders.ConfirmedOrders) Grant(order);
        }

        void OnPurchasePending(PendingOrder order)
        {
            // Enregistré avant la confirmation : en cas d'échec de l'enregistrement, la commande
            // reste en attente et reviendra. Accorder deux fois le même produit ne change rien.
            Grant(order);
            _store.ConfirmPurchase(order);
        }

        void OnPurchaseConfirmed(Order order)
        {
            if (order is FailedOrder failed) Debug.LogWarning($"PuffyBird : confirmation impossible ({failed.FailureReason} : {failed.Details}).");
        }

        void OnEntitlementRevoked(string productId)
        {
            if (!_owned.Remove(productId)) return;
            Save();
        }

        void Grant(Order order)
        {
            bool changed = false;
            foreach (var item in order.CartOrdered.Items())
            {
                string id = item.Product?.definition?.id;
                if (id != null && _owned.Add(id)) changed = true;
            }
            if (changed) Save();
        }

        void Save()
        {
            PlayerPrefs.SetString(OwnedKey, string.Join(";", _owned));
            PlayerPrefs.Save();
            _version++;
        }
    }
}
#endif
