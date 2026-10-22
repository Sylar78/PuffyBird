using System;

namespace ElementalPhenix.Core
{
    /// <summary>
    /// Simulation complète d'une partie (§5, §19), indépendante du rendu et de Unity.
    /// Chaque appel à <see cref="Step"/> avance d'un pas fixe <see cref="GameConfig.Step"/>.
    /// Les entrées sont mises en file et traitées au début du pas suivant (§15).
    /// </summary>
    public sealed class GameSimulation
    {
        const int MaxPendingPresses = 8;

        // Réglages de la partie en cours : ceux du mode classique, ou ceux de l'étape du Voyage.
        GameConfig _cfg;
        readonly GameConfig _baseCfg;
        readonly GameConfig[] _stageCfgs;
        readonly IScoreStorage _storage;
        readonly Rng _rng;
        // Tirages des extensions (tuyaux mobiles, étoiles) : séparés, pour que la suite des
        // ouvertures reste celle de la spec à graine égale.
        readonly Rng _bonusRng;
        // Éclats et courants : troisième générateur, pour ne pas décaler les tirages des extensions plus anciennes.
        readonly Rng _lootRng;
        readonly PipeField _pipes = new PipeField(4);
        readonly StarField _stars = new StarField(4);
        readonly StarField _shards = new StarField(4);
        readonly StarField _currents = new StarField(4);
        readonly Bird _bird = new Bird();
        readonly bool _bestReadable;

        int _pendingPresses;
        float _dieSoundTimer;
        float _fadeOut;
        float _fadeIn;
        bool _fadeToTitle;
        int _pairsSpawned;
        bool _dailyPending;
        uint _dailySeed;
        bool _journeyPending;
        int _journeyPendingStage;
        float _clearTime;
        float _boostTime;
        float _speedFactor = 1f;
        GameEvents _events;

        public GameSimulation(GameConfig cfg, IScoreStorage storage, uint seed, bool startOnTitle = true)
        {
            _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
            _baseCfg = cfg;
            _stageCfgs = new GameConfig[cfg.Journey.Length];
            for (int i = 0; i < _stageCfgs.Length; i++) _stageCfgs[i] = cfg.ForStage(i);
            _storage = storage ?? new MemoryScoreStorage();
            _rng = new Rng(seed);
            _bonusRng = new Rng(seed ^ 0x9E3779B9u);
            _lootRng = new Rng(seed ^ 0x85EBCA6Bu);
            _bestReadable = _storage.TryReadBest(out int best);
            Best = _bestReadable ? Math.Max(0, best) : 0;
            ResetRun();
            if (startOnTitle) SetState(GameState.Title);
            _events = GameEvents.None;
        }

