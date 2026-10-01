using PuffyBird.Core;
using PuffyBird.Rendering;
using UnityEngine;

namespace PuffyBird.UI
{
    using Element = UiLayer.Element;

    /// <summary>
    /// Interface en volume, placée dans la scène juste devant le plan de jeu et positionnée en
    /// pixels logiques selon la spec (§11) : titre, « Get Ready », score, « Game Over » et panneau
    /// de fin animés, médaille métallique, étiquette NEW, pause. Tout est créé au chargement ;
    /// en jeu on ne fait que déplacer, afficher ou masquer (aucune allocation).
    /// </summary>
    public sealed class HudView
    {
        const float HudZ = UiLayer.HudZ;
        const float PanelZ = UiLayer.PanelZ;
        /// <summary>Bouton pause : coin haut gauche, sous l'encoche, à la hauteur du score.</summary>
        const float PauseButtonX = 22f;
        const float PauseButtonSize = 26f;

        sealed class Number
        {
            public Transform Root;
            public MeshFilter[] Digits;
            public TextAlign Align;
            public int Shown = -1;
        }

        readonly UiLayer _ui;
        readonly WorldSpace _space;
        readonly GameConfig _cfg;
        readonly Transform _root;
        readonly Material _textMaterial;
        readonly Material _medalMaterial;
        readonly Material _sparkleMaterial;
        readonly Mesh[] _digitMeshes = new Mesh[10];
        readonly int[] _digitBuffer = new int[10];

        readonly Element _title;
        readonly Element _tapToPlay;
        readonly Element _titleBestLabel;
        readonly Number _titleBest;
        readonly Element _getReady;
        readonly Element _tapArrow;
        readonly Element _tapLabel;
        readonly Number _score;
        readonly Element _gameOver;
        readonly Element _panelBorder;
        readonly Element _panel;
        readonly Element _panelInner;
        readonly Element _medalLabel;
        readonly Element _scoreLabel;
        readonly Element _bestLabel;
        readonly Number _panelScore;
        readonly Number _panelBest;
        readonly Element _medal;
        readonly Element _sparkle;
        readonly Element _newTag;
        readonly Element _newLabel;
        readonly Element _retry;
        readonly Element _pause;
        readonly Element _pauseTap;
        readonly UiLayer.Button _pauseButton;

        Medal _shownMedal = (Medal)(-1);
        Vector2 _sparkleOffset;
        float _sparkleTimer;

