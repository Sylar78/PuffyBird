using ElementalPhenix.Core;
using ElementalPhenix.Rendering;
using UnityEngine;

namespace ElementalPhenix.UI
{
    using Element = UiLayer.Element;

    /// <summary>
    /// Interface en volume, placée dans la scène juste devant le plan de jeu et positionnée en
    /// pixels logiques selon la spec (§11) : titre, « Envol ! », score, « Flamme éteinte » et panneau
    /// de fin animés, gemme élémentaire (à la place de la médaille), étiquette NEW, pause, et les écrans
    /// du Voyage des éléments (étape, règle, objectif, étape réussie). Tout est créé au chargement ;
    /// en jeu on ne fait que déplacer, afficher ou masquer (aucune allocation).
    /// </summary>
    public sealed class HudView
    {
        const float HudZ = UiLayer.HudZ;
        const float PanelZ = UiLayer.PanelZ;
        /// <summary>Bouton pause : coin haut gauche, sous l'encoche, à la hauteur du score.</summary>
        const float PauseButtonX = 22f;
        const float PauseButtonSize = 26f;
        const float PulseTime = 0.45f;
        const float PulseGrowth = 0.6f;

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
        readonly Element _titleSub;
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
        float _pulse;
        readonly Element _newLabel;
        readonly Element _retry;
        readonly Element _pause;
        readonly Element _pauseTap;
        // Voyage des éléments : une ligne par étape, construite au chargement.
        readonly Element[] _stageNames;
        readonly Element[] _stageRules;
        readonly Element[] _stageGoals;
        readonly Element _stageCleared;
        readonly Element _journeyDone;
        readonly Element _nextStage;
        readonly Element _homeTap;
        readonly Element _goalLabel;
        readonly UiLayer.Button _pauseButton;
        // Éclats ramassés et jauge de braises (renaissance), sous le bouton pause.
        readonly Element _shardIcon;
        readonly Number _shardCount;
        readonly Element _embersBack;
        readonly Element _embersFill;
        readonly Element _embersFull;
        readonly Element _embersLabel;
        readonly Element _guardianBanner;
        readonly Element _rebirthBanner;
        readonly Element _epilogue;
        float _guardianTime;
        float _rebirthTime;
        // Jauge du pouvoir d'élément (sous celle des braises) et annonce du pouvoir, une par élément.
        readonly Element _powerBack;
        readonly Element[] _powerFills;
        readonly Element[] _powerBanners;
        PhoenixElement _powerShown;
        float _powerTime;

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

            _title = Text("PHENIX", TextAlign.Center, Palette.Hex("#FFB02E"), outline);
            _titleSub = Text("ELEMENTAL", TextAlign.Center, Palette.Hex("#FFE0A0"), outline);
            _tapToPlay = Text(Lang.T("TOUCHE POUR JOUER", "TAP TO PLAY", "TOCA PARA JUGAR", "TIPPEN ZUM SPIELEN", "TOQUE PARA JOGAR"), TextAlign.Center, Color.white, outline);
            _titleBestLabel = Text(Lang.T("RECORD", "BEST", "RÉCORD", "REKORD", "RECORDE"), TextAlign.Right, Palette.PanelLabel, outline);
            _titleBest = CreateNumber(TextAlign.Left, 5);
            _getReady = Text(Lang.T("ENVOL !", "TAKE FLIGHT!", "¡A VOLAR!", "ABFLUG!", "VOE!"), TextAlign.Center, Palette.GetReady, outline);
            _tapArrow = Text("^", TextAlign.Center, Color.white, outline);
            _tapLabel = Text(Lang.T("TOUCHE", "TAP", "TOCA", "TIPPEN", "TOQUE"), TextAlign.Center, Color.white, outline);
            _score = CreateNumber(TextAlign.Center, 6);
            _gameOver = Text(Lang.T("FLAMME ÉTEINTE", "FLAME OUT", "LLAMA APAGADA", "FLAMME AUS", "CHAMA APAGADA"), TextAlign.Center, Palette.GameOver, outline);

