using System.Collections.Generic;
using PuffyBird.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace PuffyBird.Rendering
{
    /// <summary>
    /// Décor 2.5D : ciel en dégradé, collines lointaines, arbres et buissons agités par le vent
    /// (vertex shader), nuages qui « respirent », sol en relief au gazon rayé qui défile.
    /// Tout le décor avance à la vitesse du monde ; la perspective donne la parallaxe naturellement
    /// (les objets lointains semblent plus lents). Le défilement s'arrête à la mort (§8.1).
    /// </summary>
    public sealed class SceneryView
    {
        /// <summary>Format d'écran le plus large géré (tablette) pour dimensionner les boucles de décor.</summary>
        const float WidestAspect = 0.8f;
        const float MaxVerticalFov = 45f;
        const float StripePeriod = 0.24f;

        struct Prop
        {
            public Transform Transform;
            public float BaseX;
            public float Span;
            public float Drift;
            public float Y;
            public float Z;
        }

        readonly List<Prop> _props = new List<Prop>();
        readonly Material _grass;
        readonly Material _dirt;
        readonly Material _hillFar;
        readonly Material _hillNear;
        readonly Material _foliage;
        readonly Material _bush;
        readonly Material _cloud;
        readonly Material _sky;
        readonly Transform _skyQuad;
        readonly System.Random _random = new System.Random(20260929);

        public SceneryView(Transform parent, MaterialLibrary materials)
        {
            var root = new GameObject("Décor").transform;
            root.SetParent(parent, false);

            // Sol : gazon rayé (dessus et façade) posé sur une épaisse couche de sable.
            _grass = materials.Lit("Gazon", Color.white, 0.25f, 0f, 0.15f);
            _grass.SetTexture(MaterialLibrary.BaseMap, MaterialLibrary.StripeTexture(Palette.GrassLight, Palette.GrassDark));
            var grassMesh = new MeshBuilder()
                .AddBox(new Vector3(0f, -0.08f, 58.4f), new Vector3(140f, 0.16f, 120f), Color.white, StripePeriod)
                .Build("Gazon");
            CreateRenderer(root, "Gazon", grassMesh, _grass, castShadows: false);

            _dirt = materials.Lit("Sable", Color.white, 0.1f, 0f, 0.1f);
            var dirtMesh = new MeshBuilder()
                .AddBox(new Vector3(0f, -0.2f, -1.6f), new Vector3(140f, 0.08f, 0.04f), Palette.SandEdge)
                .AddBox(new Vector3(0f, -2.56f, 58.4f), new Vector3(140f, 4.8f, 120f), Palette.Sand)
                .Build("Sable");
            CreateRenderer(root, "Sable", dirtMesh, _dirt, castShadows: false);

            // Ciel : grand quadrilatère au fond, dégradé et disque solaire dans le shader.
            _sky = materials.Sky("Ciel");
            var skyMesh = new MeshBuilder().AddBox(Vector3.zero, new Vector3(1f, 1f, 0.01f), Color.white).Build("Ciel");
            _skyQuad = CreateRenderer(root, "Ciel", skyMesh, _sky, castShadows: false).transform;
            _skyQuad.localPosition = new Vector3(0f, 10f, 160f);
            _skyQuad.localScale = new Vector3(400f, 240f, 1f);

            _hillFar = materials.Lit("Collines lointaines", Color.white, 0.05f, 0f, 0.25f);
            _hillNear = materials.Lit("Collines", Color.white, 0.1f, 0f, 0.3f);
            _foliage = materials.Lit("Arbres", Color.white, 0.2f, 0f, 0.4f);
            _foliage.SetFloat(MaterialLibrary.WindStrength, 0.05f);
            _foliage.SetFloat(MaterialLibrary.WindFrequency, 1.3f);
            _foliage.SetFloat(MaterialLibrary.WindHeight, 1.6f);
            _bush = materials.Lit("Buissons", Color.white, 0.25f, 0f, 0.45f);
            _bush.SetFloat(MaterialLibrary.WindStrength, 0.04f);
            _bush.SetFloat(MaterialLibrary.WindFrequency, 2.1f);
            _bush.SetFloat(MaterialLibrary.WindHeight, 0.45f);
            _cloud = materials.Lit("Nuages", Color.white, 0.1f, 0f, 0.6f);
            _cloud.SetFloat(MaterialLibrary.BreathStrength, 0.08f);
            _cloud.SetColor(MaterialLibrary.EmissionColor, new Color(0.12f, 0.12f, 0.12f));

            BuildHills(root, _hillFar, count: 7, z: 70f, minRadius: 14f, maxRadius: 22f, flatten: 0.45f, sink: 0.55f);
            BuildHills(root, _hillNear, count: 8, z: 32f, minRadius: 5f, maxRadius: 9f, flatten: 0.5f, sink: 0.45f);
            BuildTrees(root, count: 12);
            BuildBushes(root, count: 16);
            BuildClouds(root, count: 9);
        }

        public void ApplyTheme(Palette.ThemeColors colors)
        {
            _sky.SetColor(MaterialLibrary.SkyTop, colors.SkyTop);
            _sky.SetColor(MaterialLibrary.SkyHorizon, colors.SkyHorizon);
            _sky.SetColor(MaterialLibrary.SunColor, colors.SunDisc);
            _sky.SetVector(MaterialLibrary.SunPosition, new Vector4(colors.SunScreenPos.x, colors.SunScreenPos.y, 0f, 0f));
            _sky.SetFloat(MaterialLibrary.SunSize, colors.StarDensity > 0f ? 0.035f : 0.05f);
            _sky.SetFloat(MaterialLibrary.StarDensity, colors.StarDensity);

            _hillFar.SetColor(MaterialLibrary.BaseColor, colors.HillFar);
            _hillNear.SetColor(MaterialLibrary.BaseColor, colors.HillNear);
            _bush.SetColor(MaterialLibrary.BaseColor, colors.Foliage);
            _cloud.SetColor(MaterialLibrary.BaseColor, colors.Cloud);
            bool night = colors.StarDensity > 0f;
            var tint = night ? new Color(0.55f, 0.7f, 0.75f) : Color.white;
            _grass.SetColor(MaterialLibrary.BaseColor, tint);
            _dirt.SetColor(MaterialLibrary.BaseColor, night ? new Color(0.5f, 0.55f, 0.6f) : Color.white);
            _foliage.SetColor(MaterialLibrary.BaseColor, tint);
        }

        /// <param name="scrollPx">Distance défilée en px logiques (interpolée).</param>
        public void Update(double scrollPx, float time)
        {
            float scroll = (float)(scrollPx / WorldSpace.PixelsPerUnit % 100000.0);
            _grass.SetVector(MaterialLibrary.ScrollOffset, new Vector4(scroll / StripePeriod % 1f, 0f, 0f, 0f));

            for (int i = 0; i < _props.Count; i++)
            {
                var p = _props[i];
                float x = Wrap(p.BaseX - scroll - p.Drift * time, p.Span);
                p.Transform.localPosition = new Vector3(x, p.Y, p.Z);
            }
        }

        static float Wrap(float x, float span)
        {
            float half = span * 0.5f;
            float t = (x + half) % span;
            if (t < 0f) t += span;
            return t - half;
        }

        static float SpanAt(float z, float radius)
        {
            return 2f * CameraRig.HalfWidthAt(z, MaxVerticalFov, WidestAspect) + 2f * radius + 2f;
        }

        float Rand(float min, float max) => min + (float)_random.NextDouble() * (max - min);

        void AddProp(Transform t, float span, float y, float z, int index, int count, float drift = 0f)
        {
            float baseX = (index + Rand(0.15f, 0.85f)) / count * span - span * 0.5f;
            _props.Add(new Prop { Transform = t, BaseX = baseX, Span = span, Drift = drift, Y = y, Z = z });
        }

        void BuildHills(Transform root, Material material, int count, float z, float minRadius, float maxRadius, float flatten, float sink)
        {
            var mesh = new MeshBuilder().AddSphere(Vector3.zero, 1f, Color.white, 28, 16).Build("Colline");
            float span = SpanAt(z, maxRadius);
            for (int i = 0; i < count; i++)
            {
                float radius = Rand(minRadius, maxRadius);
                float zz = z + Rand(-4f, 4f);
                var t = CreateRenderer(root, "Colline", mesh, material, castShadows: false).transform;
                t.localScale = new Vector3(radius * Rand(1.2f, 1.8f), radius * flatten, radius);
                AddProp(t, span, -radius * flatten * sink, zz, i, count);
            }
        }

        void BuildTrees(Transform root, int count)
        {
            var meshes = new Mesh[3];
            for (int v = 0; v < meshes.Length; v++)
            {
                var b = new MeshBuilder();
                float h = 0.9f + v * 0.25f;
                b.AddFrustum(Vector3.zero, 0.09f, 0.06f, h, Palette.Hex("#8A5A44"), 10);
                b.AddSphere(new Vector3(0f, h + 0.25f, 0f), 0.5f + v * 0.08f, Palette.Hex("#4BC45A"), 16, 12);
                b.AddSphere(new Vector3(-0.3f, h, 0.1f), 0.35f, Palette.Hex("#5EE270"), 14, 10);
                b.AddSphere(new Vector3(0.28f, h + 0.05f, -0.12f), 0.32f, Palette.Hex("#56D466"), 14, 10);
                meshes[v] = b.Build("Arbre " + v);
            }
            for (int i = 0; i < count; i++)
            {
                float z = Rand(6f, 16f);
                var t = CreateRenderer(root, "Arbre", meshes[i % meshes.Length], _foliage, castShadows: true).transform;
                float s = Rand(0.9f, 1.4f);
                t.localScale = new Vector3(s, s * Rand(0.9f, 1.2f), s);
                t.localRotation = Quaternion.Euler(0f, Rand(0f, 360f), 0f);
                AddProp(t, SpanAt(z, 1.5f), 0f, z, i, count);
            }
        }

        void BuildBushes(Transform root, int count)
        {
            var b = new MeshBuilder();
            b.AddEllipsoid(new Vector3(0f, 0.12f, 0f), new Vector3(0.3f, 0.26f, 0.24f), Color.white, 14, 10);
            b.AddEllipsoid(new Vector3(-0.25f, 0.06f, 0.04f), new Vector3(0.22f, 0.18f, 0.2f), new Color(0.88f, 0.92f, 0.88f), 12, 8);
            b.AddEllipsoid(new Vector3(0.24f, 0.05f, -0.03f), new Vector3(0.2f, 0.16f, 0.18f), new Color(0.92f, 0.95f, 0.9f), 12, 8);
            var mesh = b.Build("Buisson");
            for (int i = 0; i < count; i++)
            {
                float z = Rand(0.9f, 3.5f);
                var t = CreateRenderer(root, "Buisson", mesh, _bush, castShadows: true).transform;
                float s = Rand(0.8f, 1.5f);
                t.localScale = new Vector3(s, s, s);
                AddProp(t, SpanAt(z, 0.6f), -0.02f, z, i, count);
            }
        }

        void BuildClouds(Transform root, int count)
        {
            var meshes = new Mesh[3];
            for (int v = 0; v < meshes.Length; v++)
            {
                var b = new MeshBuilder();
                int puffs = 4 + v;
                for (int k = 0; k < puffs; k++)
                {
                    float x = (k - (puffs - 1) * 0.5f) * 0.55f;
                    float r = 0.45f + 0.25f * Mathf.Sin(k * 1.9f + v);
                    b.AddSphere(new Vector3(x, Mathf.Abs(x) * -0.25f + r * 0.4f, (k % 2) * 0.2f), r, Color.white, 14, 10);
                }
                meshes[v] = b.Build("Nuage " + v);
            }
            for (int i = 0; i < count; i++)
            {
                float z = Rand(26f, 48f);
                var t = CreateRenderer(root, "Nuage", meshes[i % meshes.Length], _cloud, castShadows: false).transform;
                float s = Rand(1.6f, 3f);
                t.localScale = new Vector3(s, s * 0.8f, s);
                AddProp(t, SpanAt(z, 3f * s), Rand(4.5f, 9f), z, i, count, drift: Rand(0.15f, 0.35f));
            }
        }

        static MeshRenderer CreateRenderer(Transform parent, string name, Mesh mesh, Material material, bool castShadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows = true;
            return r;
        }
    }
}
