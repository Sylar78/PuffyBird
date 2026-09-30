namespace PuffyBird.Core
{
    /// <summary>
    /// Quand la publicité peut s'afficher (règles produit de CLAUDE.md) : jamais pendant une
    /// partie, rien dans la zone de jeu. La bannière, en bas de l'écran sous le sol, n'apparaît
    /// que sur l'écran titre et sur l'écran de fin, une fois le score affiché et le jeu prêt à
    /// relancer ; elle disparaît dès le tap qui lance la partie suivante.
    /// </summary>
    public static class AdPolicy
    {
        public static bool BannerVisible(GameSimulation sim, bool adsRemoved)
        {
            if (adsRemoved || sim.IsFadingOut) return false;
            switch (sim.State)
            {
                case GameState.Title:
                    return true;
                case GameState.Over:
                    return OverScreenTimeline.ButtonsVisible(sim.StateTime, sim.Config);
                default:
                    return false;
            }
        }
    }
}
