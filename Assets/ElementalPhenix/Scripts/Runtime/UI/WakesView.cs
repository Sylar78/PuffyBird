using System;
using ElementalPhenix.Core;
using ElementalPhenix.Rendering;
using UnityEngine;

namespace ElementalPhenix.UI
{
    using Element = UiLayer.Element;

    /// <summary>
    /// Menu des sillages (bouton SILLAGES du menu des phénix), sous l'aperçu du phénix qui montre le
    /// sillage parcouru : nom et flèches, prix en éclats ou « débloqué », bouton DÉBLOQUER / CHOISIR,
    /// et les éclats en réserve. Libellés construits au chargement, sauf le nombre d'éclats, refait
    /// seulement quand il change (hors partie).
    /// </summary>
    public sealed class WakesView
    {
        const float PanelTop = 266f;
        const float PanelWidth = 240f;
        const float PanelHeight = 132f;
        const float NameY = 290f;
        const float StatusY = 314f;
        const float ShardsY = 254f;
        const float ActionY = 348f;
        const float BackY = 381f;

        enum ActionLabel
        {
            Use,
            InUse,
            Unlock,
            TooExpensive,
        }

        readonly UiLayer _ui;
        readonly GameConfig _cfg;
        readonly Func<int> _shards;
        readonly Func<int> _owned;
        readonly Func<int> _selected;
        readonly Element _panelBorder;
        readonly Element _panel;
        readonly Element _panelInner;
        readonly Element[] _names = new Element[Wakes.Count];
        readonly Element[] _prices = new Element[Wakes.Count];
        readonly Element _unlocked;
        readonly Element[] _actionLabels = new Element[4];
        readonly UiLayer.Button _previous;
        readonly UiLayer.Button _next;
        readonly UiLayer.Button _action;
        readonly UiLayer.Button _back;
        Element _shardsText;
        int _shardsShown = -1;
        int _cursor;

        public WakesView(UiLayer ui, Func<int> shards, Func<int> owned, Func<int> selected)
        {
            _ui = ui;
            _cfg = ui.Space.Config;
            _shards = shards;
            _owned = owned;
            _selected = selected;
            _panelBorder = ui.Solid(Palette.Outline);
            _panel = ui.Solid(Palette.PanelEdge);
            _panelInner = ui.Solid(Palette.Panel);
            for (int i = 0; i < Wakes.Count; i++)
            {
                var wake = Wakes.Get(i);
                _names[i] = ui.Text(wake.NameIn(Lang.Current), TextAlign.Center, Color.white, Palette.Outline);
                int cost = wake.Cost;
                _prices[i] = ui.Text(Lang.T($"{cost} ÉCLATS", $"{cost} SHARDS", $"{cost} FRAGMENTOS", $"{cost} SPLITTER", $"{cost} FRAGMENTOS"), TextAlign.Center, Palette.PanelLabel, Palette.Panel);
            }
            _unlocked = ui.Text(VoxelFont.CheckIcon + Lang.T(" DÉBLOQUÉ", " UNLOCKED", " DESBLOQUEADO", " FREIGESCHALTET", " DESBLOQUEADO"), TextAlign.Center, Palette.PanelLabel, Palette.Panel);
            _actionLabels[(int)ActionLabel.Use] = ui.Text(Lang.T("CHOISIR", "USE", "ELEGIR", "WÄHLEN", "USAR"), TextAlign.Center, Color.white, Palette.Outline);
            _actionLabels[(int)ActionLabel.InUse] = ui.Text(VoxelFont.CheckIcon + Lang.T(" CHOISI", " IN USE", " ELEGIDO", " GEWÄHLT", " EM USO"), TextAlign.Center, Color.white, Palette.Outline);
            _actionLabels[(int)ActionLabel.Unlock] = ui.Text(Lang.T("DÉBLOQUER", "UNLOCK", "DESBLOQUEAR", "FREISCHALTEN", "DESBLOQUEAR"), TextAlign.Center, Color.white, Palette.Outline);
            _actionLabels[(int)ActionLabel.TooExpensive] = ui.Text(VoxelFont.LockIcon + Lang.T(" PAS ASSEZ", " NOT ENOUGH", " NO ALCANZA", " ZU WENIG", " INSUFICIENTE"), TextAlign.Center, Color.white, Palette.Outline);
            _previous = ui.CreateButton("<", Palette.Hex("#4EA6D8"), Color.white);
            _next = ui.CreateButton(">", Palette.Hex("#4EA6D8"), Color.white);
            _action = ui.CreateButton(null, Palette.GetReady, Color.white);
            _back = ui.CreateButton(Lang.T("RETOUR", "BACK", "VOLVER", "ZURÜCK", "VOLTAR"), Palette.GameOver, Color.white);
        }