        /// <summary>Réglages de la partie en cours (ceux de l'étape en mode Voyage).</summary>
        public GameConfig Config => _cfg;
        /// <summary>
        /// La partie est une étape du Voyage des éléments (<see cref="JourneyStage"/>) : décor et règle
        /// imposés, objectif de score, aucun record ni médaille. Les essais s'enchaînent jusqu'à ACCUEIL.
        /// </summary>
        public bool IsJourney { get; private set; }
        /// <summary>Étape du Voyage en cours (0 à <c>Journey.Length − 1</c>).</summary>
        public int JourneyStage { get; private set; }
        /// <summary>Points à marquer pour réussir l'étape (0 hors Voyage).</summary>
        public int JourneyGoal => IsJourney ? _baseCfg.Journey[JourneyStage].Goal : 0;
        /// <summary>L'objectif de l'étape est atteint : l'oiseau plane jusqu'à l'écran de fin, sans obstacle ni collision.</summary>
        public bool StageCleared { get; private set; }
        /// <summary>La dernière étape est réussie : le prochain tap ramène à l'écran titre.</summary>
        public bool JourneyComplete => IsJourney && StageCleared && JourneyStage == _baseCfg.Journey.Length - 1;
        /// <summary>La partie (et les suivantes, jusqu'au retour à l'accueil) suit le défi du jour : même graine, donc mêmes tuyaux et même décor pour tous.</summary>
        public bool IsDaily { get; private set; }
        public GameState State { get; private set; }
        /// <summary>Secondes écoulées dans l'état courant.</summary>
        public float StateTime { get; private set; }
        /// <summary>Secondes de simulation totales (animations sinusoïdales).</summary>
        public float Time { get; private set; }
        public int Score { get; private set; }
        public int Best { get; private set; }
        public bool NewBest { get; private set; }
        public Bird Bird => _bird;
        public PipeField Pipes => _pipes;
        public StarField Stars => _stars;
        /// <summary>Éclats d'élément à ramasser (rayon <see cref="GameConfig.ShardRadius"/>).</summary>
        public StarField Shards => _shards;
        /// <summary>Courants ascendants (Voyage, abysses) : colonnes de demi-largeur <see cref="GameConfig.CurrentHalfWidth"/> ; Y est inutilisé.</summary>
        public StarField Currents => _currents;
        /// <summary>Éclats ramassés pendant la partie en cours.</summary>
        public int ShardsThisRun { get; private set; }
        /// <summary>Braises de la jauge de renaissance (0 à <see cref="GameConfig.RebirthCost"/>).</summary>
        public int Embers { get; private set; }
        /// <summary>Le phénix a déjà profité de sa renaissance dans cette partie.</summary>
        public bool Reborn { get; private set; }
        /// <summary>Jauge pleine et renaissance pas encore utilisée : la prochaine chute ramène le phénix.</summary>
        public bool RebirthReady => !IsJourney && !Reborn && Embers >= _baseCfg.RebirthCost;
        /// <summary>Le gardien du monde est là : ses paires sont apparues et l'étape n'est pas encore réussie.</summary>
        public bool GuardianActive => IsJourney && _guardianShown && !StageCleared;
        bool _guardianShown;
        /// <summary>Multiplicateur du défilement : 1, ou jusqu'à <see cref="GameConfig.StarBoostFactor"/> après une étoile.</summary>
        public float SpeedFactor => _speedFactor;
        /// <summary>Secondes d'accélération restantes (0 sans étoile).</summary>
        public float BoostTime => _boostTime;
        /// <summary>Intensité de l'accélération dans [0, 1], pour les effets visuels.</summary>
        public float BoostAmount => (_speedFactor - 1f) / (_cfg.StarBoostFactor - 1f);
        /// <summary>
        /// L'étoile protège l'oiseau : tant que son effet dure, toucher un tuyau la consomme au lieu
        /// de terminer la partie. Le sol tue toujours.
        /// </summary>
        public bool Shielded => State == GameState.Playing && _boostTime > 0f;

        float _viewMargin;

        /// <summary>
        /// Largeur visible au-delà de l'écran logique, de chaque côté, en px (0 en 9:16), bornée à
        /// <see cref="GameConfig.MaxViewMargin"/>. Fixée par le rendu selon le format de l'écran.
        /// </summary>
        public float ViewMargin
        {
            get => _viewMargin;
            set => _viewMargin = Math.Max(0f, Math.Min(_cfg.MaxViewMargin, value));
        }
        /// <summary>Distance totale défilée en px ; figée pendant DYING et OVER (sauf après une étape réussie du Voyage).</summary>
        public double ScrollDistance { get; private set; }
        public double PrevScrollDistance { get; private set; }
        public float GroundOffset { get; private set; }
        /// <summary>Temps restant du flash blanc ; alpha = Flash / FlashTime.</summary>
        public float Flash { get; private set; }
        public BirdColor BirdColor { get; private set; }
        public Theme Theme { get; private set; }
        public int RunId { get; private set; }
        /// <summary>Identifiant de la paire touchée, ou -1.</summary>
        public int HitPipeId { get; private set; } = -1;
        /// <summary>Identifiant de la dernière paire traversée grâce à l'étoile, ou -1.</summary>
        public int ShieldPipeId { get; private set; } = -1;
        public bool DiedOnGround { get; private set; }
        public Medal Medal => IsJourney ? Medal.None : Medals.For(Score, _cfg);
        public bool Muted
        {
            get => _storage.Muted;
            set => _storage.Muted = value;
        }

        /// <summary>Opacité du fondu au noir (0 = invisible, 1 = noir).</summary>
        public float FadeAlpha
        {
            get
            {
                if (_fadeOut > 0f) return 1f - _fadeOut / _cfg.FadeTime;
                if (_fadeIn > 0f) return _fadeIn / _cfg.FadeTime;
                return 0f;
            }
        }

