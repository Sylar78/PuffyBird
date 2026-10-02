using PuffyBird.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace PuffyBird.Rendering
{
    /// <summary>
    /// Météo des décors : pluie battante (orage), flocons (neige), pétales de cerisier (Japon),
    /// feuilles tropicales (jungle), bulles qui remontent (fond marin) et éclairs de l'orage (zigzag lumineux au loin + lueur sur tout le décor).
    /// Les particules tombent derrière le plan de jeu : elles ne passent jamais devant un tuyau
    /// ni devant l'oiseau. Pools fixes créés au chargement, emportés par le défilement du monde.
    /// </summary>
    public sealed class WeatherView
    {
        const int RainCount = 80;
        const int SnowCount = 70;
        const int PetalCount = 36;
        const int LeafCount = 24;
        const int BubbleCount = 48;
        const float NearZ = 0.6f;
        const float FarZ = 11f;
        const float SpawnFov = 40f;
        const float BoltZ = 50f;

        struct Flake
        {
            public Transform Transform;
            public Vector3 Velocity;
            public float Phase;
            public float Spin;
        }

        readonly WorldSpace _space;
        readonly Flake[] _rain = new Flake[RainCount];
        readonly Flake[] _snow = new Flake[SnowCount];
        readonly Flake[] _petals = new Flake[PetalCount];
        readonly Flake[] _leaves = new Flake[LeafCount];
        readonly Flake[] _bubbles = new Flake[BubbleCount];
        readonly Transform[] _bolts = new Transform[3];
        Palette.Weather _weather;
        bool _lightning;
        float _rainTilt;
        double _lastScroll = double.NaN;
        float _nextStrike;
        float _strikeAge = -1f;
        int _bolt;

        /// <summary>Lueur de l'éclair en cours, de 0 (rien) à 1 (plein flash).</summary>
        public float Flash { get; private set; }

        public WeatherView(Transform parent, MaterialLibrary materials, WorldSpace space)
        {
            _space = space;
            var root = new GameObject("Météo").transform;
            root.SetParent(parent, false);

            var rainMesh = new MeshBuilder().AddBox(Vector3.zero, new Vector3(0.006f, 0.17f, 0.006f), Palette.Hex("#C8D4E4")).Build("Goutte");
            var rainMaterial = materials.Lit("Pluie", Color.white, 0.8f, 0f, 0.3f);
            rainMaterial.SetFloat(MaterialLibrary.VertexEmission, 0.45f);
            for (int i = 0; i < RainCount; i++) _rain[i].Transform = Create(root, "Goutte", rainMesh, rainMaterial);

            var snowMesh = new MeshBuilder().AddSphere(Vector3.zero, 1f, Color.white, 8, 6).Build("Flocon");
            var snowMaterial = materials.Lit("Flocons", Color.white, 0.5f, 0f, 0.4f);
            snowMaterial.SetFloat(MaterialLibrary.VertexEmission, 0.35f);
            for (int i = 0; i < SnowCount; i++) _snow[i].Transform = Create(root, "Flocon", snowMesh, snowMaterial);

            var petalMeshes = new[]
            {
                new MeshBuilder().AddEllipsoid(Vector3.zero, new Vector3(1f, 0.3f, 0.65f), Palette.Hex("#FFB7C9"), 8, 5).Build("Pétale"),
                new MeshBuilder().AddEllipsoid(Vector3.zero, new Vector3(1f, 0.3f, 0.65f), Palette.Hex("#FFD6E0"), 8, 5).Build("Pétale clair"),
            };
            var petalMaterial = materials.Lit("Pétales", Color.white, 0.4f, 0f, 0.5f);
            petalMaterial.SetFloat(MaterialLibrary.VertexEmission, 0.15f);
            for (int i = 0; i < PetalCount; i++) _petals[i].Transform = Create(root, "Pétale", petalMeshes[i % 2], petalMaterial);

            var leafMeshes = new[]
            {
                BuildLeaf(Palette.Hex("#3FAE4A"), Palette.Hex("#2A7A36")).Build("Feuille"),
                BuildLeaf(Palette.Hex("#8BCB3C"), Palette.Hex("#5A9A2A")).Build("Feuille claire"),
                BuildLeaf(Palette.Hex("#E3B23C"), Palette.Hex("#B07A22")).Build("Feuille sèche"),
            };
            var leafMaterial = materials.Lit("Feuilles", Color.white, 0.45f, 0f, 0.4f);
            for (int i = 0; i < LeafCount; i++) _leaves[i].Transform = Create(root, "Feuille", leafMeshes[i % leafMeshes.Length], leafMaterial);

            // Bulles : sphère claire, reflet brillant ; l'émission les garde visibles dans le bleu.
            var bubbleMesh = new MeshBuilder()
                .AddSphere(Vector3.zero, 1f, Palette.Hex("#D8F6FF"), 10, 8)
                .AddSphere(new Vector3(-0.35f, 0.4f, -0.6f), 0.28f, Color.white, 6, 4)
                .Build("Bulle");
            var bubbleMaterial = materials.Lit("Bulles", Color.white, 0.95f, 0f, 1f);
            bubbleMaterial.SetFloat(MaterialLibrary.VertexEmission, 0.5f);
            for (int i = 0; i < BubbleCount; i++) _bubbles[i].Transform = Create(root, "Bulle", bubbleMesh, bubbleMaterial);

            var boltMaterial = materials.Lit("Éclair", Color.white, 0f, 0f, 0f);
            boltMaterial.SetFloat(MaterialLibrary.VertexEmission, 3f);
            boltMaterial.SetColor(MaterialLibrary.EmissionColor, Palette.Hex("#AFC4FF") * 2f);
            var random = new System.Random(7);
            for (int i = 0; i < _bolts.Length; i++) _bolts[i] = Create(root, "Éclair", BuildBolt(random).Build("Éclair " + i), boltMaterial);
        }

        static Transform Create(Transform parent, string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.SetActive(false);
            return go.transform;
        }

        /// <summary>Feuille tropicale : limbe allongé et nervure centrale plus sombre.</summary>
        static MeshBuilder BuildLeaf(Color blade, Color rib)
        {
            return new MeshBuilder()
                .AddEllipsoid(Vector3.zero, new Vector3(1f, 0.12f, 0.42f), blade, 10, 5)
                .AddBox(new Vector3(0.1f, 0.1f, 0f), new Vector3(1.9f, 0.06f, 0.06f), rib);
        }

        /// <summary>Éclair ramifié : segments fins en zigzag, du haut (y = 0) vers le bas.</summary>
        static MeshBuilder BuildBolt(System.Random random)
        {
            var b = new MeshBuilder();
            var color = Palette.Hex("#EEF3FF");
            var p = Vector3.zero;
            int branchAt = 3 + random.Next(3);
            for (int i = 0; p.y > -12f; i++)
            {
                var next = p + new Vector3((float)(random.NextDouble() - 0.5) * 1.6f, -0.8f - (float)random.NextDouble() * 0.8f, 0f);
                AddSegment(b, p, next, 0.09f, color);
                if (i == branchAt)
                {
                    var q = p;
                    for (int k = 0; k < 4; k++)
                    {
                        var n = q + new Vector3(0.5f + (float)random.NextDouble() * 0.6f, -0.6f - (float)random.NextDouble() * 0.6f, 0f);
                        AddSegment(b, q, n, 0.05f, color);
                        q = n;
                    }
                }
                p = next;
            }
            return b;
        }

        static void AddSegment(MeshBuilder b, Vector3 from, Vector3 to, float thickness, Color color)
        {
            var d = to - from;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            b.Append(new MeshBuilder().AddBox(Vector3.zero, new Vector3(d.magnitude + thickness, thickness, thickness), color),
                Matrix4x4.TRS((from + to) * 0.5f, Quaternion.Euler(0f, 0f, angle), Vector3.one));
        }

        public void ApplyTheme(Palette.ThemeColors colors)
        {
            _weather = colors.Weather;
            _lightning = colors.Lightning;
            SetActive(_rain, _weather == Palette.Weather.Rain);
            SetActive(_snow, _weather == Palette.Weather.Snow);
            SetActive(_petals, _weather == Palette.Weather.Petals);
            SetActive(_leaves, _weather == Palette.Weather.Leaves);
            SetActive(_bubbles, _weather == Palette.Weather.Bubbles);
            for (int i = 0; i < _bolts.Length; i++) _bolts[i].gameObject.SetActive(false);
            Flash = 0f;
            _strikeAge = -1f;
            _nextStrike = Random.Range(2f, 4f);

            // Pluie poussée par le vent : les gouttes penchent dans le sens de leur chute.
            var fall = new Vector2(-2.2f, -9f);
            _rainTilt = Mathf.Atan2(fall.x, -fall.y) * Mathf.Rad2Deg;
            Scatter(_rain, anywhere: true);
            Scatter(_snow, anywhere: true);
            Scatter(_petals, anywhere: true);
            Scatter(_leaves, anywhere: true);
            Scatter(_bubbles, anywhere: true);
        }

        static void SetActive(Flake[] flakes, bool active)
        {
            for (int i = 0; i < flakes.Length; i++) flakes[i].Transform.gameObject.SetActive(active);
        }

        void Scatter(Flake[] flakes, bool anywhere)
        {
            if (!flakes[0].Transform.gameObject.activeSelf) return;
            for (int i = 0; i < flakes.Length; i++) Respawn(ref flakes[i], anywhere);
        }

        /// <summary>Nouvelle particule en haut du champ (ou n'importe où au premier affichage).</summary>
        void Respawn(ref Flake f, bool anywhere)
        {
            float z = Random.Range(NearZ, FarZ);
            float halfWidth = CameraRig.HalfWidthAt(z, SpawnFov, CameraRig.MaxAspect) + 0.5f;
            float top = TopAt(z);
            bool rising = _weather == Palette.Weather.Bubbles;
            float y = anywhere ? Random.Range(0f, top) : rising ? -Random.Range(0f, 0.6f) : top + Random.Range(0f, 1f);
            var t = f.Transform;
            t.localPosition = new Vector3(Random.Range(-halfWidth, halfWidth * 1.3f), y, z);
            f.Phase = Random.Range(0f, 100f);
            switch (_weather)
            {
                case Palette.Weather.Rain:
                    f.Velocity = new Vector3(-2.2f, -9f, 0f) * Random.Range(0.85f, 1.15f);
                    t.localRotation = Quaternion.Euler(0f, 0f, _rainTilt);
                    t.localScale = new Vector3(1f, Random.Range(0.7f, 1.2f), 1f);
                    break;
                case Palette.Weather.Snow:
                    f.Velocity = new Vector3(-0.15f, -Random.Range(0.35f, 0.7f), 0f);
                    float s = Random.Range(0.012f, 0.028f);
                    t.localScale = new Vector3(s, s, s);
                    break;
                case Palette.Weather.Bubbles:
                    // Les grosses bulles montent plus vite que les petites.
                    float b = Random.Range(0.008f, 0.026f);
                    f.Velocity = new Vector3(0f, 0.35f + b * 30f, 0f) * Random.Range(0.85f, 1.15f);
                    f.Spin = 0f;
                    t.localScale = new Vector3(b, b, b);
                    t.localRotation = Quaternion.identity;
                    break;
                case Palette.Weather.Leaves:
                    f.Velocity = new Vector3(-Random.Range(0.2f, 0.45f), -Random.Range(0.35f, 0.6f), 0f);
                    f.Spin = Random.Range(-160f, 160f);
                    float l = Random.Range(0.03f, 0.05f);
                    t.localScale = new Vector3(l, l, l);
                    t.localRotation = Random.rotation;
                    break;
                default:
                    f.Velocity = new Vector3(-Random.Range(0.25f, 0.55f), -Random.Range(0.28f, 0.5f), 0f);
                    f.Spin = Random.Range(-240f, 240f);
                    float p = Random.Range(0.018f, 0.028f);
                    t.localScale = new Vector3(p, p, p);
                    t.localRotation = Random.rotation;
                    break;
            }
        }

        float TopAt(float z)
        {
            return _space.CameraPosition.y + (z + WorldSpace.CameraDistance) * Mathf.Tan(SpawnFov * 0.5f * Mathf.Deg2Rad) + 0.3f;
        }

        /// <param name="scrollPx">Distance défilée en px logiques (interpolée).</param>
        public void Update(double scrollPx, float deltaTime, float time)
        {
            float scroll = double.IsNaN(_lastScroll) ? 0f : (float)((scrollPx - _lastScroll) / WorldSpace.PixelsPerUnit);
            _lastScroll = scrollPx;
            if (scroll < 0f || scroll > 1f) scroll = 0f; // nouvelle partie : la distance repart de zéro

            switch (_weather)
            {
                case Palette.Weather.Rain: Fall(_rain, scroll, deltaTime, time, sway: 0f); break;
                case Palette.Weather.Snow: Fall(_snow, scroll, deltaTime, time, sway: 0.25f); break;
                case Palette.Weather.Petals: Fall(_petals, scroll, deltaTime, time, sway: 0.35f); break;
                case Palette.Weather.Leaves: Fall(_leaves, scroll, deltaTime, time, sway: 0.5f); break;
                case Palette.Weather.Bubbles: Fall(_bubbles, scroll, deltaTime, time, sway: 0.18f); break;
            }
            if (_lightning) UpdateLightning(deltaTime);
        }

        void Fall(Flake[] flakes, float scroll, float dt, float time, float sway)
        {
            for (int i = 0; i < flakes.Length; i++)
            {
                ref var f = ref flakes[i];
                var t = f.Transform;
                var p = t.localPosition;
                p += f.Velocity * dt;
                p.x -= scroll;
                if (sway > 0f) p.x += Mathf.Sin(time * 1.3f + f.Phase) * sway * dt;
                if (f.Spin != 0f) t.Rotate(f.Spin * dt, f.Spin * 0.6f * dt, 0f, Space.Self);
                float halfWidth = CameraRig.HalfWidthAt(p.z, SpawnFov, CameraRig.MaxAspect) + 0.6f;
                bool gone = f.Velocity.y > 0f ? p.y > TopAt(p.z) + 0.5f : p.y < 0f;
                if (gone || p.x < -halfWidth)
                {
                    Respawn(ref f, anywhere: false);
                    continue;
                }
                t.localPosition = p;
            }
        }

        void UpdateLightning(float dt)
        {
            if (_strikeAge < 0f)
            {
                _nextStrike -= dt;
                if (_nextStrike > 0f) return;
                _strikeAge = 0f;
                _bolt = Random.Range(0, _bolts.Length);
                var bolt = _bolts[_bolt];
                float halfWidth = CameraRig.HalfWidthAt(BoltZ, SpawnFov, CameraRig.MaxAspect) * 0.8f;
                bolt.localPosition = new Vector3(Random.Range(-halfWidth, halfWidth), 15f, BoltZ);
                bolt.localScale = new Vector3(Random.value < 0.5f ? -1f : 1f, 1f, 1f);
                bolt.gameObject.SetActive(true);
            }

            _strikeAge += dt;
            // Double éclair : un premier flash, un creux, un second, puis la lueur s'éteint.
            float a = _strikeAge;
            Flash = a < 0.07f ? 1f : a < 0.13f ? 0.25f : a < 0.2f ? 0.85f : Mathf.Max(0f, 0.85f * (1f - (a - 0.2f) / 0.45f));
            _bolts[_bolt].gameObject.SetActive(a < 0.07f || (a >= 0.13f && a < 0.26f));
            if (a > 0.65f)
            {
                Flash = 0f;
                _strikeAge = -1f;
                _nextStrike = Random.Range(3.5f, 8f);
                _bolts[_bolt].gameObject.SetActive(false);
            }
        }
    }
}
