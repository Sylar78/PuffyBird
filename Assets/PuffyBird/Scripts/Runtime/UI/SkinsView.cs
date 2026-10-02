using System;
using PuffyBird.Core;
using PuffyBird.Rendering;
using PuffyBird.Store;
using UnityEngine;

namespace PuffyBird.UI
{
    using Element = UiLayer.Element;

    /// <summary>
    /// Menu des oiseaux, sous l'aperçu agrandi de l'oiseau : nom et flèches pour parcourir le
    /// catalogue, état (débloqué, score à atteindre, prix) et bouton CHOISIR / ACHETER. Les libellés sont
    /// construits au chargement, sauf les prix : la boutique les donne plus tard, une seule fois.
    /// </summary>
    public sealed class SkinsView
    {
        const float PanelTop = 266f;
        const float PanelWidth = 240f;
        const float PanelHeight = 132f;
        const float NameY = 290f;
        const float StatusY = 318f;
        const float ActionY = 348f;
        const float BackY = 381f;

        enum ActionLabel
        {
            Use,
            InUse,
            Buy,
            Locked,
        }

        readonly UiLayer _ui;
        readonly GameConfig _cfg;
        readonly IStore _store;
        readonly Func<int, bool> _isUnlocked;
        readonly Func<int> _selected;
        readonly int[] _browsable;
        readonly Element _panelBorder;
        readonly Element _panel;
        readonly Element _panelInner;
        readonly Element[] _names = new Element[Skins.Count];
        readonly Element[] _hints = new Element[Skins.Count];
        readonly Element[] _prices = new Element[Skins.Count];
        readonly string[] _priceTexts = new string[Skins.Count];
        readonly Element _unlocked;
        readonly Element _premium;
        readonly Element _storeOffline;
        readonly Element[] _actionLabels = new Element[4];
        readonly UiLayer.Button _previous;
        readonly UiLayer.Button _next;
        readonly UiLayer.Button _action;
        readonly UiLayer.Button _back;
        int _cursor;
        int _storeVersion = -1;

        /// <param name="includePaid">Une boutique existe : les oiseaux payants sont proposés.</param>
        public SkinsView(UiLayer ui, IStore store, bool includePaid, Func<int, bool> isUnlocked, Func<int> selected)
        {
            _ui = ui;
            _cfg = ui.Space.Config;
            _store = store;
            _isUnlocked = isUnlocked;
            _selected = selected;

            int count = 0;
            for (int i = 0; i < Skins.Count; i++)
            {
                if (includePaid || Skins.Get(i).Unlock != SkinUnlock.Purchase) count++;
            }
            _browsable = new int[count];
            count = 0;
            for (int i = 0; i < Skins.Count; i++)
            {
                var skin = Skins.Get(i);
                if (includePaid || skin.Unlock != SkinUnlock.Purchase) _browsable[count++] = i;
                _names[i] = ui.Text(Lang.T(skin.Name, skin.EnglishName), TextAlign.Center, Color.white, Palette.Outline);
                if (skin.Unlock == SkinUnlock.Medal)
                {
                    string hint = Lang.French ? $"FAIS {Skins.RequiredScore(skin.Medal, _cfg)} POUR DÉBLOQUER" : $"BEST {Skins.RequiredScore(skin.Medal, _cfg)} TO UNLOCK";
                    _hints[i] = ui.Text(hint, TextAlign.Center, Palette.PanelLabel, Palette.Panel);
                }
            }

            _panelBorder = ui.Solid(Palette.Outline);
            _panel = ui.Solid(Palette.PanelEdge);
            _panelInner = ui.Solid(Palette.Panel);
            _unlocked = ui.Text(VoxelFont.CheckIcon + Lang.T(" DÉBLOQUÉ", " UNLOCKED"), TextAlign.Center, Palette.PanelLabel, Palette.Panel);
            _premium = ui.Text("PREMIUM", TextAlign.Center, Palette.PanelLabel, Palette.Panel);
            _storeOffline = ui.Text(Lang.T("BOUTIQUE INDISPONIBLE", "STORE OFFLINE"), TextAlign.Center, Palette.PanelLabel, Palette.Panel);
            _actionLabels[(int)ActionLabel.Use] = ui.Text(Lang.T("CHOISIR", "USE"), TextAlign.Center, Color.white, Palette.Outline);
            _actionLabels[(int)ActionLabel.InUse] = ui.Text(VoxelFont.CheckIcon + Lang.T(" CHOISI", " IN USE"), TextAlign.Center, Color.white, Palette.Outline);
            _actionLabels[(int)ActionLabel.Buy] = ui.Text(Lang.T("ACHETER", "BUY"), TextAlign.Center, Color.white, Palette.Outline);
            _actionLabels[(int)ActionLabel.Locked] = ui.Text(VoxelFont.LockIcon + Lang.T(" BLOQUÉ", " LOCKED"), TextAlign.Center, Color.white, Palette.Outline);
            _previous = ui.CreateButton("<", Palette.Hex("#4EA6D8"), Color.white);
            _next = ui.CreateButton(">", Palette.Hex("#4EA6D8"), Color.white);
            _action = ui.CreateButton(null, Palette.GetReady, Color.white);
            _back = ui.CreateButton(Lang.T("RETOUR", "BACK"), Palette.GameOver, Color.white);
        }

        /// <summary>Oiseau affiché dans le menu (indice du catalogue).</summary>
        public int Browsed => _browsable[_cursor];