        /// <summary>Vrai pendant le fondu sortant : les entrées sont ignorées.</summary>
        public bool IsFadingOut => _fadeOut > 0f;

        public bool CanRestart => State == GameState.Over && StateTime > _cfg.OverInputDelay && !IsFadingOut;

        /// <summary>La partie a déjà eu sa seconde chance (une seule par partie).</summary>
        public bool Continued { get; private set; }

        /// <summary>Seconde chance possible : écran de fin, pas encore utilisée.</summary>
        public bool CanContinue => State == GameState.Over && !Continued && !IsFadingOut && !StageCleared;

        /// <summary>
        /// Seconde chance (vidéo récompensée choisie par le joueur) : l'oiseau repart de sa position
        /// de départ en <see cref="GameState.Ready"/>, avec le même score et le même décor ; les
        /// tuyaux et les étoiles sont retirés, le prochain tap relance la partie comme au début.
        /// </summary>
        public void ContinueRun()
        {
            if (!CanContinue) return;
            Continued = true;
            _bird.Reset(_cfg.BirdStartY);
            ClearField();
            _events |= GameEvents.Swoosh;
            SetState(GameState.Ready);
        }

        /// <summary>
        /// Renaissance des cendres (jauge pleine, une fois par partie) : comme la seconde chance,
        /// le phénix repart en <see cref="GameState.Ready"/> avec le même score et le même décor.
        /// </summary>
        void Rebirth()
        {
            Reborn = true;
            Embers = 0;
            _bird.Reset(_cfg.BirdStartY);
            ClearField();
            _events |= GameEvents.Rebirth | GameEvents.Swoosh;
            SetState(GameState.Ready);
        }

        /// <summary>Retire tuyaux, étoiles, éclats et courants, et efface la chute en cours.</summary>
        void ClearField()
        {
            _pipes.Clear();
            _stars.Clear();
            _shards.Clear();
            _currents.Clear();
            _boostTime = 0f;
            _speedFactor = 1f;
            Flash = 0f;
            HitPipeId = -1;
            ShieldPipeId = -1;
            DiedOnGround = false;
            _dieSoundTimer = 0f;
        }

        public void Press()
        {
            if (_pendingPresses < MaxPendingPresses) _pendingPresses++;
        }

        /// <summary>Pause automatique quand l'application perd le focus pendant une partie (§4.4).</summary>
        public void Pause()
        {
            if (State == GameState.Playing) SetState(GameState.Paused);
        }

        public void Resume()
        {
            if (State == GameState.Paused) SetState(GameState.Playing);
        }

        /// <summary>
        /// Abandon de la partie (bouton ACCUEIL des paramètres) : fondu au noir puis écran titre,
        /// avec un nouveau décor. Le score de la partie abandonnée n'est pas enregistré.
        /// </summary>
        public void QuitToTitle()
        {
            if (State == GameState.Title || IsFadingOut) return;
            // Figée pendant le fondu : l'oiseau ne peut plus mourir ni marquer.
            Pause();
            BeginTransition();
            _fadeToTitle = true;
        }

        /// <summary>
        /// Lance le défi du jour depuis l'écran titre : fondu, puis une partie dont toute la suite
        /// aléatoire (ouvertures, étoiles, mouvements, décor) vient de <paramref name="seed"/>. Chaque nouvel
        /// essai repart de la même graine ; ACCUEIL (<see cref="QuitToTitle"/>) quitte le défi.
        /// </summary>
        public void StartDaily(uint seed)
        {
            if (State != GameState.Title || IsFadingOut) return;
            _dailyPending = true;
            _dailySeed = seed;
            BeginTransition();
        }

        /// <summary>
        /// Lance une étape du Voyage des éléments depuis l'écran titre : fondu, puis la partie avec le
        /// décor et la règle de l'étape. Une étape réussie mène à la suivante au tap ; ACCUEIL quitte le Voyage.
        /// </summary>
        public void StartJourney(int stage)
        {
            if (State != GameState.Title || IsFadingOut) return;
            _journeyPending = true;
            _journeyPendingStage = Math.Max(0, Math.Min(_baseCfg.Journey.Length - 1, stage));
            BeginTransition();
        }

