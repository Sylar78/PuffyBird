using System;
using System.Collections.Generic;
using PuffyBird.Core;
using PuffyBird.Rendering;
using UnityEngine;

namespace PuffyBird.UI
{
    using Element = UiLayer.Element;

    /// <summary>Menu ouvert par-dessus l'écran titre, ou par-dessus la partie (paramètres).</summary>
    public enum MenuScreen
    {
        None,
        Settings,
        Skins,
        Consent,
    }

    /// <summary>
    /// Boutons de l'écran titre (paramètres, oiseaux, classement), bouton des paramètres pendant
    /// la partie, boutons de l'écran de fin (partage, classement) et panneau des paramètres.
    /// Chaque ligne du panneau est un bouton dont le libellé change selon l'état
    /// (« SONS : OUI » / « SONS : NON »...) ; tous les libellés sont construits au chargement.
    /// Ouvert pendant la partie, le panneau propose aussi ACCUEIL (abandon de la partie).
    /// </summary>
    public sealed class MenuView
    {
        const float IconButtonSize = 26f;
        const float RowWidth = 200f;
        const float RowHeight = 24f;
        const float RowGap = 6f;
        const float PanelTop = 112f;
        const float PanelWidth = 240f;
        const float OverNewSkinY = 304f;
        const float OverContinueY = 330f;
        const float OverButtonsY = 366f;
        const float ContinueButtonWidth = 190f;
        const float OverButtonWidth = 124f;
        const float SkinsButtonWidth = 100f;
        const float OverButtonHeight = 26f;

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
        readonly UiLayer.Button _homeButton;
        readonly UiLayer.Button _trophyButton;
        readonly UiLayer.Button _overRankingButton;
        readonly UiLayer.Button _overShareButton;
        readonly UiLayer.Button _skinsButton;
        readonly UiLayer.Button _continueButton;
        readonly Element _newSkin;
        SkinsView _skins;
        MenuScreen _returnTo;
        bool _inGame;

        public MenuView(UiLayer ui)
        {
            _ui = ui;
            _cfg = ui.Space.Config;
            _panelBorder = ui.Solid(Palette.Outline);
            _panel = ui.Solid(Palette.PanelEdge);
            _panelInner = ui.Solid(Palette.Panel);
            _settingsTitle = ui.Text(Lang.T("PARAMÈTRES", "SETTINGS", "AJUSTES", "EINSTELLUNGEN", "CONFIGURAÇÕES"), TextAlign.Center, Palette.PanelLabel, Palette.Panel);
            _gearButton = ui.CreateButton(VoxelFont.SettingsIcon, Palette.GameOver, Color.white);
            _backButton = ui.CreateButton(Lang.T("RETOUR", "BACK", "VOLVER", "ZURÜCK", "VOLTAR"), Palette.GetReady, Color.white);
            _homeButton = ui.CreateButton(Lang.T("ACCUEIL", "HOME", "INICIO", "MENÜ", "INÍCIO"), Palette.GameOver, Color.white);
            _trophyButton = ui.CreateButton(VoxelFont.TrophyIcon, Palette.GetReady, Color.white);
            _overRankingButton = ui.CreateButton(VoxelFont.TrophyIcon + Lang.T(" CLASSEMENT", " RANKING", " RANKING", " RANGLISTE", " RANKING"), Palette.GetReady, Color.white);
            _overShareButton = ui.CreateButton(VoxelFont.ShareIcon + Lang.T(" PARTAGER", " SHARE", " COMPARTIR", " TEILEN", " COMPARTILHAR"), Palette.Hex("#4EA6D8"), Color.white);
            _skinsButton = ui.CreateButton(Lang.T("OISEAUX", "BIRDS", "PÁJAROS", "VÖGEL", "AVES"), Palette.GameOver, Color.white);
            _continueButton = ui.CreateButton(VoxelFont.PlayIcon + Lang.T(" CONTINUER (PUB)", " CONTINUE (AD)", " CONTINUAR (ANUNCIO)", " WEITER (WERBUNG)", " CONTINUAR (ANÚNCIO)"), Palette.GetReady, Color.white);
            _newSkin = ui.Text(Lang.T("NOUVEL OISEAU DÉBLOQUÉ !", "NEW BIRD UNLOCKED!", "¡NUEVO PÁJARO DESBLOQUEADO!", "NEUER VOGEL FREIGESCHALTET!", "NOVA AVE DESBLOQUEADA!"), TextAlign.Center, Palette.GetReady, Palette.Outline);
        }

        /// <summary>Menu des oiseaux (bouton OISEAUX de l'écran titre) ; null = pas de bouton.</summary>
        public SkinsView Skins
        {
            get => _skins;
            set => _skins = value;
        }

        /// <summary>Écran de consentement aux pubs personnalisées ; null = pas de pub, pas d'écran.</summary>
        public ConsentView Consent { get; set; }

        /// <summary>Choix de consentement à afficher (true, false ou null).</summary>
        public Func<bool?> ConsentChoice { get; set; }

        /// <summary>La partie qui vient de finir a débloqué un oiseau : annonce sur l'écran de fin.</summary>
        public bool NewSkinUnlocked { get; set; }

        /// <summary>Seconde chance proposée sur l'écran de fin (partie pas encore relancée, vidéo prête).</summary>
        public Func<bool> ContinueAvailable { get; set; }

        /// <summary>Un classement en ligne existe sur cette plateforme : boutons trophée affichés.</summary>
        public bool LeaderboardAvailable { get; set; }

