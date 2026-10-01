using PuffyBird.Rendering;
using UnityEngine;

namespace PuffyBird.UI
{
    using Element = UiLayer.Element;

    /// <summary>
    /// Écran de consentement RGPD aux pubs personnalisées, au premier lancement puis depuis les
    /// réglages (PRIVACY). Accepter et refuser ont le même poids (même taille, même couleur) ;
    /// lien vers la politique de confidentialité. En français si l'appareil l'est, sinon en
    /// anglais (police sans accents).
    /// </summary>
    public sealed class ConsentView
    {
        const float PanelTop = 112f;
        const float PanelWidth = 252f;
        const float PanelHeight = 224f;
        const float LineHeight = 14f;
        const float ButtonsY = PanelTop + 162f;
        const float LinkY = PanelTop + 200f;

        static readonly string[] English =
        {
            "PUFFYBIRD IS FREE",
            "THANKS TO ADS. MAY OUR",
            "PARTNERS USE YOUR DATA",
            "TO SHOW YOU ADS THAT",
            "MATCH YOUR INTERESTS?",
            "YOU CAN CHANGE THIS IN",
            "SETTINGS > PRIVACY.",
        };

        static readonly string[] French =
        {
            "PUFFYBIRD EST GRATUIT",
            "GRACE A LA PUB. NOS",
            "PARTENAIRES PEUVENT-ILS",
            "UTILISER VOS DONNEES",
            "POUR DES PUBS ADAPTEES ?",
            "MODIFIABLE A TOUT MOMENT",
            "DANS SETTINGS > PRIVACY.",
        };

        readonly UiLayer _ui;
        readonly float _width;
        readonly Element _panelBorder;
        readonly Element _panel;
        readonly Element _panelInner;
        readonly Element _title;
        readonly Element[] _lines;
        readonly Element _accept;
        readonly Element _acceptChosen;
        readonly Element _refuse;
        readonly Element _refuseChosen;
        readonly UiLayer.Button _acceptButton;
        readonly UiLayer.Button _refuseButton;
        readonly UiLayer.Button _policyButton;

        public ConsentView(UiLayer ui)
        {
            _ui = ui;
            _width = ui.Space.Config.Width;
            bool french = Application.systemLanguage == SystemLanguage.French;

            _panelBorder = ui.Solid(Palette.Outline);
            _panel = ui.Solid(Palette.PanelEdge);
            _panelInner = ui.Solid(Palette.Panel);
            _title = ui.Text(french ? "VIE PRIVEE" : "PRIVACY", TextAlign.Center, Palette.PanelLabel, Palette.Panel);
            var text = french ? French : English;
            _lines = new Element[text.Length];
            for (int i = 0; i < text.Length; i++) _lines[i] = ui.Text(text[i], TextAlign.Center, Palette.Outline, Palette.Panel);

            string accept = french ? "ACCEPTER" : "ACCEPT";
            string refuse = french ? "REFUSER" : "REFUSE";
            _accept = ui.Text(accept, TextAlign.Center, Color.white, Palette.Outline);
            _acceptChosen = ui.Text(VoxelFont.CheckIcon + " " + accept, TextAlign.Center, Color.white, Palette.Outline);
            _refuse = ui.Text(refuse, TextAlign.Center, Color.white, Palette.Outline);
            _refuseChosen = ui.Text(VoxelFont.CheckIcon + " " + refuse, TextAlign.Center, Color.white, Palette.Outline);
            var fill = Palette.Hex("#4EA6D8");
            _acceptButton = ui.CreateButton(null, fill, Color.white);
            _refuseButton = ui.CreateButton(null, fill, Color.white);
            _policyButton = ui.CreateButton(french ? "POLITIQUE DE CONFIDENTIALITE" : "PRIVACY POLICY", Palette.PanelEdge, Color.white);
        }

        /// <param name="choice">Choix actuel : true accepté, false refusé, null pas encore répondu.</param>
        public void Draw(bool? choice)
        {
            float left = (_width - PanelWidth) * 0.5f;
            float cx = _width * 0.5f;
            _ui.PlaceBox(_panelBorder, left - 2f, PanelTop - 2f, PanelWidth + 4f, PanelHeight + 4f, UiLayer.PanelZ + 0.03f);
            _ui.PlaceBox(_panel, left, PanelTop, PanelWidth, PanelHeight, UiLayer.PanelZ + 0.02f);
            _ui.PlaceBox(_panelInner, left + 6f, PanelTop + 6f, PanelWidth - 12f, PanelHeight - 12f, UiLayer.PanelZ);
            _ui.Place(_title, cx, PanelTop + 14f, 2.2f);
            for (int i = 0; i < _lines.Length; i++) _ui.Place(_lines[i], cx, PanelTop + 42f + i * LineHeight, 1.4f);

            var acceptLabel = choice == true ? _acceptChosen : _accept;
            var refuseLabel = choice == false ? _refuseChosen : _refuse;
            UiLayer.Hide(choice == true ? _accept : _acceptChosen);
            UiLayer.Hide(choice == false ? _refuse : _refuseChosen);
            const float buttonWidth = 108f;
            float offset = buttonWidth * 0.5f + 6f;
            _ui.PlaceButton(_acceptButton, UiAction.ConsentAccept, cx - offset, ButtonsY, buttonWidth, 28f, 1.6f, acceptLabel);
            _ui.PlaceButton(_refuseButton, UiAction.ConsentRefuse, cx + offset, ButtonsY, buttonWidth, 28f, 1.6f, refuseLabel);
            _ui.PlaceButton(_policyButton, UiAction.OpenPrivacyPolicy, cx, LinkY, PanelWidth - 36f, 22f, 1.2f);
        }

        public void Hide()
        {
            UiLayer.Hide(_panelBorder);
            UiLayer.Hide(_panel);
            UiLayer.Hide(_panelInner);
            UiLayer.Hide(_title);
            foreach (var line in _lines) UiLayer.Hide(line);
            UiLayer.Hide(_accept);
            UiLayer.Hide(_acceptChosen);
            UiLayer.Hide(_refuse);
            UiLayer.Hide(_refuseChosen);
            UiLayer.Hide(_acceptButton);
            UiLayer.Hide(_refuseButton);
            UiLayer.Hide(_policyButton);
        }
    }
}
