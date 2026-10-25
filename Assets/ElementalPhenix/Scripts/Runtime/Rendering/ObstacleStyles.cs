using ElementalPhenix.Core;
using UnityEngine;

namespace ElementalPhenix.Rendering
{
    /// <summary>
    /// Couleurs des obstacles de chaque décor (formes : <see cref="ObstacleMeshes"/>) : rochers de grès,
    /// cristaux de lune, tour néon, pagode, basalte foudroyé, glace, tronc à lianes, colonne dans les
    /// nuages, récif de corail. Seule l'apparence change, jamais les rectangles de collision (§7.1, §9).
    /// </summary>
    public static class ObstacleStyles
    {
        /// <summary>Rôle d'une couleur, pris dans la palette du style (ou dans la palette contrastée).</summary>
        public enum Role : byte
        {
            Body,
            Shade,
            Rim,
            /// <summary>Lumineuse (émission des couleurs de sommet), gardée aussi en mode contrasté.</summary>
            Glow,
            /// <summary>Détail propre au décor (mousse, bois coupé, nuage, algue).</summary>
            Accent,
        }

        public sealed class Style
        {
            public Color Body;
            public Color Shade;
            public Color Rim;
            public Color Glow;
            public Color Accent;
        }

        /// <summary>Couleur sans lueur : l'alpha des couleurs de sommet masque l'émission du matériau.</summary>
        static Color Matte(string hex) => Lit(hex, 0f);

        static Color Lit(string hex, float glow)
        {
            var c = Palette.Hex(hex);
            c.a = glow;
            return c;
        }

        public static Style For(Theme theme)
        {
            switch (theme)
            {
                case Theme.Night:
                    // Cristaux de lune : pierre bleu nuit, cristaux clairs, veines de runes lumineuses.
                    return new Style { Body = Matte("#2E3A66"), Shade = Matte("#1E2647"), Rim = Lit("#9FB4F2", 0.25f), Glow = Lit("#7FE3FF", 1f), Accent = Lit("#C9D6FF", 0.4f) };
                case Theme.City:
                    // Tour néon : béton sombre, corniches de métal, fenêtres et enseignes roses et cyan.
                    return new Style { Body = Matte("#3B4150"), Shade = Matte("#22262F"), Rim = Matte("#6A7385"), Glow = Lit("#FF4FD8", 1f), Accent = Lit("#5FE8FF", 1f) };
                case Theme.Japan:
                    // Pagode : murs vermillon, toits sombres, liserés d'or, lanternes.
                    return new Style { Body = Matte("#C8392A"), Shade = Matte("#221A1C"), Rim = Matte("#E8B04A"), Glow = Lit("#FFC46A", 0.9f), Accent = Matte("#F2E6CF") };
                case Theme.Storm:
                    // Orgues de basalte : roche noire, bagues de cuivre, fissures chargées d'éclairs.
                    return new Style { Body = Matte("#3E434B"), Shade = Matte("#272A30"), Rim = Matte("#C9773A"), Glow = Lit("#8FD8FF", 1f), Accent = Matte("#565C66") };
                case Theme.Snow:
                    // Glace : cristal bleuté, éclats de givre, lueur froide.
                    return new Style { Body = Lit("#A9DCF2", 0.12f), Shade = Matte("#7FBEDD"), Rim = Lit("#F4FCFF", 0.2f), Glow = Lit("#DFF6FF", 0.6f), Accent = Lit("#CFF0FF", 0.3f) };
                case Theme.Jungle:
                    // Tronc géant : écorce brune, lianes, feuilles, bois clair de la coupe.
                    return new Style { Body = Matte("#6E4A2E"), Shade = Matte("#4A301C"), Rim = Matte("#4F8E2E"), Glow = Lit("#B8F06A", 0.45f), Accent = Matte("#D8B07A") };
                case Theme.Sky:
                    // Colonne de marbre prise dans les nuages : cannelures, chapiteau, bagues d'or.
                    return new Style { Body = Matte("#ECEEF4"), Shade = Matte("#BFC4D2"), Rim = Matte("#E8C25A"), Glow = Lit("#FFF1B8", 0.6f), Accent = Lit("#FFFFFF", 0.2f) };
                case Theme.Ocean:
                    // Récif : corail rose orangé, branches, polypes lumineux, varech.
                    return new Style { Body = Matte("#E86A5E"), Shade = Matte("#B44A43"), Rim = Matte("#FFB7A6"), Glow = Lit("#FFE0D2", 0.6f), Accent = Matte("#3E8F6A") };
                default:
                    // Prairie : rochers de grès empilés, mousse, racines.
                    return new Style { Body = Matte("#CDB78C"), Shade = Matte("#9C8259"), Rim = Matte("#6FA343"), Glow = Lit("#E9F7A0", 0.4f), Accent = Matte("#5A3E24") };
            }
        }

        /// <summary>Couleur d'un rôle ; en mode contrasté, la palette orangée (§ accessibilité), lueurs comprises.</summary>
        public static Color ColorOf(Style style, Role role, bool contrast)
        {
            if (contrast)
            {
                switch (role)
                {
                    case Role.Shade: return NoGlow(Palette.PipeShadeContrast);
                    case Role.Rim:
                    case Role.Accent: return NoGlow(Palette.PipeRimContrast);
                    case Role.Glow: return style.Glow;
                    default: return NoGlow(Palette.PipeBodyContrast);
                }
            }
            switch (role)
            {
                case Role.Shade: return style.Shade;
                case Role.Rim: return style.Rim;
                case Role.Glow: return style.Glow;
                case Role.Accent: return style.Accent;
                default: return style.Body;
            }
        }

        static Color NoGlow(Color c) => new Color(c.r, c.g, c.b, 0f);
    }
}
