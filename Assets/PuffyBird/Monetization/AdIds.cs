namespace PuffyBird.Monetization
{
    /// <summary>
    /// Identifiants LevelPlay (tableau de bord platform.ironsrc.com : Apps pour l'App Key,
    /// Ad units pour la bannière). Chaque plateforme est une app distincte chez LevelPlay.
    /// Ces valeurs sont publiques (embarquées dans l'app), pas des secrets.
    /// Tant qu'elles sont vides, le jeu tourne sans pub.
    /// </summary>
    static class AdIds
    {
#if UNITY_IOS
        public const string AppKey = "286c6f005";
        public const string BannerAdUnitId = "mcpqptymic0vwr00";
#elif UNITY_ANDROID
        public const string AppKey = "";
        public const string BannerAdUnitId = "";
#else
        public const string AppKey = "";
        public const string BannerAdUnitId = "";
#endif

        public static bool Configured => AppKey.Length > 0 && BannerAdUnitId.Length > 0;
    }
}