        public GameEvents ConsumeEvents()
        {
            var e = _events;
            _events = GameEvents.None;
            return e;
        }

        public void Step()
        {
            float dt = _cfg.Step;
            ProcessPresses();

            PrevScrollDistance = ScrollDistance;
            _bird.PrevY = _bird.Y;
            _pipes.SavePrevious();
            _stars.SavePrevious();

            // En pause, tout est figé, sauf le fondu d'un retour à l'accueil.
            if (State == GameState.Paused && !IsFadingOut) return;

            Time += dt;
            StateTime += dt;
            if (Flash > 0f) Flash = Math.Max(0f, Flash - dt);
            UpdateTransition(dt);
            UpdateDieSound(dt);

            // Après une étape réussie, le vol continue derrière l'écran de fin.
            bool scrolling = State == GameState.Title || State == GameState.Ready || State == GameState.Playing
                || (State == GameState.Over && StageCleared);
            if (scrolling)
            {
                if (State == GameState.Playing) StepBoost(ref _boostTime, ref _speedFactor, dt, _cfg);
                float dx = _cfg.ScrollSpeed * _speedFactor * dt;
                ScrollDistance += dx;
                GroundOffset = (GroundOffset + dx) % _cfg.GroundPattern;
                _bird.AnimateWings(dt, _cfg);
            }

            if (State == GameState.Title || State == GameState.Ready)
            {
                _bird.Y = _cfg.BirdStartY + (float)Math.Sin(Time * Math.PI * 2.0 * _cfg.BobFrequency) * _cfg.BobAmplitude;
                _bird.Rot = 0f;
                return;
            }

            if (State == GameState.Over && StageCleared)
            {
                Glide(dt);
                _pipes.Advance(dt, _rng, _cfg, _viewMargin, _speedFactor, spawn: false);
                AdvanceFloaters(_cfg.ScrollSpeed * _speedFactor * dt);
                return;
            }

            if (State == GameState.Playing || State == GameState.Dying)
            {
                float rotSpeed = State == GameState.Dying ? _cfg.RotSpeedDying : _cfg.RotSpeed;
                if (StageCleared) Glide(dt);
                else _bird.Integrate(dt, rotSpeed, _cfg, _cfg.GustAt(Time) + (State == GameState.Playing ? CurrentAt(_cfg.BirdCenterX, _currents, 0f, _cfg) : 0f));
                if (_bird.Y + _cfg.BirdHeight >= _cfg.GroundY)
                {
                    _bird.Y = _cfg.GroundY - _cfg.BirdHeight;
                    Die(true);
                    return;
                }
            }

            if (State != GameState.Playing) return;

            if (_pipes.Advance(dt, _rng, _cfg, _viewMargin, _speedFactor, spawn: !StageCleared)) OnPairSpawned();
            _pipes.UpdateMotion(Time, _cfg);
            AdvanceFloaters(_cfg.ScrollSpeed * _speedFactor * dt);

            if (StageCleared)
            {
                _clearTime += dt;
                if (_clearTime >= _cfg.JourneyClearTime)
                {
                    SetState(GameState.Over);
                    _events |= GameEvents.Swoosh;
                }
                return;
            }

            float cx = _cfg.BirdCenterX;
            float cy = _bird.CenterY(_cfg);
            for (int i = 0; i < _stars.Capacity; i++)
            {
                ref var star = ref _stars[i];
                if (!star.Active || !Collision.CircleCircle(cx, cy, _cfg.BirdRadius, star.X, star.Y, _cfg.StarRadius)) continue;
                star.Active = false;
                _boostTime = _cfg.StarBoostDuration;
                _events |= GameEvents.Star;
            }
            for (int i = 0; i < _shards.Capacity; i++)
            {
                ref var shard = ref _shards[i];
                if (!shard.Active || !Collision.CircleCircle(cx, cy, _cfg.BirdRadius, shard.X, shard.Y, _cfg.ShardRadius)) continue;
                shard.Active = false;
                ShardsThisRun++;
                AddEmbers(_cfg.EmbersPerShard);
                _events |= GameEvents.Shard;
            }
            for (int i = 0; i < _pipes.Count; i++)
            {
                ref var p = ref _pipes[i];
                // Le score est testé avant la collision de la même paire (§9.3).
                if (!p.Scored && cx >= p.X + _cfg.PipeWidth * 0.5f)
                {
                    p.Scored = true;
                    Score++;
                    _events |= GameEvents.Point;
                    if (IsJourney)
                    {
                        if (Score >= JourneyGoal)
                        {
                            // Objectif atteint : plus de tuyau ni de collision, l'oiseau plane jusqu'à l'écran de fin.
                            StageCleared = true;
                            _clearTime = 0f;
                            _boostTime = 0f;
                            _events |= GameEvents.StageCleared;
                            return;
                        }
                    }
                    else
                    {
                        if (Medals.For(Score, _cfg) != Medals.For(Score - 1, _cfg)) _events |= GameEvents.Milestone;
                        AddEmbers(_cfg.EmbersPerPoint);
                    }
                }
                if (p.Pierced) continue;
                if (Collision.HitsPipe(cx, cy, _cfg.BirdRadius, p.OpeningTop, p.X, _cfg, p.Kind))
                {
                    if (_boostTime > 0f)
                    {
                        ShieldPipeId = p.Id;
                        // L'étoile est perdue : l'oiseau traverse cette paire sans mourir, et la vitesse
                        // et son éclat redescendent à la normale.
                        p.Pierced = true;
                        _boostTime = 0f;
                        _events |= GameEvents.StarShield;
                        continue;
                    }
                    HitPipeId = p.Id;
                    Die(false);
                    return;
                }
                if (p.GrazeReported) continue;
                // Frôlement : tuyau approché à moins de NearMissDistance pendant la traversée, signalé
                // quand l'oiseau l'a dépassé sans le toucher.
                if (cx + _cfg.BirdRadius > p.X && cx - _cfg.BirdRadius < p.X + _cfg.PipeWidth
                    && Collision.PipeGap(cx, cy, _cfg.BirdRadius, p.OpeningTop, p.X, _cfg, p.Kind) <= _cfg.NearMissDistance)
                {
                    p.Grazed = true;
                }
                if (p.Grazed && cx - _cfg.BirdRadius >= p.X + _cfg.PipeWidth)
                {
                    p.GrazeReported = true;
                    _events |= GameEvents.NearMiss;
                }
            }
        }

