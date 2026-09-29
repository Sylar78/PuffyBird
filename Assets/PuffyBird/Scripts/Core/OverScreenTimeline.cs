using System;

namespace PuffyBird.Core
{
    /// <summary>
    /// Chronologie de l'écran de fin (§11.4, §12.1), en fonction du temps passé dans l'état
    /// OVER. Positions en pixels logiques.
    /// </summary>
    public static class OverScreenTimeline
    {
        public const float TitleStartY = 60f;
        public const float TitleEndY = 110f;
        public const float TitleSpeed = 300f;
        public const float PanelDelay = 0.3f;
        public const float PanelEndY = 180f;
        public const float PanelSpeed = 900f;
        public const float CountDelay = 0.6f;

        public static float TitleY(float t) => Math.Min(TitleEndY, TitleStartY + t * TitleSpeed);

        public static bool PanelVisible(float t) => t >= PanelDelay;

        public static float PanelY(float t, GameConfig cfg) => Math.Max(PanelEndY, cfg.Height - (t - PanelDelay) * PanelSpeed);

        /// <summary>Le score affiché compte de 0 jusqu'au score final à 30 points/s.</summary>
        public static int ShownScore(float t, int score, GameConfig cfg)
        {
            int counted = (int)Math.Floor(Math.Max(0f, t - CountDelay) * cfg.ScoreCountRate);
            return Math.Min(score, counted);
        }

        public static bool CountFinished(float t, int score, GameConfig cfg) => ShownScore(t, score, cfg) >= score && t >= CountDelay;

        public static bool NewBadgeVisible(float t, int score, bool newBest, GameConfig cfg) => newBest && CountFinished(t, score, cfg);

        public static bool ButtonsVisible(float t, GameConfig cfg) => t > cfg.OverInputDelay;
    }
}
