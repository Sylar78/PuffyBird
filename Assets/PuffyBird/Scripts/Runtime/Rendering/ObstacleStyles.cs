using PuffyBird.Core;
using UnityEngine;

namespace PuffyBird.Rendering
{
    /// <summary>
    /// Obstacles propres à chaque décor : pierre taillée, obélisque de lune, tour néon, pilier laqué,
    /// paratonnerre, pilier de glace, tronc moussu, colonne de marbre, corail. Seule l'apparence change :
    /// fût de 48 px et chapeau de 52 px sur 24 px, comme les rectangles de collision (§7.1, §9).
    /// </summary>
    public static class ObstacleStyles
    {
        /// <summary>Couleur d'une couche du chapeau, prise dans la palette du style (ou dans la palette contrastée).</summary>
        public enum Role : byte
        {
            Body,
            Shade,
            Rim,
            /// <summary>Lumineuse (émission des couleurs de sommet), gardée aussi en mode contrasté.</summary>
            Glow,
        }

        /// <summary>
        /// Une couche du chapeau : de <see cref="From"/> à <see cref="From"/> + <see cref="Height"/> (fractions
        /// de la hauteur, 0 côté fût, 1 côté ouverture), rayons en fraction du rayon du chapeau.
        /// </summary>
        public struct Layer
        {
            public float From;
            public float Height;
            public float Bottom;
            public float Top;
            public Role Role;

            public Layer(float from, float height, float bottom, float top, Role role)
            {
                From = from;
                Height = height;
                Bottom = bottom;
                Top = top;
                Role = role;
            }
        }

        public sealed class Style
        {
            /// <summary>Nombre de faces du fût et du chapeau (prisme à arêtes nettes).</summary>
            public int Sides;
            public Color Body;
            public Color Shade;
            public Color Rim;
            public Color Glow;
            public Layer[] Cap;
        }

        /// <summary>Couleur sans lueur : l'alpha des couleurs de sommet masque l'émission du matériau.</summary>
        static Color Matte(string hex) => Lit(hex, 0f);

        static Color Lit(string hex, float glow)
        {
            var c = Palette.Hex(hex);
            c.a = glow;
            return c;
        }

        static Layer L(float from, float height, float bottom, float top, Role role) => new Layer(from, height, bottom, top, role);