        /// <summary>Nouvelle partie : décor tiré au hasard, état READY (§19 resetRun).</summary>
        public void ResetRun()
        {
            // L'oiseau est toujours de la même couleur, mais le tirage de §6.8 est conservé :
            // la suite des ouvertures pour une graine donnée reste celle de la spec.
            if (_dailyPending)
            {
                IsDaily = true;
                IsJourney = false;
                _dailyPending = false;
            }
            else if (_journeyPending)
            {
                IsJourney = true;
                IsDaily = false;
                JourneyStage = _journeyPendingStage;
                _journeyPending = false;
            }
            else if (_fadeToTitle)
            {
                IsDaily = false;
                IsJourney = false;
            }
            else if (IsJourney && StageCleared)
            {
                // Étape réussie : la suivante (la dernière ramène à l'écran titre, voir HandlePress).
                JourneyStage = Math.Min(JourneyStage + 1, _baseCfg.Journey.Length - 1);
            }
            _cfg = IsJourney ? _stageCfgs[JourneyStage] : _baseCfg;
            StageCleared = false;
            _clearTime = 0f;
            if (IsDaily)
            {
                _rng.Reseed(_dailySeed);
                _bonusRng.Reseed(_dailySeed ^ 0x9E3779B9u);
                _lootRng.Reseed(_dailySeed ^ 0x85EBCA6Bu);
            }
            _rng.Range(0, 2);
            BirdColor = _cfg.BirdColor;
            Theme = (Theme)_rng.Range(0, _cfg.ThemeCount - 1);
            if (IsJourney) Theme = _baseCfg.Journey[JourneyStage].Theme;
            _bird.Reset(_cfg.BirdStartY);
            _pipes.Clear();
            _stars.Clear();
            _shards.Clear();
            _currents.Clear();
            _pairsSpawned = 0;
            ShardsThisRun = 0;
            Embers = 0;
            Reborn = false;
            _guardianShown = false;
            _boostTime = 0f;
            _speedFactor = 1f;
            Score = 0;
            NewBest = false;
            Continued = false;
            Flash = 0f;
            HitPipeId = -1;
            ShieldPipeId = -1;
            DiedOnGround = false;
            _dieSoundTimer = 0f;
            RunId++;
            SetState(GameState.Ready);
            _events |= GameEvents.NewRun;
        }