        /// <summary>Sillage affiché dans le menu (indice du catalogue).</summary>
        public int Browsed => _cursor;

        public void Reset() => _cursor = Mathf.Clamp(_selected(), 0, Wakes.Count - 1);

        public void Step(int direction)
        {
            int n = Wakes.Count;
            _cursor = ((_cursor + direction) % n + n) % n;
        }

        public void Draw()
        {
            float left = (_cfg.Width - PanelWidth) * 0.5f;
            float cx = _cfg.Width * 0.5f;
            _ui.PlaceBox(_panelBorder, left - 2f, PanelTop - 2f, PanelWidth + 4f, PanelHeight + 4f, UiLayer.PanelZ + 0.03f);
            _ui.PlaceBox(_panel, left, PanelTop, PanelWidth, PanelHeight, UiLayer.PanelZ + 0.02f);
            _ui.PlaceBox(_panelInner, left + 6f, PanelTop + 6f, PanelWidth - 12f, PanelHeight - 12f, UiLayer.PanelZ);
            _ui.Place(ShardsText(_shards()), cx, ShardsY, 1.6f);

            for (int i = 0; i < Wakes.Count; i++)
            {
                if (i != _cursor) UiLayer.Hide(_names[i]);
            }
            _ui.Place(_names[_cursor], cx, NameY - VoxelFont.GlyphHeight * 1.2f, 2.2f, maxWidth: PanelWidth - 96f);
            _ui.PlaceButton(_previous, UiAction.WakePrevious, left + 26f, NameY, 30f, 26f, 2f, hitMargin: 6f);
            _ui.PlaceButton(_next, UiAction.WakeNext, left + PanelWidth - 26f, NameY, 30f, 26f, 2f, hitMargin: 6f);

            var wake = Wakes.Get(_cursor);
            bool owned = Wakes.IsOwned(_cursor, _owned());
            Element status = owned ? _unlocked : _prices[_cursor];
            for (int i = 0; i < Wakes.Count; i++)
            {
                if (_prices[i] != status) UiLayer.Hide(_prices[i]);
            }
            if (_unlocked != status) UiLayer.Hide(_unlocked);
            _ui.Place(status, cx, StatusY - VoxelFont.GlyphHeight * 0.8f, 1.6f, maxWidth: PanelWidth - 16f);

            ActionLabel action = owned
                ? (_cursor == _selected() ? ActionLabel.InUse : ActionLabel.Use)
                : (_shards() >= wake.Cost ? ActionLabel.Unlock : ActionLabel.TooExpensive);
            for (int i = 0; i < _actionLabels.Length; i++)
            {
                if (i != (int)action) UiLayer.Hide(_actionLabels[i]);
            }
            var label = _actionLabels[(int)action];
            var tap = action == ActionLabel.Use ? UiAction.WakeUse : (action == ActionLabel.Unlock ? UiAction.WakeUnlock : UiAction.None);
            _ui.PlaceButton(_action, tap, cx, ActionY, 150f, 26f, 1.8f, label, hitMargin: tap == UiAction.None ? 0f : 4f);
            _ui.PlaceButton(_back, UiAction.OpenSkins, cx, BackY, 120f, 22f, 1.6f);
        }

        public void Hide()
        {
            UiLayer.Hide(_panelBorder);
            UiLayer.Hide(_panel);
            UiLayer.Hide(_panelInner);
            UiLayer.Hide(_shardsText);
            foreach (var e in _names) UiLayer.Hide(e);
            foreach (var e in _prices) UiLayer.Hide(e);
            UiLayer.Hide(_unlocked);
            foreach (var e in _actionLabels) UiLayer.Hide(e);
            UiLayer.Hide(_previous);
            UiLayer.Hide(_next);
            UiLayer.Hide(_action);
            UiLayer.Hide(_back);
        }

        /// <summary>« ÉCLATS : n », reconstruit seulement quand n change (hors partie).</summary>
        Element ShardsText(int shards)
        {
            if (_shardsText != null && shards == _shardsShown) return _shardsText;
            _shardsShown = shards;
            var mesh = VoxelFont.Build(ShardsLabel(shards), TextAlign.Center, Palette.Hex("#8FE6FF"), Palette.Outline);
            if (_shardsText == null)
            {
                _shardsText = _ui.Create("Éclats", mesh, _ui.TextMaterial);
            }
            else
            {
                UnityEngine.Object.Destroy(_shardsText.Filter.sharedMesh);
                _shardsText.Filter.sharedMesh = mesh;
            }
            return _shardsText;
        }

        public static string ShardsLabel(int shards)
            => Lang.T($"ÉCLATS : {shards}", $"SHARDS: {shards}", $"FRAGMENTOS: {shards}", $"SPLITTER: {shards}", $"FRAGMENTOS: {shards}");
    }
}
