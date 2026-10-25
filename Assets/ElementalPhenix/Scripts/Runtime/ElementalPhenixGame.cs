using ElementalPhenix.Ads;
using ElementalPhenix.Audio;
using ElementalPhenix.Core;
using ElementalPhenix.Feedback;
using ElementalPhenix.Metrics;
using ElementalPhenix.Rendering;
using ElementalPhenix.Social;
using ElementalPhenix.Store;
using ElementalPhenix.UI;
using UnityEngine;

namespace ElementalPhenix
{
    /// <summary>
    /// Point d'entrée du jeu : construit toute la scène par code (caméra, lumières, décor,
    /// tuyaux, oiseau, interface, sons), fait avancer la simulation à pas fixe et synchronise
    /// le rendu. La simulation (<see cref="GameSimulation"/>) ne connaît pas Unity ; ce composant
    /// ne fait que la lire.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ElementalPhenixGame : MonoBehaviour
    {
        [Tooltip("Le pilote automatique joue tout seul (démo, tests visuels). Touche B pour basculer.")]
        [SerializeField] bool autoPilot;

        [Tooltip("Accessibilité : atténue le flash blanc d'impact et supprime la secousse (§21.4).")]
        [SerializeField] bool reduceFlash;

        [Tooltip("Images par seconde visées sur mobile.")]
        [SerializeField] int targetFrameRate = 60;

        GameConfig _cfg;
        GameSimulation _sim;
        FixedStepClock _clock;
        InputReader _input;
        WorldSpace _space;
        CameraRig _cameraRig;
        LightingRig _lighting;
        PostFxController _postFx;
        SceneryView _scenery;
        WeatherView _weather;
        PipeView _pipes;
        StarView _stars;
        BirdView _bird;
        BoostTrailView _trail;
        StageFxView _stageFx;
        ShardView _shardView;
        CurrentView _currentView;
        GuardianView _guardian;
        WakeTrailView _wake;
        WakesView _wakes;
        JourneyMapView _journeyMap;
        /// <summary>Éclats de la partie déjà comptés, et ceux pas encore enregistrés (enregistrés hors du jeu).</summary>
        int _seenShards;
        int _pendingShards;
        UiLayer _ui;
        HudView _hud;
        MenuView _menu;
        SfxPlayer _sfx;
        MusicPlayer _music;
        GamePrefs _prefs;
        Haptics _haptics;
        Leaderboard _leaderboard;
        IStore _store;
        SkinsView _skins;
        int _shownSkin = -1;
        int _bestAtRunStart;
        /// <summary>L'oiseau de la série est déjà débloqué : inutile de compter plus loin.</summary>
        bool _streakSkinOwned;
        /// <summary>La série de 30 jours vient d'être atteinte pendant cette partie.</summary>
        bool _streakUnlockedThisRun;
        IBannerAds _banner;
        IRewardedAds _rewarded;
        /// <summary>Secondes d'attente restantes de la vidéo de la seconde chance (0 = pas demandée).</summary>
        float _continueWait;
        IGameMetrics _metrics;
        float _playSeconds;
        bool _bannerShown;
        int _shownRun = -1;
        PerformanceMonitor _perf;
        int _nearMissesThisRun;
        int _today;
        float _todayRefreshed = -100f;
        bool _sharing;
        float _realTime;

        public GameSimulation Simulation => _sim;

        void Awake()
        {
            Application.targetFrameRate = targetFrameRate;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            _cfg = GameConfig.CreateDefault();
            _prefs = new GamePrefs();
            _streakSkinOwned = _prefs.LongestStreak >= _cfg.StreakDaysForSkin;
            _haptics = new Haptics { Enabled = _prefs.Haptics };
            _sim = new GameSimulation(_cfg, new PlayerPrefsScoreStorage(), (uint)System.Environment.TickCount);
            _clock = new FixedStepClock(_cfg.Step, _cfg.MaxFrameDelta);
            _input = new InputReader();
            _space = new WorldSpace(_cfg);
            _banner = AdServices.CreateBanner();
            _rewarded = AdServices.CreateRewarded();
            _metrics = MetricsServices.Create();

            var materials = new MaterialLibrary();
            var world = transform;

            var camera = Camera.main;
            if (camera == null)
            {
                var camGo = new GameObject("Caméra") { tag = "MainCamera" };
                camera = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            _cameraRig = new CameraRig(camera, _space);
            _lighting = new LightingRig(world, materials);
            _postFx = new PostFxController(world);
            _scenery = new SceneryView(world, materials);
            _weather = new WeatherView(world, materials, _space);
            _pipes = new PipeView(world, materials, _space);
            _stars = new StarView(world, materials, _space, _sim.Stars.Capacity);
            _bird = new BirdView(world, materials, _space);
            _trail = new BoostTrailView(world, materials, _space);
            _stageFx = new StageFxView(world, materials, _space);
            _shardView = new ShardView(world, materials, _space, _sim.Shards.Capacity);
            _currentView = new CurrentView(world, materials, _space, _sim.Currents.Capacity);
            _guardian = new GuardianView(world, materials, _space);
            _wake = new WakeTrailView(world, materials);
            _ui = new UiLayer(world, materials, _space);
            _hud = new HudView(_ui, materials);
            _menu = new MenuView(_ui);
            _leaderboard = new Leaderboard();
            _store = StoreServices.Create();
            _skins = new SkinsView(_ui, _store, !(_store is NoStore), IsSkinUnlocked, () => SelectedSkin, CurrentStreakDays);
            _menu.Skins = _skins;
            _wakes = new WakesView(_ui, () => _prefs.Shards, () => _prefs.WakesOwned, () => Wakes.IndexOf(_prefs.Wake));
            _menu.Wakes = _wakes;
            _journeyMap = new JourneyMapView(_ui);
            _menu.JourneyMap = _journeyMap;
            _menu.JourneyCleared = () => Mathf.Min(_prefs.JourneyCleared, _cfg.Journey.Length);
            _menu.ShardCount = () => _prefs.Shards + _pendingShards;
            if (PrivacyConsentNeeded)
            {
                _menu.Consent = new ConsentView(_ui);
                _menu.ConsentChoice = () => AdServices.Consent;
            }
            _menu.LeaderboardAvailable = _leaderboard.Available;
            _menu.DailyBestScore = () => _prefs.DailyBest(Today);
            _menu.JourneyStage = NextJourneyStage;
            _menu.ContinueAvailable = () => _sim.CanContinue && _rewarded.Enabled;
            _menu.AddSettingsRow(UiAction.ToggleMusic, () => _prefs.Music ? 0 : 1, null, Lang.T("MUSIQUE : OUI", "MUSIC ON", "MÚSICA: SÍ", "MUSIK: AN", "MÚSICA: SIM"), Lang.T("MUSIQUE : NON", "MUSIC OFF", "MÚSICA: NO", "MUSIK: AUS", "MÚSICA: NÃO"));
            _menu.AddSettingsRow(UiAction.ToggleSound, () => _sim.Muted ? 1 : 0, null, Lang.T("SONS : OUI", "SOUND ON", "SONIDO: SÍ", "TON: AN", "SOM: SIM"), Lang.T("SONS : NON", "SOUND OFF", "SONIDO: NO", "TON: AUS", "SOM: NÃO"));
            _menu.AddSettingsRow(UiAction.ToggleHaptics, () => _prefs.Haptics ? 0 : 1, null, Lang.T("VIBRATIONS : OUI", "VIBRATION ON", "VIBRACIÓN: SÍ", "VIBRATION: AN", "VIBRAÇÃO: SIM"), Lang.T("VIBRATIONS : NON", "VIBRATION OFF", "VIBRACIÓN: NO", "VIBRATION: AUS", "VIBRAÇÃO: NÃO"));
            _menu.AddSettingsRow(UiAction.CycleQuality, () => (int)CurrentQuality, null, 
                Lang.T("QUALITÉ : BASSE", "QUALITY: LOW", "CALIDAD: BAJA", "QUALITÄT: NIEDRIG", "QUALIDADE: BAIXA"),
                Lang.T("QUALITÉ : MOYENNE", "QUALITY: MEDIUM", "CALIDAD: MEDIA", "QUALITÄT: MITTEL", "QUALIDADE: MÉDIA"),
                Lang.T("QUALITÉ : HAUTE", "QUALITY: HIGH", "CALIDAD: ALTA", "QUALITÄT: HOCH", "QUALIDADE: ALTA"));
            _menu.AddSettingsRow(UiAction.ToggleReduceFlash, () => _prefs.ReduceFlash ? 0 : 1, null, Lang.T("MOINS DE FLASHS : OUI", "REDUCE FLASHES: ON", "MENOS DESTELLOS: SÍ", "WENIGER BLITZE: AN", "MENOS FLASHES: SIM"), Lang.T("MOINS DE FLASHS : NON", "REDUCE FLASHES: OFF", "MENOS DESTELLOS: NO", "WENIGER BLITZE: AUS", "MENOS FLASHES: NÃO"));
            _menu.AddSettingsRow(UiAction.ToggleHighContrast, () => _prefs.HighContrast ? 0 : 1, null, Lang.T("TUYAUX CONTRASTÉS : OUI", "HIGH CONTRAST: ON", "ALTO CONTRASTE: SÍ", "HOHER KONTRAST: AN", "ALTO CONTRASTE: SIM"), Lang.T("TUYAUX CONTRASTÉS : NON", "HIGH CONTRAST: OFF", "ALTO CONTRASTE: NO", "HOHER KONTRAST: AUS", "ALTO CONTRASTE: NÃO"));
            _menu.AddSettingsRow(UiAction.OpenPrivacy, null, () => PrivacyConsentNeeded, Lang.T("CONFIDENTIALITÉ", "PRIVACY", "PRIVACIDAD", "DATENSCHUTZ", "PRIVACIDADE"));
            _menu.AddSettingsRow(UiAction.RemoveAds, null, () => AdServices.AdsEnabled && _store.Ready && !_store.Owns(Products.NoAds), Lang.T("SUPPRIMER LES PUBS", "REMOVE ADS", "QUITAR ANUNCIOS", "WERBUNG ENTFERNEN", "REMOVER ANÚNCIOS"));
            _menu.AddSettingsRow(UiAction.RestorePurchases, null, () => !(_store is NoStore), Lang.T("RESTAURER LES ACHATS", "RESTORE PURCHASES", "RESTAURAR COMPRAS", "KÄUFE WIEDERHERSTELLEN", "RESTAURAR COMPRAS"));
            _sfx = new SfxPlayer(world);
            _sfx.Muted = _sim.Muted;
            _music = MusicPlayer.Create(world);
            GraphicsQuality.Apply(CurrentQuality, _lighting, _postFx);
            _pipes.SetHighContrast(_prefs.HighContrast);
            _perf = new PerformanceMonitor(targetFrameRate);

            ApplyRun();

            // Consentement RGPD : demandé une fois avant toute pub ; la régie attend la réponse.
            if (_prefs.Consent == GamePrefs.ConsentUnknown) _menu.Open(MenuScreen.Consent);
            else AdServices.SetConsent(_prefs.Consent == GamePrefs.ConsentGranted);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _realTime += dt;

            var input = _input.Read();
            if (input.Mute) ToggleSound();
            if (input.ToggleAutoPilot) autoPilot = !autoPilot;
#if UNITY_EDITOR
            // Captures pour les stores : à la résolution de la vue Game, dans <projet>/Captures.
            if (input.Screenshot)
            {
                System.IO.Directory.CreateDirectory("Captures");
                string file = $"Captures/ElementalPhenix-{_sim.Theme}-{System.DateTime.Now:yyyyMMdd-HHmmss}.png";
                ScreenCapture.CaptureScreenshot(file);
                Debug.Log($"Capture enregistrée : {file} ({Screen.width} × {Screen.height})");
            }
#endif
            if (input.Pause)
            {
                // L'écran de consentement du premier lancement attend une réponse.
                if (_menu.Screen == MenuScreen.Consent)
                {
                    if (AdServices.Consent.HasValue) _menu.Back();
                }
                else if (_menu.IsOpen)
                {
                    _menu.Close();
                }
                else if (_sim.State == GameState.Paused) _sim.Resume();
                else _sim.Pause();
            }
            // Menu ouvert : seuls ses boutons réagissent, un tap ailleurs ne lance pas la partie.
            // Pendant l'attente de la vidéo de la seconde chance, un tap ne relance pas non plus.
            bool waitingAd = _continueWait > 0f;
            if (!_menu.IsOpen && !waitingAd)
            {
                for (int i = 0; i < input.Presses; i++) _sim.Press();
            }
            for (int i = 0; i < input.Taps; i++)
            {
                var action = _ui.HitTest(_cameraRig.ScreenToLogical(_input.TapPosition(i)));
                if (action != UiAction.None) OnUiAction(action);
                else if (!_menu.IsOpen && !waitingAd) _sim.Press();
            }
            UpdateContinue(dt);

            _cameraRig.UpdateViewport();
            _sim.ViewMargin = _cameraRig.SideMarginPx;
            // Pouvoir d'élément : celui du phénix choisi (le phénix de départ tant que le choix n'est pas débloqué).
            int played = SelectedSkin;
            _sim.Element = Skins.Get(IsSkinUnlocked(played) ? played : 0).Element;
            int steps = _clock.Advance(dt);
            for (int i = 0; i < steps; i++)
            {
                if (autoPilot && AutoPilot.ShouldFlap(_sim)) _sim.Press();
                if (autoPilot && _sim.CanRestart) _sim.Press();
                _sim.Step();
            }

            // Durée de jeu de la partie (ou depuis la seconde chance), pour la mesure d'audience.
            if (_sim.State == GameState.Ready) _playSeconds = 0f;
            else if (_sim.State == GameState.Playing) _playSeconds += dt;

            var events = _sim.ConsumeEvents();
            if (_sim.RunId != _shownRun) ApplyRun();
            React(events);
            BankShards();
            UpdateBanner();
            // Meilleur score envoyé au classement dès qu'il monte (à la mort) et que le joueur est connecté.
            _leaderboard.SubmitBest(_sim.Best);
            _menu.NewSkinUnlocked = _streakUnlockedThisRun || Skins.NewlyUnlocked(_bestAtRunStart, _sim.Best, _cfg) > 0;
            _leaderboard.Update();
            ReportAchievements();
            MonitorPerformance(dt);
            // Musique baissée pendant la pause.
            _music.SetVolume(!_prefs.Music ? 0f : (_sim.State == GameState.Paused ? 0.4f : 1f));
            Render(dt);
        }

        void OnUiAction(UiAction action)
        {
            switch (action)
            {
                case UiAction.Pause:
                    _sim.Pause();
                    break;
                case UiAction.OpenSettings:
                    // Pendant la partie, l'oiseau est figé tant que les paramètres sont ouverts.
                    _sim.Pause();
                    _menu.Open(MenuScreen.Settings);
                    break;
                case UiAction.CloseMenu:
                    _menu.Close();
                    break;
                case UiAction.GoHome:
                    _metrics.QuitToTitle(_sim.Score);
                    _menu.Close();
                    _sim.QuitToTitle();
                    break;
                case UiAction.Continue:
                    // Vidéo pas encore chargée : on l'attend un peu (UpdateContinue).
                    if (_sim.CanContinue && _rewarded.Enabled && _continueWait <= 0f) _continueWait = _cfg.ContinueAdWait;
                    break;
                case UiAction.CycleQuality:
                    _prefs.Quality = ((int)CurrentQuality + 1) % GraphicsQuality.Count;
                    GraphicsQuality.Apply(CurrentQuality, _lighting, _postFx);
                    break;
                case UiAction.RemoveAds:
                    _store.Buy(Products.NoAds);
                    break;
                case UiAction.RestorePurchases:
                    _store.Restore();
                    break;
                case UiAction.OpenPrivacy:
                    _menu.Open(MenuScreen.Consent);
                    break;
                case UiAction.ConsentAccept:
                case UiAction.ConsentRefuse:
                    bool granted = action == UiAction.ConsentAccept;
                    _prefs.Consent = granted ? GamePrefs.ConsentGranted : GamePrefs.ConsentRefused;
                    AdServices.SetConsent(granted);
                    _menu.Back();
                    break;
                case UiAction.OpenPrivacyPolicy:
                    Application.OpenURL(AdServices.PrivacyPolicyUrl);
                    break;
                case UiAction.PlayDaily:
                    _sim.StartDaily(DailyChallenge.SeedForDay(Today));
                    break;
                case UiAction.PlayJourney:
                    _menu.Open(MenuScreen.Journey);
                    break;
                case UiAction.MapPlay:
                    if (JourneyMapView.IsOpen(_journeyMap.Selected, _prefs.JourneyCleared))
                    {
                        _menu.Close();
                        _sim.StartJourney(_journeyMap.Selected);
                    }
                    break;
                case UiAction.OpenWakes:
                    _menu.Open(MenuScreen.Wakes);
                    break;
                case UiAction.WakePrevious:
                    _wakes.Step(-1);
                    break;
                case UiAction.WakeNext:
                    _wakes.Step(1);
                    break;
                case UiAction.WakeUse:
                    if (Wakes.IsOwned(_wakes.Browsed, _prefs.WakesOwned))
                    {
                        _prefs.Wake = Wakes.Get(_wakes.Browsed).Id;
                        _haptics.Play(HapticKind.Medium);
                    }
                    break;
                case UiAction.WakeUnlock:
                    if (_prefs.UnlockWake(_wakes.Browsed))
                    {
                        _prefs.Wake = Wakes.Get(_wakes.Browsed).Id;
                        _haptics.Play(HapticKind.Heavy);
                        _trail.OnMilestone(_bird.Position, Medal.Platinum);
                    }
                    break;
                case UiAction.ToggleReduceFlash:
                    _prefs.ReduceFlash = !_prefs.ReduceFlash;
                    break;
                case UiAction.ToggleHighContrast:
                    _prefs.HighContrast = !_prefs.HighContrast;
                    _pipes.SetHighContrast(_prefs.HighContrast);
                    break;
                case UiAction.OpenSkins:
                    _menu.Open(MenuScreen.Skins);
                    break;
                case UiAction.SkinPrevious:
                    _skins.Step(-1);
                    break;
                case UiAction.SkinNext:
                    _skins.Step(1);
                    break;
                case UiAction.SkinUse:
                    if (IsSkinUnlocked(_skins.Browsed))
                    {
                        _prefs.Skin = Skins.Get(_skins.Browsed).Id;
                        _metrics.SkinSelected(_prefs.Skin);
                        _haptics.Play(HapticKind.Medium);
                    }
                    break;
                case UiAction.SkinBuy:
                    string product = Skins.Get(_skins.Browsed).ProductId;
                    if (product != null) _store.Buy(product);
                    break;
                case UiAction.OpenLeaderboard:
                    _leaderboard.Show();
                    break;
                case UiAction.Share:
                    if (!_sharing) StartCoroutine(ShareScoreWithImage());
                    break;
                case UiAction.ToggleMusic:
                    _prefs.Music = !_prefs.Music;
                    break;
                case UiAction.ToggleSound:
                    ToggleSound();
                    break;
                case UiAction.MapNode0:
                case UiAction.MapNode1:
                case UiAction.MapNode2:
                case UiAction.MapNode3:
                case UiAction.MapNode4:
                case UiAction.MapNode5:
                case UiAction.MapNode6:
                case UiAction.MapNode7:
                case UiAction.MapNode8:
                    _journeyMap.Select(action - UiAction.MapNode0);
                    break;
                case UiAction.ToggleHaptics:
                    _prefs.Haptics = !_prefs.Haptics;
                    _haptics.Enabled = _prefs.Haptics;
                    _haptics.Play(HapticKind.Medium);
                    break;
            }
        }

        /// <summary>Numéro du jour local, relu toutes les 30 s (évite de convertir l'heure à chaque image).</summary>
        int Today
        {
            get
            {
                if (_realTime - _todayRefreshed > 30f)
                {
                    _todayRefreshed = _realTime;
                    _today = DailyStreak.DayNumber(System.DateTime.Now);
                }
                return _today;
            }
        }

        /// <summary>Accessibilité : réglage du joueur, ou option de l'inspecteur.</summary>
        bool ReduceFlash => reduceFlash || _prefs.ReduceFlash;

        /// <summary>
        /// Partage du score : capture de l'écran de fin sans ses boutons (une image à blanc), envoyée
        /// avec le message. Hors partie, donc sans effet sur la fluidité.
        /// </summary>
        System.Collections.IEnumerator ShareScoreWithImage()
        {
            _sharing = true;
            int score = _sim.Score;
            _menu.HideOverlay = true;
            yield return null;
            yield return new WaitForEndOfFrame();
            byte[] jpeg = null;
            try
            {
                var shot = ScreenCapture.CaptureScreenshotAsTexture();
                if (shot != null)
                {
                    jpeg = shot.EncodeToJPG(88);
                    Destroy(shot);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"ElementalPhenix : capture impossible ({e.Message}).");
            }
            _menu.HideOverlay = false;
            ShareService.ShareScore(score, jpeg);
            _sharing = false;
        }

        /// <summary>Envoie à la plateforme les succès gagnés (hors connexion, plus tard).</summary>
        void ReportAchievements()
        {
            var tracker = _prefs.Achievements;
            bool changed = false;
            while (tracker.NextToReport() is Achievement next)
            {
                if (!_leaderboard.Unlock(next)) break;
                tracker.MarkReported(next);
                changed = true;
            }
            if (changed) _prefs.SaveAchievements();
        }

        void Unlock(Achievement achievement)
        {
            if (_prefs.Achievements.Unlock(achievement)) _prefs.SaveAchievements();
        }

        /// <summary>Mesure la fluidité en partie ; si le joueur n'a pas choisi sa qualité, la baisse d'un cran quand l'appareil peine.</summary>
        void MonitorPerformance(float dt)
        {
#if !UNITY_EDITOR
            if (_sim.State != GameState.Playing)
            {
                _perf.Reset();
                return;
            }
            if (_prefs.Quality >= 0 || !_perf.Record(dt)) return;
            int level = (int)CurrentQuality;
            if (level <= 0) return;
            _prefs.AutoQuality = level - 1;
            GraphicsQuality.Apply(CurrentQuality, _lighting, _postFx);
            _perf.Reset();
            Debug.Log($"ElementalPhenix : {_perf.LastFps:0} images/s, qualité abaissée au niveau {(GraphicsLevel)_prefs.AutoQuality}.");
#endif
        }

        /// <summary>
        /// Seconde chance demandée : la vidéo est montrée dès qu'elle est chargée. Si aucune ne l'est
        /// au bout de <see cref="GameConfig.ContinueAdWait"/> secondes (pas de réseau, pas de pub
        /// disponible), la partie reprend sans vidéo plutôt que de priver le joueur de sa chance.
        /// </summary>
        void UpdateContinue(float dt)
        {
            if (_continueWait <= 0f) return;
            if (!_sim.CanContinue)
            {
                _continueWait = 0f;
                return;
            }
            if (_rewarded.IsReady)
            {
                _continueWait = 0f;
                _rewarded.Show(OnRewardedFinished);
                return;
            }
            _continueWait -= dt;
            if (_continueWait > 0f) return;
            _continueWait = 0f;
            Debug.Log("ElementalPhenix : pas de vidéo récompensée disponible, seconde chance accordée sans pub.");
            OnRewardedFinished(true);
        }

        /// <summary>Vidéo de la seconde chance terminée : la partie reprend si elle a été vue jusqu'au bout.</summary>
        void OnRewardedFinished(bool rewarded)
        {
            if (!rewarded || !_sim.CanContinue) return;
            _metrics.ContinueUsed(_sim.Score);
            _sim.ContinueRun();
        }

        /// <summary>Une régie ou une mesure d'audience est branchée : le consentement est demandé.</summary>
        static bool PrivacyConsentNeeded => AdServices.AdsEnabled || MetricsServices.Enabled;

        /// <summary>Qualité choisie dans les réglages, sinon déduite de l'appareil.</summary>
        GraphicsLevel CurrentQuality => _prefs.Quality >= 0 && _prefs.Quality < GraphicsQuality.Count
            ? (GraphicsLevel)_prefs.Quality
            : (_prefs.AutoQuality >= 0 && _prefs.AutoQuality < GraphicsQuality.Count ? (GraphicsLevel)_prefs.AutoQuality : GraphicsQuality.Auto());

        /// <summary>Jours de suite joués, tel qu'affiché (0 si un jour a été manqué ; bloqué à l'objectif une fois atteint).</summary>
        int CurrentStreakDays()
        {
            if (_streakSkinOwned) return _cfg.StreakDaysForSkin;
            return DailyStreak.Current(_prefs.StreakDay, _prefs.Streak, DailyStreak.DayNumber(System.DateTime.Now));
        }

        /// <summary>Oiseau choisi par le joueur (préférence), même s'il n'est pas encore disponible.</summary>
        int SelectedSkin => Skins.IndexOf(_prefs.Skin);

        bool IsSkinUnlocked(int index)
        {
            var skin = Skins.Get(index);
            string product = skin.ProductId;
            return Skins.IsUnlocked(skin, _sim.Best, _cfg, product != null && _store.Owns(product), _prefs.LongestStreak);
        }

        /// <summary>
        /// Oiseau affiché : celui parcouru dans le menu des oiseaux, sinon celui choisi s'il est
        /// débloqué (un achat pas encore confirmé par la boutique ne change pas la préférence).
        /// </summary>
        int DisplayedSkin()
        {
            if (_menu.Screen == MenuScreen.Skins) return _skins.Browsed;
            int selected = SelectedSkin;
            return IsSkinUnlocked(selected) ? selected : 0;
        }

        /// <summary>Étape du Voyage proposée sur l'écran titre : la première non réussie, ou la première une fois le Voyage terminé.</summary>
        int NextJourneyStage() => _prefs.JourneyCleared >= _cfg.Journey.Length ? 0 : _prefs.JourneyCleared;

        /// <summary>
        /// Éclats ramassés : comptés tout de suite, enregistrés dès que le phénix ne vole plus (fin de
        /// partie, attente du tap) pour ne jamais écrire sur le disque pendant le vol.
        /// </summary>
        void BankShards()
        {
            int delta = _sim.ShardsThisRun - _seenShards;
            if (delta > 0)
            {
                _pendingShards += delta;
                _seenShards = _sim.ShardsThisRun;
            }
            if (_pendingShards > 0 && _sim.State != GameState.Playing && _sim.State != GameState.Dying)
            {
                _prefs.AddShards(_pendingShards);
                _pendingShards = 0;
            }
        }

        void ToggleSound()
        {
            _sim.Muted = !_sim.Muted;
            _sfx.Muted = _sim.Muted;
        }

        /// <summary>Règle de l'étape du Voyage en cours, dont les effets sont montrés (aucune hors Voyage).</summary>
        StageRule StageRuleShown => _sim.IsJourney ? _cfg.Journey[_sim.JourneyStage].Rule : StageRule.FirstFlight;

        void React(GameEvents events)
        {
            _sfx.Play(events);
            if ((events & GameEvents.Flap) != 0) _stageFx.OnFlap(_bird.Position);
            if ((events & GameEvents.Shard) != 0)
            {
                _trail.OnNearMiss(_bird.Position);
                _haptics.Play(HapticKind.Light);
            }
            if ((events & GameEvents.EmbersFull) != 0) _haptics.Play(HapticKind.Medium);
            if ((events & GameEvents.PowerReady) != 0) _haptics.Play(HapticKind.Light);
            if ((events & GameEvents.ElementPower) != 0)
            {
                _hud.ShowPower(_sim.Element);
                _trail.OnStar(_bird.Position);
                _haptics.Play(HapticKind.Heavy);
            }
            if ((events & GameEvents.ShadowStep) != 0)
            {
                // L'ombre a avalé le choc : étincelles là où le phénix réapparaît.
                _trail.OnNearMiss(_bird.Position);
                _haptics.Play(HapticKind.Medium);
            }
            if ((events & GameEvents.Rebirth) != 0)
            {
                _trail.OnMilestone(_bird.Position, Medal.Gold);
                _trail.OnStar(_bird.Position);
                _hud.ShowRebirth();
                _haptics.Play(HapticKind.Heavy);
                if (!ReduceFlash) _cameraRig.Shake(0.03f, 0.3f);
            }
            if ((events & GameEvents.Guardian) != 0)
            {
                _hud.ShowGuardian();
                _haptics.Play(HapticKind.Medium);
                if (!ReduceFlash) _cameraRig.Shake(0.02f, 0.4f);
            }
            if ((events & GameEvents.RunStarted) != 0)
            {
                _nearMissesThisRun = 0;
                if (_sim.IsDaily) Unlock(Achievement.DailyChallenge);
            }
            if ((events & GameEvents.Point) != 0 && !_sim.IsJourney)
            {
                _prefs.Achievements.UnlockForScore(_sim.Score, _cfg);
                _prefs.SaveAchievements();
            }
            if ((events & GameEvents.RunStarted) != 0 && !_streakSkinOwned)
            {
                _prefs.RecordPlayDay(DailyStreak.DayNumber(System.DateTime.Now));
                if (_prefs.LongestStreak >= _cfg.StreakDaysForSkin)
                {
                    _streakSkinOwned = true;
                    _streakUnlockedThisRun = true;
                }
                _prefs.Achievements.UnlockForStreak(_prefs.LongestStreak, _cfg);
                _prefs.SaveAchievements();
            }
            if ((events & GameEvents.Flap) != 0)
            {
                _bird.OnFlap();
                _haptics.Play(HapticKind.Light);
            }
            if ((events & GameEvents.Star) != 0)
            {
                _trail.OnStar(_bird.Position);
                _haptics.Play(HapticKind.Medium);
                Unlock(Achievement.FirstStar);
            }
            if ((events & GameEvents.StarShield) != 0)
            {
                // L'étoile a encaissé le choc : étincelles, tuyau qui vibre, la partie continue.
                _trail.OnNearMiss(_bird.Position);
                _pipes.Hit(_sim.ShieldPipeId);
                _haptics.Play(HapticKind.Heavy);
                if (!ReduceFlash) _cameraRig.Shake(0.03f, 0.2f);
            }
            if ((events & GameEvents.NearMiss) != 0)
            {
                _trail.OnNearMiss(_bird.Position);
                _haptics.Play(HapticKind.Medium);
                if (++_nearMissesThisRun >= AchievementTracker.CloseCallsRequired) Unlock(Achievement.CloseCalls);
            }
            if ((events & GameEvents.Milestone) != 0)
            {
                _trail.OnMilestone(_bird.Position, Medals.For(_sim.Score, _cfg));
                _hud.PulseScore();
                _haptics.Play(HapticKind.Medium);
                if (!ReduceFlash) _cameraRig.Shake(0.02f, 0.2f);
            }
            if ((events & GameEvents.StageCleared) != 0)
            {
                _prefs.RecordJourneyStage(_sim.JourneyStage);
                _trail.OnMilestone(_bird.Position, Medal.Platinum);
                _hud.PulseScore();
                _haptics.Play(HapticKind.Medium);
                if (!ReduceFlash) _cameraRig.Shake(0.02f, 0.2f);
            }
            if ((events & GameEvents.Hit) != 0)
            {
                _haptics.Play(HapticKind.Heavy);
                _metrics.PlayerDied(_sim.Score, _sim.Theme.ToString(), _playSeconds, _sim.Continued);
                if (_sim.IsDaily) _prefs.RecordDaily(Today, _sim.Score);
                _bird.OnHit();
                _pipes.Hit(_sim.HitPipeId);
                if (!ReduceFlash) _cameraRig.Shake(0.05f, 0.25f);
            }
        }

        void UpdateBanner()
        {
            bool show = AdPolicy.BannerVisible(_sim, adsRemoved: _store.Owns(Products.NoAds));
            if (show == _bannerShown) return;
            _bannerShown = show;
            _banner.SetVisible(show);
        }

        /// <summary>Nouvelle partie : décor tiré au hasard (§8.2) et sa musique.</summary>
        void ApplyRun()
        {
            _shownRun = _sim.RunId;
            _seenShards = 0;
            _bestAtRunStart = _sim.Best;
            _streakUnlockedThisRun = false;
            var theme = Palette.Theme(_sim.Theme);
            _lighting.ApplyTheme(theme);
            _scenery.ApplyTheme(theme);
            _weather.ApplyTheme(theme);
            _postFx.ApplyTheme(theme);
            _pipes.SetTheme(_sim.Theme);
            _guardian.SetTheme(_sim.Theme);
            _cameraRig.Camera.backgroundColor = theme.SkyHorizon;
            _music.SetTheme(_sim.Theme);
        }

        void Render(float dt)
        {
            float alpha = _clock.Alpha;
            double scroll = _sim.PrevScrollDistance + (_sim.ScrollDistance - _sim.PrevScrollDistance) * alpha;

            _cameraRig.Update(dt, ReduceFlash);
            _lighting.Update(_realTime);
            _scenery.Update(scroll, _realTime);
            _weather.Update(scroll, dt, _realTime);
            // Éclairs atténués si l'option « réduire les flashs » est active (§20).
            float lightning = _weather.Flash * (ReduceFlash ? 0.2f : 1f);
            _scenery.SetFlash(lightning);
            _lighting.SetFlash(lightning);
            _pipes.Update(_sim.Pipes, _sim.Config.PipeGap, alpha, dt);
            _stars.Update(_sim.Stars, alpha, _realTime);
            _shardView.Update(_sim.Shards, alpha, _realTime, dt, _bird.Position);
            _currentView.Update(_sim.Currents, alpha, _realTime);
            _guardian.Update(_sim, dt, _realTime);
            int skin = DisplayedSkin();
            if (skin != _shownSkin)
            {
                _shownSkin = skin;
                _bird.SetSkin(skin);
                _trail.SetSkin(skin);
            }
            bool preview = _menu.Screen == MenuScreen.Skins || _menu.Screen == MenuScreen.Wakes;
            _bird.SetPreview(preview);
            _bird.Update(_sim, alpha, dt);
            _bird.SetBoost(_sim.State == GameState.Playing ? _sim.BoostAmount : 0f, _realTime, dt);
            _trail.Update(_sim, _bird.Position, dt, _realTime);
            _stageFx.Update(_sim, StageRuleShown, _bird.Position, dt);
            // Sillage : celui parcouru dans son menu (aperçu), sinon celui choisi, tant que le phénix vole.
            var state = _sim.State;
            bool flying = state == GameState.Title || state == GameState.Ready || state == GameState.Playing;
            _wake.SetWake(_menu.Screen == MenuScreen.Wakes ? _wakes.Browsed : Wakes.IndexOf(_prefs.Wake));
            float wakeScroll = preview ? 1.4f : (flying ? _sim.Config.ScrollSpeed * _sim.SpeedFactor / WorldSpace.PixelsPerUnit : 0f);
            _wake.Update(_bird.Position, flying || preview, wakeScroll, dt);
            _ui.BeginFrame();
            bool menuCoversTitle = _menu.Screen == MenuScreen.Settings || _menu.Screen == MenuScreen.Consent || _menu.Screen == MenuScreen.Journey;
            _hud.Update(_sim, _realTime, dt, _cameraRig.SafeTopPx, _menu.IsOpen, menuCoversTitle);
            _menu.Update(_sim, _cameraRig.SafeTopPx);
            _postFx.Update(_sim.Flash / _cfg.FlashTime, _sim.FadeAlpha, lightning, ReduceFlash, _sim.State == GameState.Playing ? _sim.SlowAmount : 0f);
        }

        // Pause automatique quand l'application passe en arrière-plan (§4.4).
        void OnApplicationPause(bool paused)
        {
            if (paused) _sim?.Pause();
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused) _sim?.Pause();
        }
    }
}
