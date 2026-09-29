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
        readonly PipeField _pipes = new PipeField(4);
        readonly Bird _bird = new Bird();
        readonly bool _bestReadable;

        int _pendingPresses;
        float _dieSoundTimer;
        float _fadeOut;
        float _fadeIn;
        GameEvents _events;

        public GameSimulation(GameConfig cfg, IScoreStorage storage, uint seed, bool startOnTitle = true)
        {
            _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
            _storage = storage ?? new MemoryScoreStorage();
            _rng = new Rng(seed);
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

            if (State == GameState.Paused) return;

            Time += dt;
            StateTime += dt;
            if (Flash > 0f) Flash = Math.Max(0f, Flash - dt);
            UpdateTransition(dt);
            UpdateDieSound(dt);

            bool scrolling = State == GameState.Title || State == GameState.Ready || State == GameState.Playing;
            if (scrolling)
            {
                ScrollDistance += _cfg.ScrollSpeed * dt;
                GroundOffset = (GroundOffset + _cfg.ScrollSpeed * dt) % _cfg.GroundPattern;
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

            _pipes.Advance(dt, _rng, _cfg);

            float cx = _cfg.BirdCenterX;
            float cy = _bird.CenterY(_cfg);
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
                if (Collision.HitsPipe(cx, cy, _cfg.BirdRadius, p.GapTop, p.X, _cfg))
                {
                    HitPipeId = p.Id;
                    Die(false);
                    return;
                }
            }
        }

        /// <summary>Nouvelle partie : couleur et décor tirés au hasard, état READY (§19 resetRun).</summary>
        public void ResetRun()
        {
            BirdColor = (BirdColor)_rng.Range(0, 2);
            Theme = (Theme)_rng.Range(0, 1);
            _bird.Reset(_cfg.BirdStartY);
            _pipes.Clear();
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
    }
}
