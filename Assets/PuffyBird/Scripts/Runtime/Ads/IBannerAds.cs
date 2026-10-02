namespace PuffyBird.Ads
{
    /// <summary>Bannière publicitaire en bas de l'écran. La décision d'affichage vient de <c>AdPolicy</c>.</summary>
    public interface IBannerAds
    {
        void SetVisible(bool visible);
    }

    /// <summary>
    /// Vidéo récompensée, toujours choisie par le joueur (bouton CONTINUER de l'écran de fin).
    /// <c>onFinished(true)</c> quand la récompense est acquise, <c>false</c> sinon (vidéo fermée
    /// avant la fin, erreur d'affichage).
    /// </summary>
    public interface IRewardedAds
    {
        bool IsReady { get; }
        void Show(System.Action<bool> onFinished);
    }

    /// <summary>Pas de vidéo récompensée : le bouton CONTINUER n'apparaît pas.</summary>
    public sealed class NoRewardedAds : IRewardedAds
    {
        public bool IsReady => false;
        public void Show(System.Action<bool> onFinished) => onFinished(false);
    }

    /// <summary>Aucune régie configurée : rien ne s'affiche.</summary>
    public sealed class NoBannerAds : IBannerAds
    {
        public void SetVisible(bool visible) { }
    }

    /// <summary>
    /// Point d'accroche de la régie. L'intégration LevelPlay (<c>Assets/PuffyBird/Monetization</c>,
    /// hors assembly car le package n'a pas de nom d'assembly stable à référencer) s'y inscrit
    /// avant le chargement de la scène ; sans elle, le jeu tourne sans pub.
    /// </summary>
    public static class AdServices
    {
        /// <summary>Politique de confidentialité, ouverte depuis l'écran de consentement.</summary>
        public const string PrivacyPolicyUrl = "https://sylar78.github.io/puffybird-site/";

        public static System.Func<IBannerAds> BannerFactory;

        public static IBannerAds CreateBanner() => BannerFactory?.Invoke() ?? new NoBannerAds();

        public static System.Func<IRewardedAds> RewardedFactory;

        public static IRewardedAds CreateRewarded() => RewardedFactory?.Invoke() ?? new NoRewardedAds();

        /// <summary>Une régie est branchée : le consentement doit être demandé.</summary>
        public static bool AdsEnabled => BannerFactory != null;

        /// <summary>
        /// Choix du joueur sur l'écran de consentement : pubs personnalisées acceptées ou refusées,
        /// null tant qu'il n'a pas répondu. La régie attend la réponse avant de s'initialiser.
        /// </summary>
        public static bool? Consent { get; private set; }

        /// <summary>Le joueur a changé d'avis (paramètres > CONFIDENTIALITÉ).</summary>
        public static event System.Action<bool> ConsentChanged;

        public static void SetConsent(bool granted)
        {
            bool changed = Consent != granted;
            Consent = granted;
            if (changed) ConsentChanged?.Invoke(granted);
        }
    }
}
