#if PUFFYBIRD_ANALYTICS
using System.Collections;
using PuffyBird.Ads;
using PuffyBird.Metrics;
using Unity.Services.Analytics;
using Unity.Services.Core;
using UnityEngine;
#if UNITY_6000_2_OR_NEWER
using UnityEngine.UnityConsent;
#endif

namespace PuffyBird.Monetization
{
    /// <summary>
    /// Mesure d'audience par Unity Analytics (package <c>com.unity.services.analytics</c>, compilé
    /// sous le symbole <c>PUFFYBIRD_ANALYTICS</c>). La collecte ne démarre qu'avec l'accord du joueur
    /// sur l'écran de consentement (<see cref="AdServices.Consent"/>) et s'arrête s'il le retire.
    /// Les événements personnalisés doivent exister dans le tableau de bord (voir
    /// <c>docs/mesure-audience.md</c>).
    /// </summary>
    sealed class UnityAnalyticsMetrics : MonoBehaviour, IGameMetrics
    {
        static UnityAnalyticsMetrics _instance;

        bool _ready;
        bool _collecting;
        bool _applied;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            var go = new GameObject("Unity Analytics");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<UnityAnalyticsMetrics>();
            MetricsServices.Factory = () => _instance;
        }

        IEnumerator Start()
        {
            // Consentement d'abord (écran du premier lancement) : avec Unity 6.2 et plus, la collecte
            // démarre dès l'initialisation si l'accord est déjà déclaré.
            yield return new WaitUntil(() => AdServices.Consent.HasValue);
            var init = UnityServices.InitializeAsync();
            yield return new WaitUntil(() => init.IsCompleted);
            if (init.IsFaulted)
            {
                Debug.LogWarning($"Unity Analytics : initialisation impossible ({init.Exception?.GetBaseException().Message}), pas de mesure pour cette session.");
                yield break;
            }
            _ready = true;
            Apply(AdServices.Consent.Value);
            AdServices.ConsentChanged += Apply;
        }

        void OnDestroy()
        {
            AdServices.ConsentChanged -= Apply;
        }

        void Apply(bool granted)
        {
            // Toujours appliqué la première fois : un accord retiré doit l'emporter sur l'état gardé par Unity.
            if (!_ready || (_applied && granted == _collecting)) return;
            _applied = true;
            _collecting = granted;
#if UNITY_6000_2_OR_NEWER
            // Unity 6.2 et plus (Analytics 6.1+) : la collecte suit le consentement déclaré au
            // module UnityConsent ; StartDataCollection / StopDataCollection n'existent plus.
            var state = EndUserConsent.GetConsentState();
            state.AnalyticsIntent = granted ? ConsentStatus.Granted : ConsentStatus.Denied;
            EndUserConsent.SetConsentState(state);
#else
            if (granted) AnalyticsService.Instance.StartDataCollection();
            else AnalyticsService.Instance.StopDataCollection();
#endif
        }

        public void PlayerDied(int score, string theme, float seconds, bool continued)
        {
            if (!_collecting) return;
            AnalyticsService.Instance.RecordEvent(new CustomEvent("playerDied")
            {
                { "score", score },
                { "theme", theme },
                { "seconds", seconds },
                { "continued", continued },
            });
        }

        public void ContinueUsed(int score)
        {
            if (!_collecting) return;
            AnalyticsService.Instance.RecordEvent(new CustomEvent("continueUsed") { { "score", score } });
        }

        public void QuitToTitle(int score)
        {
            if (!_collecting) return;
            AnalyticsService.Instance.RecordEvent(new CustomEvent("quitToTitle") { { "score", score } });
        }

        public void SkinSelected(string skinId)
        {
            if (!_collecting) return;
            AnalyticsService.Instance.RecordEvent(new CustomEvent("skinSelected") { { "skinId", skinId } });
        }
    }
}
#endif
