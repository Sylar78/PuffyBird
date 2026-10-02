using PuffyBird.Core;

namespace PuffyBird.Social
{
    /// <summary>
    /// Identifiants du classement en ligne et lien de partage. Valeurs publiques (ni clé ni secret).
    /// Mise en place côté Apple et Google : <c>docs/classement-en-ligne.md</c>.
    /// </summary>
    public static class SocialIds
    {
        /// <summary>ID du classement Game Center, à créer à l'identique dans App Store Connect.</summary>
        public const string GameCenterLeaderboard = "puffybird.best";

        /// <summary>
        /// ID du projet Play Games Services (nombre de la Play Console, « Configuration des services
        /// de jeux Play »). Vide : pas de Play Games, ni bibliothèque ni bouton sur Android.
        /// </summary>
        public const string PlayGamesAppId = "";

        /// <summary>ID du classement Play Games (de la forme <c>CgkI…</c>).</summary>
        public const string PlayGamesLeaderboard = "";

        /// <summary>Lien ajouté au message de partage du score.</summary>
        public const string ShareUrl = "https://sylar78.github.io/puffybird-site/";

        /// <summary>Préfixe des succès Game Center : <c>puffybird.ach.</c> + <see cref="AchievementTracker.Key"/>, à créer à l'identique dans App Store Connect.</summary>
        public const string GameCenterAchievementPrefix = "puffybird.ach.";

        /// <summary>
        /// IDs des succès Play Games (de la forme <c>CgkI…</c>), dans l'ordre de l'énumération
        /// <see cref="Achievement"/> : Score10, Score20, Score30, Score40, FirstStar, CloseCalls, Streak7,
        /// Streak30, DailyChallenge. Un ID vide : ce succès n'est pas envoyé sur Android.
        /// </summary>
        public static readonly string[] PlayGamesAchievements = { "", "", "", "", "", "", "", "", "" };

        public static string GameCenterAchievement(Achievement a) => GameCenterAchievementPrefix + AchievementTracker.Key(a);

        public static string PlayGamesAchievement(Achievement a)
        {
            int i = (int)a;
            return i < PlayGamesAchievements.Length ? PlayGamesAchievements[i] : "";
        }

        public static bool PlayGamesConfigured => PlayGamesAppId.Length > 0 && PlayGamesLeaderboard.Length > 0;
    }
}
