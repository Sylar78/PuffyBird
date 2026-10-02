namespace PuffyBird.Monetization
{
    /// <summary>
    /// Identifiants LevelPlay (tableau de bord platform.ironsrc.com : Apps pour l'App Key,
    /// Ad units pour la bannière et la vidéo récompensée). Chaque plateforme est une app distincte
    /// chez LevelPlay.
    /// Ces valeurs sont publiques (embarquées dans l'app), pas des secrets.
    /// Tant qu'elles sont vides, le jeu tourne sans pub ; sans ID de vidéo récompensée, pas de
    /// bouton CONTINUER.
    /// </summary>
    static class AdIds
    {
#if UNITY_IOS
        public const string AppKey = "286c6f005";
        public const string BannerAdUnitId = "mcpqptymic0vwr00";
        public const string RewardedAdUnitId = "f7f01pntxarp2toz"; // « Puffy Rewarded Video 1 »
#elif UNITY_ANDROID
        public const string AppKey = "2871b6fc5";
        public const string BannerAdUnitId = "35omvjrd6xncoif3"; // « Puffy Banner 1 »
        public const string RewardedAdUnitId = "60abel8gnmr1ppf1"; // « Puffy Rewarded Video 1 »
#else
        public const string AppKey = "";
        public const string BannerAdUnitId = "";
        public const string RewardedAdUnitId = "";
#endif

        public static bool Configured => AppKey.Length > 0 && BannerAdUnitId.Length > 0;
    }
}
