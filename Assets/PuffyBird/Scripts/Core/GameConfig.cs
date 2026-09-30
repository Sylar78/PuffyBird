namespace PuffyBird.Core
{
    /// <summary>
    /// Toutes les constantes de gameplay (Annexe A de la spec), dans le repère logique
    /// 288 × 512, y vers le bas, en pixels logiques et en secondes.
    /// Aucune autre classe ne doit contenir de valeur de réglage en dur.
    /// </summary>
    public sealed class GameConfig
    {
        // Écran logique (§3)
        public float Width = 288f;
        public float Height = 512f;
        public float GroundY = 400f;
        public float GroundHeight = 112f;
        public float GroundPattern = 12f;

        // Boucle de jeu (§4)
        public float Step = 1f / 60f;
        public float MaxFrameDelta = 0.25f;

        // Oiseau (§6)
        public float Gravity = 900f;
        public float FlapVelocity = -270f;
        public float MaxFallSpeed = 300f;
        // La spec (§6.2) donne −240, mais ce plafond ramène le saut à ≈ 34 px au lieu des
        // 40,5 px annoncés en §6.3 et testés en §23.2. On applique donc la simplification
        // prévue en §6.4 : plafond de montée = vitesse du flap (saut complet, comme FlapPyBird).
        public float MaxRiseSpeed = -270f;
        public float RotOnFlap = 45f;
        public float RotSpeed = 90f;
        public float RotSpeedDying = 360f;
        public float RotVisibleMax = 20f;
        public float RotMin = -90f;
        public float BirdX = 57f;
        public float BirdStartY = 244f;
        public float BirdWidth = 34f;
        public float BirdHeight = 24f;
        public float BirdRadius = 11f;
        public float WingFrameTime = 1f / 12f;
        public int[] WingSequence = { 0, 1, 2, 1 };
        public float BobAmplitude = 4f;
        public float BobFrequency = 1.25f;

        // Tuyaux (§7)
        public float ScrollSpeed = 120f;
        public float PipeWidth = 52f;
        public float PipeBodyWidth = 48f;
        public float PipeCapHeight = 24f;
        public float PipeGap = 100f;
        public float PipeSpacing = 150f;
        public int GapTopMin = 80;
        public int GapTopMax = 220;
        public float FirstPipeX = 388f;
        public float PipeDespawnMargin = 4f;
        public float SpawnLookahead = 10f;
        /// <summary>
        /// Largeur visible en plus de l'écran logique, de chaque côté, au plus (écran plus large que
        /// 9:16, option « extension » du §21.3). Les tuyaux apparaissent et disparaissent au-delà,
        /// hors champ. Au-delà de 64 px, le pool de 4 paires ne suffirait plus.
        /// </summary>
        public float MaxViewMargin = 64f;

        // Tuyaux mobiles (extension) : à partir de ce score, les nouvelles paires montent et
        // descendent d'un bloc ; l'ouverture garde sa hauteur. Amplitude = 30 % de l'ouverture.
        public int MovingPipesFromScore = 15;

        /// <summary>Couleur de l'oiseau, fixe (demande du 30/09/2026) au lieu du tirage de §6.8.</summary>
        public BirdColor BirdColor = BirdColor.Blue;
        /// <summary>Nombre de décors tirés au hasard à chaque partie (<see cref="Theme"/>).</summary>
        public int ThemeCount = 6;
        public float PipeMoveAmplitude = 30f;
        public float PipeMovePeriod = 2.6f;

        // Étoiles de vitesse (extension) : placées entre deux paires, elles accélèrent le
        // défilement de 30 % pendant 5 s en tout (montée et retour compris).
        public int StarFirstPair = 3;
        public float StarChance = 0.15f;
        public float StarRadius = 10f;
        public float StarJitter = 18f;
        public float StarBoostFactor = 1.3f;
        public float StarBoostDuration = 5f;
        public float StarBoostRamp = 0.3f;

        // Séquence de mort, écrans (§11, §12)
        public float FlashTime = 0.12f;
        public float DieSoundDelay = 0.25f;
        public float OverInputDelay = 0.8f;
        public float ScoreCountRate = 30f;
        public float FadeTime = 0.25f;
        /// <summary>Distance entre le haut de la zone sûre de l'écran (sous l'encoche) et le score en jeu.</summary>
        public float ScoreTopMargin = 10f;

        // Médailles (§10.3)
        public int MedalBronze = 10;
        public int MedalSilver = 20;
        public int MedalGold = 30;
        public int MedalPlatinum = 40;

        public float BirdCenterX => BirdX + BirdWidth * 0.5f;

        /// <summary>Réglages de référence (mode Normal).</summary>
        public static GameConfig CreateDefault() => new GameConfig();
    }
}