        /// <summary>Ouvre le menu sur l'oiseau choisi.</summary>
        public void Reset()
        {
            int selected = _selected();
            _cursor = 0;
            for (int i = 0; i < _browsable.Length; i++)
            {
                if (_browsable[i] == selected) _cursor = i;
            }
        }

        public void Step(int direction)
        {
            int n = _browsable.Length;
            _cursor = ((_cursor + direction) % n + n) % n;
        }

        public void Draw()
        {
            RefreshPrices();

            float left = (_cfg.Width - PanelWidth) * 0.5f;
            float cx = _cfg.Width * 0.5f;
            _ui.PlaceBox(_panelBorder, left - 2f, PanelTop - 2f, PanelWidth + 4f, PanelHeight + 4f, UiLayer.PanelZ + 0.03f);
            _ui.PlaceBox(_panel, left, PanelTop, PanelWidth, PanelHeight, UiLayer.PanelZ + 0.02f);
            _ui.PlaceBox(_panelInner, left + 6f, PanelTop + 6f, PanelWidth - 12f, PanelHeight - 12f, UiLayer.PanelZ);

            int browsed = Browsed;
            var skin = Skins.Get(browsed);
            for (int i = 0; i < Skins.Count; i++)
            {
                if (i != browsed) UiLayer.Hide(_names[i]);
            }
            _ui.Place(_names[browsed], cx, NameY - VoxelFont.GlyphHeight * 1.2f, 2.4f);
            _ui.PlaceButton(_previous, UiAction.SkinPrevious, left + 26f, NameY, 30f, 26f, 2f, hitMargin: 6f);
            _ui.PlaceButton(_next, UiAction.SkinNext, left + PanelWidth - 26f, NameY, 30f, 26f, 2f, hitMargin: 6f);

            bool unlocked = _isUnlocked(browsed);
            bool inUse = browsed == _selected();
            Element status;
            ActionLabel action;
            if (unlocked)
            {
                status = _unlocked;
                action = inUse ? ActionLabel.InUse : ActionLabel.Use;
            }
            else if (skin.Unlock == SkinUnlock.Medal)
            {
                status = _hints[browsed];
                action = ActionLabel.Locked;
            }
            else if (_store.Ready)
            {
                status = _prices[browsed] ?? _premium;
                action = ActionLabel.Buy;
            }
            else
            {
                status = _storeOffline;
                action = ActionLabel.Locked;
            }

            HideStatuses(status);
            _ui.Place(status, cx, StatusY - VoxelFont.GlyphHeight * 0.8f, 1.6f);

            for (int i = 0; i < _actionLabels.Length; i++)
            {
                if (i != (int)action) UiLayer.Hide(_actionLabels[i]);
            }
            var label = _actionLabels[(int)action];
            switch (action)
            {
                case ActionLabel.Use:
                    _ui.PlaceButton(_action, UiAction.SkinUse, cx, ActionY, 140f, 26f, 1.8f, label);
                    break;
                case ActionLabel.Buy:
                    _ui.PlaceButton(_action, UiAction.SkinBuy, cx, ActionY, 140f, 26f, 1.8f, label);
                    break;
                default:
                    // Pas d'action : bouton affiché sans zone de tap.
                    _ui.PlaceButton(_action, UiAction.None, cx, ActionY, 140f, 26f, 1.8f, label, hitMargin: 0f);
                    break;
            }

            _ui.PlaceButton(_back, UiAction.CloseMenu, cx, BackY, 120f, 22f, 1.6f);
        }

        public void Hide()
        {
            UiLayer.Hide(_panelBorder);
            UiLayer.Hide(_panel);
            UiLayer.Hide(_panelInner);
            foreach (var e in _names) UiLayer.Hide(e);
            HideStatuses(null);
            foreach (var e in _actionLabels) UiLayer.Hide(e);
            UiLayer.Hide(_previous);
            UiLayer.Hide(_next);
            UiLayer.Hide(_action);
            UiLayer.Hide(_back);
        }

        void HideStatuses(Element keep)
        {
            for (int i = 0; i < Skins.Count; i++)
            {
                if (_hints[i] != keep) UiLayer.Hide(_hints[i]);
                if (_prices[i] != keep) UiLayer.Hide(_prices[i]);
            }
            if (_unlocked != keep) UiLayer.Hide(_unlocked);
            if (_premium != keep) UiLayer.Hide(_premium);
            if (_storeOffline != keep) UiLayer.Hide(_storeOffline);
        }

        /// <summary>Prix localisés : construits quand la boutique les donne (rare, hors partie).</summary>
        void RefreshPrices()
        {
            if (_store.Version == _storeVersion) return;
            _storeVersion = _store.Version;
            for (int i = 0; i < Skins.Count; i++)
            {
                string productId = Skins.Get(i).ProductId;
                if (productId == null) continue;
                string price = _store.Price(productId);
                if (string.IsNullOrEmpty(price) || price == _priceTexts[i]) continue;
                _priceTexts[i] = price;
                var mesh = VoxelFont.Build(price, TextAlign.Center, Palette.PanelLabel, Palette.Panel);
                if (_prices[i] == null)
                {
                    _prices[i] = _ui.Create("Prix " + Skins.Get(i).Id, mesh, _ui.TextMaterial);
                }
                else
                {
                    UnityEngine.Object.Destroy(_prices[i].Filter.sharedMesh);
                    _prices[i].Filter.sharedMesh = mesh;
                }
            }
        }
    }
}
