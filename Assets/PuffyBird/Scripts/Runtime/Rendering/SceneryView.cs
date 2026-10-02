using System.Collections.Generic;
using PuffyBird.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace PuffyBird.Rendering
{
    /// <summary>
    /// Décor 2.5D : ciel en dégradé, collines, nuages qui « respirent », sol en relief (gazon rayé,
    /// bordure festonnée, touffes d'herbe au vent, petites fleurs) et un ensemble propre à chaque
    /// thème : forêt (jour, nuit, orage), ville au crépuscule, Japon médiéval, forêt enneigée,
    /// jungle tropicale, ciel au-dessus des nuages et fond marin.
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
        const int GroupJungle = 7;
        const int GroupSky = 8;
        const int GroupOcean = 9;
        const int GroupClouds = 10;
        const int GroupCount = 11;

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
        readonly Material _jungle;
        readonly Material _ruins;
        readonly Material _islands;
        readonly Material _balloons;
        readonly Material _kelp;
        readonly Material _reef;
        readonly Material _fish;
        readonly System.Random _random = new System.Random(20260929);
        Palette.ThemeColors _theme;
        float _flash;

        public SceneryView(Transform parent, MaterialLibrary materials)
        {
            var root = new GameObject("Décor").transform;
            root.SetParent(parent, false);
            string[] names = { "Commun", "Forêt", "Ville", "Japon", "Hiver", "Buissons", "Fleurs", "Jungle", "Ciel", "Fond marin", "Nuages" };
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

            _jungle = materials.Lit("Jungle", Color.white, 0.3f, 0f, 0.4f);
            _jungle.SetFloat(MaterialLibrary.WindFrequency, 1.1f);
            _jungle.SetFloat(MaterialLibrary.WindHeight, 2.2f);
            _jungle.SetFloat(MaterialLibrary.WindSpread, 1.5f);
            _ruins = materials.Lit("Temple", Color.white, 0.15f, 0f, 0.25f);
            // Cascades, arc-en-ciel, brûleurs, méduses et trésor brillent : l'alpha des sommets masque l'émission.
            _islands = materials.Lit("Îles", Color.white, 0.25f, 0f, 0.4f);
            _islands.SetFloat(MaterialLibrary.VertexEmission, 0.9f);
            _balloons = materials.Lit("Montgolfières", Color.white, 0.4f, 0f, 0.45f);
            _balloons.SetFloat(MaterialLibrary.VertexEmission, 1.6f);
            _kelp = materials.Lit("Algues", Color.white, 0.35f, 0f, 0.4f);
            _kelp.SetFloat(MaterialLibrary.WindFrequency, 0.75f);
            _kelp.SetFloat(MaterialLibrary.WindHeight, 2.4f);
            _kelp.SetFloat(MaterialLibrary.WindSpread, 2.5f);
            _reef = materials.Lit("Récif", Color.white, 0.35f, 0f, 0.45f);
            _reef.SetFloat(MaterialLibrary.VertexEmission, 1.2f);
            _fish = materials.Lit("Poissons", Color.white, 0.6f, 0f, 0.5f);

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

            BuildTemples(count: 3);
            BuildJungleTrees(count: 7);
            BuildPalms(count: 10);
            BuildFerns(count: 12);

            BuildRainbow();
            BuildIslands(count: 7);
            BuildBalloons(count: 4);

            BuildShipwreck();
            BuildSeaRocks(count: 8);
            BuildKelp(count: 18);
            BuildCorals(count: 16);
            BuildTreasure();
            BuildJellyfish(count: 5);
            BuildFishSchools(count: 6);
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
            _jungle.SetColor(MaterialLibrary.BaseColor, tint);
            _ruins.SetColor(MaterialLibrary.BaseColor, tint);
            _islands.SetColor(MaterialLibrary.BaseColor, tint);
            _balloons.SetColor(MaterialLibrary.BaseColor, tint);
            _kelp.SetColor(MaterialLibrary.BaseColor, tint);
            _reef.SetColor(MaterialLibrary.BaseColor, tint);
            _fish.SetColor(MaterialLibrary.BaseColor, tint);

            // Le vent de l'orage couche la végétation.
            float wind = colors.Wind;
            _foliage.SetFloat(MaterialLibrary.WindStrength, 0.05f * wind);
            _bush.SetFloat(MaterialLibrary.WindStrength, 0.04f * wind);
            _tufts.SetFloat(MaterialLibrary.WindStrength, 0.012f * wind);
            _flowers.SetFloat(MaterialLibrary.WindStrength, 0.01f * wind);
            _cherry.SetFloat(MaterialLibrary.WindStrength, 0.04f * wind);
            _pines.SetFloat(MaterialLibrary.WindStrength, 0.025f * wind);
            _jungle.SetFloat(MaterialLibrary.WindStrength, 0.05f * wind);
            // Sous l'eau, la houle fait onduler les algues plus amplement que le vent.
            _kelp.SetFloat(MaterialLibrary.WindStrength, 0.22f * wind);

            var set = colors.Set;
            _groups[GroupForest].SetActive(set == Palette.SetPiece.Forest);
            _groups[GroupCity].SetActive(set == Palette.SetPiece.City);
            _groups[GroupJapan].SetActive(set == Palette.SetPiece.Japan);
            _groups[GroupWinter].SetActive(set == Palette.SetPiece.Winter);
            _groups[GroupJungle].SetActive(set == Palette.SetPiece.Jungle);
            _groups[GroupSky].SetActive(set == Palette.SetPiece.Sky);
            _groups[GroupOcean].SetActive(set == Palette.SetPiece.Ocean);
            _groups[GroupBushes].SetActive(set != Palette.SetPiece.Winter);
            _groups[GroupFlowers].SetActive(colors.Flowers);
            _groups[GroupClouds].SetActive(colors.Clouds);
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
                var t = CreateRenderer(Group(GroupClouds), "Nuage", meshes[i % meshes.Length], _cloud, castShadows: false).transform;
                float s = Rand(1.6f, 3f);
                t.localScale = new Vector3(s, s * 0.8f, s);
                AddProp(t, GroupClouds, SpanAt(z, 3f * s), Rand(4.5f, 9f), z, i, count, drift: Rand(0.15f, 0.35f));
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

        // ---------------------------------------------------------------- Jungle tropicale

        static Color Lum(Color c, float glow) => new Color(c.r, c.g, c.b, glow);

        /// <summary>Feuille allongée partant de <paramref name="at"/> ; renvoie la position de sa pointe.</summary>
        static Vector3 AddLeaf(MeshBuilder b, Vector3 at, float length, float width, float yaw, float pitch, Color color)
        {
            var rotation = Quaternion.Euler(0f, yaw, pitch);
            b.Append(new MeshBuilder().AddEllipsoid(new Vector3(length * 0.5f, 0f, 0f), new Vector3(length * 0.5f, width * 0.18f, width), color, 10, 6),
                Matrix4x4.TRS(at, rotation, Vector3.one));
            return at + rotation * new Vector3(length * 0.92f, 0f, 0f);
        }

        /// <summary>Palme ou fronde en deux parties, la seconde retombant de <paramref name="bend"/> degrés.</summary>
        static void AddFrond(MeshBuilder b, Vector3 at, float length, float width, float yaw, float pitch, float bend, Color inner, Color outer)
        {
            var tip = AddLeaf(b, at, length * 0.55f, width, yaw, pitch, inner);
            AddLeaf(b, tip, length * 0.5f, width * 0.8f, yaw, pitch - bend, outer);
        }

        void BuildTemples(int count)
        {
            var b = new MeshBuilder();
            var stone = Palette.Hex("#9A967A");
            var stoneLight = Palette.Hex("#AEAA8C");
            var moss = Palette.Hex("#6E8A52");
            var dark = Palette.Hex("#2E3328");
            // Pyramide à degrés, escalier central et sanctuaire au sommet, gagnés par la mousse.
            float y = 0f;
            for (int k = 0; k < 5; k++)
            {
                float w = 3.2f - k * 0.55f;
                const float h = 0.45f;
                b.AddBox(new Vector3(0f, y + h * 0.5f, 0f), new Vector3(w, h, w * 0.9f), stone);
                b.AddBox(new Vector3(0f, y + h - 0.03f, 0f), new Vector3(w + 0.03f, 0.06f, w * 0.9f + 0.03f), moss);
                b.AddBox(new Vector3(0f, y + h * 0.5f, -w * 0.45f - 0.1f), new Vector3(0.7f, h, 0.22f), stoneLight);
                y += h;
            }
            b.AddBox(new Vector3(0f, y + 0.35f, 0f), new Vector3(1f, 0.7f, 0.85f), stone);
            b.AddBox(new Vector3(0f, y + 0.74f, 0f), new Vector3(1.15f, 0.1f, 1f), moss);
            b.AddBox(new Vector3(0f, y + 0.28f, -0.43f), new Vector3(0.32f, 0.5f, 0.02f), dark);
            // Lianes qui pendent des terrasses.
            for (int i = 0; i < 6; i++)
            {
                float x = -1.3f + i * 0.5f;
                float len = Rand(0.4f, 1.1f);
                b.AddCylinder(new Vector3(x, 1.8f - len, -1.2f + Mathf.Abs(x) * 0.2f), 0.025f, len, moss, 5);
            }
            var mesh = b.Build("Temple");
            for (int i = 0; i < count; i++)
            {
                var t = Place(GroupJungle, "Temple", mesh, _ruins, false, Rand(30f, 45f), 3.2f, i, count);
                float s = Rand(1.4f, 2f);
                t.localScale = new Vector3(s, s, s);
                t.localRotation = Quaternion.Euler(0f, Rand(-15f, 15f), 0f);
            }
        }

        void BuildJungleTrees(int count)
        {
            var bark = Palette.Hex("#7A6450");
            var vine = Palette.Hex("#3E7A32");
            var meshes = new Mesh[3];
            for (int v = 0; v < meshes.Length; v++)
            {
                var b = new MeshBuilder();
                float h = 2.2f + v * 0.4f;
                b.AddFrustum(Vector3.zero, 0.18f, 0.11f, h, bark, 10);
                // Contreforts : ailes de bois au pied du fromager.
                for (int k = 0; k < 4; k++)
                {
                    b.Append(new MeshBuilder().AddEllipsoid(new Vector3(0.16f, 0.12f, 0f), new Vector3(0.2f, 0.3f, 0.05f), bark, 8, 6),
                        Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, k * 90f + 30f, 0f), Vector3.one));
                }
                b.AddEllipsoid(new Vector3(0f, h + 0.2f, 0f), new Vector3(1.1f, 0.4f, 0.9f), Palette.Hex("#2E7D3A"), 16, 10);
                b.AddEllipsoid(new Vector3(-0.75f, h, 0.1f), new Vector3(0.7f, 0.32f, 0.6f), Palette.Hex("#3A9A46"), 14, 8);
                b.AddEllipsoid(new Vector3(0.78f, h + 0.05f, -0.1f), new Vector3(0.65f, 0.3f, 0.55f), Palette.Hex("#348C40"), 14, 8);
                b.AddEllipsoid(new Vector3(0.1f, h + 0.5f, 0f), new Vector3(0.6f, 0.3f, 0.5f), Palette.Hex("#45A852"), 14, 8);
                // Lianes et leurs petites feuilles, qui ondulent avec le feuillage.
                for (int k = 0; k < 6; k++)
                {
                    float x = -0.95f + k * 0.38f + Rand(-0.08f, 0.08f);
                    float len = Rand(0.7f, 1.5f);
                    var top = new Vector3(x, h - 0.05f, Rand(-0.4f, -0.2f));
                    b.AddCylinder(top - new Vector3(0f, len, 0f), 0.014f, len, vine, 5);
                    for (float d = 0.2f; d < len; d += 0.22f)
                        b.AddEllipsoid(top - new Vector3(0f, d, 0f), new Vector3(0.05f, 0.03f, 0.04f), Palette.Hex("#4FA83E"), 6, 4);
                }
                meshes[v] = b.Build("Fromager " + v);
            }
            for (int i = 0; i < count; i++)
            {
                var t = Place(GroupJungle, "Fromager", meshes[i % meshes.Length], _jungle, true, Rand(9f, 20f), 2.4f, i, count);
                float s = Rand(1f, 1.4f);
                t.localScale = new Vector3(s, s * Rand(0.95f, 1.15f), s);
                t.localRotation = Quaternion.Euler(0f, Rand(-25f, 25f), 0f);
            }
        }

        void BuildPalms(int count)
        {
            var ringA = Palette.Hex("#9A7A55");
            var ringB = Palette.Hex("#86684A");
            var frondIn = Palette.Hex("#2F9A3E");
            var frondOut = Palette.Hex("#45B552");
            var meshes = new Mesh[3];
            for (int v = 0; v < meshes.Length; v++)
            {
                var b = new MeshBuilder();
                // Tronc annelé et courbé : segments décalés selon une parabole.
                const int segments = 8;
                float h = 1.8f + v * 0.35f;
                float bend = 0.3f + v * 0.08f;
                float seg = h / segments;
                Vector3 top = Vector3.zero;
                for (int k = 0; k < segments; k++)
                {
                    float t = (float)k / segments;
                    var at = new Vector3(bend * t * t, k * seg, 0f);
                    b.AddFrustum(at, 0.085f - t * 0.03f, 0.08f - t * 0.03f, seg * 1.02f, k % 2 == 0 ? ringA : ringB, 8);
                    top = at + new Vector3(0f, seg, 0f);
                }
                for (int k = 0; k < 3; k++)
                {
                    float a = k * 2.1f;
                    b.AddSphere(top + new Vector3(Mathf.Cos(a) * 0.07f, -0.06f, Mathf.Sin(a) * 0.07f), 0.06f, Palette.Hex("#6B4A2A"), 8, 6);
                }
                for (int k = 0; k < 8; k++)
                    AddFrond(b, top, Rand(0.8f, 1f), 0.13f, k * 45f + v * 12f + Rand(-8f, 8f), Rand(5f, 25f), Rand(35f, 50f), frondIn, frondOut);
                meshes[v] = b.Build("Palmier " + v);
            }
            for (int i = 0; i < count; i++)
            {
                var t = Place(GroupJungle, "Palmier", meshes[i % meshes.Length], _jungle, true, Rand(4f, 13f), 1.6f, i, count);
                float s = Rand(0.9f, 1.3f);
                t.localScale = new Vector3(s, s, s);
                t.localRotation = Quaternion.Euler(0f, Rand(0f, 360f), 0f);
            }
        }

        void BuildFerns(int count)
        {
            var meshes = new Mesh[3];
            for (int v = 0; v < meshes.Length; v++)
            {
                var b = new MeshBuilder();
                int fronds = 8 + v;
                for (int k = 0; k < fronds; k++)
                    AddFrond(b, Vector3.zero, Rand(0.45f, 0.62f), 0.08f, k * 360f / fronds + Rand(-10f, 10f), Rand(40f, 60f), Rand(50f, 70f),
                        Palette.Hex("#2E9A40"), Palette.Hex("#3FB552"));
                if (v > 0)
                {
                    // Hibiscus : cinq pétales autour d'un long pistil jaune.
                    var head = new Vector3(0.05f, 0.42f, -0.12f);
                    var petal = v == 1 ? Palette.Hex("#E8344A") : Palette.Hex("#FF5C9A");
                    b.AddCylinder(new Vector3(0.05f, 0f, -0.12f), 0.01f, 0.42f, Palette.Hex("#3E8A2E"), 5);
                    for (int p = 0; p < 5; p++)
                        AddLeaf(b, head, 0.11f, 0.05f, p * 72f, 25f, petal);
                    b.Append(new MeshBuilder().AddCylinder(Vector3.zero, 0.008f, 0.12f, Palette.Hex("#FFD23F"), 5),
                        Matrix4x4.TRS(head, Quaternion.Euler(-30f, 0f, 0f), Vector3.one));
                }
                meshes[v] = b.Build("Fougère " + v);
            }
            for (int i = 0; i < count; i++)
            {
                var t = Place(GroupJungle, "Fougère", meshes[i % meshes.Length], _jungle, true, Rand(1f, 4f), 0.7f, i, count, y: -0.02f);
                float s = Rand(0.8f, 1.3f);
                t.localScale = new Vector3(s, s, s);
                t.localRotation = Quaternion.Euler(0f, Rand(0f, 360f), 0f);
            }
        }

        // ---------------------------------------------------------------- Ciel

        void BuildRainbow()
        {
            // Arc de sept bandes : segments jointifs le long d'un demi-cercle, légèrement lumineux.
            var b = new MeshBuilder();
            const float radius = 16f;
            const float band = 0.7f;
            const int steps = 60;
            for (int c = 0; c < Palette.Rainbow.Length; c++)
            {
                float r = radius - c * band;
                var color = Lum(Palette.Rainbow[c], 0.45f);
                for (int i = 0; i < steps; i++)
                {
                    float a0 = Mathf.PI * i / steps;
                    float a1 = Mathf.PI * (i + 1) / steps;
                    float mid = (a0 + a1) * 0.5f;
                    var center = new Vector3(Mathf.Cos(mid) * r, Mathf.Sin(mid) * r, 0f);
                    float length = r * (a1 - a0) * 1.08f;
                    b.Append(new MeshBuilder().AddBox(Vector3.zero, new Vector3(band, length, 0.1f), color),
                        Matrix4x4.TRS(center, Quaternion.Euler(0f, 0f, mid * Mathf.Rad2Deg), Vector3.one));
                }
            }
            Place(GroupSky, "Arc-en-ciel", b.Build("Arc-en-ciel"), _islands, false, 85f, radius, 0, 1, y: -2f);
        }

        void BuildIslands(int count)
        {
            var rock = Opaque(Palette.Hex("#8C7D70"));
            var rockDark = Opaque(Palette.Hex("#6E6258"));
            var earth = Opaque(Palette.Hex("#8A6A4A"));
            var grass = Opaque(Palette.Hex("#7ED957"));
            var leaves = Opaque(Palette.Hex("#4BC45A"));
            var trunk = Opaque(Palette.Hex("#8A5A44"));
            var water = Lum(Palette.Hex("#BFEFFF"), 0.5f);
            var mist = Lum(Palette.Hex("#FFFFFF"), 0.3f);
            var meshes = new Mesh[3];
            for (int v = 0; v < meshes.Length; v++)
            {
                var b = new MeshBuilder();
                float r = 0.8f + v * 0.25f;
                // Rocher en cône renversé sous une galette de terre et de gazon.
                b.Append(new MeshBuilder().AddCone(Vector3.zero, r, r * 1.5f, rock, 10),
                    Matrix4x4.TRS(new Vector3(0f, -0.1f, 0f), Quaternion.Euler(180f, 0f, 0f), Vector3.one));
                b.Append(new MeshBuilder().AddCone(Vector3.zero, r * 0.5f, r * 0.9f, rockDark, 8),
                    Matrix4x4.TRS(new Vector3(r * 0.4f, -0.3f, 0.1f), Quaternion.Euler(180f, 0f, 0f), Vector3.one));
                b.AddFrustum(new Vector3(0f, -0.14f, 0f), r * 0.98f, r * 1.02f, 0.14f, earth, 16);
                b.AddEllipsoid(new Vector3(0f, 0.01f, 0f), new Vector3(r * 1.04f, 0.09f, r * 1.04f), grass, 18, 8);
                for (int k = 0; k < 2 + v; k++)
                {
                    var at = new Vector3(Rand(-r, r) * 0.55f, 0f, Rand(-r, r) * 0.45f);
                    float h = Rand(0.25f, 0.45f);
                    b.AddFrustum(at, 0.04f, 0.03f, h, trunk, 6);
                    b.AddSphere(at + new Vector3(0f, h + 0.12f, 0f), Rand(0.16f, 0.24f), leaves, 10, 8);
                }
                if (v != 1)
                {
                    // Cascade qui tombe du bord dans le vide, nuage d'écume en bas.
                    b.AddBox(new Vector3(r * 0.92f, -0.85f, -r * 0.2f), new Vector3(0.14f, 1.7f, 0.05f), water);
                    b.AddEllipsoid(new Vector3(r * 0.92f, -1.7f, -r * 0.2f), new Vector3(0.22f, 0.12f, 0.15f), mist, 10, 6);
                }
                meshes[v] = b.Build("Île " + v);
            }
            for (int i = 0; i < count; i++)
            {
                float z = Rand(14f, 40f);
                var t = Place(GroupSky, "Île", meshes[i % meshes.Length], _islands, false, z, 4.5f, i, count, y: Rand(2.8f, 5.5f) + z * 0.05f);
                float s = Rand(1f, 1.6f) * (0.7f + z * 0.03f);
                t.localScale = new Vector3(s, s, s);
                t.localRotation = Quaternion.Euler(0f, Rand(-30f, 30f), 0f);
            }
        }

        void BuildBalloons(int count)
        {
            Color[][] colors =
            {
                new[] { Palette.Hex("#FF5A5A"), Palette.Hex("#FFD23F") },
                new[] { Palette.Hex("#3EA6FF"), Palette.Hex("#FFFFFF") },
                new[] { Palette.Hex("#9B5CFF"), Palette.Hex("#FF9F1C") },
            };
            var basket = Opaque(Palette.Hex("#8A5A34"));
            var rope = Opaque(Palette.Hex("#5A4636"));
            var meshes = new Mesh[colors.Length];
            for (int v = 0; v < meshes.Length; v++)
            {
                var b = new MeshBuilder();
                var main = Opaque(colors[v][0]);
                var stripe = Opaque(colors[v][1]);
                b.AddEllipsoid(Vector3.zero, new Vector3(0.45f, 0.52f, 0.45f), main, 18, 12);
                b.AddEllipsoid(new Vector3(0f, 0.05f, 0f), new Vector3(0.458f, 0.1f, 0.458f), stripe, 18, 6);
                b.AddEllipsoid(new Vector3(0f, -0.26f, 0f), new Vector3(0.4f, 0.05f, 0.4f), stripe, 18, 6);
                b.AddFrustum(new Vector3(0f, -0.62f, 0f), 0.12f, 0.22f, 0.18f, stripe, 14);
                b.AddBox(new Vector3(0f, -0.86f, 0f), new Vector3(0.16f, 0.12f, 0.16f), basket);
                for (int k = 0; k < 4; k++)
                {
                    float x = (k % 2 == 0 ? -1f : 1f) * 0.07f;
                    float z = (k < 2 ? -1f : 1f) * 0.07f;
                    b.AddCylinder(new Vector3(x, -0.8f, z), 0.006f, 0.19f, rope, 4);
                }
                b.AddSphere(new Vector3(0f, -0.66f, 0f), 0.045f, Lum(Palette.Hex("#FFB13B"), 1f), 8, 6);
                meshes[v] = b.Build("Montgolfière " + v);
            }
            for (int i = 0; i < count; i++)
            {
                float z = Rand(8f, 24f);
                var t = CreateRenderer(Group(GroupSky), "Montgolfière", meshes[i % meshes.Length], _balloons, false).transform;
                float s = Rand(0.8f, 1.2f);
                t.localScale = new Vector3(s, s, s);
                AddProp(t, GroupSky, SpanAt(z, 1f), Rand(3.2f, 5.5f), z, i, count, drift: Rand(-0.12f, 0.12f));
            }
        }

        // ---------------------------------------------------------------- Fond marin

        void BuildShipwreck()
        {
            var b = new MeshBuilder();
            var wood = Palette.Hex("#5A4636");
            var woodDark = Palette.Hex("#3F3128");
            var sail = Palette.Hex("#B8B09A");
            var hull = new MeshBuilder()
                .AddBox(new Vector3(0f, 0.45f, 0f), new Vector3(3.4f, 0.9f, 1.1f), wood)
                .AddBox(new Vector3(0f, 0.92f, 0f), new Vector3(3.5f, 0.08f, 1.15f), woodDark)
                .AddBox(new Vector3(-1.2f, 1.2f, 0f), new Vector3(0.9f, 0.5f, 1f), wood)
                .AddFrustum(new Vector3(1.7f, 0.1f, 0f), 0.55f, 0.1f, 0.9f, wood, 4);
            b.Append(hull, Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 0f, 7f), Vector3.one));
            // Mât brisé, vergue et lambeau de voile.
            b.Append(new MeshBuilder()
                    .AddCylinder(Vector3.zero, 0.06f, 2.2f, woodDark, 8)
                    .AddBox(new Vector3(0f, 1.7f, 0f), new Vector3(1.2f, 0.05f, 0.05f), woodDark)
                    .AddBox(new Vector3(0.15f, 1.3f, 0.03f), new Vector3(0.8f, 0.7f, 0.02f), sail),
                Matrix4x4.TRS(new Vector3(0.2f, 0.85f, 0f), Quaternion.Euler(0f, 0f, -22f), Vector3.one));
            Place(GroupOcean, "Épave", b.Build("Épave"), _ruins, false, 24f, 4f, 0, 1, y: -0.35f);
        }

        void BuildSeaRocks(int count)
        {
            var b = new MeshBuilder();
            b.AddEllipsoid(new Vector3(0f, 0.15f, 0f), new Vector3(0.6f, 0.45f, 0.5f), Palette.Hex("#5E7A82"), 14, 10);
            b.AddEllipsoid(new Vector3(0.55f, 0.05f, 0.1f), new Vector3(0.4f, 0.3f, 0.35f), Palette.Hex("#6E8A90"), 12, 8);
            b.AddEllipsoid(new Vector3(-0.5f, 0f, -0.05f), new Vector3(0.35f, 0.22f, 0.3f), Palette.Hex("#52707A"), 12, 8);
            for (int k = 0; k < 6; k++)
                b.AddSphere(new Vector3(Rand(-0.5f, 0.6f), Rand(0.2f, 0.45f), -0.42f), Rand(0.03f, 0.05f), Palette.Hex("#D9D2C0"), 6, 4);
            var mesh = b.Build("Rocher");
            for (int i = 0; i < count; i++)
            {
                float z = Rand(7f, 22f);
                var t = Place(GroupOcean, "Rocher", mesh, _ruins, z < 12f, z, 1.4f, i, count, y: -0.1f);
                float s = Rand(0.9f, 1.8f);
                t.localScale = new Vector3(s, s * Rand(0.8f, 1.2f), s);
                t.localRotation = Quaternion.Euler(0f, Rand(0f, 360f), 0f);
            }
        }

        void BuildKelp(int count)
        {
            Color[] blades = { Palette.Hex("#6E8F2E"), Palette.Hex("#8AA83A"), Palette.Hex("#5C8A34") };
            var stalk = Palette.Hex("#5A7A2A");
            var meshes = new Mesh[3];
            for (int v = 0; v < meshes.Length; v++)
            {
                var b = new MeshBuilder();
                float h = 1.6f + v * 0.6f;
                b.AddCylinder(Vector3.zero, 0.02f, h, stalk, 5);
                int k = 0;
                for (float y = 0.15f; y < h; y += 0.17f, k++)
                {
                    var at = new Vector3(0f, y, 0f);
                    AddLeaf(b, at, Rand(0.26f, 0.36f), 0.06f, (k % 2) * 180f + Rand(-30f, 30f), Rand(35f, 55f), blades[(k + v) % blades.Length]);
                    b.AddSphere(at, 0.025f, Palette.Hex("#C9B23C"), 6, 4);
                }
                meshes[v] = b.Build("Algue " + v);
            }
            for (int i = 0; i < count; i++)
            {
                var t = Place(GroupOcean, "Algue", meshes[i % meshes.Length], _kelp, true, Rand(2f, 14f), 0.5f, i, count);
                float s = Rand(0.85f, 1.3f);
                t.localScale = new Vector3(s, s, s);
                t.localRotation = Quaternion.Euler(0f, Rand(0f, 360f), 0f);
            }
        }

        void BuildCorals(int count)
        {
            var meshes = new Mesh[4];

            // Corail branchu : rameaux inclinés terminés par des pointes claires.
            var staghorn = new MeshBuilder();
            var orange = Lum(Palette.Hex("#FF8A4C"), 0.2f);
            var orangeTip = Lum(Palette.Hex("#FFD0A0"), 0.4f);
            staghorn.AddSphere(Vector3.zero, 0.09f, orange, 10, 6);
            for (int k = 0; k < 7; k++)
            {
                var rotation = Quaternion.Euler(Rand(15f, 45f), k * 51f, 0f);
                float len = Rand(0.22f, 0.36f);
                staghorn.Append(new MeshBuilder().AddFrustum(Vector3.zero, 0.035f, 0.02f, len, orange, 6), Matrix4x4.TRS(Vector3.zero, rotation, Vector3.one));
                staghorn.AddSphere(rotation * new Vector3(0f, len, 0f), 0.026f, orangeTip, 6, 4);
            }
            meshes[0] = staghorn.Build("Corail branchu");

            // Corail cerveau : dôme bosselé.
            var brain = new MeshBuilder();
            var pink = Lum(Palette.Hex("#E07AA8"), 0.15f);
            brain.AddEllipsoid(new Vector3(0f, 0.06f, 0f), new Vector3(0.24f, 0.16f, 0.22f), pink, 16, 10);
            for (int k = 0; k < 10; k++)
            {
                float a = k * 0.63f;
                brain.AddSphere(new Vector3(Mathf.Cos(a) * 0.15f, 0.12f + (k % 3) * 0.02f, Mathf.Sin(a) * 0.13f), 0.06f, Lum(Palette.Hex("#F09AC0"), 0.15f), 8, 6);
            }
            meshes[1] = brain.Build("Corail cerveau");

            // Gorgone : éventail violet sur un court pied.
            var fan = new MeshBuilder();
            var violet = Lum(Palette.Hex("#B04ADF"), 0.3f);
            fan.AddCylinder(Vector3.zero, 0.02f, 0.12f, violet, 6);
            fan.AddEllipsoid(new Vector3(0f, 0.38f, 0f), new Vector3(0.3f, 0.28f, 0.015f), violet, 16, 10);
            fan.AddEllipsoid(new Vector3(0.18f, 0.3f, 0.01f), new Vector3(0.16f, 0.18f, 0.012f), Lum(Palette.Hex("#D07AFF"), 0.35f), 12, 8);
            meshes[2] = fan.Build("Gorgone");

            // Éponges tubulaires jaunes, ouvertes en haut.
            var tubes = new MeshBuilder();
            for (int k = 0; k < 4; k++)
            {
                var at = new Vector3((k - 1.5f) * 0.09f, 0f, (k % 2) * 0.07f);
                float h = Rand(0.18f, 0.38f);
                tubes.AddCylinder(at, 0.045f, h, Lum(Palette.Hex("#FFC93C"), 0.2f), 10, caps: false);
                tubes.AddDisc(at + new Vector3(0f, h - 0.03f, 0f), 0.04f, Vector3.up, Opaque(Palette.Hex("#7A4A1A")), 10);
            }
            meshes[3] = tubes.Build("Éponges");

            for (int i = 0; i < count; i++)
            {
                var t = Place(GroupOcean, "Corail", meshes[i % meshes.Length], _reef, true, Rand(0.9f, 4.5f), 0.5f, i, count, y: -0.02f);
                float s = Rand(0.9f, 1.6f);
                t.localScale = new Vector3(s, s, s);
                t.localRotation = Quaternion.Euler(0f, Rand(0f, 360f), 0f);
            }
        }

        void BuildTreasure()
        {
            var b = new MeshBuilder();
            var wood = Opaque(Palette.Hex("#7A4A26"));
            var band = Opaque(Palette.Hex("#B8862E"));
            var gold = Lum(Palette.Hex("#FFC93C"), 0.8f);
            b.AddBox(new Vector3(0f, 0.1f, 0f), new Vector3(0.36f, 0.2f, 0.24f), wood);
            b.AddBox(new Vector3(-0.1f, 0.1f, 0f), new Vector3(0.03f, 0.21f, 0.25f), band);
            b.AddBox(new Vector3(0.1f, 0.1f, 0f), new Vector3(0.03f, 0.21f, 0.25f), band);
            // Couvercle ouvert, charnière à l'arrière ; pièces d'or qui débordent.
            b.Append(new MeshBuilder().AddBox(new Vector3(0f, 0.03f, -0.12f), new Vector3(0.36f, 0.06f, 0.24f), wood),
                Matrix4x4.TRS(new Vector3(0f, 0.2f, 0.12f), Quaternion.Euler(-65f, 0f, 0f), Vector3.one));
            b.AddEllipsoid(new Vector3(0f, 0.2f, 0f), new Vector3(0.16f, 0.05f, 0.1f), gold, 12, 6);
            for (int k = 0; k < 7; k++)
                b.AddEllipsoid(new Vector3(Rand(-0.35f, 0.35f), 0.008f, Rand(-0.25f, -0.12f)), new Vector3(0.025f, 0.006f, 0.025f), gold, 8, 4);
            // Deux étoiles de mer posées sur le sable.
            b.Append(new MeshBuilder().AddStar(Vector3.zero, 0.09f, 0.035f, 0.02f, Opaque(Palette.Hex("#FF7A45"))),
                Matrix4x4.TRS(new Vector3(0.5f, 0.01f, -0.1f), Quaternion.Euler(90f, 20f, 0f), Vector3.one));
            b.Append(new MeshBuilder().AddStar(Vector3.zero, 0.07f, 0.028f, 0.02f, Opaque(Palette.Hex("#E8344A"))),
                Matrix4x4.TRS(new Vector3(-0.55f, 0.01f, 0.05f), Quaternion.Euler(90f, -40f, 0f), Vector3.one));
            Place(GroupOcean, "Trésor", b.Build("Trésor"), _reef, true, 2.4f, 0.7f, 0, 1);
        }

        void BuildJellyfish(int count)
        {
            Color[] tints = { Palette.Hex("#FF8AD8"), Palette.Hex("#7FE6FF") };
            var meshes = new Mesh[tints.Length];
            for (int v = 0; v < meshes.Length; v++)
            {
                var b = new MeshBuilder();
                b.AddEllipsoid(Vector3.zero, new Vector3(0.18f, 0.13f, 0.18f), Lum(tints[v], 0.6f), 16, 10);
                b.AddEllipsoid(new Vector3(0f, -0.01f, 0f), new Vector3(0.1f, 0.08f, 0.1f), Lum(Color.Lerp(tints[v], Color.white, 0.6f), 0.9f), 10, 6);
                for (int k = 0; k < 7; k++)
                {
                    float a = k * Mathf.PI * 2f / 7f;
                    float len = Rand(0.3f, 0.5f);
                    b.AddCylinder(new Vector3(Mathf.Cos(a) * 0.11f, -0.02f - len, Mathf.Sin(a) * 0.11f), 0.008f, len, Lum(tints[v], 0.5f), 4);
                }
                meshes[v] = b.Build("Méduse " + v);
            }
            for (int i = 0; i < count; i++)
            {
                float z = Rand(6f, 16f);
                var t = CreateRenderer(Group(GroupOcean), "Méduse", meshes[i % meshes.Length], _reef, false).transform;
                float s = Rand(0.8f, 1.4f);
                t.localScale = new Vector3(s, s, s);
                AddProp(t, GroupOcean, SpanAt(z, 0.5f), Rand(1.4f, 3.8f), z, i, count, drift: Rand(0.05f, 0.18f));
            }
        }

        void BuildFishSchools(int count)
        {
            Color[][] looks =
            {
                new[] { Palette.Hex("#FFD23F"), Palette.Hex("#FFE98A") },     // poisson-chirurgien jaune
                new[] { Palette.Hex("#FF7A1A"), Palette.Hex("#FFFFFF") },     // poisson-clown
                new[] { Palette.Hex("#3E7BFF"), Palette.Hex("#FFD23F") },     // demoiselle bleue
            };
            var eye = Palette.Hex("#1A1016");
            var meshes = new Mesh[looks.Length];
            for (int v = 0; v < meshes.Length; v++)
            {
                var b = new MeshBuilder();
                var body = looks[v][0];
                var accent = looks[v][1];
                int fish = 6 + v;
                for (int k = 0; k < fish; k++)
                {
                    // Banc : poissons décalés, tête vers +x.
                    var at = new Vector3(Rand(-0.45f, 0.45f), Rand(-0.25f, 0.25f), Rand(-0.25f, 0.25f));
                    float s = Rand(0.8f, 1.15f);
                    var one = new MeshBuilder();
                    one.AddEllipsoid(Vector3.zero, new Vector3(0.09f, 0.055f, 0.03f), body, 12, 8);
                    if (v == 1)
                    {
                        one.AddEllipsoid(new Vector3(0.035f, 0f, 0f), new Vector3(0.012f, 0.057f, 0.032f), accent, 8, 6);
                        one.AddEllipsoid(new Vector3(-0.035f, 0f, 0f), new Vector3(0.012f, 0.05f, 0.03f), accent, 8, 6);
                    }
                    one.Append(new MeshBuilder().AddEllipsoid(new Vector3(-0.03f, 0f, 0f), new Vector3(0.035f, 0.022f, 0.008f), v == 2 ? accent : body, 8, 4),
                        Matrix4x4.TRS(new Vector3(-0.08f, 0f, 0f), Quaternion.Euler(0f, 0f, 30f), Vector3.one));
                    one.Append(new MeshBuilder().AddEllipsoid(new Vector3(-0.03f, 0f, 0f), new Vector3(0.035f, 0.022f, 0.008f), v == 2 ? accent : body, 8, 4),
                        Matrix4x4.TRS(new Vector3(-0.08f, 0f, 0f), Quaternion.Euler(0f, 0f, -30f), Vector3.one));
                    one.AddEllipsoid(new Vector3(-0.005f, 0.05f, 0f), new Vector3(0.04f, 0.025f, 0.006f), accent, 8, 4);
                    one.AddSphere(new Vector3(0.055f, 0.015f, -0.022f), 0.009f, eye, 6, 4);
                    one.AddSphere(new Vector3(0.055f, 0.015f, 0.022f), 0.009f, eye, 6, 4);
                    b.Append(one, Matrix4x4.TRS(at, Quaternion.Euler(0f, Rand(-15f, 15f), Rand(-8f, 8f)), Vector3.one * s));
                }
                meshes[v] = b.Build("Banc de poissons " + v);
            }
            for (int i = 0; i < count; i++)
            {
                float z = Rand(3f, 14f);
                var t = CreateRenderer(Group(GroupOcean), "Banc de poissons", meshes[i % meshes.Length], _fish, false).transform;
                float s = Rand(0.9f, 1.4f);
                // Un banc sur deux remonte le courant (vers la droite), l'autre le suit.
                bool upstream = i % 2 == 0;
                t.localScale = new Vector3(upstream ? s : -s, s, s);
                AddProp(t, GroupOcean, SpanAt(z, 1f), Rand(0.9f, 3.4f), z, i, count, drift: upstream ? Rand(-0.45f, -0.2f) : Rand(0.25f, 0.5f));
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
