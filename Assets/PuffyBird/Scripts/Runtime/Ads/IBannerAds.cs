namespace PuffyBird.Ads
{
    /// <summary>Bannière publicitaire en bas de l'écran. La décision d'affichage vient de <c>AdPolicy</c>.</summary>
    public interface IBannerAds
    {
        void SetVisible(bool visible);
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
        public static System.Func<IBannerAds> BannerFactory;

        public static IBannerAds CreateBanner() => BannerFactory?.Invoke() ?? new NoBannerAds();
    }
}
