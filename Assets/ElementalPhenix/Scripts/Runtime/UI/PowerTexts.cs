using ElementalPhenix.Core;
using ElementalPhenix.Rendering;
using UnityEngine;

namespace ElementalPhenix.UI
{
    /// <summary>
    /// Textes des pouvoirs d'élément, dans les cinq langues : nom du pouvoir (annonce en jeu, jauge
    /// pleine) et ce qu'il fait (menu PHÉNIX). Lus au chargement seulement.
    /// </summary>
    public static class PowerTexts
    {
        /// <summary>Nom du pouvoir, annoncé quand il se déclenche.</summary>
        public static string Name(PhoenixElement element)
        {
            switch (element)
            {
                case PhoenixElement.Fire: return Lang.T("EMBRASEMENT !", "BLAZE!", "¡LLAMARADA!", "FEUERSBRUNST!", "LABAREDA!");
                case PhoenixElement.Water: return Lang.T("MARÉE LENTE !", "SLOW TIDE!", "¡MAREA LENTA!", "TRÄGE FLUT!", "MARÉ LENTA!");
                case PhoenixElement.Wind: return Lang.T("COURANT PORTEUR !", "TAILWIND!", "¡VIENTO A FAVOR!", "RÜCKENWIND!", "VENTO A FAVOR!");
                case PhoenixElement.Light: return Lang.T("RÉVÉLATION !", "REVELATION!", "¡REVELACIÓN!", "OFFENBARUNG!", "REVELAÇÃO!");
                case PhoenixElement.Shadow: return Lang.T("PAS DE L'OMBRE !", "SHADOW STEP!", "¡PASO SOMBRÍO!", "SCHATTENSCHRITT!", "PASSO SOMBRIO!");
                default: return "";
            }
        }

        /// <summary>Ce que fait le pouvoir, en quelques mots (menu PHÉNIX).</summary>
        public static string Description(PhoenixElement element)
        {
            switch (element)
            {
                case PhoenixElement.Fire: return Lang.T("POUVOIR : BRÛLE UN OBSTACLE", "POWER: BURNS AN OBSTACLE", "PODER: QUEMA UN OBSTÁCULO", "KRAFT: VERBRENNT EIN HINDERNIS", "PODER: QUEIMA UM OBSTÁCULO");
                case PhoenixElement.Water: return Lang.T("POUVOIR : RALENTIT LE TEMPS", "POWER: SLOWS TIME", "PODER: RALENTIZA EL TIEMPO", "KRAFT: VERLANGSAMT DIE ZEIT", "PODER: DESACELERA O TEMPO");
                case PhoenixElement.Wind: return Lang.T("POUVOIR : PORTE VERS LE PASSAGE", "POWER: CARRIES YOU THROUGH", "PODER: TE LLEVA AL PASO", "KRAFT: TRÄGT DICH HINDURCH", "PODER: LEVA À PASSAGEM");
                case PhoenixElement.Light: return Lang.T("POUVOIR : ÉLARGIT LES PASSAGES", "POWER: WIDENS THE GAPS", "PODER: ENSANCHA LOS PASOS", "KRAFT: WEITET DIE LÜCKEN", "PODER: ALARGA AS PASSAGENS");
                case PhoenixElement.Shadow: return Lang.T("POUVOIR : TRAVERSE PAR L'OMBRE", "POWER: PHASES THROUGH", "PODER: CRUZA EN SOMBRA", "KRAFT: GLEITET DURCH SCHATTEN", "PODER: ATRAVESSA NA SOMBRA");
                default: return "";
            }
        }

        /// <summary>Couleur du pouvoir : jauge, annonce et effets.</summary>
        public static Color Tint(PhoenixElement element)
        {
            switch (element)
            {
                case PhoenixElement.Fire: return Palette.Hex("#FF7A1A");
                case PhoenixElement.Water: return Palette.Hex("#5CC8FF");
                case PhoenixElement.Wind: return Palette.Hex("#7CF0B4");
                case PhoenixElement.Light: return Palette.Hex("#FFE38A");
                case PhoenixElement.Shadow: return Palette.Hex("#B07CFF");
                default: return Color.white;
            }
        }
    }
}