        public HudView(UiLayer ui, MaterialLibrary materials)
        {
            _ui = ui;
            _space = ui.Space;
            _cfg = _space.Config;
            _root = ui.Root;
            _textMaterial = ui.TextMaterial;

            _medalMaterial = materials.Lit("Médaille", Color.white, 0.85f, 0.9f, 0.6f);
            _sparkleMaterial = materials.Lit("Étincelle", Color.white, 0f, 0f, 0f);
            _sparkleMaterial.SetColor(MaterialLibrary.EmissionColor, Color.white * 4f);

            var outline = Palette.Outline;
            for (int d = 0; d < 10; d++) _digitMeshes[d] = VoxelFont.Build(d.ToString(), TextAlign.Left, Color.white, outline);

            _title = Text("PUFFYBIRD", TextAlign.Center, Palette.Hex("#F8C82A"), outline);
            _tapToPlay = Text("TAP TO PLAY", TextAlign.Center, Color.white, outline);
            _titleBestLabel = Text("BEST", TextAlign.Right, Palette.PanelLabel, outline);
            _titleBest = CreateNumber(TextAlign.Left, 5);
            _getReady = Text("GET READY!", TextAlign.Center, Palette.GetReady, outline);
            _tapArrow = Text("^", TextAlign.Center, Color.white, outline);
            _tapLabel = Text("TAP", TextAlign.Center, Color.white, outline);
            _score = CreateNumber(TextAlign.Center, 6);
            _gameOver = Text("GAME OVER", TextAlign.Center, Palette.GameOver, outline);

            _panelBorder = Solid(Palette.Outline);
            _panel = Solid(Palette.PanelEdge);
            _panelInner = Solid(Palette.Panel);
            _medalLabel = Text("MEDAL", TextAlign.Left, Palette.PanelLabel, Palette.Panel);
            _scoreLabel = Text("SCORE", TextAlign.Right, Palette.PanelLabel, Palette.Panel);
            _bestLabel = Text("BEST", TextAlign.Right, Palette.PanelLabel, Palette.Panel);
            _panelScore = CreateNumber(TextAlign.Right, 6);
            _panelBest = CreateNumber(TextAlign.Right, 6);

            var medalMesh = new MeshBuilder()
                .AddCylinder(new Vector3(0f, -0.5f, 0f), 0.5f, 1f, Color.white, 36)
                .AddCylinder(new Vector3(0f, -0.62f, 0f), 0.36f, 0.14f, new Color(0.85f, 0.85f, 0.85f), 36)
                .Build("Médaille");
            _medal = Create("Médaille", medalMesh, _medalMaterial);
            var sparkleMesh = new MeshBuilder()
                .AddBox(Vector3.zero, new Vector3(1f, 0.18f, 0.18f), Color.white)
                .AddBox(Vector3.zero, new Vector3(0.18f, 1f, 0.18f), Color.white)
                .Build("Étincelle");
            _sparkle = Create("Étincelle", sparkleMesh, _sparkleMaterial);

            _newTag = Solid(Palette.NewTag);
            _newLabel = Text("NEW", TextAlign.Center, Color.white, Palette.NewTag);
            _retry = Text("TAP TO RETRY", TextAlign.Center, Color.white, outline);
            _pause = Text("PAUSE", TextAlign.Center, Color.white, outline);
            _pauseTap = Text("TAP", TextAlign.Center, Color.white, outline);
            _pauseButton = ui.CreateButton(VoxelFont.PauseIcon, Palette.GameOver, Color.white);
        }

        Element Create(string name, Mesh mesh, Material material) => _ui.Create(name, mesh, material);

        Element Text(string text, TextAlign align, Color front, Color outline) => _ui.Text(text, align, front, outline);

        Element Solid(Color color) => _ui.Solid(color);

