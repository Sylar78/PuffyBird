using ElementalPhenix.Core;
using ElementalPhenix.Rendering;
using UnityEngine;

namespace ElementalPhenix.UI
{
    using Element = UiLayer.Element;

    /// <summary>
    /// Carte du Voyage des éléments (bouton VOYAGE de l'écran titre) : les neuf mondes reliés en
    /// serpentin, chacun de sa couleur, cochés une fois réussis, le prochain qui palpite. Toucher un
    /// monde affiche son nom, sa règle et son histoire ; JOUER lance l'étape si elle est ouverte
    /// (réussie, ou la suivante). Tout est construit au chargement.
    /// </summary>
    public sealed class JourneyMapView
    {
        const float PanelLeft = 14f;
        const float PanelTop = 70f;
        const float PanelWidth = 260f;
        const float PanelHeight = 336f;
        const float TitleY = 82f;
        const float FirstRowY = 128f;
        const float RowStep = 48f;
        const float NodeWidth = 44f;
        const float NodeHeight = 30f;
        const float NameY = 268f;
        const float RuleY = 290f;
        const float StoryY = 310f;
        const float StoryLineStep = 14f;
        const float ActionY = 352f;
        const float BackY = 386f;

        static readonly float[] Columns = { 64f, 144f, 224f };

        readonly UiLayer _ui;
        readonly GameConfig _cfg;
        readonly int _count;
        readonly Element _panelBorder;
        readonly Element _panel;
        readonly Element _panelInner;
        readonly Element _title;
        readonly UiLayer.Button[] _open;
        readonly UiLayer.Button[] _locked;
        readonly Element[] _checks;
        readonly Element[] _links;
        readonly Element _selection;
        readonly Element[] _names;
        readonly Element[] _rules;
        readonly Element[] _story;
        readonly Element _lockedHint;
        readonly UiLayer.Button _play;
        readonly UiLayer.Button _back;

        public JourneyMapView(UiLayer ui)
        {
            _ui = ui;
            _cfg = ui.Space.Config;
            _count = _cfg.Journey.Length;

            _panelBorder = ui.Solid(Palette.Outline);
            _panel = ui.Solid(Palette.PanelEdge);
            _panelInner = ui.Solid(Palette.Panel);
            _title = ui.Text(Lang.T("VOYAGE DES ÉLÉMENTS", "JOURNEY OF ELEMENTS", "VIAJE DE LOS ELEMENTOS", "REISE DER ELEMENTE", "JORNADA DOS ELEMENTOS"), TextAlign.Center, Palette.PanelLabel, Palette.Panel);
            _selection = ui.Solid(Palette.GetReady);
            _open = new UiLayer.Button[_count];
            _locked = new UiLayer.Button[_count];
            _checks = new Element[_count];
            _links = new Element[_count - 1];
            _names = new Element[_count];
            _rules = new Element[_count];
            _story = new Element[_count * 2];
            for (int i = 0; i < _count; i++)
            {
                var stage = _cfg.Journey[i];
                string number = (i + 1).ToString();
                _open[i] = ui.CreateButton(number, WorldColor(stage.Theme), Color.white);
                _locked[i] = ui.CreateButton(VoxelFont.LockIcon, Palette.Hex("#6B6B6B"), Palette.Hex("#C8C8C8"));
                _checks[i] = ui.Text(VoxelFont.CheckIcon, TextAlign.Center, Palette.GetReady, Palette.Outline);
                _names[i] = ui.Text(JourneyTexts.StageName(stage.Theme), TextAlign.Center, Palette.PanelLabel, Palette.Panel);
                _rules[i] = ui.Text(JourneyTexts.RuleName(stage.Rule), TextAlign.Center, WorldColor(stage.Theme), Palette.Outline);
                _story[i * 2] = ui.Text(JourneyTexts.Story(stage.Theme, 0), TextAlign.Center, Palette.PanelLabel, Palette.Panel);
                _story[i * 2 + 1] = ui.Text(JourneyTexts.Story(stage.Theme, 1), TextAlign.Center, Palette.PanelLabel, Palette.Panel);
                if (i < _count - 1) _links[i] = ui.Solid(Palette.PanelEdge);
            }
            _lockedHint = ui.Text(VoxelFont.LockIcon + Lang.T(" RÉUSSIS L'ÉTAPE PRÉCÉDENTE", " CLEAR THE PREVIOUS STAGE", " SUPERA LA ETAPA ANTERIOR", " SCHAFFE DIE VORIGE ETAPPE", " CONCLUA A ETAPA ANTERIOR"), TextAlign.Center, Palette.PanelLabel, Palette.Panel);
            _play = ui.CreateButton(VoxelFont.PlayIcon + Lang.T(" JOUER", " PLAY", " JUGAR", " SPIELEN", " JOGAR"), Palette.Hex("#C0392B"), Color.white);
            _back = ui.CreateButton(Lang.T("RETOUR", "BACK", "VOLVER", "ZURÜCK", "VOLTAR"), Palette.GameOver, Color.white);
        }