            _panelBorder = Solid(Palette.Outline);
            _panel = Solid(Palette.PanelEdge);
            _panelInner = Solid(Palette.Panel);
            _medalLabel = Text(Lang.T("GEMME", "GEM", "GEMA", "JUWEL", "GEMA"), TextAlign.Left, Palette.PanelLabel, Palette.Panel);
            _scoreLabel = Text(Lang.T("SCORE", "SCORE", "PUNTOS", "PUNKTE", "PONTOS"), TextAlign.Right, Palette.PanelLabel, Palette.Panel);
            _bestLabel = Text(Lang.T("RECORD", "BEST", "RÉCORD", "REKORD", "RECORDE"), TextAlign.Right, Palette.PanelLabel, Palette.Panel);
            _panelScore = CreateNumber(TextAlign.Right, 6);
            _panelBest = CreateNumber(TextAlign.Right, 6);

            // Gemme taillée : couronne à facettes au-dessus, pavillon en pointe dessous (couleur : le matériau).
            var gemMesh = new MeshBuilder()
                .AddPrism(new Vector3(0f, 0.08f, 0f), 0.5f, 0.3f, 0.2f, Color.white, 8)
                .AddPrism(new Vector3(0f, -0.42f, 0f), 0f, 0.5f, 0.5f, new Color(0.82f, 0.82f, 0.82f), 8)
                .Build("Gemme");
            _medal = Create("Gemme", gemMesh, _medalMaterial);
            var sparkleMesh = new MeshBuilder()
                .AddBox(Vector3.zero, new Vector3(1f, 0.18f, 0.18f), Color.white)
                .AddBox(Vector3.zero, new Vector3(0.18f, 1f, 0.18f), Color.white)
                .Build("Étincelle");
            _sparkle = Create("Étincelle", sparkleMesh, _sparkleMaterial);

            _newTag = Solid(Palette.NewTag);
            _newLabel = Text(Lang.T("NOUVEAU", "NEW", "NUEVO", "NEU", "NOVO"), TextAlign.Center, Color.white, Palette.NewTag);
            _retry = Text(Lang.T("TOUCHE POUR REJOUER", "TAP TO RETRY", "TOCA PARA REINTENTAR", "ERNEUT TIPPEN", "TOQUE PARA TENTAR"), TextAlign.Center, Color.white, outline);
            _pause = Text(Lang.T("PAUSE", "PAUSE", "PAUSA", "PAUSE", "PAUSA"), TextAlign.Center, Color.white, outline);
            _pauseTap = Text(Lang.T("TOUCHE POUR REPRENDRE", "TAP TO RESUME", "TOCA PARA SEGUIR", "ZUM FORTSETZEN TIPPEN", "TOQUE PARA CONTINUAR"), TextAlign.Center, Color.white, outline);
            _pauseButton = ui.CreateButton(VoxelFont.PauseIcon, Palette.GameOver, Color.white);

