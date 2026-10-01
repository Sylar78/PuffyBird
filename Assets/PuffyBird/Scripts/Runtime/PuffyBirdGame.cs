using PuffyBird.Ads;
using PuffyBird.Audio;
using PuffyBird.Core;
using PuffyBird.Rendering;
using PuffyBird.UI;
using UnityEngine;

namespace PuffyBird
{
    /// <summary>
    /// Point d'entrée du jeu : construit toute la scène par code (caméra, lumières, décor,
    /// tuyaux, oiseau, interface, sons), fait avancer la simulation à pas fixe et synchronise
    /// le rendu. La simulation (<see cref="GameSimulation"/>) ne connaît pas Unity ; ce composant
    /// ne fait que la lire.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PuffyBirdGame : MonoBehaviour
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
        HudView _hud;
        SfxPlayer _sfx;
        IBannerAds _banner;
        bool _bannerShown;
        int _shownRun = -1;
        float _realTime;

        public GameSimulation Simulation => _sim;

        void Awake()
        {
            Application.targetFrameRate = targetFrameRate;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            _cfg = GameConfig.CreateDefault();
            _sim = new GameSimulation(_cfg, new PlayerPrefsScoreStorage(), (uint)System.Environment.TickCount);
            _clock = new FixedStepClock(_cfg.Step, _cfg.MaxFrameDelta);
            _input = new InputReader();
            _space = new WorldSpace(_cfg);
            _banner = AdServices.CreateBanner();

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
            _hud = new HudView(world, materials, _space);
            _sfx = new SfxPlayer(world);
            _sfx.Muted = _sim.Muted;

            ApplyRun();
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _realTime += dt;

            var input = _input.Read();
            if (input.Mute)
            {
                _sim.Muted = !_sim.Muted;
                _sfx.Muted = _sim.Muted;
            }
            if (input.ToggleAutoPilot) autoPilot = !autoPilot;
#if UNITY_EDITOR
            // Captures pour les stores : à la résolution de la vue Game, dans <projet>/Captures.
            if (input.Screenshot)
            {
                System.IO.Directory.CreateDirectory("Captures");
                string file = $"Captures/PuffyBird-{_sim.Theme}-{System.DateTime.Now:yyyyMMdd-HHmmss}.png";
                ScreenCapture.CaptureScreenshot(file);
                Debug.Log($"Capture enregistrée : {file} ({Screen.width} × {Screen.height})");
            }
#endif
            if (input.Pause)
            {
                if (_sim.State == GameState.Paused) _sim.Resume();
                else _sim.Pause();
            }
            for (int i = 0; i < input.Presses; i++) _sim.Press();

            _cameraRig.UpdateViewport();
            _sim.ViewMargin = _cameraRig.SideMarginPx;
            int steps = _clock.Advance(dt);
            for (int i = 0; i < steps; i++)
            {
                if (autoPilot && AutoPilot.ShouldFlap(_sim)) _sim.Press();
                if (autoPilot && _sim.CanRestart) _sim.Press();
                _sim.Step();
            }

            var events = _sim.ConsumeEvents();
            if (_sim.RunId != _shownRun) ApplyRun();
            React(events);
            UpdateBanner();
            Render(dt);
        }

        void React(GameEvents events)
        {
            _sfx.Play(events);
            if ((events & GameEvents.Flap) != 0) _bird.OnFlap();
            if ((events & GameEvents.Star) != 0) _trail.OnStar(_bird.Position);
            if ((events & GameEvents.Hit) != 0)
            {
                _bird.OnHit();
                _pipes.Hit(_sim.HitPipeId);
                if (!reduceFlash) _cameraRig.Shake(0.05f, 0.25f);
            }
        }

        void UpdateBanner()
        {
            bool show = AdPolicy.BannerVisible(_sim, adsRemoved: false);
            if (show == _bannerShown) return;
            _bannerShown = show;
            _banner.SetVisible(show);
        }

        /// <summary>Nouvelle partie : couleur de l'oiseau et décor tiré au hasard (§6.8, §8.2).</summary>
        void ApplyRun()
        {
            _shownRun = _sim.RunId;
            var theme = Palette.Theme(_sim.Theme);
            _lighting.ApplyTheme(theme);
            _scenery.ApplyTheme(theme);
            _weather.ApplyTheme(theme);
            _postFx.ApplyTheme(theme);
            _cameraRig.Camera.backgroundColor = theme.SkyHorizon;
            _bird.SetColor(_sim.BirdColor);
        }

        void Render(float dt)
        {
            float alpha = _clock.Alpha;
            double scroll = _sim.PrevScrollDistance + (_sim.ScrollDistance - _sim.PrevScrollDistance) * alpha;

            _cameraRig.Update(dt, reduceFlash);
            _lighting.Update(_realTime);
            _scenery.Update(scroll, _realTime);
            _weather.Update(scroll, dt, _realTime);
            // Éclairs atténués si l'option « réduire les flashs » est active (§20).
            float lightning = _weather.Flash * (reduceFlash ? 0.2f : 1f);
            _scenery.SetFlash(lightning);
            _lighting.SetFlash(lightning);
            _pipes.Update(_sim.Pipes, alpha, dt);
            _stars.Update(_sim.Stars, alpha, _realTime);
            _bird.Update(_sim, alpha, dt);
            _bird.SetBoost(_sim.State == GameState.Playing ? _sim.BoostAmount : 0f, _realTime, dt);
            _trail.Update(_sim, _bird.Position, dt, _realTime);
            _hud.Update(_sim, _realTime, dt, _cameraRig.SafeTopPx);
            _postFx.Update(_sim.Flash / _cfg.FlashTime, _sim.FadeAlpha, lightning, reduceFlash);
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