        public MenuScreen Screen { get; private set; }

        public bool IsOpen => Screen != MenuScreen.None;

        public void Open(MenuScreen screen)
        {
            if (screen == MenuScreen.Skins && _skins == null) return;
            if (screen == MenuScreen.Consent && Consent == null) return;
            _returnTo = screen == MenuScreen.Consent ? Screen : MenuScreen.None;
            Screen = screen;
            if (screen == MenuScreen.Skins) _skins.Reset();
        }

        public void Close() => Screen = MenuScreen.None;

        /// <summary>Revient à l'écran d'où le consentement a été ouvert (paramètres), ou ferme le menu.</summary>
        public void Back() => Screen = _returnTo;

        /// <summary>
        /// Ajoute une ligne aux paramètres : un bouton qui déclenche <paramref name="action"/> et
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
            var state = sim.State;
            bool fading = sim.IsFadingOut;
            bool title = state == GameState.Title && !fading;
            // Pendant la partie, les paramètres restent accessibles (la partie est mise en pause).
            bool inGame = !fading && (state == GameState.Ready || state == GameState.Playing || state == GameState.Paused);
            _inGame = inGame;
            if (!title && !inGame) Close();
            // Le menu des oiseaux ne s'ouvre que depuis l'écran titre.
            if (!title && Screen == MenuScreen.Skins) Close();

            float iconY = safeTopPx + _cfg.ScoreTopMargin + IconButtonSize * 0.5f;
            if ((title || inGame) && !IsOpen) _ui.PlaceButton(_gearButton, UiAction.OpenSettings, _cfg.Width - 22f, iconY, IconButtonSize, IconButtonSize, 2f, hitMargin: 9f);
            else UiLayer.Hide(_gearButton);
            if (title && !IsOpen)
            {
                if (LeaderboardAvailable) _ui.PlaceButton(_trophyButton, UiAction.OpenLeaderboard, 22f, iconY, IconButtonSize, IconButtonSize, 2f, hitMargin: 9f);
                else UiLayer.Hide(_trophyButton);
                if (_skins != null) _ui.PlaceButton(_skinsButton, UiAction.OpenSkins, _cfg.Width * 0.5f, iconY, SkinsButtonWidth, IconButtonSize, 2f, hitMargin: 6f);
                else UiLayer.Hide(_skinsButton);
            }
            else
            {
                UiLayer.Hide(_trophyButton);
                UiLayer.Hide(_skinsButton);
            }

            // Fin de partie : seconde chance, partage et classement entre le panneau et « TOUCHE POUR REJOUER ».
            bool overButtons = sim.State == GameState.Over && !sim.IsFadingOut && OverScreenTimeline.ButtonsVisible(sim.StateTime, _cfg);
            if (overButtons)
            {
                float cx = _cfg.Width * 0.5f;
                if (LeaderboardAvailable)
                {
                    float offset = OverButtonWidth * 0.5f + 6f;
                    _ui.PlaceButton(_overShareButton, UiAction.Share, cx - offset, OverButtonsY, OverButtonWidth, OverButtonHeight, 1.6f);
                    _ui.PlaceButton(_overRankingButton, UiAction.OpenLeaderboard, cx + offset, OverButtonsY, OverButtonWidth, OverButtonHeight, 1.6f);
                }
                else
                {
                    _ui.PlaceButton(_overShareButton, UiAction.Share, cx, OverButtonsY, OverButtonWidth, OverButtonHeight, 1.6f);
                    UiLayer.Hide(_overRankingButton);
                }
            }
            else
            {
                UiLayer.Hide(_overShareButton);
                UiLayer.Hide(_overRankingButton);
            }
            if (overButtons && ContinueAvailable != null && ContinueAvailable())
                _ui.PlaceButton(_continueButton, UiAction.Continue, _cfg.Width * 0.5f, OverContinueY, ContinueButtonWidth, OverButtonHeight, 1.6f);
            else UiLayer.Hide(_continueButton);
            if (overButtons && NewSkinUnlocked) _ui.Place(_newSkin, _cfg.Width * 0.5f, OverNewSkinY, 1.4f);
            else UiLayer.Hide(_newSkin);

            if (Screen == MenuScreen.Settings) DrawSettings();
            else HideSettings();
            if (Screen == MenuScreen.Skins) _skins.Draw();
            else _skins?.Hide();
            if (Screen == MenuScreen.Consent) Consent.Draw(ConsentChoice?.Invoke());
            else Consent?.Hide();
        }

        void DrawSettings()
        {
            int visible = 0;
            foreach (var row in _rows)
            {
                if (row.Visible == null || row.Visible()) visible++;
            }
            int buttons = visible + (_inGame ? 2 : 1);
            float contentHeight = 34f + buttons * (RowHeight + RowGap) + 6f;
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
            if (_inGame)
            {
                _ui.PlaceButton(_homeButton, UiAction.GoHome, cx, y, RowWidth * 0.6f, RowHeight, 1.6f, hitMargin: RowGap * 0.5f);
                y += RowHeight + RowGap;
            }
            else
            {
                UiLayer.Hide(_homeButton);
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
            UiLayer.Hide(_homeButton);
            foreach (var row in _rows) HideRow(row);
        }

        static void HideRow(Row row)
        {
            UiLayer.Hide(row.Button);
            foreach (var label in row.Labels) UiLayer.Hide(label);
        }
    }
}
