using PuffyBird.Core;
using UnityEngine;

namespace PuffyBird.Rendering
{
    /// <summary>Couleurs du jeu (§14.2), adaptées au rendu éclairé.</summary>
    public static class Palette
    {
        public static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
        }

        public struct BirdColors
        {
            public Color Body;
            public Color Belly;
            public Color Shade;
        }

        public static BirdColors Bird(BirdColor color)
        {
            switch (color)
            {
                case BirdColor.Red:
                    return new BirdColors { Body = Hex("#E5452C"), Belly = Hex("#F38A6A"), Shade = Hex("#B0271B") };
                case BirdColor.Blue:
                    return new BirdColors { Body = Hex("#4EA6D8"), Belly = Hex("#9BD4F0"), Shade = Hex("#2C6FA0") };
                default:
                    return new BirdColors { Body = Hex("#F8C82A"), Belly = Hex("#FBE88D"), Shade = Hex("#E08A1E") };
            }
        }

        public static readonly Color Beak = Hex("#F75A3B");
        public static readonly Color Outline = Hex("#543847");
        public static readonly Color White = Color.white;
        public static readonly Color Pupil = Hex("#1A1016");

        // Tuyaux : céramique jade à bague dorée, volontairement différents des tuyaux verts de l'original (§22).
        public static readonly Color PipeBody = Hex("#3FB6A8");
        public static readonly Color PipeShade = Hex("#2B8C82");
        public static readonly Color PipeRim = Hex("#F2C35B");

        public static readonly Color GrassLight = Hex("#9CE659");
        public static readonly Color GrassDark = Hex("#73BF2E");
        public static readonly Color Sand = Hex("#DED895");
        public static readonly Color SandEdge = Hex("#D7A84C");

        public static readonly Color GetReady = Hex("#8EE05A");
        public static readonly Color GameOver = Hex("#FC8D4B");
        public static readonly Color PanelLabel = Hex("#F37C47");
        public static readonly Color NewTag = Hex("#E23E1B");
        public static readonly Color Panel = Hex("#DED895");
        public static readonly Color PanelEdge = Hex("#D7A84C");
        public static readonly Color MedalEmpty = Hex("#E5DFA0");

        public static Color MedalColor(Medal medal)
        {
            switch (medal)
            {
                case Medal.Bronze: return Hex("#D0874B");
                case Medal.Silver: return Hex("#C7C7C7");
                case Medal.Gold: return Hex("#F3C22B");
                case Medal.Platinum: return Hex("#E8F4F8");
                default: return MedalEmpty;
            }
        }

        /// <summary>Ambiance d'un thème : ciel, brouillard, lumière principale.</summary>
        public struct ThemeColors
        {
            public Color SkyTop;
            public Color SkyHorizon;
            public Color Fog;
            public Color SunColor;
            public float SunIntensity;
            public Vector3 SunEuler;
            public Color AmbientSky;
            public Color AmbientEquator;
            public Color AmbientGround;
            public Color HillFar;
            public Color HillNear;
            public Color Foliage;
            public Color FoliageDark;
            public Color Trunk;
            public Color Cloud;
            public float StarDensity;
            public Vector2 SunScreenPos;
            public Color SunDisc;
        }

        public static ThemeColors Theme(Theme theme)
        {
            if (theme == Core.Theme.Night)
            {
                return new ThemeColors
                {
                    SkyTop = Hex("#06243A"),
                    SkyHorizon = Hex("#0B6A73"),
                    Fog = Hex("#0C4F5A"),
                    SunColor = Hex("#9FC7FF"),
                    SunIntensity = 0.55f,
                    SunEuler = new Vector3(38f, 25f, 0f),
                    AmbientSky = Hex("#1D3E66"),
                    AmbientEquator = Hex("#16404A"),
                    AmbientGround = Hex("#10261E"),
                    HillFar = Hex("#0F4652"),
                    HillNear = Hex("#12574F"),
                    Foliage = Hex("#1F7A4A"),
                    FoliageDark = Hex("#155C38"),
                    Trunk = Hex("#4A3040"),
                    Cloud = Hex("#5E8FA3"),
                    StarDensity = 0.012f,
                    SunScreenPos = new Vector2(0.72f, 0.78f),
                    SunDisc = Hex("#F4F1D8"),
                };
            }
            return new ThemeColors
            {
                SkyTop = Hex("#2E9BD6"),
                SkyHorizon = Hex("#A8E6E0"),
                Fog = Hex("#B7E7E2"),
                SunColor = Hex("#FFF1D6"),
                SunIntensity = 1.35f,
                SunEuler = new Vector3(42f, 32f, 0f),
                AmbientSky = Hex("#8CCBEA"),
                AmbientEquator = Hex("#BFE6D8"),
                AmbientGround = Hex("#7E9A55"),
                HillFar = Hex("#7FC8B8"),
                HillNear = Hex("#5FB77A"),
                Foliage = Hex("#5EE270"),
                FoliageDark = Hex("#4BC45A"),
                Trunk = Hex("#8A5A44"),
                Cloud = Hex("#FFFFFF"),
                StarDensity = 0f,
                SunScreenPos = new Vector2(0.25f, 0.8f),
                SunDisc = Hex("#FFF6C8"),
            };
        }
    }
}
