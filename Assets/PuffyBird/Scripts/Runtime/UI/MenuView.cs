using System;
using System.Collections.Generic;
using PuffyBird.Core;
using PuffyBird.Rendering;
using UnityEngine;

namespace PuffyBird.UI
{
    using Element = UiLayer.Element;

    /// <summary>Menu ouvert par-dessus l'écran titre.</summary>
    public enum MenuScreen
    {
        None,
        Settings,
    }

    /// <summary>
    /// Boutons de l'écran titre (réglages) et panneau des réglages. Chaque ligne du panneau est un
    /// bouton dont le libellé change selon l'état (« SOUND ON » / « SOUND OFF »...) ; tous les
    /// libellés sont construits au chargement.
    /// </summary>
    public sealed class MenuView
    {
        const float IconButtonSize = 26f;
        const float RowWidth = 200f;
        const float RowHeight = 24f;
        const float RowGap = 8f;
        const float PanelTop = 128f;
        const float PanelWidth = 240f;

        sealed class Row
        {
            public UiAction Action;
            public UiLayer.Button Button;
            public Element[] Labels;
            public Func<int> Variant;
            public Func<bool> Visible;
        }

        readonly UiLayer _ui;
        readonly GameConfig _cfg;
        readonly List<Row> _rows = new List<Row>();
        readonly Element _panelBorder;
        readonly Element _panel;
        readonly Element _panelInner;
        readonly Element _settingsTitle;
        readonly UiLayer.Button _gearButton;
        readonly UiLayer.Button _backButton;

        public MenuView(UiLayer ui)
        {
            _ui = ui;
            _cfg = ui.Space.Config;
            _panelBorder = ui.Solid(Palette.Outline);
            _panel = ui.Solid(Palette.PanelEdge);
            _panelInner = ui.Solid(Palette.Panel);
            _settingsTitle = ui.Text("SETTINGS", TextAlign.Center, Palette.PanelLabel, Palette.Panel);
            _gearButton = ui.CreateButton(VoxelFont.SettingsIcon, Palette.GameOver, Color.white);
            _backButton = ui.CreateButton("BACK", Palette.GetReady, Color.white);
        }

        public MenuScreen Screen { get; private set; }

        public bool IsOpen => Screen != MenuScreen.None;

        public void Open(MenuScreen screen) => Screen = screen;

        public void Close() => Screen = MenuScreen.None;

        /// <summary>
        /// Ajoute une ligne aux réglages : un bouton qui déclenche <paramref name="action"/> et
        /// affiche <c>labels[variant()]</c>. À appeler au chargement.
        /// </summary>
        public void AddSettingsRow(UiAction action, Func<int> variant, Func<bool> visible, params string[] labels)
        {
            var row = new Row
            {
                Action = action,
                Button = _ui.CreateButton(null, Palette.Hex("#4EA6D8"), Color.white),
                Labels = new Element[labels.Length],
                Variant = variant,
                Visible = visible,
            };
            for (int i = 0; i < labels.Length; i++) row.Labels[i] = _ui.Text(labels[i], TextAlign.Center, Color.white, Palette.Outline);
            _rows.Add(row);
        }

        public void Update(GameSimulation sim, float safeTopPx)
        {
            bool title = sim.State == GameState.Title && !sim.IsFadingOut;
            if (!title) Close();

            if (title && !IsOpen)
            {
                float y = safeTopPx + _cfg.ScoreTopMargin + IconButtonSize * 0.5f;
                _ui.PlaceButton(_gearButton, UiAction.OpenSettings, _cfg.Width - 22f, y, IconButtonSize, IconButtonSize, 2f, hitMargin: 9f);
            }
            else
            {
                UiLayer.Hide(_gearButton);
            }

            if (Screen == MenuScreen.Settings) DrawSettings();
            else HideSettings();
        }

        void DrawSettings()
        {
            int visible = 0;
            foreach (var row in _rows)
            {
                if (row.Visible == null || row.Visible()) visible++;
            }
            float contentHeight = 34f + (visible + 1) * (RowHeight + RowGap) + 6f;
            float left = (_cfg.Width - PanelWidth) * 0.5f;
            _ui.PlaceBox(_panelBorder, left - 2f, PanelTop - 2f, PanelWidth + 4f, contentHeight + 4f, UiLayer.PanelZ + 0.03f);
            _ui.PlaceBox(_panel, left, PanelTop, PanelWidth, contentHeight, UiLayer.PanelZ + 0.02f);
            _ui.PlaceBox(_panelInner, left + 6f, PanelTop + 6f, PanelWidth - 12f, contentHeight - 12f, UiLayer.PanelZ);
            _ui.Place(_settingsTitle, _cfg.Width * 0.5f, PanelTop + 14f, 2f);

            float y = PanelTop + 34f + RowHeight * 0.5f + 4f;
            float cx = _cfg.Width * 0.5f;
            foreach (var row in _rows)
            {
                if (row.Visible != null && !row.Visible())
                {
                    HideRow(row);
                    continue;
                }
                int variant = row.Variant == null ? 0 : Mathf.Clamp(row.Variant(), 0, row.Labels.Length - 1);
                for (int i = 0; i < row.Labels.Length; i++)
                {
                    if (i != variant) UiLayer.Hide(row.Labels[i]);
                }
                _ui.PlaceButton(row.Button, row.Action, cx, y, RowWidth, RowHeight, 1.6f, row.Labels[variant], hitMargin: RowGap * 0.5f);
                y += RowHeight + RowGap;
            }
            _ui.PlaceButton(_backButton, UiAction.CloseMenu, cx, y, RowWidth * 0.6f, RowHeight, 1.6f, hitMargin: RowGap * 0.5f);
        }

        void HideSettings()
        {
            UiLayer.Hide(_panelBorder);
            UiLayer.Hide(_panel);
            UiLayer.Hide(_panelInner);
            UiLayer.Hide(_settingsTitle);
            UiLayer.Hide(_backButton);
            foreach (var row in _rows) HideRow(row);
        }

        static void HideRow(Row row)
        {
            UiLayer.Hide(row.Button);
            foreach (var label in row.Labels) UiLayer.Hide(label);
        }
    }
}