        public static Style For(Theme theme)
        {
            switch (theme)
            {
                case Theme.Night:
                    // Obélisque de lune : pierre bleu nuit, bandeau de runes lumineuses.
                    return new Style
                    {
                        Sides = 4, Body = Matte("#33406E"), Shade = Matte("#222B4F"), Rim = Matte("#5A6BA8"), Glow = Lit("#7FE3FF", 1f),
                        Cap = new[] { L(0f, 0.2f, 0.92f, 1f, Role.Shade), L(0.2f, 0.25f, 1f, 1f, Role.Body), L(0.45f, 0.15f, 1.02f, 1.02f, Role.Glow), L(0.6f, 0.4f, 1f, 1f, Role.Rim) },
                    };
                case Theme.City:
                    // Tour néon : métal sombre, deux anneaux roses lumineux.
                    return new Style
                    {
                        Sides = 6, Body = Matte("#3B4150"), Shade = Matte("#262A33"), Rim = Matte("#5C6475"), Glow = Lit("#FF4FD8", 1f),
                        Cap = new[] { L(0f, 0.18f, 0.92f, 1f, Role.Shade), L(0.18f, 0.14f, 1.02f, 1.02f, Role.Glow), L(0.32f, 0.36f, 1f, 1f, Role.Body), L(0.68f, 0.12f, 1.02f, 1.02f, Role.Glow), L(0.8f, 0.2f, 1f, 1f, Role.Rim) },
                    };
                case Theme.Japan:
                    // Pilier laqué vermillon, bague dorée et poutre noire, à la manière d'un torii.
                    return new Style
                    {
                        Sides = 20, Body = Matte("#D2412C"), Shade = Matte("#1E1717"), Rim = Matte("#E8B04A"), Glow = Lit("#FFD27A", 0.6f),
                        Cap = new[] { L(0f, 0.12f, 1f, 1f, Role.Rim), L(0.12f, 0.33f, 0.98f, 0.98f, Role.Body), L(0.45f, 0.55f, 1.03f, 1.03f, Role.Shade) },
                    };
                case Theme.Storm:
                    // Paratonnerre : fer sombre, bagues de cuivre, bord chargé d'électricité.
                    return new Style
                    {
                        Sides = 8, Body = Matte("#4A5059"), Shade = Matte("#2E3238"), Rim = Matte("#C9773A"), Glow = Lit("#8FD8FF", 1f),
                        Cap = new[] { L(0f, 0.15f, 0.95f, 1f, Role.Rim), L(0.15f, 0.2f, 1f, 1f, Role.Body), L(0.35f, 0.15f, 1.02f, 1.02f, Role.Rim), L(0.5f, 0.3f, 1f, 1f, Role.Shade), L(0.8f, 0.2f, 1f, 1f, Role.Glow) },
                    };
                case Theme.Snow:
                    // Pilier de glace : cristal hexagonal, givre clair et lueur froide au bord.
                    return new Style
                    {
                        Sides = 6, Body = Lit("#BFE6F7", 0.12f), Shade = Matte("#8CCBE8"), Rim = Matte("#F4FCFF"), Glow = Lit("#DFF6FF", 0.6f),
                        Cap = new[] { L(0f, 0.3f, 0.9f, 1.03f, Role.Shade), L(0.3f, 0.4f, 1.03f, 1.03f, Role.Rim), L(0.7f, 0.3f, 1.03f, 1f, Role.Glow) },
                    };
                case Theme.Jungle:
                    // Tronc moussu : écorce brune, anneaux de mousse.
                    return new Style
                    {
                        Sides = 11, Body = Matte("#7A5233"), Shade = Matte("#553620"), Rim = Matte("#5E9E35"), Glow = Lit("#B8F06A", 0.5f),
                        Cap = new[] { L(0f, 0.2f, 0.95f, 1f, Role.Shade), L(0.2f, 0.3f, 1.03f, 1.03f, Role.Rim), L(0.5f, 0.2f, 1f, 1f, Role.Body), L(0.7f, 0.3f, 1.03f, 1.03f, Role.Rim) },
                    };
                case Theme.Sky:
                    // Colonne de marbre cannelée, chapiteau à bague d'or.
                    return new Style
                    {
                        Sides = 16, Body = Matte("#ECEEF4"), Shade = Matte("#C9CDD8"), Rim = Matte("#E8C25A"), Glow = Lit("#FFF1B8", 0.6f),
                        Cap = new[] { L(0f, 0.15f, 0.9f, 1f, Role.Shade), L(0.15f, 0.15f, 1.03f, 1.03f, Role.Rim), L(0.3f, 0.3f, 1f, 1f, Role.Body), L(0.6f, 0.4f, 1.03f, 1f, Role.Shade) },
                    };
                case Theme.Ocean:
                    // Corail : rose orangé, bord de polypes qui luisent doucement.
                    return new Style
                    {
                        Sides = 9, Body = Matte("#F27466"), Shade = Matte("#C24F45"), Rim = Matte("#FFB7A6"), Glow = Lit("#FFD9CC", 0.5f),
                        Cap = new[] { L(0f, 0.3f, 0.9f, 1.03f, Role.Shade), L(0.3f, 0.4f, 1.03f, 1.03f, Role.Body), L(0.7f, 0.3f, 1.03f, 1f, Role.Glow) },
                    };
                default:
                    // Prairie : pierre de grès taillée, chapiteau ceint de mousse.
                    return new Style
                    {
                        Sides = 8, Body = Matte("#D8C49A"), Shade = Matte("#A88E62"), Rim = Matte("#79B04A"), Glow = Lit("#E9F7A0", 0.5f),
                        Cap = new[] { L(0f, 0.2f, 0.92f, 1f, Role.Shade), L(0.2f, 0.25f, 1f, 1f, Role.Body), L(0.45f, 0.15f, 1.03f, 1.03f, Role.Rim), L(0.6f, 0.4f, 1f, 1f, Role.Body) },
                    };
            }
        }

        /// <summary>Couleur d'une couche ; en mode contrasté, la palette orangée (§ accessibilité), lueurs comprises.</summary>
        public static Color ColorOf(Style style, Role role, bool contrast)
        {
            if (contrast)
            {
                switch (role)
                {
                    case Role.Shade: return NoGlow(Palette.PipeShadeContrast);
                    case Role.Rim: return NoGlow(Palette.PipeRimContrast);
                    case Role.Glow: return style.Glow;
                    default: return NoGlow(Palette.PipeBodyContrast);
                }
            }
            switch (role)
            {
                case Role.Shade: return style.Shade;
                case Role.Rim: return style.Rim;
                case Role.Glow: return style.Glow;
                default: return style.Body;
            }
        }

        static Color NoGlow(Color c) => new Color(c.r, c.g, c.b, 0f);
    }
}
