using System;

namespace PuffyBird.Core
{
    /// <summary>
    /// Simulation complète d'une partie (§5, §19), indépendante du rendu et de Unity.
    /// Chaque appel à <see cref="Step"/> avance d'un pas fixe <see cref="GameConfig.Step"/>.
    /// Les entrées sont mises en file et traitées au début du pas suivant (§15).
    /// </summary>
    public sealed class GameSimulation
    {
        const int MaxPendingPresses = 8;

        readonly GameConfig _cfg;
        readonly IScoreStorage _storage;
        readonly Rng _rng;
        // Tirages des extensions (tuyaux mobiles, étoiles) : séparés, pour que la suite des
        // ouvertures reste celle de la spec à graine égale.
        readonly Rng _bonusRng;
        readonly PipeField _pipes = new PipeField(4);
        readonly StarField _stars = new StarField(4);
        readonly Bird _bird = new Bird();
        readonly bool _bestReadable;

        int _pendingPresses;
        float _dieSoundTimer;
        float _fadeOut;
        float _fadeIn;
        bool _fadeToTitle;
        int _pairsSpawned;
        float _boostTime;
        float _speedFactor = 1f;
        GameEvents _events;

        public GameSimulation(GameConfig cfg, IScoreStorage storage, uint seed, bool startOnTitle = true)
        {
            _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
            _storage = storage ?? new MemoryScoreStorage();
            _rng = new Rng(seed);
            _bonusRng = new Rng(seed ^ 0x9E3779B9u);
            _bestReadable = _storage.TryReadBest(out int best);
            Best = _bestReadable ? Math.Max(0, best) : 0;
            ResetRun();
            if (startOnTitle) SetState(GameState.Title);
            _events = GameEvents.None;
        }

        public GameConfig Config => _cfg;
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
        /// <summary>Multiplicateur du défilement : 1, ou jusqu'à <see cref="GameConfig.StarBoostFactor"/> après une étoile.</summary>
        public float SpeedFactor => _speedFactor;
        /// <summary>Secondes d'accélération restantes (0 sans étoile).</summary>
        public float BoostTime => _boostTime;
        /// <summary>Intensité de l'accélération dans [0, 1], pour les effets visuels.</summary>
        public float BoostAmount => (_speedFactor - 1f) / (_cfg.StarBoostFactor - 1f);

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
        /// <summary>Distance totale défilée en px ; figée pendant DYING et OVER.</summary>
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
        public bool DiedOnGround { get; private set; }
        public Medal Medal => Medals.For(Score, _cfg);
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

            bool scrolling = State == GameState.Title || State == GameState.Ready || State == GameState.Playing;
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

            if (State == GameState.Playing || State == GameState.Dying)
            {
                float rotSpeed = State == GameState.Dying ? _cfg.RotSpeedDying : _cfg.RotSpeed;
                _bird.Integrate(dt, rotSpeed, _cfg);
                if (_bird.Y + _cfg.BirdHeight >= _cfg.GroundY)
                {
                    _bird.Y = _cfg.GroundY - _cfg.BirdHeight;
                    Die(true);
                    return;
                }
            }

            if (State != GameState.Playing) return;

            if (_pipes.Advance(dt, _rng, _cfg, _viewMargin, _speedFactor)) OnPairSpawned();
            _pipes.UpdateMotion(Time, _cfg);
            _stars.Advance(_cfg.ScrollSpeed * _speedFactor * dt, _cfg, _viewMargin);

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
            for (int i = 0; i < _pipes.Count; i++)
            {
                ref var p = ref _pipes[i];
                // Le score est testé avant la collision de la même paire (§9.3).
                if (!p.Scored && cx >= p.X + _cfg.PipeWidth * 0.5f)
                {
                    p.Scored = true;
                    Score++;
                    _events |= GameEvents.Point;
                }
                if (Collision.HitsPipe(cx, cy, _cfg.BirdRadius, p.OpeningTop, p.X, _cfg))
                {
                    HitPipeId = p.Id;
                    Die(false);
                    return;
                }
            }
        }

        /// <summary>Nouvelle partie : décor tiré au hasard, état READY (§19 resetRun).</summary>
        public void ResetRun()
        {
            // L'oiseau est toujours de la même couleur, mais le tirage de §6.8 est conservé :
            // la suite des ouvertures pour une graine donnée reste celle de la spec.
            _rng.Range(0, 2);
            BirdColor = _cfg.BirdColor;
            Theme = (Theme)_rng.Range(0, _cfg.ThemeCount - 1);
            _bird.Reset(_cfg.BirdStartY);
            _pipes.Clear();
            _stars.Clear();
            _pairsSpawned = 0;
            _boostTime = 0f;
            _speedFactor = 1f;
            Score = 0;
            NewBest = false;
            Flash = 0f;
            HitPipeId = -1;
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
                    _pipes.SpawnRandom(_cfg.FirstPipeX, _rng, _cfg);
                    OnPairSpawned();
                    Flap();
                    break;
                case GameState.Playing:
                    Flap();
                    break;
                case GameState.Paused:
                    Resume();
                    break;
                case GameState.Over:
                    if (StateTime > _cfg.OverInputDelay) BeginTransition();
                    break;
            }
        }

        /// <summary>
        /// Extensions appliquées à la paire qui vient d'apparaître : mouvement vertical à partir de
        /// <see cref="GameConfig.MovingPipesFromScore"/> paires franchies, et parfois une étoile de
        /// vitesse à mi-chemin de la paire précédente, à la hauteur moyenne des deux ouvertures.
        /// </summary>
        void OnPairSpawned()
        {
            int index = _pairsSpawned++;
            ref var last = ref _pipes.Last;
            if (index >= _cfg.MovingPipesFromScore)
            {
                last.MoveAmplitude = _cfg.PipeMoveAmplitude;
                last.MovePhase = (float)(_bonusRng.NextFloat() * 2.0 * Math.PI);
                last.Shift = last.ShiftAt(Time, _cfg);
                last.PrevShift = last.Shift;
            }
            if (index >= _cfg.StarFirstPair && _pipes.Count >= 2 && _bonusRng.NextFloat() < _cfg.StarChance)
            {
                ref var prev = ref _pipes[_pipes.Count - 2];
                float x = (prev.X + _cfg.PipeWidth + last.X) * 0.5f;
                float y = (prev.GapTop + last.GapTop + _cfg.PipeGap) * 0.5f + (_bonusRng.NextFloat() * 2f - 1f) * _cfg.StarJitter;
                _stars.Spawn(x, y);
            }
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
                if (Score > Best)
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

        internal void ForceBoost(float seconds)
        {
            _boostTime = seconds;
        }
    }
}