        void ProcessPresses()
        {
            while (_pendingPresses > 0)
            {
                _pendingPresses--;
                HandlePress();
            }
        }

        void HandlePress()
        {
            if (IsFadingOut) return;
            switch (State)
            {
                case GameState.Title:
                    BeginTransition();
                    break;
                case GameState.Ready:
                    SetState(GameState.Playing);
                    _events |= GameEvents.RunStarted;
                    _pipes.SpawnRandom(_cfg.FirstPipeX, _rng, _cfg);
                    OnPairSpawned();
                    Flap();
                    break;
                case GameState.Playing:
                    if (!StageCleared) Flap();
                    break;
                case GameState.Paused:
                    Resume();
                    break;
                case GameState.Over:
                    if (StateTime <= _cfg.OverInputDelay) break;
                    BeginTransition();
                    // Voyage terminé : retour à l'écran titre.
                    if (JourneyComplete) _fadeToTitle = true;
                    break;
            }
        }

        /// <summary>
        /// Extensions appliquées à la paire qui vient d'apparaître, tirées avec le second générateur :
        /// <list type="bullet">
        /// <item>parfois un tuyau seul, plus long, dont l'ouverture touche le haut de l'écran ou le sol,
        /// du côté de l'ouverture précédente pour rester franchissable ;</item>
        /// <item>à partir de <see cref="GameConfig.MovingPipesFromScore"/> paires, un mouvement vertical
        /// une fois sur deux environ ;</item>
        /// <item>parfois une étoile de vitesse à mi-chemin de la paire précédente ;</item>
        /// <item>la distance jusqu'à la paire suivante : parfois une pause sans tuyau, et un peu plus
        /// de place après un tuyau seul pour rejoindre l'ouverture suivante.</item>
        /// </list>
        /// </summary>
        void OnPairSpawned()
        {
            int index = _pairsSpawned++;
            ref var last = ref _pipes.Last;
            float spacing = _cfg.PipeSpacing;
            int guardianStep = GuardianStepOf();

            if (guardianStep >= 0)
            {
                // Gardien : paire fixe dont l'ouverture suit le motif du monde, espacement normal.
                last.BaseTop = GuardianTop(guardianStep);
                if (!_guardianShown)
                {
                    _guardianShown = true;
                    _events |= GameEvents.Guardian;
                }
            }
            else if (index >= _cfg.SinglePipeFromPair && _pipes.Count >= 2 && _bonusRng.NextFloat() < _cfg.SinglePipeChance)
            {
                ref var prev = ref _pipes[_pipes.Count - 2];
                float middle = (_cfg.GroundY - _cfg.PipeGap) * 0.5f;
                if (prev.Kind == PipeKind.Pair && prev.BaseTop + prev.MoveAmplitude <= middle)
                {
                    last.Kind = PipeKind.BottomOnly;
                    last.BaseTop = 0f;
                }
                else if (prev.Kind == PipeKind.Pair && prev.BaseTop - prev.MoveAmplitude >= middle)
                {
                    last.Kind = PipeKind.TopOnly;
                    last.BaseTop = _cfg.GroundY - _cfg.PipeGap;
                }
                if (last.Kind != PipeKind.Pair) spacing = _cfg.AfterSinglePipeSpacing;
            }
            if (guardianStep < 0 && index >= _cfg.MovingPipesFromScore && last.Kind == PipeKind.Pair && _bonusRng.NextFloat() < _cfg.MovingPipeChance)
            {
                last.MoveAmplitude = _cfg.PipeMoveAmplitude;
                last.MovePhase = (float)(_bonusRng.NextFloat() * 2.0 * Math.PI);
                last.Shift = last.ShiftAt(Time, _cfg);
                last.PrevShift = last.Shift;
            }
            // Entre cette paire et la précédente : une étoile, sinon un courant, sinon un éclat (au plus un).
            if (_pipes.Count >= 2)
            {
                ref var prev = ref _pipes[_pipes.Count - 2];
                float x = (prev.X + _cfg.PipeWidth + last.X) * 0.5f;
                float middle = (prev.BaseTop + last.BaseTop + _cfg.PipeGap) * 0.5f;
                if (index >= _cfg.StarFirstPair && _bonusRng.NextFloat() < _cfg.StarChance)
                {
                    _stars.Spawn(x, middle + (_bonusRng.NextFloat() * 2f - 1f) * _cfg.StarJitter);
                }
                else if (_cfg.CurrentChance > 0f && index >= _cfg.CurrentFirstPair && _lootRng.NextFloat() < _cfg.CurrentChance)
                {
                    _currents.Spawn(x, 0f);
                }
                else if (index >= _cfg.ShardFirstPair && _lootRng.NextFloat() < _cfg.ShardChance)
                {
                    // À l'écart du milieu, en haut ou en bas : il faut aller le chercher.
                    float side = _lootRng.NextFloat() < 0.5f ? -1f : 1f;
                    float y = middle + side * _cfg.ShardSpread * (0.5f + 0.5f * _lootRng.NextFloat());
                    y = Math.Max(_cfg.ShardMarginTop, Math.Min(_cfg.GroundY - _cfg.ShardMarginGround, y));
                    _shards.Spawn(x, y);
                }
            }
            if (guardianStep < 0 && index >= _cfg.BreatherFromPair && _bonusRng.NextFloat() < _cfg.BreatherChance) spacing = _cfg.BreatherSpacing;
            _pipes.NextSpacing = spacing;
        }