        /// <summary>Monde affiché sous la carte (indice d'étape).</summary>
        public int Selected { get; private set; }

        /// <summary>Ouvre la carte sur l'étape proposée.</summary>
        public void Reset(int stage) => Selected = Mathf.Clamp(stage, 0, _count - 1);

        public void Select(int stage) => Selected = Mathf.Clamp(stage, 0, _count - 1);

        /// <summary>Une étape est ouverte si elle est réussie ou si c'est la suivante.</summary>
        public static bool IsOpen(int stage, int cleared) => stage <= cleared;

        /// <summary>Couleur d'un monde sur la carte.</summary>
        public static Color WorldColor(Theme theme)
        {
            switch (theme)
            {
                case Theme.Night: return Palette.Hex("#3B4BA8");
                case Theme.City: return Palette.Hex("#C2399E");
                case Theme.Japan: return Palette.Hex("#D9443B");
                case Theme.Storm: return Palette.Hex("#6A5ACD");
                case Theme.Snow: return Palette.Hex("#4FA8D8");
                case Theme.Jungle: return Palette.Hex("#2E8B57");
                case Theme.Sky: return Palette.Hex("#E8962B");
                case Theme.Ocean: return Palette.Hex("#1F7FA8");
                default: return Palette.Hex("#5DAE3B");
            }
        }

        static float NodeX(int i)
        {
            int row = i / 3;
            int col = i % 3;
            // Serpentin : la deuxième rangée se lit de droite à gauche.
            return Columns[row % 2 == 0 ? col : 2 - col];
        }

        static float NodeY(int i) => FirstRowY + (i / 3) * RowStep;

