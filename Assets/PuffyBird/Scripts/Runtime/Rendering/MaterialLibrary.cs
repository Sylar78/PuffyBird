using UnityEngine;

namespace PuffyBird.Rendering
{
    /// <summary>
    /// Crée les matériaux à partir des shaders du dossier Resources (ils sont ainsi toujours
    /// inclus dans les builds, même sans matériau enregistré sur disque).
    /// </summary>
    public sealed class MaterialLibrary
    {
        public static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        public static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        public static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        public static readonly int VertexEmission = Shader.PropertyToID("_VertexEmission");
        public static readonly int Metallic = Shader.PropertyToID("_Metallic");
        public static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
        public static readonly int RimColor = Shader.PropertyToID("_RimColor");
        public static readonly int RimStrength = Shader.PropertyToID("_RimStrength");
        public static readonly int RimPower = Shader.PropertyToID("_RimPower");
        public static readonly int WindStrength = Shader.PropertyToID("_WindStrength");
        public static readonly int WindFrequency = Shader.PropertyToID("_WindFrequency");
        public static readonly int WindHeight = Shader.PropertyToID("_WindHeight");
        public static readonly int BreathStrength = Shader.PropertyToID("_BreathStrength");
        public static readonly int WobbleAmount = Shader.PropertyToID("_WobbleAmount");
        public static readonly int WobbleFrequency = Shader.PropertyToID("_WobbleFrequency");
        public static readonly int BendAmount = Shader.PropertyToID("_BendAmount");
        public static readonly int ScrollOffset = Shader.PropertyToID("_ScrollOffset");

        public static readonly int SkyTop = Shader.PropertyToID("_TopColor");
        public static readonly int SkyHorizon = Shader.PropertyToID("_HorizonColor");
        public static readonly int SunColor = Shader.PropertyToID("_SunColor");
        public static readonly int SunPosition = Shader.PropertyToID("_SunPosition");
        public static readonly int SunSize = Shader.PropertyToID("_SunSize");
        public static readonly int StarDensity = Shader.PropertyToID("_StarDensity");

        readonly Shader _lit;
        readonly Shader _sky;

        public MaterialLibrary()
        {
            _lit = Resources.Load<Shader>("Shaders/PuffyStylizedLit");
            _sky = Resources.Load<Shader>("Shaders/PuffySky");
            if (_lit == null || !_lit.isSupported)
            {
                Debug.LogWarning("PuffyBird : shader PuffyStylizedLit indisponible, repli sur URP/Lit. Lancer PuffyBird > Configurer le projet.");
                _lit = Shader.Find("Universal Render Pipeline/Lit");
            }
            if (_sky == null || !_sky.isSupported) _sky = Shader.Find("Universal Render Pipeline/Unlit");
        }

        /// <summary>Matériau éclairé stylisé. La couleur finale = couleur × couleur de sommet.</summary>
        public Material Lit(string name, Color color, float smoothness = 0.35f, float metallic = 0f, float rim = 0.35f)
        {
            var m = new Material(_lit) { name = name };
            m.SetColor(BaseColor, color);
            m.SetFloat(Smoothness, smoothness);
            m.SetFloat(Metallic, metallic);
            m.SetColor(RimColor, Color.white);
            m.SetFloat(RimStrength, rim);
            m.SetFloat(RimPower, 3f);
            m.SetColor(EmissionColor, Color.black);
            return m;
        }

        public Material Sky(string name)
        {
            return new Material(_sky) { name = name };
        }

        /// <summary>Texture de rayures diagonales pour le gazon (§8.1).</summary>
        public static Texture2D StripeTexture(Color light, Color dark, int size = 64)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "GrassStripes",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool stripe = ((x + y) % size) < size / 2;
                    float grain = Mathf.PerlinNoise(x * 0.35f, y * 0.35f) * 0.08f - 0.04f;
                    var c = stripe ? light : dark;
                    c = new Color(c.r + grain, c.g + grain, c.b + grain, 1f);
                    pixels[y * size + x] = c;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(true, true);
            return tex;
        }
    }
}