        /// <summary>
        /// Rang de la paire qui vient d'apparaître dans le motif du gardien (0 à <see cref="GameConfig.GuardianPairs"/> − 1),
        /// ou −1 : hors Voyage, ou pas encore les dernières paires de l'étape. Compté depuis les points
        /// restant à marquer, pour rester juste après une seconde chance.
        /// </summary>
        int GuardianStepOf()
        {
            if (!IsJourney) return -1;
            int unscored = 0;
            for (int i = 0; i < _pipes.Count; i++)
            {
                if (!_pipes[i].Scored) unscored++;
            }
            int point = Score + unscored;
            int first = JourneyGoal - _cfg.GuardianPairs + 1;
            if (point < first || point > JourneyGoal) return -1;
            return point - first;
        }

        /// <summary>Haut de l'ouverture de la paire <paramref name="step"/> du motif du gardien, dans la plage de §7.2.</summary>
        float GuardianTop(int step)
        {
            float center = (_cfg.GapTopMin + _cfg.GapTopMax) * 0.5f;
            float a = _cfg.GuardianStep;
            float top;
            switch (_cfg.GuardianPattern)
            {
                case GuardianPattern.Climb: top = center + a - step * a; break;
                case GuardianPattern.Dive: top = center - a + step * a; break;
                default: top = center + (step % 2 == 0 ? a : -a); break;
            }
            return Math.Max(_cfg.GapTopMin, Math.Min(_cfg.GapTopMax, top));
        }

        /// <summary>
        /// Accélération des courants à l'abscisse <paramref name="x"/> (0 hors courant). <paramref name="scroll"/>
        /// décale les courants vers la gauche (anticipation du pilote automatique).
        /// </summary>
        public static float CurrentAt(float x, StarField currents, float scroll, GameConfig cfg)
        {
            for (int i = 0; i < currents.Capacity; i++)
            {
                ref var c = ref currents[i];
                if (c.Active && Math.Abs(x - (c.X - scroll)) <= cfg.CurrentHalfWidth) return cfg.CurrentAcceleration;
            }
            return 0f;
        }

        void AdvanceFloaters(float dx)
        {
            _stars.Advance(dx, _cfg, _viewMargin);
            _shards.Advance(dx, _cfg, _viewMargin, _cfg.ShardRadius);
            _currents.Advance(dx, _cfg, _viewMargin, _cfg.CurrentHalfWidth);
        }

        void AddEmbers(int amount)
        {
            if (IsJourney || Reborn || Embers >= _cfg.RebirthCost) return;
            Embers = Math.Min(_cfg.RebirthCost, Embers + amount);
            if (Embers >= _cfg.RebirthCost) _events |= GameEvents.EmbersFull;
        }

