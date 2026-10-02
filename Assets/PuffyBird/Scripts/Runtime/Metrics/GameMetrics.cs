namespace PuffyBird.Metrics
{
    /// <summary>
    /// Mesure d'audience : quelques événements du jeu (mort, seconde chance, abandon, oiseau choisi).
    /// Les sessions, la rétention et les joueurs actifs sont mesurés par le service lui-même.
    /// Rien n'est envoyé sans l'accord du joueur (écran de consentement).
    /// </summary>
    public interface IGameMetrics
    {
        /// <param name="score">Score au moment de la mort.</param>
        /// <param name="theme">Décor de la partie.</param>
        /// <param name="seconds">Durée de jeu depuis le premier tap (ou depuis la seconde chance).</param>
        /// <param name="continued">Mort survenue après la seconde chance.</param>
        void PlayerDied(int score, string theme, float seconds, bool continued);
        void ContinueUsed(int score);
        void QuitToTitle(int score);
        void SkinSelected(string skinId);
    }

    /// <summary>Pas de service de mesure installé : rien n'est envoyé.</summary>
    public sealed class NoGameMetrics : IGameMetrics
    {
        public void PlayerDied(int score, string theme, float seconds, bool continued) { }
        public void ContinueUsed(int score) { }
        public void QuitToTitle(int score) { }
        public void SkinSelected(string skinId) { }
    }

    /// <summary>
    /// Point d'accroche du service (Unity Analytics, <c>Assets/PuffyBird/Monetization</c>, compilé
    /// seulement si le package est installé) ; sans lui, le jeu tourne sans mesure.
    /// </summary>
    public static class MetricsServices
    {
        public static System.Func<IGameMetrics> Factory;

        public static IGameMetrics Create() => Factory?.Invoke() ?? new NoGameMetrics();

        /// <summary>Un service de mesure est branché : le consentement doit être demandé.</summary>
        public static bool Enabled => Factory != null;
    }
}