        Number CreateNumber(TextAlign align, int maxDigits)
        {
            var root = new GameObject("Nombre").transform;
            root.SetParent(_root, false);
            var number = new Number { Root = root, Align = align, Digits = new MeshFilter[maxDigits] };
            for (int i = 0; i < maxDigits; i++)
            {
                var go = new GameObject("Chiffre " + i);
                go.transform.SetParent(root, false);
                number.Digits[i] = go.AddComponent<MeshFilter>();
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = _textMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            root.gameObject.SetActive(false);
            return number;
        }

        // ───────── placement ─────────

        void Place(Element e, float px, float py, float voxelPx, float z = HudZ) => _ui.Place(e, px, py, voxelPx, z);

        void PlaceBox(Element e, float left, float top, float width, float height, float z) => _ui.PlaceBox(e, left, top, width, height, z);

        void PlaceNumber(Number n, int value, float px, float py, float voxelPx, float z = HudZ)
        {
            var p = _space.OnScreen(px, py, z, out float t);
            float s = voxelPx / WorldSpace.PixelsPerUnit * t;
            n.Root.localPosition = p;
            n.Root.localScale = new Vector3(s, s, s);
            if (!n.Root.gameObject.activeSelf) n.Root.gameObject.SetActive(true);
            if (n.Shown == value) return;
            n.Shown = value;

            int count = 0;
            int v = value < 0 ? 0 : value;
            do
            {
                _digitBuffer[count++] = v % 10;
                v /= 10;
            } while (v > 0 && count < n.Digits.Length);

            float width = count * VoxelFont.Advance - 1;
            float start = n.Align == TextAlign.Left ? 0f : (n.Align == TextAlign.Center ? -width * 0.5f : -width);
            for (int i = 0; i < n.Digits.Length; i++)
            {
                var digit = n.Digits[i];
                bool used = i < count;
                if (digit.gameObject.activeSelf != used) digit.gameObject.SetActive(used);
                if (!used) continue;
                digit.sharedMesh = _digitMeshes[_digitBuffer[count - 1 - i]];
                digit.transform.localPosition = new Vector3(start + i * VoxelFont.Advance, 0f, 0f);
            }
        }

        static void Hide(Element e) => UiLayer.Hide(e);

        static void Hide(Number n)
        {
            if (n.Root.gameObject.activeSelf) n.Root.gameObject.SetActive(false);
        }

        static bool Blink(float time) => time * 1.6f % 1f < 0.65f;

        // ───────── mise à jour ─────────

        /// <param name="safeTopPx">Haut de la zone sûre de l'écran, en y logique (voir <c>CameraRig.SafeTopPx</c>).</param>
        /// <param name="menuOpen">Un menu couvre l'écran titre : seul le titre reste affiché.</param>
        public void Update(GameSimulation sim, float realTime, float deltaTime, float safeTopPx, bool menuOpen)
        {
            var state = sim.State;
            float t = sim.StateTime;
            float bob = Mathf.Sin(realTime * Mathf.PI * 2f * 0.6f) * 3f;

            // Titre
            if (state == GameState.Title)
            {
                Place(_title, 144f, 96f + bob, 4f);
                if (!menuOpen && Blink(realTime)) Place(_tapToPlay, 144f, 330f, 2.4f); else Hide(_tapToPlay);
                if (!menuOpen && sim.Best > 0)
                {
                    Place(_titleBestLabel, 140f, 360f, 1.8f);
                    PlaceNumber(_titleBest, sim.Best, 150f, 358f, 2.2f);
                }
                else
                {
                    Hide(_titleBestLabel);
                    Hide(_titleBest);
                }
            }
            else
            {
                Hide(_title);
                Hide(_tapToPlay);
                Hide(_titleBestLabel);
                Hide(_titleBest);
            }

            // Get Ready
            if (state == GameState.Ready)
            {
                Place(_getReady, 144f, 100f, 3.2f);
                float arrowBob = Mathf.Abs(Mathf.Sin(realTime * Mathf.PI * 1.25f)) * 6f;
                Place(_tapArrow, 144f, 286f - arrowBob, 3f);
                Place(_tapLabel, 144f, 316f, 2f);
            }
            else
            {
                Hide(_getReady);
                Hide(_tapArrow);
                Hide(_tapLabel);
            }

            // Score en jeu : tout en haut de l'écran visible, sous l'encoche, pour dégager la vue.
            bool showScore = state == GameState.Ready || state == GameState.Playing || state == GameState.Dying || state == GameState.Paused;
            if (showScore) PlaceNumber(_score, sim.Score, 144f, safeTopPx + _cfg.ScoreTopMargin, 4f);
            else Hide(_score);

            // Bouton pause, seulement pendant la partie (un tap ailleurs fait sauter l'oiseau).
            if (state == GameState.Playing)
            {
                float y = safeTopPx + _cfg.ScoreTopMargin + PauseButtonSize * 0.5f;
                _ui.PlaceButton(_pauseButton, UiAction.Pause, PauseButtonX, y, PauseButtonSize, PauseButtonSize, 2f, hitMargin: 9f);
            }
            else
            {
                UiLayer.Hide(_pauseButton);
            }

            // Pause
            if (state == GameState.Paused)
            {
                Place(_pause, 144f, 190f, 4f);
                if (Blink(realTime)) Place(_pauseTap, 144f, 250f, 2.4f); else Hide(_pauseTap);
            }
            else
            {
                Hide(_pause);
                Hide(_pauseTap);
            }

            // Fin de partie
            if (state == GameState.Over) UpdateOver(sim, t, deltaTime);
            else HideOver();
        }

        void UpdateOver(GameSimulation sim, float t, float deltaTime)
        {
            Place(_gameOver, 144f, OverScreenTimeline.TitleY(t) - 12f, 3.4f);

            if (OverScreenTimeline.PanelVisible(t))
            {
                float py = OverScreenTimeline.PanelY(t, _cfg);
                PlaceBox(_panelBorder, 22f, py - 2f, 244f, 118f, PanelZ + 0.03f);
                PlaceBox(_panel, 24f, py, 240f, 114f, PanelZ + 0.02f);
                PlaceBox(_panelInner, 30f, py + 6f, 228f, 102f, PanelZ);
                Place(_medalLabel, 44f, py + 12f, 1.7f);
                Place(_scoreLabel, 244f, py + 12f, 1.7f);
                Place(_bestLabel, 244f, py + 56f, 1.7f);
                PlaceNumber(_panelScore, OverScreenTimeline.ShownScore(t, sim.Score, _cfg), 244f, py + 28f, 2.2f);
                PlaceNumber(_panelBest, sim.Best, 244f, py + 72f, 2.2f);
                UpdateMedal(sim.Medal, 70f, py + 66f, t, deltaTime);

                if (OverScreenTimeline.NewBadgeVisible(t, sim.Score, sim.NewBest, _cfg))
                {
                    PlaceBox(_newTag, 150f, py + 58f, 34f, 14f, HudZ + 0.02f);
                    Place(_newLabel, 167f, py + 60.5f, 1.4f, HudZ - 0.02f);
                }
                else
                {
                    Hide(_newTag);
                    Hide(_newLabel);
                }
            }
            else
            {
                HidePanel();
            }

            if (OverScreenTimeline.ButtonsVisible(t, _cfg) && !sim.IsFadingOut && Blink(t)) Place(_retry, 144f, 392f, 2.2f);
            else Hide(_retry);
        }

        void UpdateMedal(Medal medal, float px, float py, float t, float deltaTime)
        {
            if (medal != _shownMedal)
            {
                _shownMedal = medal;
                bool has = medal != Medal.None;
                _medalMaterial.SetColor(MaterialLibrary.BaseColor, Palette.MedalColor(medal));
                _medalMaterial.SetFloat(MaterialLibrary.Metallic, has ? 0.9f : 0f);
                _medalMaterial.SetFloat(MaterialLibrary.Smoothness, has ? 0.82f : 0.2f);
            }

            var p = _space.OnScreen(px, py, HudZ + 0.05f, out float scale);
            float d = 44f / WorldSpace.PixelsPerUnit * scale;
            _medal.Transform.localPosition = p;
            _medal.Transform.localRotation = Quaternion.Euler(90f, Mathf.Sin(t * 2.2f) * 18f, 0f);
            _medal.Transform.localScale = new Vector3(d, 0.06f * scale, d);
            if (!_medal.GameObject.activeSelf) _medal.GameObject.SetActive(true);

            // Étincelle : apparaît à un endroit aléatoire de la médaille, en boucle (§10.3).
            if (medal == Medal.None)
            {
                Hide(_sparkle);
                return;
            }
            _sparkleTimer -= deltaTime;
            if (_sparkleTimer <= 0f)
            {
                _sparkleTimer = 0.5f;
                _sparkleOffset = Random.insideUnitCircle * 15f;
            }
            float pulse = Mathf.Sin((1f - _sparkleTimer / 0.5f) * Mathf.PI);
            Place(_sparkle, px + _sparkleOffset.x, py + _sparkleOffset.y, 7f * pulse, HudZ - 0.02f);
        }

        void HidePanel()
        {
            Hide(_panelBorder);
            Hide(_panel);
            Hide(_panelInner);
            Hide(_medalLabel);
            Hide(_scoreLabel);
            Hide(_bestLabel);
            Hide(_panelScore);
            Hide(_panelBest);
            Hide(_medal);
            Hide(_sparkle);
            Hide(_newTag);
            Hide(_newLabel);
        }

        void HideOver()
        {
            Hide(_gameOver);
            Hide(_retry);
            HidePanel();
        }
    }
}
