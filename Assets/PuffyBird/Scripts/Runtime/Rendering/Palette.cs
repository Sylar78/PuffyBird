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

        // Étoiles de vitesse et traînée multicolore.
        public static readonly Color Star = Hex("#FFD23F");
        public static readonly Color StarRim = Hex("#FFF4B8");
        public static readonly Color[] Rainbow =
        {
            Hex("#FF4D6D"), Hex("#FF9F1C"), Hex("#FFE14D"), Hex("#5BE37D"),
            Hex("#3EC6FF"), Hex("#6C7BFF"), Hex("#C86BFF"),
        };

        // Sol : brins d'herbe (niveaux de gris, teintés par le décor) et petites fleurs.
        public static readonly Color[] Flowers = { Hex("#FFFFFF"), Hex("#FFE14D"), Hex("#FF8FB1"), Hex("#B9A7FF") };
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

        /// <summary>Oiseau doré pailleté pendant l'accélération d'une étoile.</summary>
        public static readonly BirdColors GoldBird = new BirdColors
        {
            Body = Hex("#FFC21A"),
            Belly = Hex("#FFE680"),
            Shade = Hex("#E88F00"),
        };
        public static readonly Color GoldGlow = Hex("#FFB300");
        public static readonly Color Glitter = Hex("#FFF6C9");

        /// <summary>Éléments de décor propres à un thème, en plus du ciel, des collines et du sol.</summary>
        public enum SetPiece
        {
            Forest,
            City,
            Japan,
            Winter,
        }

        public enum Weather
        {
            None,
            Rain,
            Snow,
            Petals,
        }

        /// <summary>Ambiance d'un thème : ciel, brouillard, lumière, décor, météo, post-traitement.</summary>
        public struct ThemeColors
        {
            public SetPiece Set;
            public Weather Weather;
            public bool Lightning;
            public bool Fireflies;
            public Color SkyTop;
            public Color SkyHorizon;
            public Color Fog;
            public float FogStart;
            public float FogEnd;
            public Color SunColor;
            public float SunIntensity;
            public Vector3 SunEuler;
            public Color AmbientSky;
            public Color AmbientEquator;
            public Color AmbientGround;
            public Color HillFar;
            public Color HillNear;
            public Color Foliage;
            public Color Cloud;
            public float StarDensity;
            public Vector2 SunScreenPos;
            public float SunSize;
            public Color SunDisc;
            /// <summary>Teinte du gazon (la texture est en niveaux de gris).</summary>
            public Color Grass;
            public Color Dirt;
            /// <summary>Teinte appliquée aux éléments du décor (plus sombre la nuit ou sous l'orage).</summary>
            public Color SceneTint;
            public float Wind;
            public bool Flowers;
            public float BloomIntensity;
            public float BloomThreshold;
            public Color BloomTint;
            public Color ColorFilter;
            public float Vignette;
            public float Exposure;
        }

        public static ThemeColors Theme(Theme theme)
        {
            var t = new ThemeColors
            {
                Set = SetPiece.Forest,
                Weather = Weather.None,
                FogStart = 18f,
                FogEnd = 95f,
                SunSize = 0.05f,
                SceneTint = Color.white,
                Wind = 1f,
                Flowers = true,
                BloomIntensity = 0.45f,
                BloomThreshold = 1.05f,
                BloomTint = Color.white,
                ColorFilter = Hex("#FFF8EE"),
                Vignette = 0.2f,
                Exposure = 0f,
                Dirt = Color.white,
            };
            switch (theme)
            {
                case Core.Theme.Night:
                    t.Fireflies = true;
                    t.SkyTop = Hex("#06243A");
                    t.SkyHorizon = Hex("#0B6A73");
                    t.Fog = Hex("#0C4F5A");
                    t.FogStart = 12f;
                    t.FogEnd = 70f;
                    t.SunColor = Hex("#9FC7FF");
                    t.SunIntensity = 0.55f;
                    t.SunEuler = new Vector3(38f, 25f, 0f);
                    t.AmbientSky = Hex("#1D3E66");
                    t.AmbientEquator = Hex("#16404A");
                    t.AmbientGround = Hex("#10261E");
                    t.HillFar = Hex("#0F4652");
                    t.HillNear = Hex("#12574F");
                    t.Foliage = Hex("#1F7A4A");
                    t.Cloud = Hex("#5E8FA3");
                    t.StarDensity = 0.012f;
                    t.SunScreenPos = new Vector2(0.72f, 0.78f);
                    t.SunSize = 0.035f;
                    t.SunDisc = Hex("#F4F1D8");
                    t.Grass = Hex("#6FB86A");
                    t.Dirt = new Color(0.5f, 0.55f, 0.6f);
                    t.SceneTint = new Color(0.55f, 0.7f, 0.75f);
                    t.BloomIntensity = 1.1f;
                    t.BloomThreshold = 0.8f;
                    t.BloomTint = Hex("#FFE3A8");
                    t.ColorFilter = Hex("#D6E4FF");
                    t.Vignette = 0.32f;
                    t.Exposure = 0.15f;
                    break;

                // Ville au crépuscule : immeubles aux fenêtres allumées, soleil bas et orangé.
                case Core.Theme.City:
                    t.Set = SetPiece.City;
                    t.SkyTop = Hex("#33306E");
                    t.SkyHorizon = Hex("#FF9F70");
                    t.Fog = Hex("#C7879A");
                    t.FogStart = 16f;
                    t.FogEnd = 85f;
                    t.SunColor = Hex("#FFB27A");
                    t.SunIntensity = 1.05f;
                    t.SunEuler = new Vector3(20f, 40f, 0f);
                    t.AmbientSky = Hex("#7A6FB0");
                    t.AmbientEquator = Hex("#D99A8A");
                    t.AmbientGround = Hex("#5A5A55");
                    t.HillFar = Hex("#8C7AA6");
                    t.HillNear = Hex("#6E8C74");
                    t.Foliage = Hex("#5AA862");
                    t.Cloud = Hex("#FFC3B0");
                    t.StarDensity = 0.003f;
                    t.SunScreenPos = new Vector2(0.8f, 0.36f);
                    t.SunSize = 0.06f;
                    t.SunDisc = Hex("#FFD49A");
                    t.Grass = Hex("#8FCB6A");
                    t.Dirt = new Color(0.86f, 0.8f, 0.8f);
                    t.SceneTint = new Color(0.97f, 0.9f, 0.92f);
                    t.BloomIntensity = 0.9f;
                    t.BloomThreshold = 0.85f;
                    t.BloomTint = Hex("#FFD9B0");
                    t.ColorFilter = Hex("#FFEFE6");
                    t.Vignette = 0.26f;
                    break;

                // Japon médiéval au printemps : mont enneigé, pagodes, torii, cerisiers en fleurs.
                case Core.Theme.Japan:
                    t.Set = SetPiece.Japan;
                    t.Weather = Weather.Petals;
                    t.SkyTop = Hex("#76B2E6");
                    t.SkyHorizon = Hex("#FFDCE4");
                    t.Fog = Hex("#F2D8E2");
                    t.SunColor = Hex("#FFF0E2");
                    t.SunIntensity = 1.25f;
                    t.SunEuler = new Vector3(36f, -30f, 0f);
                    t.AmbientSky = Hex("#B8D0F0");
                    t.AmbientEquator = Hex("#F2D6DC");
                    t.AmbientGround = Hex("#8A9A66");
                    t.HillFar = Hex("#A9B9DA");
                    t.HillNear = Hex("#7DB07A");
                    t.Foliage = Hex("#6DBE68");
                    t.Cloud = Hex("#FFFFFF");
                    t.SunScreenPos = new Vector2(0.72f, 0.74f);
                    t.SunDisc = Hex("#FFE6E6");
                    t.Grass = Hex("#9CDB6A");
                    t.Dirt = new Color(1f, 0.94f, 0.9f);
                    t.BloomIntensity = 0.55f;
                    t.ColorFilter = Hex("#FFF4F6");
                    break;

                // Orage : ciel bas et sombre, pluie battante, éclairs, végétation qui plie.
                case Core.Theme.Storm:
                    t.Weather = Weather.Rain;
                    t.Lightning = true;
                    t.SkyTop = Hex("#232C38");
                    t.SkyHorizon = Hex("#5B6876");
                    t.Fog = Hex("#505B67");
                    t.FogStart = 8f;
                    t.FogEnd = 55f;
                    t.SunColor = Hex("#B8C4D6");
                    t.SunIntensity = 0.6f;
                    t.SunEuler = new Vector3(55f, 20f, 0f);
                    t.AmbientSky = Hex("#4E5A6A");
                    t.AmbientEquator = Hex("#48535E");
                    t.AmbientGround = Hex("#2E3A2C");
                    t.HillFar = Hex("#3C4850");
                    t.HillNear = Hex("#33503B");
                    t.Foliage = Hex("#3C7A46");
                    t.Cloud = Hex("#6A7480");
                    // Pas de soleil visible : disque noir hors de l'écran (taille non nulle pour le shader).
                    t.SunScreenPos = new Vector2(-1f, -1f);
                    t.SunSize = 0.02f;
                    t.SunDisc = Color.black;
                    t.Grass = Hex("#5E9A5A");
                    t.Dirt = new Color(0.55f, 0.55f, 0.56f);
                    t.SceneTint = new Color(0.68f, 0.74f, 0.78f);
                    t.Wind = 2.6f;
                    t.Flowers = false;
                    t.BloomIntensity = 0.7f;
                    t.BloomThreshold = 0.9f;
                    t.BloomTint = Hex("#D8E4FF");
                    t.ColorFilter = Hex("#DDE6F0");
                    t.Vignette = 0.34f;
                    t.Exposure = 0.1f;
                    break;

                // Neige : sapins enneigés, sol blanc, flocons.
                case Core.Theme.Snow:
                    t.Set = SetPiece.Winter;
                    t.Weather = Weather.Snow;
                    t.SkyTop = Hex("#86ADD4");
                    t.SkyHorizon = Hex("#E6EFF6");
                    t.Fog = Hex("#DCE6EF");
                    t.FogStart = 14f;
                    t.FogEnd = 80f;
                    t.SunColor = Hex("#FFF6EA");
                    t.SunIntensity = 1.1f;
                    t.SunEuler = new Vector3(30f, 35f, 0f);
                    t.AmbientSky = Hex("#C4D8EC");
                    t.AmbientEquator = Hex("#DCE6EE");
                    t.AmbientGround = Hex("#C8D4DE");
                    t.HillFar = Hex("#D2E0EE");
                    t.HillNear = Hex("#EEF4F8");
                    t.Foliage = Hex("#E6EEF4");
                    t.Cloud = Hex("#FFFFFF");
                    t.SunScreenPos = new Vector2(0.3f, 0.76f);
                    t.SunDisc = Hex("#FFFBEA");
                    t.Grass = Hex("#F4F8FC");
                    t.Dirt = new Color(0.78f, 0.84f, 0.95f);
                    t.Flowers = false;
                    t.BloomIntensity = 0.4f;
                    t.BloomThreshold = 1.15f;
                    t.ColorFilter = Hex("#F2F7FF");
                    t.Vignette = 0.18f;
                    break;

                default:
                    t.SkyTop = Hex("#2E9BD6");
                    t.SkyHorizon = Hex("#A8E6E0");
                    t.Fog = Hex("#B7E7E2");
                    t.SunColor = Hex("#FFF1D6");
                    t.SunIntensity = 1.35f;
                    t.SunEuler = new Vector3(42f, 32f, 0f);
                    t.AmbientSky = Hex("#8CCBEA");
                    t.AmbientEquator = Hex("#BFE6D8");
                    t.AmbientGround = Hex("#7E9A55");
                    t.HillFar = Hex("#7FC8B8");
                    t.HillNear = Hex("#5FB77A");
                    t.Foliage = Hex("#5EE270");
                    t.Cloud = Hex("#FFFFFF");
                    t.SunScreenPos = new Vector2(0.25f, 0.8f);
                    t.SunDisc = Hex("#FFF6C8");
                    t.Grass = Hex("#A3E85E");
                    break;
            }
            return t;
        }
    }
}
