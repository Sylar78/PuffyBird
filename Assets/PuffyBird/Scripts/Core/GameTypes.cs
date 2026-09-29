using System;

namespace PuffyBird.Core
{
    /// <summary>États de la machine de jeu (§5).</summary>
    public enum GameState
    {
        Title,
        Ready,
        Playing,
        Dying,
        Over,
        Paused,
    }

    public enum BirdColor
    {
        Yellow,
        Red,
        Blue,
    }

    public enum Theme
    {
        Day,
        Night,
    }

    public enum Medal
    {
        None,
        Bronze,
        Silver,
        Gold,
        Platinum,
    }

    /// <summary>
    /// Événements émis par la simulation pendant un ou plusieurs pas, consommés
    /// une fois par image par l'audio et le rendu.
    /// </summary>
    [Flags]
    public enum GameEvents
    {
        None = 0,
        Flap = 1 << 0,
        Point = 1 << 1,
        Hit = 1 << 2,
        Die = 1 << 3,
        Swoosh = 1 << 4,
        NewRun = 1 << 5,
        StateChanged = 1 << 6,
    }

    public static class Medals
    {
        public static Medal For(int score, GameConfig cfg)
        {
            if (score >= cfg.MedalPlatinum) return Medal.Platinum;
            if (score >= cfg.MedalGold) return Medal.Gold;
            if (score >= cfg.MedalSilver) return Medal.Silver;
            if (score >= cfg.MedalBronze) return Medal.Bronze;
            return Medal.None;
        }
    }
}