            var shardMesh = new MeshBuilder()
                .AddCone(Vector3.zero, 0.5f, 0.75f, Palette.Hex("#8FE6FF"), 6)
                .Append(new MeshBuilder().AddCone(Vector3.zero, 0.5f, 0.75f, Palette.Hex("#B07CFF"), 6), Matrix4x4.Rotate(Quaternion.Euler(180f, 0f, 0f)))
                .Build("Éclat (interface)");
            _shardIcon = Create("Éclat", shardMesh, _textMaterial);
            _shardCount = CreateNumber(TextAlign.Left, 4);
            _embersBack = Solid(Palette.Outline);
            _embersFill = Solid(Palette.Hex("#FF7A1A"));
            _embersFull = Solid(Palette.Hex("#FFD24D"));
            _embersLabel = Text(Lang.T("RENAISSANCE", "REBIRTH", "RENACER", "WIEDERGEBURT", "RENASCER"), TextAlign.Left, Palette.Hex("#FFD24D"), outline);
            _guardianBanner = Text(Lang.T("LE GARDIEN APPARAÎT !", "THE GUARDIAN APPEARS!", "¡APARECE EL GUARDIÁN!", "DER WÄCHTER ERSCHEINT!", "O GUARDIÃO APARECE!"), TextAlign.Center, Palette.GameOver, outline);
            _rebirthBanner = Text(Lang.T("RENAISSANCE !", "REBIRTH!", "¡RENACIMIENTO!", "WIEDERGEBURT!", "RENASCIMENTO!"), TextAlign.Center, Palette.Hex("#FFB02E"), outline);
            _epilogue = Text(JourneyTexts.Epilogue, TextAlign.Center, Color.white, outline);
            _powerBack = Solid(Palette.Outline);
            int elements = System.Enum.GetValues(typeof(PhoenixElement)).Length;
            _powerFills = new Element[elements];
            _powerBanners = new Element[elements];
            for (int i = 1; i < elements; i++)
            {
                var element = (PhoenixElement)i;
                _powerFills[i] = Solid(PowerTexts.Tint(element));
                _powerBanners[i] = Text(PowerTexts.Name(element), TextAlign.Center, PowerTexts.Tint(element), outline);
            }

            int stages = _cfg.Journey.Length;
            _stageNames = new Element[stages];
            _stageRules = new Element[stages];
            _stageGoals = new Element[stages];
            for (int i = 0; i < stages; i++)
            {
                var stage = _cfg.Journey[i];
                int n = i + 1;
                string name = JourneyTexts.StageName(stage.Theme);
                _stageNames[i] = Text(Lang.T($"ÉTAPE {n} : {name}", $"STAGE {n}: {name}", $"ETAPA {n}: {name}", $"ETAPPE {n}: {name}", $"ETAPA {n}: {name}"), TextAlign.Center, Palette.GetReady, outline);
                _stageRules[i] = Text(JourneyTexts.RuleName(stage.Rule), TextAlign.Center, Color.white, outline);
                int goal = stage.Goal;
                _stageGoals[i] = Text(Lang.T($"OBJECTIF : {goal}", $"GOAL: {goal}", $"OBJETIVO: {goal}", $"ZIEL: {goal}", $"OBJETIVO: {goal}"), TextAlign.Center, Color.white, outline);
            }
            _stageCleared = Text(Lang.T("ÉTAPE RÉUSSIE !", "STAGE CLEAR!", "¡ETAPA SUPERADA!", "ETAPPE GESCHAFFT!", "ETAPA CONCLUÍDA!"), TextAlign.Center, Palette.GetReady, outline);
            _journeyDone = Text(Lang.T("VOYAGE TERMINÉ !", "JOURNEY COMPLETE!", "¡VIAJE COMPLETADO!", "REISE BEENDET!", "JORNADA CONCLUÍDA!"), TextAlign.Center, Palette.GetReady, outline);
            _nextStage = Text(Lang.T("TOUCHE : ÉTAPE SUIVANTE", "TAP FOR NEXT STAGE", "TOCA: SIGUIENTE ETAPA", "TIPPEN: NÄCHSTE ETAPPE", "TOQUE: PRÓXIMA ETAPA"), TextAlign.Center, Color.white, outline);
            _homeTap = Text(Lang.T("TOUCHE POUR L'ACCUEIL", "TAP TO GO HOME", "TOCA PARA VOLVER", "TIPPEN ZUM MENÜ", "TOQUE PARA VOLTAR"), TextAlign.Center, Color.white, outline);
            _goalLabel = Text(Lang.T("OBJECTIF", "GOAL", "OBJETIVO", "ZIEL", "OBJETIVO"), TextAlign.Right, Palette.PanelLabel, Palette.Panel);
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

        /// <summary>Fait gonfler un instant le score en jeu (palier de médaille).</summary>
        public void PulseScore() => _pulse = PulseTime;

