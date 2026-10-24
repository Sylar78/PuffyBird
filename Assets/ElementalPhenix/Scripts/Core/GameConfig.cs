namespace ElementalPhenix.Core
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

        // Tuyaux mobiles (extension) : à partir de ce score, une nouvelle paire sur deux environ
        // (tirage) monte et descend d'un bloc ; l'ouverture garde sa hauteur. Amplitude = 30 % de l'ouverture.
        public int MovingPipesFromScore = 15;
        public float MovingPipeChance = 0.5f;

        // Tuyau seul (extension) : à partir de cette paire, parfois un seul tuyau plus long ; l'ouverture
        // (PipeGap) est alors entre lui et le haut de l'écran, ou entre lui et le sol. La paire
        // suivante est un peu plus loin pour laisser le temps de rejoindre son ouverture.
        public int SinglePipeFromPair = 5;
        public float SinglePipeChance = 0.12f;
        public float AfterSinglePipeSpacing = 225f;

        // Pauses (extension) : à partir de cette paire, parfois un long moment sans tuyau.
        public int BreatherFromPair = 6;
        public float BreatherChance = 0.1f;
        public float BreatherSpacing = 330f;

        /// <summary>Couleur de l'oiseau, fixe (demande du 30/09/2026) au lieu du tirage de §6.8.</summary>
        public BirdColor BirdColor = BirdColor.Blue;
        /// <summary>Nombre de décors tirés au hasard à chaque partie (<see cref="Theme"/>).</summary>
        public int ThemeCount = 9;
        public float PipeMoveAmplitude = 30f;
        public float PipeMovePeriod = 2.6f;

        // Étoiles de vitesse (extension) : placées entre deux paires, elles accélèrent le
        // défilement de 15 % pendant 5 s en tout (montée et retour compris).
        public int StarFirstPair = 3;
        public float StarChance = 0.15f;
        public float StarRadius = 10f;
        public float StarJitter = 18f;
        public float StarBoostFactor = 1.15f;
        public float StarBoostDuration = 5f;
        public float StarBoostRamp = 0.3f;

        // Éclats d'élément (extension) : parfois un éclat à ramasser entre deux paires, à l'écart du
        // milieu des ouvertures (il faut aller le chercher). Ils s'accumulent pour débloquer des sillages.
        public int ShardFirstPair = 1;
        public float ShardChance = 0.4f;
        public float ShardRadius = 8f;
        /// <summary>Écart vertical maximal de l'éclat par rapport au milieu des deux ouvertures voisines.</summary>
        public float ShardSpread = 42f;
        public float ShardMarginTop = 24f;
        public float ShardMarginGround = 30f;

        // Renaissance des cendres (extension) : la jauge de braises se remplit (un point, un éclat) ;
        // pleine, le phénix renaît une fois par partie au lieu de s'éteindre (hors Voyage).
        public int RebirthCost = 20;
        public int EmbersPerPoint = 1;
        public int EmbersPerShard = 2;

        // Courants (Voyage, abysses) : colonnes qui portent vers le haut entre deux paires.
        public float CurrentChance;
        public int CurrentFirstPair = 1;
        /// <summary>Accélération dans un courant, ajoutée à la gravité (px/s², y vers le bas : négative = vers le haut).</summary>
        public float CurrentAcceleration = -750f;
        public float CurrentHalfWidth = 22f;

        // Gardien (Voyage) : les dernières paires de chaque étape suivent un motif propre au monde.
        public int GuardianPairs = 3;
        public GuardianPattern GuardianPattern = GuardianPattern.Zigzag;
        /// <summary>Écart du haut des ouvertures du gardien par rapport au milieu de la plage (§7.2).</summary>
        public float GuardianStep = 55f;

        // Pouvoirs d'élément (ElementPowers) : la jauge se remplit avec les points et les éclats ; pleine,
        // le pouvoir du phénix se déclenche dès que le prochain obstacle est à moins de PowerTriggerDistance
        // (bord gauche de l'obstacle moins centre du phénix). Sans phénix (PhoenixElement.None), rien ne change.
        /// <summary>
        /// Pouvoirs aussi en mode classique et au défi du jour (toujours actifs en Voyage). Faux en attendant
        /// la décision sur le phénix acheté (24/10/2026) : le mode classique et le classement restent ceux de la spec.
        /// </summary>
        public bool ElementPowersInClassic;
        public int PowerChargeCost = 10;
        public int PowerChargePerPoint = 1;
        public int PowerChargePerShard = 1;
        public float PowerTriggerDistance = 90f;
        // Eau : ralenti de tout le jeu (défilement, chute, tuyaux mobiles), trajectoires identiques mais plus lentes.
        public float WaterSlowDuration = 3f;
        public float WaterTimeScale = 0.6f;
        public float WaterSlowRamp = 0.3f;
        // Vent : souffle vers le milieu de l'ouverture visée, jusqu'à ce qu'elle soit franchie.
        public float WindGuideMaxTime = 2.5f;
        /// <summary>Vitesse voulue par px d'écart au centre de l'ouverture (1/s).</summary>
        public float WindGuideGain = 5f;
        public float WindGuideMaxSpeed = 220f;
        /// <summary>Rapidité avec laquelle la vitesse rejoint la vitesse voulue (1/s).</summary>
        public float WindGuideResponse = 10f;
        // Lumière : les LightPairs prochaines ouvertures s'agrandissent de LightWiden px (moitié en haut, moitié en bas).
        public float LightWiden = 36f;
        public int LightPairs = 2;
        public float LightWidenRate = 120f;

        /// <summary>
        /// Seconde chance : attente maximale (s) du chargement de la vidéo après le tap sur CONTINUER.
        /// Si aucune vidéo n'a pu être chargée, la partie reprend quand même.
        /// </summary>
        public float ContinueAdWait = 3f;

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

        /// <summary>Jours de suite à jouer pour gagner l'oiseau de la série.</summary>
        public int StreakDaysForSkin = 30;

        /// <summary>Distance max (px) entre l'oiseau et un tuyau pour compter comme un frôlement.</summary>
        public float NearMissDistance = 5f;

        /// <summary>Rafales de vent (mode Voyage, orage) : 0 dans le mode classique.</summary>
        public float GustAcceleration;
        public float GustPeriod = 1f;

        /// <summary>
        /// Mode Voyage des éléments : une étape par décor, chacune avec sa règle. L'objectif atteint,
        /// l'oiseau plane <see cref="JourneyClearTime"/> secondes sans obstacle avant l'écran de fin.
        /// Les écarts de physique restent franchissables (testé avec le pilote automatique).
        /// </summary>
        public JourneyStage[] Journey =
        {
            new JourneyStage(Theme.Day, 10, StageRule.FirstFlight) { Guardian = GuardianPattern.Climb },
            new JourneyStage(Theme.Night, 12, StageRule.MoreStars) { Guardian = GuardianPattern.Dive, StarChance = 0.45f, StarFirstPair = 1 },
            new JourneyStage(Theme.City, 12, StageRule.DenseTraffic) { Guardian = GuardianPattern.Zigzag, SpacingScale = 0.8f, GapScale = 1.1f },
            new JourneyStage(Theme.Japan, 15, StageRule.NarrowGates) { Guardian = GuardianPattern.Climb, GapScale = 0.86f, SpacingScale = 1.2f },
            new JourneyStage(Theme.Storm, 15, StageRule.Gusts) { Guardian = GuardianPattern.Zigzag, GustAcceleration = 260f, GustPeriod = 2.4f },
            new JourneyStage(Theme.Snow, 15, StageRule.FrozenWings) { Guardian = GuardianPattern.Dive, GravityScale = 1.18f },
            new JourneyStage(Theme.Jungle, 18, StageRule.WildRush) { Guardian = GuardianPattern.Climb, ScrollScale = 1.15f },
            new JourneyStage(Theme.Sky, 18, StageRule.LightAir) { Guardian = GuardianPattern.Zigzag, GravityScale = 0.75f, FlapScale = 0.85f, MaxFallScale = 0.8f },
            new JourneyStage(Theme.Ocean, 20, StageRule.SlowSwim) { Guardian = GuardianPattern.Dive, CurrentChance = 0.45f, GravityScale = 0.6f, FlapScale = 0.75f, MaxFallScale = 0.6f, ScrollScale = 0.85f },
        };
        public float JourneyClearTime = 1.5f;
        /// <summary>Rappel de l'oiseau vers sa hauteur de départ pendant le vol de fin d'étape (1/s).</summary>
        public float JourneyGlideReturn = 2.5f;

        public float BirdCenterX => BirdX + BirdWidth * 0.5f;

        /// <summary>Réglages de référence (mode Normal).</summary>
        public static GameConfig CreateDefault() => new GameConfig();

        /// <summary>Copie des réglages avec la règle de l'étape <paramref name="index"/> du Voyage. Alloue : au chargement seulement.</summary>
        public GameConfig ForStage(int index)
        {
            var stage = Journey[index];
            var c = (GameConfig)MemberwiseClone();
            c.Gravity = Gravity * stage.GravityScale;
            c.FlapVelocity = FlapVelocity * stage.FlapScale;
            c.MaxRiseSpeed = MaxRiseSpeed * stage.FlapScale;
            c.MaxFallSpeed = MaxFallSpeed * stage.MaxFallScale;
            c.ScrollSpeed = ScrollSpeed * stage.ScrollScale;
            c.PipeGap = PipeGap * stage.GapScale;
            c.PipeSpacing = PipeSpacing * stage.SpacingScale;
            c.GustAcceleration = stage.GustAcceleration;
            c.GustPeriod = stage.GustPeriod;
            c.CurrentChance = stage.CurrentChance;
            c.GuardianPattern = stage.Guardian;
            if (stage.StarChance >= 0f) c.StarChance = stage.StarChance;
            if (stage.StarFirstPair >= 0) c.StarFirstPair = stage.StarFirstPair;
            if (stage.MovingPipesFromScore >= 0) c.MovingPipesFromScore = stage.MovingPipesFromScore;
            if (stage.MovingPipeChance >= 0f) c.MovingPipeChance = stage.MovingPipeChance;
            if (stage.SinglePipeFromPair >= 0) c.SinglePipeFromPair = stage.SinglePipeFromPair;
            if (stage.SinglePipeChance >= 0f) c.SinglePipeChance = stage.SinglePipeChance;
            return c;
        }

        /// <summary>Accélération des rafales au temps <paramref name="time"/> (0 sans rafales).</summary>
        public float GustAt(float time)
        {
            if (GustAcceleration == 0f) return 0f;
            return GustAcceleration * (float)System.Math.Sin(time * 2.0 * System.Math.PI / GustPeriod);
        }
    }
}