        /// <param name="cleared">Étapes réussies d'affilée depuis la première (0 à 9).</param>
        public void Draw(int cleared, float time)
        {
            _ui.PlaceBox(_panelBorder, PanelLeft - 2f, PanelTop - 2f, PanelWidth + 4f, PanelHeight + 4f, UiLayer.PanelZ + 0.03f);
            _ui.PlaceBox(_panel, PanelLeft, PanelTop, PanelWidth, PanelHeight, UiLayer.PanelZ + 0.02f);
            _ui.PlaceBox(_panelInner, PanelLeft + 6f, PanelTop + 6f, PanelWidth - 12f, PanelHeight - 12f, UiLayer.PanelZ);
            _ui.Place(_title, _cfg.Width * 0.5f, TitleY, 1.8f, maxWidth: PanelWidth - 20f);

            // Chemin : un trait entre chaque monde et le suivant, derrière les boutons.
            for (int i = 0; i < _links.Length; i++)
            {
                float x0 = NodeX(i), y0 = NodeY(i), x1 = NodeX(i + 1), y1 = NodeY(i + 1);
                float left = Mathf.Min(x0, x1) - 3f, top = Mathf.Min(y0, y1) - 3f;
                _ui.PlaceBox(_links[i], left, top, Mathf.Abs(x1 - x0) + 6f, Mathf.Abs(y1 - y0) + 6f, UiLayer.PanelZ - 0.01f);
            }

            for (int i = 0; i < _count; i++)
            {
                bool open = IsOpen(i, cleared);
                float x = NodeX(i), y = NodeY(i);
                // La prochaine étape palpite doucement.
                float grow = i == cleared ? 1f + 0.08f * Mathf.Sin(time * 5f) : 1f;
                float w = NodeWidth * grow, h = NodeHeight * grow;
                if (open)
                {
                    UiLayer.Hide(_locked[i]);
                    _ui.PlaceButton(_open[i], UiAction.MapNode0 + i, x, y, w, h, 2f, hitMargin: 2f);
                }
                else
                {
                    UiLayer.Hide(_open[i]);
                    _ui.PlaceButton(_locked[i], UiAction.MapNode0 + i, x, y, w, h, 1.6f, hitMargin: 2f);
                }
                if (i < cleared) _ui.Place(_checks[i], x + w * 0.5f - 2f, y - h * 0.5f - 6f, 1.2f, UiLayer.HudZ - 0.04f);
                else UiLayer.Hide(_checks[i]);
            }
            float sx = NodeX(Selected), sy = NodeY(Selected);
            _ui.PlaceBox(_selection, sx - NodeWidth * 0.5f - 6f, sy - NodeHeight * 0.5f - 6f, NodeWidth + 12f, NodeHeight + 12f, UiLayer.HudZ + 0.14f);

            float cx = _cfg.Width * 0.5f;
            for (int i = 0; i < _count; i++)
            {
                bool shown = i == Selected;
                if (shown)
                {
                    _ui.Place(_names[i], cx, NameY, 2f, maxWidth: PanelWidth - 20f);
                    _ui.Place(_rules[i], cx, RuleY, 1.4f, maxWidth: PanelWidth - 20f);
                    _ui.Place(_story[i * 2], cx, StoryY, 1.2f, maxWidth: PanelWidth - 20f);
                    _ui.Place(_story[i * 2 + 1], cx, StoryY + StoryLineStep, 1.2f, maxWidth: PanelWidth - 20f);
                }
                else
                {
                    UiLayer.Hide(_names[i]);
                    UiLayer.Hide(_rules[i]);
                    UiLayer.Hide(_story[i * 2]);
                    UiLayer.Hide(_story[i * 2 + 1]);
                }
            }

            if (IsOpen(Selected, cleared))
            {
                UiLayer.Hide(_lockedHint);
                _ui.PlaceButton(_play, UiAction.MapPlay, cx, ActionY, 140f, 26f, 1.8f);
            }
            else
            {
                UiLayer.Hide(_play);
                _ui.Place(_lockedHint, cx, ActionY - VoxelFont.GlyphHeight * 0.6f, 1.2f, maxWidth: PanelWidth - 20f);
            }
            _ui.PlaceButton(_back, UiAction.CloseMenu, cx, BackY, 120f, 22f, 1.6f);
        }

        public void Hide()
        {
            UiLayer.Hide(_panelBorder);
            UiLayer.Hide(_panel);
            UiLayer.Hide(_panelInner);
            UiLayer.Hide(_title);
            UiLayer.Hide(_selection);
            foreach (var b in _open) UiLayer.Hide(b);
            foreach (var b in _locked) UiLayer.Hide(b);
            foreach (var e in _checks) UiLayer.Hide(e);
            foreach (var e in _links) UiLayer.Hide(e);
            foreach (var e in _names) UiLayer.Hide(e);
            foreach (var e in _rules) UiLayer.Hide(e);
            foreach (var e in _story) UiLayer.Hide(e);
            UiLayer.Hide(_lockedHint);
            UiLayer.Hide(_play);
            UiLayer.Hide(_back);
        }
    }
}