        /// <summary>Annonce l'arrivée du gardien du monde.</summary>
        public void ShowGuardian() => _guardianTime = 1.8f;

        /// <summary>Annonce la renaissance des cendres, jusqu'au tap qui relance la partie.</summary>
        public void ShowRebirth() => _rebirthTime = 1f;

        /// <summary>Annonce le pouvoir d'élément qui vient de se déclencher.</summary>
        public void ShowPower(PhoenixElement element)
        {
            _powerShown = element;
            _powerTime = 1.2f;
        }

        void Place(Element e, float px, float py, float voxelPx, float z = HudZ, float maxWidth = 0f) => _ui.Place(e, px, py, voxelPx, z, maxWidth);

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
        /// <param name="menuOpen">Un menu est ouvert : « TOUCHE POUR JOUER », le record, « PRÊT ? » et « PAUSE » disparaissent.</param>
        /// <param name="menuCoversTitle">Le panneau du menu monte jusqu'au titre du jeu, qui disparaît aussi.</param>
        public void Update(GameSimulation sim, float realTime, float deltaTime, float safeTopPx, bool menuOpen, bool menuCoversTitle)
        {
            var state = sim.State;
            if (_pulse > 0f) _pulse -= deltaTime;
            float t = sim.StateTime;
            float bob = Mathf.Sin(realTime * Mathf.PI * 2f * 0.6f) * 3f;

            // Titre
            if (state == GameState.Title)
            {
                if (menuCoversTitle)
                {
                    Hide(_title);
                    Hide(_titleSub);
                }
                else
                {
                    Place(_titleSub, 144f, 84f + bob, 3f);
                    Place(_title, 144f, 112f + bob, 4.4f);
                }
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
                Hide(_titleSub);
                Hide(_tapToPlay);
                Hide(_titleBestLabel);
                Hide(_titleBest);
            }

            // Get Ready
            int journeyStage = sim.IsJourney ? sim.JourneyStage : -1;
            bool journeyReady = state == GameState.Ready && !menuOpen && sim.IsJourney;
            // Voyage : nom de l'étape, sa règle et son objectif à la place de « ENVOL ! ».
            for (int i = 0; i < _stageNames.Length; i++)
            {
                if (journeyReady && i == journeyStage)
                {
                    Place(_stageNames[i], 144f, 86f, 2.6f, maxWidth: 264f);
                    Place(_stageRules[i], 144f, 116f, 2.2f, maxWidth: 264f);
                    Place(_stageGoals[i], 144f, 142f, 1.6f);
                }
                else
                {
                    Hide(_stageNames[i]);
                    bool goalInGame = i == journeyStage && !menuOpen && (state == GameState.Playing || state == GameState.Paused);
                    if (goalInGame) Place(_stageGoals[i], 144f, safeTopPx + _cfg.ScoreTopMargin + 36f, 1.3f);
                    else Hide(_stageGoals[i]);
                    // Rappel de la règle sous l'objectif, pour savoir ce qui change pendant l'étape.
                    if (goalInGame && _cfg.Journey[i].Rule != StageRule.FirstFlight) Place(_stageRules[i], 144f, safeTopPx + _cfg.ScoreTopMargin + 48f, 1.1f, maxWidth: 200f);
                    else Hide(_stageRules[i]);
                }
            }

            if (state == GameState.Ready && !menuOpen)
            {
                if (sim.IsJourney) Hide(_getReady);
                else Place(_getReady, 144f, 100f, 3.2f);
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
            if (showScore)
            {
                // Gonflement bref puis retour : courbe en cloche, pic au milieu.
                float k = _pulse > 0f ? Mathf.Sin(Mathf.Clamp01(1f - _pulse / PulseTime) * Mathf.PI) : 0f;
                PlaceNumber(_score, sim.Score, 144f, safeTopPx + _cfg.ScoreTopMargin, 4f * (1f + PulseGrowth * k));
            }
            else Hide(_score);

            UpdateRunExtras(sim, realTime, deltaTime, safeTopPx, showScore && !menuOpen);

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
            if (state == GameState.Paused && !menuOpen && !sim.IsFadingOut)
            {
                Place(_pause, 144f, 190f, 4f);
                if (Blink(realTime)) Place(_pauseTap, 144f, 250f, 1.8f); else Hide(_pauseTap);
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

        /// <summary>
        /// Éclats de la partie et jauge de braises sous le bouton pause, annonces du gardien et de la
        /// renaissance. Rien n'est créé ici : on place, on étire ou on masque.
        /// </summary>
        void UpdateRunExtras(GameSimulation sim, float realTime, float deltaTime, float safeTopPx, bool show)
        {
            if (_guardianTime > 0f) _guardianTime -= deltaTime;
            if (_powerTime > 0f) _powerTime -= deltaTime;
            if (sim.State != GameState.Ready) _rebirthTime = 0f;

            float y = safeTopPx + _cfg.ScoreTopMargin + PauseButtonSize + 12f;
            if (show && sim.ShardsThisRun > 0)
            {
                var p = _space.OnScreen(14f, y + 4f, HudZ, out float t);
                float d = 12f / WorldSpace.PixelsPerUnit * t;
                _shardIcon.Transform.localPosition = p;
                _shardIcon.Transform.localRotation = Quaternion.Euler(0f, realTime * 90f, 0f);
                _shardIcon.Transform.localScale = new Vector3(d, d, d);
                UiLayer.Show(_shardIcon);
                PlaceNumber(_shardCount, sim.ShardsThisRun, 24f, y - 2f, 1.4f);
                y += 18f;
            }
            else
            {
                Hide(_shardIcon);
                Hide(_shardCount);
            }

            // Jauge de braises : hors Voyage, tant que la renaissance n'a pas servi.
            if (show && !sim.IsJourney && !sim.Reborn)
            {
                float k = Mathf.Clamp01((float)sim.Embers / _cfg.RebirthCost);
                const float width = 56f;
                PlaceBox(_embersBack, 8f, y, width + 4f, 8f, HudZ + 0.06f);
                bool full = sim.RebirthReady;
                var fill = full ? _embersFull : _embersFill;
                Hide(full ? _embersFill : _embersFull);
                if (k > 0f) PlaceBox(fill, 10f, y + 2f, width * k, 4f, HudZ + 0.03f);
                else Hide(fill);
                // Pleine : le libellé clignote à côté de la jauge.
                if (full && Blink(realTime)) Place(_embersLabel, 8f, y + 11f, 1f);
                else Hide(_embersLabel);
                y += 22f;
            }
            else
            {
                Hide(_embersBack);
                Hide(_embersFill);
                Hide(_embersFull);
                Hide(_embersLabel);
            }

            // Jauge du pouvoir d'élément, à la couleur du phénix : pleine, elle clignote jusqu'au déclenchement.
            int shownPower = show && sim.PowersActive ? (int)sim.Element : 0;
            for (int i = 1; i < _powerFills.Length; i++)
            {
                if (i != shownPower) Hide(_powerFills[i]);
            }
            if (shownPower > 0)
            {
                const float width = 56f;
                float k = Mathf.Clamp01((float)sim.ElementCharge / _cfg.PowerChargeCost);
                PlaceBox(_powerBack, 8f, y, width + 4f, 8f, HudZ + 0.06f);
                bool visible = k > 0f && (!sim.PowerReady || Blink(realTime * 2f));
                if (visible) PlaceBox(_powerFills[shownPower], 10f, y + 2f, width * k, 4f, HudZ + 0.03f);
                else Hide(_powerFills[shownPower]);
            }
            else Hide(_powerBack);
            for (int i = 1; i < _powerBanners.Length; i++)
            {
                if (i == (int)_powerShown && _powerTime > 0f && sim.State == GameState.Playing) Place(_powerBanners[i], 144f, 120f, 2.6f, maxWidth: 264f);
                else Hide(_powerBanners[i]);
            }

            if (_guardianTime > 0f && sim.State == GameState.Playing) Place(_guardianBanner, 144f, 150f, 2.4f, maxWidth: 264f);
            else Hide(_guardianBanner);
            if (_rebirthTime > 0f && sim.State == GameState.Ready) Place(_rebirthBanner, 144f, 150f, 3f, maxWidth: 264f);
            else Hide(_rebirthBanner);
        }

        void UpdateOver(GameSimulation sim, float t, float deltaTime)
        {
            // Titre : flamme éteinte, étape réussie ou voyage terminé.
            var title = !sim.StageCleared ? _gameOver : (sim.JourneyComplete ? _journeyDone : _stageCleared);
            Place(title, 144f, OverScreenTimeline.TitleY(t) - 12f, 3.4f, maxWidth: 264f);
            if (title != _gameOver) Hide(_gameOver);
            if (title != _stageCleared) Hide(_stageCleared);
            if (title != _journeyDone) Hide(_journeyDone);
            if (title == _journeyDone) Place(_epilogue, 144f, OverScreenTimeline.TitleY(t) + 22f, 1.3f, maxWidth: 264f);
            else Hide(_epilogue);

            if (OverScreenTimeline.PanelVisible(t))
            {
                float py = OverScreenTimeline.PanelY(t, _cfg);
                PlaceBox(_panelBorder, 22f, py - 2f, 244f, 118f, PanelZ + 0.03f);
                PlaceBox(_panel, 24f, py, 240f, 114f, PanelZ + 0.02f);
                PlaceBox(_panelInner, 30f, py + 6f, 228f, 102f, PanelZ);
                Place(_medalLabel, 44f, py + 12f, 1.7f);
                Place(_scoreLabel, 244f, py + 12f, 1.7f);
                // Voyage : l'objectif de l'étape au lieu du record, gemme d'aurore si l'étape est réussie.
                Place(sim.IsJourney ? _goalLabel : _bestLabel, 244f, py + 56f, 1.7f);
                Hide(sim.IsJourney ? _bestLabel : _goalLabel);
                PlaceNumber(_panelScore, OverScreenTimeline.ShownScore(t, sim.Score, _cfg), 244f, py + 28f, 2.2f);
                PlaceNumber(_panelBest, sim.IsJourney ? sim.JourneyGoal : sim.Best, 244f, py + 72f, 2.2f);
                var gem = sim.IsJourney ? (sim.StageCleared ? Medal.Platinum : Medal.None) : sim.Medal;
                UpdateMedal(gem, 70f, py + 66f, t, deltaTime);

                if (OverScreenTimeline.NewBadgeVisible(t, sim.Score, sim.NewBest, _cfg))
                {
                    PlaceBox(_newTag, 122f, py + 58f, 58f, 14f, HudZ + 0.02f);
                    Place(_newLabel, 151f, py + 61f, 1.2f, HudZ - 0.02f);
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

            var retry = !sim.StageCleared ? _retry : (sim.JourneyComplete ? _homeTap : _nextStage);
            if (OverScreenTimeline.ButtonsVisible(t, _cfg) && !sim.IsFadingOut && Blink(t)) Place(retry, 144f, 392f, 2.2f, maxWidth: 264f);
            else Hide(retry);
            if (retry != _retry) Hide(_retry);
            if (retry != _nextStage) Hide(_nextStage);
            if (retry != _homeTap) Hide(_homeTap);
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
            // La gemme tourne lentement sur elle-même, facettes à la lumière.
            _medal.Transform.localRotation = Quaternion.Euler(12f, t * 70f, 0f);
            _medal.Transform.localScale = new Vector3(d, d, d);
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
            Hide(_goalLabel);
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
            Hide(_stageCleared);
            Hide(_journeyDone);
            Hide(_epilogue);
            Hide(_retry);
            Hide(_nextStage);
            Hide(_homeTap);
            HidePanel();
        }
    }
}
