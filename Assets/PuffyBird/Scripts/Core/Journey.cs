namespace PuffyBird.Core
{
    /// <summary>
    /// Une étape du mode Voyage des éléments : un décor, un objectif de score et une règle propre
    /// (physique ou obstacles). Les facteurs valent 1 et les valeurs négatives gardent le réglage du
    /// mode classique. Appliquée par <see cref="GameConfig.ForStage"/>.
    /// </summary>
    public sealed class JourneyStage
    {
        public Theme Theme;
        /// <summary>Points à marquer pour réussir l'étape.</summary>
        public int Goal;
        public float GravityScale = 1f;
        /// <summary>Facteur du battement ; le plafond de montée suit (saut complet, §6.4).</summary>
        public float FlapScale = 1f;
        public float MaxFallScale = 1f;
        public float ScrollScale = 1f;
        /// <summary>Rafales : accélération verticale ajoutée à la gravité, en sinusoïde (px/s², y vers le bas).</summary>
        public float GustAcceleration;
        public float GustPeriod = 1f;
        public float StarChance = -1f;
        public int StarFirstPair = -1;
        public int MovingPipesFromScore = -1;
        public float MovingPipeChance = -1f;
        public int SinglePipeFromPair = -1;
        public float SinglePipeChance = -1f;

        public JourneyStage(Theme theme, int goal)
        {
            Theme = theme;
            Goal = goal;
        }
    }
}
