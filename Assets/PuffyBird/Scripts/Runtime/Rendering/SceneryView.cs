using System.Collections.Generic;
using PuffyBird.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace PuffyBird.Rendering
{
    /// <summary>
    /// Décor 2.5D : ciel en dégradé, collines, nuages qui « respirent », sol en relief (gazon rayé,
    /// bordure festonnée, touffes d'herbe au vent, petites fleurs) et un ensemble propre à chaque
    /// thème : forêt (jour, nuit, orage), ville au crépuscule, Japon médiéval, forêt enneigée.
    /// Tout le décor avance à la vitesse du monde ; la perspective donne la parallaxe naturellement
    /// (les objets lointains semblent plus lents). Le défilement s'arrête à la mort (§8.1).
    /// Tout est construit au chargement ; changer de thème ne fait qu'activer et recolorer.
    /// Rien n'est placé devant le plan de jeu sauf le sol : le décor ne masque jamais un tuyau.
    /// </summary>
    public sealed class SceneryView
    {
        /// <summary>
        /// Format d'écran le plus large affiché, pour dimensionner les boucles de décor : chaque
        /// élément ne réapparaît de l'autre côté qu'une fois sorti du champ.
        /// </summary>
        const float WidestAspect = CameraRig.MaxAspect;
        const float MaxVerticalFov = 45f;
        const float StripePeriod = 0.24f;
        const float GroundFront = -1.6f;
        const float StripLength = 3f;
        const int StripCount = 5;

        const int GroupCommon = 0;
        const int GroupForest = 1;
        const int GroupCity = 2;
        const int GroupJapan = 3;
        const int GroupWinter = 4;
        const int GroupBushes = 5;
        const int GroupFlowers = 6;
        const int GroupCount = 7;

        struct Prop
        {
            public Transform Transform;
            public int Group;
            public float BaseX;
            public float Span;
            public float Drift;
            public float Y;
            public float Z;
        }

        readonly List<Prop> _props = new List<Prop>();
        readonly GameObject[] _groups = new GameObject[GroupCount];
        readonly Material _grass;
        readonly Material _tufts;
        readonly Material _flowers;
        readonly Material _dirt;
        readonly Material _hillFar;
        readonly Material _hillNear;
        readonly Material _foliage;
        readonly Material _bush;
        readonly Material _cloud;
        readonly Material _buildings;
        readonly Material _japan;
        readonly Material _cherry;
        readonly Material _mountain;
        readonly Material _pines;
        readonly Material _snow;
        readonly Material _sky;
        readonly System.Random _random = new System.Random(20260929);
        Palette.ThemeColors _theme;
        float _flash;

        public SceneryView(Transform parent, MaterialLibrary materials)
        {
            var root = new GameObject("Décor").transform;
            root.SetParent(parent, false);
            string[] names = { "Commun", "Forêt", "Ville", "Japon", "Hiver", "Buissons", "Fleurs" };
            for (int g = 0; g < GroupCount; g++)
            {
                _groups[g] = new GameObject(names[g]);
                _groups[g].transform.SetParent(root, false);
            }

            // Sol : gazon rayé (dessus et façade, teinté selon le thème) sur une épaisse couche de sable.
            _grass = materials.Lit("Gazon", Color.white, 0.3f, 0f, 0.15f);
            _grass.SetTexture(MaterialLibrary.BaseMap, MaterialLibrary.StripeTexture());
            var grassMesh = new MeshBuilder()
                .AddBox(new Vector3(0f, -0.08f, 58.4f), new Vector3(140f, 0.16f, 120f), Color.white, StripePeriod)
                .Build("Gazon");
            CreateRenderer(Group(GroupCommon), "Gazon", grassMesh, _grass, castShadows: false);

            _dirt = materials.Lit("Sable", Color.white, 0.1f, 0f, 0.1f);
            var dirtMesh = new MeshBuilder()
                .AddBox(new Vector3(0f, -0.2f, GroundFront), new Vector3(140f, 0.08f, 0.04f), Palette.SandEdge)
                .AddBox(new Vector3(0f, -2.56f, 58.4f), new Vector3(140f, 4.8f, 120f), Palette.Sand)
                .Build("Sable");
            CreateRenderer(Group(GroupCommon), "Sable", dirtMesh, _dirt, castShadows: false);

            _tufts = materials.Lit("Touffes", Color.white, 0.35f, 0f, 0.35f);
            _tufts.SetFloat(MaterialLibrary.WindFrequency, 3.1f);
            _tufts.SetFloat(MaterialLibrary.WindHeight, 0.09f);
            _tufts.SetFloat(MaterialLibrary.WindSpread, 6f);
            _flowers = materials.Lit("Fleurs", Color.white, 0.3f, 0f, 0.4f);
            _flowers.SetFloat(MaterialLibrary.WindFrequency, 2.6f);
            _flowers.SetFloat(MaterialLibrary.WindHeight, 0.1f);
            _flowers.SetFloat(MaterialLibrary.WindSpread, 6f);
            BuildGroundStrips();

            // Ciel : grand quadrilatère au fond, dégradé et disque solaire dans le shader.
            _sky = materials.Sky("Ciel");
            var skyMesh = new MeshBuilder().AddBox(Vector3.zero, new Vector3(1f, 1f, 0.01f), Color.white).Build("Ciel");
            var skyQuad = CreateRenderer(Group(GroupCommon), "Ciel", skyMesh, _sky, castShadows: false).transform;
            skyQuad.localPosition = new Vector3(0f, 10f, 160f);
            skyQuad.localScale = new Vector3(400f, 240f, 1f);

            _hillFar = materials.Lit("Collines lointaines", Color.white, 0.05f, 0f, 0.25f);
            _hillNear = materials.Lit("Collines", Color.white, 0.1f, 0f, 0.3f);
            _foliage = materials.Lit("Arbres", Color.white, 0.2f, 0f, 0.4f);
            _foliage.SetFloat(MaterialLibrary.WindFrequency, 1.3f);
            _foliage.SetFloat(MaterialLibrary.WindHeight, 1.6f);
            _bush = materials.Lit("Buissons", Color.white, 0.25f, 0f, 0.45f);
            _bush.SetFloat(MaterialLibrary.WindFrequency, 2.1f);
            _bush.SetFloat(MaterialLibrary.WindHeight, 0.45f);
            _cloud = materials.Lit("Nuages", Color.white, 0.1f, 0f, 0.6f);
            _cloud.SetFloat(MaterialLibrary.BreathStrength, 0.08f);
            _cloud.SetColor(MaterialLibrary.EmissionColor, new Color(0.12f, 0.12f, 0.12f));

            // Fenêtres, lampadaires et lanternes brillent : l'alpha des sommets masque l'émission.
            _buildings = materials.Lit("Immeubles", Color.white, 0.35f, 0f, 0.3f);
            _buildings.SetFloat(MaterialLibrary.VertexEmission, 1.6f);
            _japan = materials.Lit("Japon", Color.white, 0.3f, 0f, 0.3f);
            _japan.SetFloat(MaterialLibrary.VertexEmission, 1.4f);
            _cherry = materials.Lit("Cerisiers", Color.white, 0.25f, 0f, 0.5f);
            _cherry.SetFloat(MaterialLibrary.WindFrequency, 1.2f);
            _cherry.SetFloat(MaterialLibrary.WindHeight, 1.5f);
            _mountain = materials.Lit("Mont", Color.white, 0.2f, 0f, 0.3f);
            _pines = materials.Lit("Sapins", Color.white, 0.25f, 0f, 0.35f);
            _pines.SetFloat(MaterialLibrary.WindFrequency, 1.1f);
            _pines.SetFloat(MaterialLibrary.WindHeight, 1.8f);
            _snow = materials.Lit("Neige", Color.white, 0.55f, 0f, 0.5f);

            BuildHills(Group(GroupCommon), _hillFar, count: 7, z: 70f, minRadius: 14f, maxRadius: 22f, flatten: 0.45f, sink: 0.55f);
            BuildHills(Group(GroupCommon), _hillNear, count: 8, z: 32f, minRadius: 5f, maxRadius: 9f, flatten: 0.5f, sink: 0.45f);
            BuildClouds(count: 9);

            BuildTrees(count: 12);
            BuildBushes(count: 16);
            BuildBuildings();
            BuildStreetLamps(count: 6);
            BuildMountains();
            BuildPagodas(count: 5);
            BuildToriis(count: 3);
            BuildCherryTrees(count: 10);
            BuildLanterns(count: 5);
            BuildPines(count: 14);
            BuildSnowMounds(count: 12);
            BuildSnowmen(count: 2);
        }

        Transform Group(int g) => _groups[g].transform;

        public void ApplyTheme(Palette.ThemeColors colors)
        {
            _theme = colors;
            _sky.SetColor(MaterialLibrary.SkyTop, colors.SkyTop);
            _sky.SetColor(MaterialLibrary.SkyHorizon, colors.SkyHorizon);
            _sky.SetColor(MaterialLibrary.SunColor, colors.SunDisc);
            _sky.SetVector(MaterialLibrary.SunPosition, new Vector4(colors.SunScreenPos.x, colors.SunScreenPos.y, 0f, 0f));
            _sky.SetFloat(MaterialLibrary.SunSize, colors.SunSize);
            _sky.SetFloat(MaterialLibrary.StarDensity, colors.StarDensity);
            _flash = 0f;

            var tint = colors.SceneTint;
            _hillFar.SetColor(MaterialLibrary.BaseColor, colors.HillFar);
            _hillNear.SetColor(MaterialLibrary.BaseColor, colors.HillNear);
            _bush.SetColor(MaterialLibrary.BaseColor, colors.Foliage);
            _cloud.SetColor(MaterialLibrary.BaseColor, colors.Cloud);
            _grass.SetColor(MaterialLibrary.BaseColor, colors.Grass * tint);
            _tufts.SetColor(MaterialLibrary.BaseColor, colors.Grass * tint);
            _flowers.SetColor(MaterialLibrary.BaseColor, tint);
            _dirt.SetColor(MaterialLibrary.BaseColor, colors.Dirt);
            _foliage.SetColor(MaterialLibrary.BaseColor, tint);
            _buildings.SetColor(MaterialLibrary.BaseColor, tint);
            _japan.SetColor(MaterialLibrary.BaseColor, tint);
            _cherry.SetColor(MaterialLibrary.BaseColor, tint);
            _mountain.SetColor(MaterialLibrary.BaseColor, tint);
            _pines.SetColor(MaterialLibrary.BaseColor, tint);
            _snow.SetColor(MaterialLibrary.BaseColor, tint);

            // Le vent de l'orage couche la végétation.
            float wind = colors.Wind;
            _foliage.SetFloat(MaterialLibrary.WindStrength, 0.05f * wind);
            _bush.SetFloat(MaterialLibrary.WindStrength, 0.04f * wind);
            _tufts.SetFloat(MaterialLibrary.WindStrength, 0.012f * wind);
            _flowers.SetFloat(MaterialLibrary.WindStrength, 0.01f * wind);
            _cherry.SetFloat(MaterialLibrary.WindStrength, 0.04f * wind);
            _pines.SetFloat(MaterialLibrary.WindStrength, 0.025f * wind);

            var set = colors.Set;
            _groups[GroupForest].SetActive(set == Palette.SetPiece.Forest);
            _groups[GroupCity].SetActive(set == Palette.SetPiece.City);
            _groups[GroupJapan].SetActive(set == Palette.SetPiece.Japan);
            _groups[GroupWinter].SetActive(set == Palette.SetPiece.Winter);
            _groups[GroupBushes].SetActive(set != Palette.SetPiece.Winter);
            _groups[GroupFlowers].SetActive(colors.Flowers);
        }

        /// <summary>Éclair : le ciel s'illumine un instant (0 = normal, 1 = pleine lueur).</summary>
        public void SetFlash(float amount)
        {
            if (amount <= 0f && _flash <= 0f) return;
            _flash = amount;
            var glow = Palette.Hex("#C9D6FF");
            _sky.SetColor(MaterialLibrary.SkyTop, Color.Lerp(_theme.SkyTop, glow, amount * 0.6f));
            _sky.SetColor(MaterialLibrary.SkyHorizon, Color.Lerp(_theme.SkyHorizon, glow, amount * 0.8f));
            _cloud.SetColor(MaterialLibrary.BaseColor, Color.Lerp(_theme.Cloud, Color.white, amount));
        }

        /// <param name="scrollPx">Distance défilée en px logiques (interpolée).</param>
        public void Update(double scrollPx, float time)
        {
            float scroll = (float)(scrollPx / WorldSpace.PixelsPerUnit % 100000.0);
            _grass.SetVector(MaterialLibrary.ScrollOffset, new Vector4(scroll / StripePeriod % 1f, 0f, 0f, 0f));

            for (int i = 0; i < _props.Count; i++)
            {
                var p = _props[i];
                if (!_groups[p.Group].activeSelf) continue;
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

        static Color Gray(float v, float glow = 0f) => new Color(v, v, v, glow);

        static Color Opaque(Color c) => new Color(c.r, c.g, c.b, 0f);

        void AddProp(Transform t, int group, float span, float y, float z, int index, int count, float drift = 0f)
        {
            float baseX = (index + Rand(0.15f, 0.85f)) / count * span - span * 0.5f;
            _props.Add(new Prop { Transform = t, Group = group, BaseX = baseX, Span = span, Drift = drift, Y = y, Z = z });
        }

        Transform Place(int group, string name, Mesh mesh, Material material, bool shadows, float z, float radius, int index, int count, float y = 0f)
        {
            var t = CreateRenderer(Group(group), name, mesh, material, shadows).transform;
            AddProp(t, group, SpanAt(z, radius), y, z, index, count);
            return t;
        }

        // ---------------------------------------------------------------- Sol

        /// <summary>
        /// Bandes de sol jointives qui défilent avec le gazon : bordure festonnée sur l'arête avant,
        /// touffes d'herbe (courtes devant le plan de jeu, plus hautes derrière) et fleurs.
        /// </summary>
        void BuildGroundStrips()
        {
            var tuftMeshes = new Mesh[StripCount];
            var flowerMeshes = new Mesh[StripCount];
            for (int k = 0; k < StripCount; k++)
            {
                var tufts = new MeshBuilder();
                // Bordure : petits dômes serrés qui débordent de l'arête avant du gazon.
                for (float x = 0f; x < StripLength; x += 0.075f)
                {
                    var radii = new Vector3(Rand(0.045f, 0.06f), Rand(0.028f, 0.04f), 0.045f);
                    tufts.AddEllipsoid(new Vector3(x + Rand(-0.01f, 0.01f), -0.012f, GroundFront + 0.012f), radii, Gray(Rand(0.88f, 1f)), 8, 5);
                }
                // Touffes : devant le plan de jeu, à peine plus hautes que le gazon ; derrière, plus fournies.
                for (int i = 0; i < 70; i++)
                {
                    bool front = i < 20;
                    float z = front ? Rand(GroundFront + 0.08f, -0.35f) : Rand(0.35f, 3.4f);
                    float height = front ? Rand(0.02f, 0.034f) : Rand(0.045f, 0.095f);
                    AddTuft(tufts, new Vector3(Rand(0f, StripLength), -0.004f, z), height);
                }
                tuftMeshes[k] = tufts.Build("Touffes " + k);

                var flowers = new MeshBuilder();
                for (int i = 0; i < 9; i++)
                {
                    var root = new Vector3(Rand(0f, StripLength), 0f, Rand(0.4f, 3f));
                    float h = Rand(0.05f, 0.085f);
                    float lean = Rand(-0.01f, 0.01f);
                    flowers.AddBlade(root, 0.008f, h, lean, Palette.Hex("#3E8A2E"), Palette.Hex("#6CC04A"));
                    var head = root + new Vector3(lean, h, 0f);
                    var petal = Palette.Flowers[_random.Next(Palette.Flowers.Length)];
                    for (int p = 0; p < 5; p++)
                    {
                        float a = p * Mathf.PI * 2f / 5f;
                        flowers.AddSphere(head + new Vector3(Mathf.Cos(a) * 0.011f, Mathf.Sin(a) * 0.011f, -0.002f), 0.008f, petal, 6, 4);
                    }
                    flowers.AddSphere(head + new Vector3(0f, 0f, -0.006f), 0.006f, Palette.Hex("#FFC93C"), 6, 4);
                }
                flowerMeshes[k] = flowers.Build("Fleurs " + k);
            }

            float span = StripLength * StripCount;
            for (int k = 0; k < StripCount; k++)
            {
                float baseX = k * StripLength - span * 0.5f;
                var tufts = CreateRenderer(Group(GroupCommon), "Touffes", tuftMeshes[k], _tufts, castShadows: false).transform;
                _props.Add(new Prop { Transform = tufts, Group = GroupCommon, BaseX = baseX, Span = span, Y = 0f, Z = 0f });
                var flowers = CreateRenderer(Group(GroupFlowers), "Fleurs", flowerMeshes[k], _flowers, castShadows: false).transform;
                _props.Add(new Prop { Transform = flowers, Group = GroupFlowers, BaseX = baseX, Span = span, Y = 0f, Z = 0f });
            }
        }

        void AddTuft(MeshBuilder b, Vector3 root, float height)
        {
            int blades = _random.Next(4, 7);
            for (int j = 0; j < blades; j++)
            {
                float spread = (j - (blades - 1) * 0.5f) / blades;
                float h = height * Rand(0.65f, 1f);
                var at = root + new Vector3(spread * 0.03f, 0f, Rand(-0.01f, 0.01f));
                b.AddBlade(at, Rand(0.009f, 0.014f), h, spread * 0.035f + Rand(-0.006f, 0.006f), Gray(Rand(0.5f, 0.62f)), Gray(Rand(0.95f, 1.08f)));
            }
        }

        // ---------------------------------------------------------------- Commun

        void BuildHills(Transform root, Material material, int count, float z, float minRadius, float maxRadius, float flatten, float sink)
        {
            var mesh = new MeshBuilder().AddSphere(Vector3.zero, 1f, Color.white, 28, 16).Build("Colline");
            float span = SpanAt(z, maxRadius * 1.8f); // étirement horizontal maximal ci-dessous
            for (int i = 0; i < count; i++)
            {
                float radius = Rand(minRadius, maxRadius);
                float zz = z + Rand(-4f, 4f);
                var t = CreateRenderer(root, "Colline", mesh, material, castShadows: false).transform;
                t.localScale = new Vector3(radius * Rand(1.2f, 1.8f), radius * flatten, radius);
                AddProp(t, GroupCommon, span, -radius * flatten * sink, zz, i, count);
            }
        }

        void BuildClouds(int count)
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
                var t = CreateRenderer(Group(GroupCommon), "Nuage", meshes[i % meshes.Length], _cloud, castShadows: false).transform;
                float s = Rand(1.6f, 3f);
                t.localScale = new Vector3(s, s * 0.8f, s);
                AddProp(t, GroupCommon, SpanAt(z, 3f * s), Rand(4.5f, 9f), z, i, count, drift: Rand(0.15f, 0.35f));
            }
        }

        // ---------------------------------------------------------------- Forêt (jour, nuit, orage)

        void BuildTrees(int count)
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
                var t = Place(GroupForest, "Arbre", meshes[i % meshes.Length], _foliage, true, z, 1.5f, i, count);
                float s = Rand(0.9f, 1.4f);
                t.localScale = new Vector3(s, s * Rand(0.9f, 1.2f), s);
                t.localRotation = Quaternion.Euler(0f, Rand(0f, 360f), 0f);
            }
        }

        void BuildBushes(int count)
        {
            var b = new MeshBuilder();
            b.AddEllipsoid(new Vector3(0f, 0.12f, 0f), new Vector3(0.3f, 0.26f, 0.24f), Color.white, 14, 10);
            b.AddEllipsoid(new Vector3(-0.25f, 0.06f, 0.04f), new Vector3(0.22f, 0.18f, 0.2f), new Color(0.88f, 0.92f, 0.88f), 12, 8);
            b.AddEllipsoid(new Vector3(0.24f, 0.05f, -0.03f), new Vector3(0.2f, 0.16f, 0.18f), new Color(0.92f, 0.95f, 0.9f), 12, 8);
            var mesh = b.Build("Buisson");
            for (int i = 0; i < count; i++)
            {
                float z = Rand(0.9f, 3.5f);
                var t = Place(GroupBushes, "Buisson", mesh, _bush, true, z, 0.6f, i, count, y: -0.02f);
                float s = Rand(0.8f, 1.5f);
                t.localScale = new Vector3(s, s, s);
            }
        }

        // ---------------------------------------------------------------- Ville au crépuscule

        void BuildBuildings()
        {
            Color[] walls =
            {
                Palette.Hex("#5A6488"), Palette.Hex("#6E6A8E"), Palette.Hex("#4E5A78"),
                Palette.Hex("#7A7090"), Palette.Hex("#8A6F7E"), Palette.Hex("#5F7390"),
            };
            var tall = new Mesh[6];
            for (int v = 0; v < tall.Length; v++)
                tall[v] = BuildBuilding(Rand(1.6f, 2.6f), Rand(3.5f, 7.5f), Rand(1.2f, 2f), walls[v], v % 3 == 0).Build("Immeuble " + v);
            var low = new Mesh[4];
            for (int v = 0; v < low.Length; v++)
                low[v] = BuildBuilding(Rand(1f, 1.6f), Rand(1.1f, 1.9f), Rand(0.8f, 1.2f), walls[(v + 2) % walls.Length], false).Build("Maison " + v);

            // Ligne d'horizon : grands immeubles au loin, voilés par le brouillard du soir.
            for (int i = 0; i < 14; i++)
            {
                float z = Rand(22f, 40f);
                var t = Place(GroupCity, "Immeuble", tall[i % tall.Length], _buildings, false, z, 2f, i, 14);
                float s = Rand(0.9f, 1.3f);
                t.localScale = new Vector3(s, s * Rand(0.85f, 1.25f), s);
            }
            // Bâtiments bas plus proches (boutiques, maisons de ville).
            for (int i = 0; i < 9; i++)
            {
                float z = Rand(10f, 16f);
                var t = Place(GroupCity, "Maison", low[i % low.Length], _buildings, true, z, 1.2f, i, 9);
                float s = Rand(0.9f, 1.2f);
                t.localScale = new Vector3(s, s, s);
            }
        }

        MeshBuilder BuildBuilding(float width, float height, float depth, Color wall, bool antenna)
        {
            var b = new MeshBuilder();
            var body = Opaque(wall);
            var trim = Opaque(Color.Lerp(wall, Color.black, 0.3f));
            b.AddBox(new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, depth), body);
            b.AddBox(new Vector3(0f, height + 0.04f, 0f), new Vector3(width + 0.08f, 0.08f, depth + 0.08f), trim);

            // Fenêtres sur la façade : environ la moitié allumées (émission), les autres sombres.
            const float cell = 0.34f;
            int columns = Mathf.Max(2, Mathf.FloorToInt((width - 0.2f) / cell));
            int rows = Mathf.Max(2, Mathf.FloorToInt((height - 0.3f) / (cell * 1.2f)));
            float x0 = -(columns - 1) * cell * 0.5f;
            var lit = Palette.Hex("#FFD58A");
            var litWarm = Palette.Hex("#FFB85C");
            var dark = Opaque(Palette.Hex("#2A3050"));
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    float roll = (float)_random.NextDouble();
                    var color = roll < 0.35f ? lit : roll < 0.5f ? litWarm : dark;
                    var center = new Vector3(x0 + c * cell, 0.35f + r * cell * 1.2f, -depth * 0.5f - 0.01f);
                    b.AddBox(center, new Vector3(cell * 0.55f, cell * 0.7f, 0.02f), color);
                }
            }
            if (antenna)
            {
                b.AddCylinder(new Vector3(width * 0.2f, height + 0.08f, 0f), 0.02f, 0.9f, trim, 6);
                b.AddSphere(new Vector3(width * 0.2f, height + 1f, 0f), 0.05f, Palette.Hex("#FF4D4D"), 8, 6);
            }
            else
            {
                // Château d'eau sur le toit.
                b.AddCylinder(new Vector3(-width * 0.2f, height + 0.08f, 0f), 0.22f, 0.35f, Opaque(Palette.Hex("#6B5548")), 12);
                b.AddCone(new Vector3(-width * 0.2f, height + 0.43f, 0f), 0.25f, 0.15f, trim, 12);
            }
            return b;
        }

        void BuildStreetLamps(int count)
        {
            var b = new MeshBuilder();
            var metal = Opaque(Palette.Hex("#2E3440"));
            b.AddCylinder(Vector3.zero, 0.022f, 0.85f, metal, 8);
            b.AddBox(new Vector3(0.07f, 0.86f, 0f), new Vector3(0.18f, 0.025f, 0.025f), metal);
            b.AddSphere(new Vector3(0.14f, 0.82f, 0f), 0.045f, Palette.Hex("#FFE2A0"), 10, 8);
            var mesh = b.Build("Lampadaire");
            for (int i = 0; i < count; i++) Place(GroupCity, "Lampadaire", mesh, _buildings, true, Rand(1.4f, 3.2f), 0.3f, i, count);
        }

        // ---------------------------------------------------------------- Japon médiéval

        void BuildMountains()
        {
            var b = new MeshBuilder();
            // Flancs (rayon 22 → 5,2 sur 10 de haut), puis calotte de neige posée juste au-dessus.
            var snow = Palette.Hex("#F4F7FF");
            b.AddFrustum(Vector3.zero, 22f, 5.2f, 10f, Palette.Hex("#7C8FC2"), 32, false);
            b.AddFrustum(new Vector3(0f, 7.2f, 0f), 10.1f, 5.35f, 2.8f, snow, 32, false);
            b.AddCone(new Vector3(0f, 10f, 0f), 5.35f, 0.9f, snow, 32);
            var mesh = b.Build("Mont");
            for (int i = 0; i < 2; i++) Place(GroupJapan, "Mont", mesh, _mountain, false, 62f, 22f, i, 2, y: -2.5f);
        }

        void BuildPagodas(int count)
        {
            var meshes = new Mesh[2];
            for (int v = 0; v < meshes.Length; v++)
            {
                var b = new MeshBuilder();
                var wall = Opaque(Palette.Hex("#F2E6D4"));
                var beam = Opaque(Palette.Hex("#B8433A"));
                var roof = Opaque(Palette.Hex("#3C4658"));
                var glow = Palette.Hex("#FFC870");
                int tiers = 3 + v * 2;
                float y = 0f;
                b.AddBox(new Vector3(0f, 0.06f, 0f), new Vector3(1.1f, 0.12f, 1.1f), Opaque(Palette.Hex("#9A9082")));
                y = 0.12f;
                for (int t = 0; t < tiers; t++)
                {
                    float w = 0.7f - t * 0.08f;
                    float h = t == 0 ? 0.42f : 0.28f;
                    b.AddBox(new Vector3(0f, y + h * 0.5f, 0f), new Vector3(w, h, w), wall);
                    b.AddBox(new Vector3(0f, y + h * 0.5f, -w * 0.5f - 0.005f), new Vector3(w * 0.35f, h * 0.45f, 0.01f), glow);
                    b.AddBox(new Vector3(0f, y + h - 0.02f, 0f), new Vector3(w + 0.04f, 0.04f, w + 0.04f), beam);
                    y += h;
                    // Toit à pans : pyramide tronquée à quatre côtés, tournée pour suivre les murs.
                    // Rayon aux coins (quatre côtés) : demi-largeur × √2, avec un débord de 0,2.
                    b.Append(new MeshBuilder().AddFrustum(Vector3.zero, (w * 0.5f + 0.2f) * 1.414f, w * 0.45f, 0.16f, roof, 4),
                        Matrix4x4.TRS(new Vector3(0f, y, 0f), Quaternion.Euler(0f, 45f, 0f), Vector3.one));
                    y += 0.14f;
                }
                b.AddCylinder(new Vector3(0f, y, 0f), 0.018f, 0.4f, Opaque(Palette.Hex("#D9A441")), 8);
                meshes[v] = b.Build("Pagode " + v);
            }
            for (int i = 0; i < count; i++)
            {
                float z = Rand(14f, 24f);
                var t = Place(GroupJapan, "Pagode", meshes[i % meshes.Length], _japan, true, z, 1.2f, i, count);
                float s = Rand(1.3f, 1.9f);
                t.localScale = new Vector3(s, s, s);
                t.localRotation = Quaternion.Euler(0f, Rand(-12f, 12f), 0f);
            }
        }

        void BuildToriis(int count)
        {
            var b = new MeshBuilder();
            var red = Opaque(Palette.Hex("#D8392B"));
            var black = Opaque(Palette.Hex("#23232A"));
            b.AddCylinder(new Vector3(-0.32f, 0f, 0f), 0.035f, 0.78f, red, 10);
            b.AddCylinder(new Vector3(0.32f, 0f, 0f), 0.035f, 0.78f, red, 10);
            b.AddBox(new Vector3(0f, 0.6f, 0f), new Vector3(0.82f, 0.04f, 0.045f), red);
            b.AddBox(new Vector3(0f, 0.8f, 0f), new Vector3(1f, 0.05f, 0.075f), black);
            b.AddBox(new Vector3(0f, 0.765f, 0f), new Vector3(0.9f, 0.03f, 0.06f), red);
            b.AddBox(new Vector3(0f, 0.69f, 0f), new Vector3(0.05f, 0.1f, 0.04f), red);
            var mesh = b.Build("Torii");
            for (int i = 0; i < count; i++)
            {
                var t = Place(GroupJapan, "Torii", mesh, _japan, true, Rand(3.5f, 8f), 0.6f, i, count);
                float s = Rand(1f, 1.3f);
                t.localScale = new Vector3(s, s, s);
            }
        }

        void BuildCherryTrees(int count)
        {
            Color[] blossoms = { Palette.Hex("#FFB7C9"), Palette.Hex("#FF9FBF"), Palette.Hex("#FFD1DC"), Palette.Hex("#FFC4D4") };
            var meshes = new Mesh[3];
            for (int v = 0; v < meshes.Length; v++)
            {
                var b = new MeshBuilder();
                float h = 0.75f + v * 0.2f;
                var bark = Palette.Hex("#5A3A36");
                b.AddFrustum(Vector3.zero, 0.08f, 0.05f, h, bark, 10);
                b.Append(new MeshBuilder().AddFrustum(Vector3.zero, 0.04f, 0.02f, 0.4f, bark, 8),
                    Matrix4x4.TRS(new Vector3(0f, h * 0.8f, 0f), Quaternion.Euler(0f, 0f, 50f), Vector3.one));
                for (int k = 0; k < 6; k++)
                {
                    float a = k * 1.05f + v;
                    var c = new Vector3(Mathf.Cos(a) * 0.35f, h + 0.1f + Mathf.Sin(a * 1.7f) * 0.12f, Mathf.Sin(a) * 0.2f);
                    b.AddSphere(c, Rand(0.24f, 0.34f), blossoms[(k + v) % blossoms.Length], 12, 8);
                }
                b.AddSphere(new Vector3(0f, h + 0.3f, 0f), 0.38f, blossoms[v % blossoms.Length], 14, 10);
                meshes[v] = b.Build("Cerisier " + v);
            }
            for (int i = 0; i < count; i++)
            {
                float z = Rand(5f, 15f);
                var t = Place(GroupJapan, "Cerisier", meshes[i % meshes.Length], _cherry, true, z, 1.3f, i, count);
                float s = Rand(0.9f, 1.4f);
                t.localScale = new Vector3(s, s * Rand(0.9f, 1.15f), s);
                t.localRotation = Quaternion.Euler(0f, Rand(0f, 360f), 0f);
            }
        }

        void BuildLanterns(int count)
        {
            var b = new MeshBuilder();
            var stone = Opaque(Palette.Hex("#9C9A94"));
            b.AddCylinder(Vector3.zero, 0.06f, 0.04f, stone, 8);
            b.AddCylinder(new Vector3(0f, 0.04f, 0f), 0.025f, 0.16f, stone, 8);
            b.AddBox(new Vector3(0f, 0.22f, 0f), new Vector3(0.1f, 0.03f, 0.1f), stone);
            b.AddBox(new Vector3(0f, 0.27f, 0f), new Vector3(0.075f, 0.07f, 0.075f), Palette.Hex("#FFC870"));
            b.Append(new MeshBuilder().AddCone(Vector3.zero, 0.1f, 0.07f, stone, 4),
                Matrix4x4.TRS(new Vector3(0f, 0.305f, 0f), Quaternion.Euler(0f, 45f, 0f), Vector3.one));
            var mesh = b.Build("Lanterne");
            for (int i = 0; i < count; i++) Place(GroupJapan, "Lanterne", mesh, _japan, true, Rand(1.2f, 3f), 0.2f, i, count);
        }

        // ---------------------------------------------------------------- Forêt enneigée

        void BuildPines(int count)
        {
            var meshes = new Mesh[3];
            var needles = Palette.Hex("#2F6B4F");
            var needlesLight = Palette.Hex("#3B7F5C");
            var snow = Palette.Hex("#F7FBFF");
            for (int v = 0; v < meshes.Length; v++)
            {
                var b = new MeshBuilder();
                b.AddCylinder(Vector3.zero, 0.06f, 0.3f, Palette.Hex("#6B4A3A"), 8);
                int tiers = 3 + v % 2;
                float y = 0.22f;
                float r = 0.55f + v * 0.06f;
                for (int t = 0; t < tiers; t++)
                {
                    float h = 0.55f - t * 0.06f;
                    b.AddCone(new Vector3(0f, y, 0f), r, h, t % 2 == 0 ? needles : needlesLight, 14);
                    // Neige posée sur chaque étage : un cône plus petit, décalé vers le haut.
                    b.AddCone(new Vector3(0f, y + h * 0.42f, 0f), r * 0.6f, h * 0.6f, snow, 14);
                    y += h * 0.55f;
                    r *= 0.75f;
                }
                meshes[v] = b.Build("Sapin " + v);
            }
            for (int i = 0; i < count; i++)
            {
                float z = Rand(5f, 16f);
                var t = Place(GroupWinter, "Sapin", meshes[i % meshes.Length], _pines, true, z, 1.2f, i, count);
                float s = Rand(0.9f, 1.6f);
                t.localScale = new Vector3(s, s * Rand(0.95f, 1.25f), s);
                t.localRotation = Quaternion.Euler(0f, Rand(0f, 360f), 0f);
            }
        }

        void BuildSnowMounds(int count)
        {
            var b = new MeshBuilder();
            b.AddEllipsoid(Vector3.zero, new Vector3(0.34f, 0.16f, 0.26f), Palette.Hex("#FFFFFF"), 16, 10);
            b.AddEllipsoid(new Vector3(0.26f, -0.02f, 0.05f), new Vector3(0.2f, 0.11f, 0.18f), Palette.Hex("#EEF4FA"), 12, 8);
            var mesh = b.Build("Congère");
            for (int i = 0; i < count; i++)
            {
                var t = Place(GroupWinter, "Congère", mesh, _snow, true, Rand(0.9f, 3.6f), 0.6f, i, count, y: -0.05f);
                float s = Rand(0.8f, 1.6f);
                t.localScale = new Vector3(s, s * Rand(0.8f, 1.1f), s);
            }
        }

        void BuildSnowmen(int count)
        {
            var b = new MeshBuilder();
            var snow = Palette.Hex("#FFFFFF");
            var coal = Palette.Hex("#1E1E24");
            b.AddSphere(new Vector3(0f, 0.13f, 0f), 0.15f, snow, 16, 10);
            b.AddSphere(new Vector3(0f, 0.34f, 0f), 0.11f, snow, 14, 10);
            b.AddSphere(new Vector3(0f, 0.5f, 0f), 0.075f, snow, 12, 8);
            b.AddSphere(new Vector3(-0.025f, 0.52f, -0.065f), 0.011f, coal, 6, 4);
            b.AddSphere(new Vector3(0.025f, 0.52f, -0.065f), 0.011f, coal, 6, 4);
            b.Append(new MeshBuilder().AddCone(Vector3.zero, 0.014f, 0.08f, Palette.Hex("#FF8A2A"), 8),
                Matrix4x4.TRS(new Vector3(0f, 0.495f, -0.07f), Quaternion.Euler(-90f, 0f, 0f), Vector3.one));
            b.AddBox(new Vector3(0f, 0.43f, 0f), new Vector3(0.2f, 0.035f, 0.2f), Palette.Hex("#D8392B"));
            b.AddCylinder(new Vector3(0f, 0.565f, 0f), 0.06f, 0.08f, coal, 12);
            b.AddCylinder(new Vector3(0f, 0.56f, 0f), 0.09f, 0.012f, coal, 12);
            var mesh = b.Build("Bonhomme de neige");
            for (int i = 0; i < count; i++) Place(GroupWinter, "Bonhomme de neige", mesh, _snow, true, Rand(1.8f, 3.2f), 0.3f, i, count);
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
