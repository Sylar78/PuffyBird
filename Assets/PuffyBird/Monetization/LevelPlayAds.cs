using System.Collections;
using PuffyBird.Ads;
using Unity.Services.LevelPlay;
using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace PuffyBird.Monetization
{
    /// <summary>
    /// Intégration LevelPlay (package « Ads Mediation »). Ordre imposé : réponse du joueur à l'écran
    /// de consentement du jeu (<see cref="AdServices.Consent"/>), demande de suivi iOS (ATT) s'il a
    /// accepté, réglages de confidentialité, puis <c>LevelPlay.Init</c> ; la bannière n'est créée
    /// qu'après une initialisation réussie. Ce fichier vit hors des assemblies du jeu (Assembly-CSharp),
    /// qui référence automatiquement le package ; il s'inscrit dans <see cref="AdServices"/>.
    /// </summary>
    sealed class LevelPlayAds : MonoBehaviour
    {
        const float RetryDelay = 60f;

        static LevelPlayAds _instance;

        readonly LevelPlayBanner _banner = new LevelPlayBanner();
        readonly LevelPlayRewarded _rewarded = new LevelPlayRewarded();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            if (!AdIds.Configured) return;
            var go = new GameObject("LevelPlay");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<LevelPlayAds>();
            AdServices.BannerFactory = () => _instance._banner;
            if (AdIds.RewardedAdUnitId.Length > 0) AdServices.RewardedFactory = () => _instance._rewarded;
        }

        IEnumerator Start()
        {
            yield return new WaitUntil(() => AdServices.Consent.HasValue);
            bool granted = AdServices.Consent.Value;
#if UNITY_IOS && !UNITY_EDITOR
            // Refus : pas de demande de suivi, l'identifiant publicitaire reste inaccessible.
            if (granted) yield return TrackingAuthorization.Request();
#endif
            // Public visé : 13 ans et plus (COPPA « Not directed »).
            ApplyConsent(granted);
            AdServices.ConsentChanged += OnConsentChanged;

            LevelPlay.OnInitSuccess += OnInitSuccess;
            LevelPlay.OnInitFailed += OnInitFailed;
            LevelPlay.Init(AdIds.AppKey);
        }

        /// <summary>Accord : pubs personnalisées. Refus : pubs non personnalisées, pas de vente des données (CCPA).</summary>
        static void ApplyConsent(bool granted)
        {
            LevelPlayPrivacySettings.SetGDPRConsent(granted);
            LevelPlayPrivacySettings.SetCCPA(!granted);
        }

        void OnConsentChanged(bool granted)
        {
            ApplyConsent(granted);
#if UNITY_IOS && !UNITY_EDITOR
            if (granted) StartCoroutine(TrackingAuthorization.Request());
#endif
        }

        void OnDestroy()
        {
            AdServices.ConsentChanged -= OnConsentChanged;
            LevelPlay.OnInitSuccess -= OnInitSuccess;
            LevelPlay.OnInitFailed -= OnInitFailed;
            _banner.Destroy();
            _rewarded.Destroy();
        }

        void OnInitSuccess(LevelPlayConfiguration configuration)
        {
            _banner.Create(AdIds.BannerAdUnitId, this);
            if (AdIds.RewardedAdUnitId.Length > 0) _rewarded.Create(AdIds.RewardedAdUnitId, this);
        }

        void OnInitFailed(LevelPlayInitError error)
        {
            Debug.LogWarning($"LevelPlay : initialisation impossible ({error.ErrorMessage}), pas de pub pour cette session.");
        }

        internal void RetryLater(System.Action action) => StartCoroutine(RetryAfterDelay(action));

        static IEnumerator RetryAfterDelay(System.Action action)
        {
            yield return new WaitForSecondsRealtime(RetryDelay);
            action();
        }
    }

    /// <summary>
    /// Bannière standard (320 × 50) en bas au centre, sous le sol du jeu. Elle se charge une fois,
    /// puis n'est que montrée ou cachée ; cachée, son rafraîchissement automatique est suspendu.
    /// </summary>
    sealed class LevelPlayBanner : IBannerAds
    {
        LevelPlayBannerAd _ad;
        LevelPlayAds _owner;
        bool _loaded;
        bool _wanted;
        bool _shown;

        public void Create(string adUnitId, LevelPlayAds owner)
        {
            _owner = owner;
            var config = new LevelPlayBannerAd.Config.Builder();
            config.SetSize(LevelPlayAdSize.BANNER);
            config.SetPosition(LevelPlayBannerPosition.BottomCenter);
            config.SetDisplayOnLoad(false);
            config.SetRespectSafeArea(true);
            _ad = new LevelPlayBannerAd(adUnitId, config.Build());
            _ad.OnAdLoaded += OnAdLoaded;
            _ad.OnAdLoadFailed += OnAdLoadFailed;
            _ad.OnAdDisplayFailed += OnAdDisplayFailed;
            _ad.LoadAd();
        }

        public void SetVisible(bool visible)
        {
            _wanted = visible;
            Apply();
        }

        public void Destroy()
        {
            if (_ad == null) return;
            _ad.OnAdLoaded -= OnAdLoaded;
            _ad.OnAdLoadFailed -= OnAdLoadFailed;
            _ad.OnAdDisplayFailed -= OnAdDisplayFailed;
            _ad.DestroyAd();
            _ad = null;
        }

        void Apply()
        {
            if (_ad == null || !_loaded || _wanted == _shown) return;
            _shown = _wanted;
            if (_shown)
            {
                _ad.ShowAd();
                _ad.ResumeAutoRefresh();
            }
            else
            {
                _ad.HideAd();
                _ad.PauseAutoRefresh();
            }
        }

        void OnAdLoaded(LevelPlayAdInfo info)
        {
            if (_loaded) return; // rafraîchissement automatique : déjà affichée ou cachée
            _loaded = true;
            _shown = false; // SetDisplayOnLoad(false) : chargée mais pas encore affichée
            _ad.PauseAutoRefresh();
            Apply();
        }

        void OnAdLoadFailed(LevelPlayAdError error)
        {
            if (_loaded) return; // l'ancienne bannière reste en place
            Debug.Log($"LevelPlay : bannière indisponible ({error.ErrorMessage}), nouvel essai plus tard.");
            _owner.RetryLater(() => _ad?.LoadAd());
        }

        void OnAdDisplayFailed(LevelPlayAdInfo info, LevelPlayAdError error)
        {
            Debug.Log($"LevelPlay : affichage de la bannière impossible ({error.ErrorMessage}).");
        }
    }

    /// <summary>
    /// Vidéo récompensée de la seconde chance : chargée après l'initialisation, rechargée après
    /// chaque affichage. Certaines régies annoncent la récompense après la fermeture de la vidéo :
    /// le résultat est donné au plus tard <see cref="RewardGrace"/> secondes après la fermeture.
    /// </summary>
    sealed class LevelPlayRewarded : IRewardedAds
    {
        const float RewardGrace = 0.5f;

        LevelPlayRewardedAd _ad;
        LevelPlayAds _owner;
        System.Action<bool> _onFinished;
        bool _rewarded;

        public bool IsReady => _ad != null && _onFinished == null && _ad.IsAdReady();

        public void Create(string adUnitId, LevelPlayAds owner)
        {
            _owner = owner;
            _ad = new LevelPlayRewardedAd(adUnitId);
            _ad.OnAdLoadFailed += OnAdLoadFailed;
            _ad.OnAdDisplayFailed += OnAdDisplayFailed;
            _ad.OnAdRewarded += OnAdRewarded;
            _ad.OnAdClosed += OnAdClosed;
            _ad.LoadAd();
        }

        public void Show(System.Action<bool> onFinished)
        {
            if (!IsReady)
            {
                onFinished(false);
                return;
            }
            _onFinished = onFinished;
            _rewarded = false;
            _ad.ShowAd();
        }

        public void Destroy()
        {
            if (_ad == null) return;
            _ad.OnAdLoadFailed -= OnAdLoadFailed;
            _ad.OnAdDisplayFailed -= OnAdDisplayFailed;
            _ad.OnAdRewarded -= OnAdRewarded;
            _ad.OnAdClosed -= OnAdClosed;
            _ad.DestroyAd();
            _ad = null;
        }

        void Finish()
        {
            var callback = _onFinished;
            _onFinished = null;
            callback?.Invoke(_rewarded);
        }

        void OnAdLoadFailed(LevelPlayAdError error)
        {
            Debug.Log($"LevelPlay : vidéo récompensée indisponible ({error.ErrorMessage}), nouvel essai plus tard.");
            _owner.RetryLater(() => _ad?.LoadAd());
        }

        void OnAdDisplayFailed(LevelPlayAdInfo info, LevelPlayAdError error)
        {
            Debug.Log($"LevelPlay : affichage de la vidéo récompensée impossible ({error.ErrorMessage}).");
            _rewarded = false;
            Finish();
            _ad?.LoadAd();
        }

        void OnAdRewarded(LevelPlayAdInfo info, LevelPlayReward reward)
        {
            _rewarded = true;
        }

        void OnAdClosed(LevelPlayAdInfo info)
        {
            _ad?.LoadAd();
            if (_rewarded) Finish();
            else _owner.StartCoroutine(FinishAfterGrace());
        }

        IEnumerator FinishAfterGrace()
        {
            yield return new WaitForSecondsRealtime(RewardGrace);
            Finish();
        }
    }

#if UNITY_IOS && !UNITY_EDITOR
    /// <summary>
    /// Demande d'autorisation de suivi (App Tracking Transparency, iOS 14.5+), une seule fois :
    /// le système ne repose pas la question. Pont natif : <c>Assets/Plugins/iOS/ATTRequester.mm</c>.
    /// </summary>
    static class TrackingAuthorization
    {
        const int NotDetermined = 0;

        delegate void Callback(int status);

        [DllImport("__Internal")] static extern int _ATT_GetStatus();
        [DllImport("__Internal")] static extern void _ATT_RequestPermission(Callback callback);

        static bool _done;

        [AOT.MonoPInvokeCallback(typeof(Callback))]
        static void OnAnswer(int status) => _done = true;

        public static IEnumerator Request()
        {
            if (_ATT_GetStatus() != NotDetermined) yield break;
            _done = false;
            _ATT_RequestPermission(OnAnswer);
            yield return new WaitUntil(() => _done);
        }
    }
#endif
}
