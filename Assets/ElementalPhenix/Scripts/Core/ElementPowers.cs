using System;

namespace ElementalPhenix.Core
{
    /// <summary>Élément d'un phénix du catalogue : il décide de son pouvoir (<see cref="ElementPowers"/>).</summary>
    public enum PhoenixElement
    {
        /// <summary>Aucun pouvoir : la physique et les obstacles de la spec, sans changement.</summary>
        None,
        /// <summary>Embrasement : l'obstacle visé brûle, le phénix le traverse.</summary>
        Fire,
        /// <summary>Marée lente : tout le jeu ralentit quelques secondes.</summary>
        Water,
        /// <summary>Courant porteur : un souffle mène le phénix vers le milieu de l'ouverture visée.</summary>
        Wind,
        /// <summary>Révélation : les obstacles suivants s'écartent.</summary>
        Light,
        /// <summary>Pas de l'ombre : au contact de l'obstacle visé, le phénix réapparaît au milieu de l'ouverture.</summary>
        Shadow,
    }

    /// <summary>
    /// Pouvoirs d'élément (demande du 23/10/2026) : la jauge se remplit avec les points et les éclats ;
    /// pleine, le pouvoir du phénix se déclenche seul à l'approche de l'obstacle suivant (le jeu reste
    /// à une touche). Les cinq pouvoirs aident à franchir un obstacle environ, chacun à sa façon.
    /// Fonctions pures, rejouées telles quelles par le pilote automatique.
    /// </summary>
    public static class ElementPowers
    {
        /// <summary>
        /// Un pas du ralenti de l'eau : le minuteur (en temps réel) décroît, l'échelle de temps rejoint
        /// sa cible en <see cref="GameConfig.WaterSlowRamp"/> secondes, à l'entrée comme à la sortie.
        /// </summary>
        public static void StepSlow(ref float slowTime, ref float timeScale, float dt, GameConfig cfg)
        {
            if (slowTime > 0f) slowTime = Math.Max(0f, slowTime - dt);
            float target = slowTime > cfg.WaterSlowRamp ? cfg.WaterTimeScale : 1f;
            float rate = (1f - cfg.WaterTimeScale) / cfg.WaterSlowRamp * dt;
            timeScale = timeScale > target ? Math.Max(target, timeScale - rate) : Math.Min(target, timeScale + rate);
        }

        /// <summary>
        /// Accélération du courant porteur (px/s², y vers le bas), à ajouter à la gravité : elle la
        /// compense et amène la vitesse verticale vers une vitesse qui rejoint le centre <paramref name="targetCenterY"/>.
        /// Un tap remplace toujours la vitesse ; le souffle la ramène ensuite.
        /// </summary>
        public static float GuideAcceleration(float centerY, float vy, float targetCenterY, GameConfig cfg)
        {
            float wanted = (targetCenterY - centerY) * cfg.WindGuideGain;
            if (wanted > cfg.WindGuideMaxSpeed) wanted = cfg.WindGuideMaxSpeed;
            if (wanted < -cfg.WindGuideMaxSpeed) wanted = -cfg.WindGuideMaxSpeed;
            return (wanted - vy) * cfg.WindGuideResponse - cfg.Gravity;
        }

        /// <summary>Centre vertical de l'ouverture d'une paire, élargissement compris.</summary>
        public static float OpeningCenter(in PipePair p, GameConfig cfg) => p.OpeningTop + p.Gap(cfg) * 0.5f;
    }
}
