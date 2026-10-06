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
        // Tuyaux contrastés (accessibilité) : orangé vif (vermillon, la teinte que distinguent le mieux les daltoniens) et bague claire.
        public static readonly Color PipeBodyContrast = Hex("#E8650A");
        public static readonly Color PipeShadeContrast = Hex("#9A3F00");
        public static readonly Color PipeRimContrast = Hex("#FFF4C8");

        // Étoiles de vitesse ; arc-en-ciel du décor.
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

        /// <summary>Accessoire modelé sur le corps d'un oiseau (<see cref="Skins"/>).</summary>
        public enum Accessory
        {
            None,
            Sunglasses,
            Crown,
            Headband,
            Antenna,
            FlameCrest,
        }

        /// <summary>Silhouette d'un oiseau : boule dodue, ou phénix (cou, grandes ailes, longue queue).</summary>
        public enum BodyShape
        {
            Puffy,
            Phoenix,
        }

        /// <summary>Apparence d'un oiseau du catalogue : couleurs, accessoire et finition du plumage.</summary>
        public struct SkinLook
        {
            public BirdColors Colors;
            public Accessory Accessory;
            public float Smoothness;
            public float Metallic;
            public float Glitter;
            public Color Emission;
            public Color Rim;
            public float RimStrength;
            public BodyShape Shape;
            /// <summary>Lueur des parties dont la couleur de sommet a un alpha non nul (plumes de feu du phénix).</summary>
            public float VertexEmission;
            /// <summary>Angle moyen des ailes (degrés, positif = levées) et amplitude du battement.</summary>
            public float WingLift;
            public float WingAmplitude;
            /// <summary>Facteur de flexion du bout des ailes (1 = oiseau dodu).</summary>
            public float WingBend;
            /// <summary>Couleur des plumes qui volent à l'impact.</summary>
            public Color Feather;
            /// <summary>Plumage si <see cref="Shape"/> vaut <see cref="BodyShape.Phoenix"/>.</summary>
            public PhoenixColors Phoenix;
        }

        static SkinLook Look(string body, string belly, string shade, Accessory accessory = Accessory.None)
        {
            return new SkinLook
            {
                Colors = new BirdColors { Body = Hex(body), Belly = Hex(belly), Shade = Hex(shade) },
                Accessory = accessory,
                Smoothness = 0.45f,
                Metallic = 0f,
                Glitter = 0f,
                Emission = Color.black,
                Rim = Color.white,
                RimStrength = 0.5f,
                Shape = BodyShape.Puffy,
                VertexEmission = 0f,
                WingLift = 0f,
                WingAmplitude = 45f,
                WingBend = 1f,
                Feather = Hex(body),
            };
        }

        /// <summary>
        /// Plumage d'un phénix en armure : plumes sombres, armure et liseré, flammes (deux teintes
        /// et pointe), gemmes, rubans de la tête et bec de métal. L'alpha règle la lueur de chaque
        /// partie (<see cref="SkinLook.VertexEmission"/>) : l'armure ne brille pas, les flammes oui.
        /// </summary>
        static PhoenixColors Plumage(string body, string plume, string armor, string trim, string flameA, string flameB,
            string flameTip, string gem, string gemCore, string ribbon)
        {
            return new PhoenixColors
            {
                Body = Glow(body, 0f),
                Plume = Glow(plume, 0.55f),
                Armor = Glow(armor, 0f),
                ArmorTrim = Glow(trim, 0.15f),
                FlameA = Glow(flameA, 0.9f),
                FlameB = Glow(flameB, 0.9f),
                FlameTip = Glow(flameTip, 1f),
                Gem = Glow(gem, 1f),
                GemCore = Glow(gemCore, 0.7f),
                Ribbon = Glow(ribbon, 0.85f),
            };
        }

        static SkinLook PhoenixLook(PhoenixColors plumage, string rim, string feather)
        {
            var look = Look("#000000", "#000000", "#000000");
            look.Colors = new BirdColors { Body = plumage.Body, Belly = plumage.Plume, Shade = plumage.Armor };
            look.Shape = BodyShape.Phoenix;
            look.Phoenix = plumage;
            look.Emission = (Color)plumage.FlameA * 0.06f;
            look.VertexEmission = 1.4f;
            look.Smoothness = 0.78f;
            look.Metallic = 0.35f;
            look.Glitter = 0.25f;
            look.Rim = Hex(rim);
            look.RimStrength = 0.9f;
            look.WingLift = 28f;
            look.WingAmplitude = 50f;
            look.WingBend = 0.35f;
            look.Feather = Hex(feather);
            return look;
        }

        /// <summary>
        /// Apparence de l'oiseau <paramref name="id"/> (identifiants de <see cref="Skins"/>) : cinq
        /// phénix en armure, un par élément. Les identifiants d'origine sont gardés pour les choix
        /// enregistrés et le produit des boutiques.
        /// </summary>
        public static SkinLook Skin(string id)
        {
            switch (id)
            {
                case "blue": // Eau : armure argent, flammes bleues, gemmes azur.
                    return PhoenixLook(Plumage("#0A1638", "#2E6BFF", "#B9C3D2", "#EEF3FA", "#1F5BFF", "#4FB4FF",
                        "#BFEFFF", "#3FA9FF", "#E6F7FF", "#3A3AD8"), "#7FD4FF", "#2E7BFF");
                case "mint": // Vent : armure argent doré, flammes or et ambre, gemmes vertes.
                    return PhoenixLook(Plumage("#1A1006", "#E89A1A", "#C9C2A8", "#F2EBD0", "#FF9F1A", "#FFC83A",
                        "#FFF2A0", "#3FCF4F", "#D8FFC8", "#FFD23F"), "#FFD23F", "#FFB02E");
                case "streak": // Lumière : armure d'or, plumes blanches, voiles nacrés, gemmes turquoise.
                {
                    var look = PhoenixLook(Plumage("#E8E6F0", "#FFFFFF", "#D9A441", "#F7E3A0", "#FFFFFF", "#CFFFF4",
                        "#FFF6C8", "#33E0D0", "#E6FFFB", "#7FF0E0"), "#CFFFF4", "#FFFFFF");
                    look.Glitter = 0.6f;
                    look.VertexEmission = 1f;
                    return look;
                }
                case "ninja": // Ténèbres : armure acier sombre, flammes violettes, gemmes roses.
                {
                    var look = PhoenixLook(Plumage("#0C0612", "#7A2BD8", "#3A3946", "#B8B8C8", "#8A2BFF", "#C040FF",
                        "#F08CFF", "#FF4FD8", "#FFD0F4", "#E040C8"), "#C86BFF", "#8A2BFF");
                    look.Emission = Hex("#5A1A9A") * 0.12f;
                    return look;
                }
                default: // Feu (phénix de départ) : armure d'or, flammes rouges et orangées, gemmes rubis.
                    return PhoenixLook(Fire, "#FF9A3D", "#FF5A1A");
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

        /// <summary>Couleurs d'un phénix en armure ; l'alpha règle la lueur de chaque partie (<see cref="SkinLook.VertexEmission"/>).</summary>
        public struct PhoenixColors
        {
            /// <summary>Base sombre des plumes et du corps.</summary>
            public Color Body;
            /// <summary>Plumage lumineux du cou, du ventre et des voiles d'ailes.</summary>
            public Color Plume;
            public Color Armor;
            public Color ArmorTrim;
            public Color FlameA;
            public Color FlameB;
            public Color FlameTip;
            public Color Gem;
            public Color GemCore;
            /// <summary>Rubans de la tête et longue plume de la queue.</summary>
            public Color Ribbon;
        }

        static Color Glow(string hex, float glow)
        {
            var c = Hex(hex);
            c.a = glow;
            return c;
        }

        /// <summary>Phénix de feu, l'oiseau de départ.</summary>
        public static readonly PhoenixColors Fire = Plumage("#2A0604", "#FF4A12", "#E0A030", "#FFE0A0", "#FF3A0A",
            "#FF8A1A", "#FFD23F", "#FF2A2A", "#FFD0C0", "#FFB02E");

        /// <summary>
        /// Plumage d'un phénix pendant l'accélération d'une étoile : ses propres couleurs, éclaircies et
        /// plus lumineuses (corps sombre relevé vers son plumage, flammes et gemmes vers le blanc),
        /// au lieu d'un doré commun à tous les oiseaux.
        /// </summary>
        public static PhoenixColors Radiant(PhoenixColors c)
        {
            return new PhoenixColors
            {
                Body = Brighten(Color.Lerp(c.Body, c.Plume, 0.6f), 0.15f, 0.45f),
                Plume = Brighten(c.Plume, 0.3f, 0.9f),
                Armor = Brighten(c.Armor, 0.2f, 0.25f),
                ArmorTrim = Brighten(c.ArmorTrim, 0.35f, 0.6f),
                FlameA = Brighten(c.FlameA, 0.25f, 1f),
                FlameB = Brighten(c.FlameB, 0.3f, 1f),
                FlameTip = Brighten(c.FlameTip, 0.5f, 1f),
                Gem = Brighten(c.Gem, 0.3f, 1f),
                GemCore = Brighten(c.GemCore, 0.5f, 1f),
                Ribbon = Brighten(c.Ribbon, 0.3f, 1f),
            };
        }

        /// <summary>Lueur qui pulse autour du phénix sous étoile : la couleur de ses flammes.</summary>
        public static Color BoostGlow(PhoenixColors c) => Opaque(Color.Lerp(c.FlameA, c.FlameB, 0.5f));

        /// <summary>Paillettes et éclats du phénix sous étoile : la pointe de ses flammes, éclaircie.</summary>
        public static Color BoostGlitter(PhoenixColors c) => Opaque(Color.Lerp(c.FlameTip, Color.white, 0.35f));

        static Color Brighten(Color c, float toWhite, float glow)
        {
            var r = Color.Lerp(c, Color.white, toWhite);
            r.a = Mathf.Max(c.a, glow);
            return r;
        }

        /// <summary>
        /// Couleurs de la traînée de l'étoile pour un phénix : ses flammes, ses gemmes et ses rubans,
        /// pour qu'elle prolonge le plumage au lieu d'un arc-en-ciel.
        /// </summary>
        public static Color[] TrailColors(PhoenixColors c)
        {
            return new[] { Opaque(c.FlameA), Opaque(c.FlameB), Opaque(c.FlameTip), Opaque(c.Gem), Opaque(c.Ribbon), Opaque(c.Plume) };
        }

        static Color Opaque(Color c) => new Color(c.r, c.g, c.b, 1f);

        public static readonly Color Glitter = Hex("#FFF6C9");

        /// <summary>Éléments de décor propres à un thème, en plus du ciel, des collines et du sol.</summary>
        public enum SetPiece
        {
            Forest,
            City,
            Japan,
            Winter,
            Jungle,
            Sky,
            Ocean,
        }

        public enum Weather
        {
            None,
            Rain,
            Snow,
            Petals,
            /// <summary>Feuilles tropicales qui tombent en tournoyant.</summary>
            Leaves,
            /// <summary>Bulles qui remontent vers la surface.</summary>
            Bubbles,
        }

        /// <summary>Ambiance d'un thème : ciel, brouillard, lumière, décor, météo, post-traitement.</summary>
        public struct ThemeColors
        {
            public SetPiece Set;
            public Weather Weather;
            public bool Lightning;
            public bool Fireflies;
            /// <summary>Nuages dans le ciel (absents sous la mer).</summary>
            public bool Clouds;
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
                Clouds = true,
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

                // Jungle tropicale : palmiers, fromagers à lianes, temple en ruine dans la brume.
                case Core.Theme.Jungle:
                    t.Set = SetPiece.Jungle;
                    t.Weather = Weather.Leaves;
                    t.SkyTop = Hex("#3FA7C9");
                    t.SkyHorizon = Hex("#D2F2C8");
                    t.Fog = Hex("#A4D4AE");
                    t.FogStart = 10f;
                    t.FogEnd = 70f;
                    t.SunColor = Hex("#FFEFC2");
                    t.SunIntensity = 1.25f;
                    t.SunEuler = new Vector3(48f, -25f, 0f);
                    t.AmbientSky = Hex("#8CCBB0");
                    t.AmbientEquator = Hex("#A8D8A0");
                    t.AmbientGround = Hex("#4E7A3A");
                    t.HillFar = Hex("#5AA07E");
                    t.HillNear = Hex("#2F8F4E");
                    t.Foliage = Hex("#2FA84A");
                    t.Cloud = Hex("#FFFFFF");
                    t.SunScreenPos = new Vector2(0.78f, 0.82f);
                    t.SunDisc = Hex("#FFF4C8");
                    t.Grass = Hex("#6FD24A");
                    t.Dirt = new Color(0.85f, 0.75f, 0.6f);
                    t.Wind = 1.1f;
                    t.BloomIntensity = 0.55f;
                    t.ColorFilter = Hex("#F4FFF0");
                    t.Vignette = 0.24f;
                    break;

                // Ciel : au-dessus d'une mer de nuages, îles flottantes, montgolfières, arc-en-ciel.
                case Core.Theme.Sky:
                    t.Set = SetPiece.Sky;
                    t.SkyTop = Hex("#2F74D8");
                    t.SkyHorizon = Hex("#D6EBFF");
                    t.Fog = Hex("#E2EEFF");
                    t.FogStart = 30f;
                    t.FogEnd = 160f;
                    t.SunColor = Hex("#FFF3DC");
                    t.SunIntensity = 1.4f;
                    t.SunEuler = new Vector3(35f, 30f, 0f);
                    t.AmbientSky = Hex("#A9CBFF");
                    t.AmbientEquator = Hex("#E2EEFF");
                    t.AmbientGround = Hex("#D2DEF2");
                    t.HillFar = Hex("#F2F7FF");
                    t.HillNear = Hex("#FFFFFF");
                    t.Foliage = Hex("#FFFFFF");
                    t.Cloud = Hex("#FFFFFF");
                    t.SunScreenPos = new Vector2(0.72f, 0.8f);
                    t.SunSize = 0.06f;
                    t.SunDisc = Hex("#FFFBE6");
                    t.Grass = Hex("#F6F9FF");
                    t.Dirt = new Color(0.93f, 0.95f, 1f);
                    t.Wind = 0.6f;
                    t.Flowers = false;
                    t.BloomIntensity = 0.6f;
                    t.BloomThreshold = 1f;
                    t.ColorFilter = Hex("#F6F9FF");
                    t.Vignette = 0.15f;
                    break;

                // Fond marin : sable, algues géantes, coraux, bancs de poissons, méduses, bulles.
                case Core.Theme.Ocean:
                    t.Set = SetPiece.Ocean;
                    t.Weather = Weather.Bubbles;
                    t.Clouds = false;
                    t.SkyTop = Hex("#43C8E2");
                    t.SkyHorizon = Hex("#0C3D68");
                    t.Fog = Hex("#16607E");
                    t.FogStart = 6f;
                    t.FogEnd = 48f;
                    t.SunColor = Hex("#BFF4FF");
                    t.SunIntensity = 1f;
                    t.SunEuler = new Vector3(70f, 10f, 0f);
                    t.AmbientSky = Hex("#4FB8D0");
                    t.AmbientEquator = Hex("#1F6E8C");
                    t.AmbientGround = Hex("#2A4A5A");
                    t.HillFar = Hex("#1B4F6E");
                    t.HillNear = Hex("#2C6E7E");
                    t.Foliage = Hex("#E8738A");
                    t.Cloud = Hex("#FFFFFF");
                    // Lumière de la surface : grand disque pâle tout en haut.
                    t.SunScreenPos = new Vector2(0.5f, 1f);
                    t.SunSize = 0.09f;
                    t.SunDisc = Hex("#DFFBFF");
                    t.Grass = Hex("#E2CF98");
                    t.Dirt = new Color(0.9f, 0.85f, 0.7f);
                    t.SceneTint = new Color(0.85f, 0.95f, 1f);
                    t.Wind = 0.7f;
                    t.Flowers = false;
                    t.BloomIntensity = 0.9f;
                    t.BloomThreshold = 0.85f;
                    t.BloomTint = Hex("#BFF4FF");
                    t.ColorFilter = Hex("#DDF4FF");
                    t.Vignette = 0.3f;
                    t.Exposure = 0.05f;
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
