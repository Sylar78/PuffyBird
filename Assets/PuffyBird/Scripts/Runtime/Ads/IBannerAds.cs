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
}