        /// <summary>
        /// Un pas de l'accélération : le minuteur décroît, le multiplicateur rejoint sa cible en
        /// <see cref="GameConfig.StarBoostRamp"/> secondes. Fonction pure, rejouée par le pilote automatique.
        /// </summary>
        public static void StepBoost(ref float boostTime, ref float speedFactor, float dt, GameConfig cfg)
        {
            if (boostTime > 0f) boostTime = Math.Max(0f, boostTime - dt);
            // Le retour à la vitesse normale commence avant la fin : 5 s après la prise, c'est fini.
            float target = boostTime > cfg.StarBoostRamp ? cfg.StarBoostFactor : 1f;
            float rate = (cfg.StarBoostFactor - 1f) / cfg.StarBoostRamp * dt;
            speedFactor = speedFactor < target ? Math.Min(target, speedFactor + rate) : Math.Max(target, speedFactor - rate);
        }

        /// <summary>
        /// Vol de fin d'étape : l'oiseau rejoint en douceur sa hauteur de départ, nez à l'horizontale,
        /// sans gravité ni collision.
        /// </summary>
        void Glide(float dt)
        {
            float target = _cfg.BirdStartY - _bird.Y;
            _bird.Vy = target * _cfg.JourneyGlideReturn;
            _bird.Y += _bird.Vy * dt;
            _bird.Rot += (0f - _bird.Rot) * Math.Min(1f, dt * 4f);
        }

        void Flap()
        {
            _bird.Flap(_cfg);
            _events |= GameEvents.Flap;
        }

        void Die(bool onGround)
        {
            if (State == GameState.Playing)
            {
                _events |= GameEvents.Hit;
                Flash = _cfg.FlashTime;
                DiedOnGround = onGround;
                // Le Voyage change la physique : ses scores ne comptent pas pour le record.
                if (!IsJourney && Score > Best)
                {
                    Best = Score;
                    NewBest = true;
                    // Valeur existante illisible : on ne l'écrase jamais (§17).
                    if (_bestReadable) _storage.WriteBest(Best);
                }
                if (!onGround)
                {
                    _dieSoundTimer = _cfg.DieSoundDelay;
                    SetState(GameState.Dying);
                    if (_bird.Vy < 0f) _bird.Vy = 0f;
                    return;
                }
            }
            if (onGround)
            {
                // Jauge de braises pleine : le phénix renaît au lieu de s'éteindre.
                if (RebirthReady)
                {
                    Rebirth();
                    return;
                }
                SetState(GameState.Over);
                _events |= GameEvents.Swoosh;
            }
        }

        void BeginTransition()
        {
            _fadeToTitle = false;
            _events |= GameEvents.Swoosh;
            _fadeOut = _cfg.FadeTime;
            _fadeIn = 0f;
        }

        void UpdateTransition(float dt)
        {
            if (_fadeOut > 0f)
            {
                _fadeOut -= dt;
                if (_fadeOut <= 0f)
                {
                    _fadeOut = 0f;
                    ResetRun();
                    if (_fadeToTitle) SetState(GameState.Title);
                    _fadeToTitle = false;
                    _fadeIn = _cfg.FadeTime;
                }
            }
            else if (_fadeIn > 0f)
            {
                _fadeIn = Math.Max(0f, _fadeIn - dt);
            }
        }

        void UpdateDieSound(float dt)
        {
            if (_dieSoundTimer <= 0f) return;
            _dieSoundTimer -= dt;
            if (_dieSoundTimer <= 0f)
            {
                _dieSoundTimer = 0f;
                _events |= GameEvents.Die;
            }
        }

        void SetState(GameState state)
        {
            State = state;
            StateTime = 0f;
            _events |= GameEvents.StateChanged;
        }

        // Accès réservés aux tests.
        internal void ForcePlaying()
        {
            SetState(GameState.Playing);
        }

        internal void ForceScore(int score)
        {
            Score = score;
        }

        internal void ForceBoost(float seconds)
        {
            _boostTime = seconds;
        }

        internal void ForceEmbers(int embers)
        {
            Embers = embers;
        }
    }
}
