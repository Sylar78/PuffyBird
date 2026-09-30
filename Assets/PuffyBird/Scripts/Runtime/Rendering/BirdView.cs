using PuffyBird.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace PuffyBird.Rendering
{
    /// <summary>
    /// L'oiseau en volume : corps dodu, ventre clair, yeux, bec, houppette et deux ailes animées.
    /// Le battement suit la séquence haut → milieu → bas → milieu de la spec (§6.6), en continu ;
    /// les plumes du bout des ailes fléchissent dans le vertex shader. Petits nuages de « puff »
    /// à chaque battement, plumes qui volent à l'impact. Tout est purement visuel : la hitbox
    /// reste le cercle de 11 px de la simulation.
    /// </summary>
    public sealed class BirdView
    {
        const float Yaw = 22f;
        const float WingAmplitude = 45f;
        const int PuffCount = 12;
        const int FeatherCount = 10;

        struct Particle
        {
            public Transform Transform;
            public Vector3 Velocity;
            public Vector3 Spin;
            public float Age;
            public float Life;
            public float Size;
        }

        readonly WorldSpace _space;
        readonly GameConfig _cfg;
        readonly Transform _root;
        readonly Transform _model;
        readonly MeshFilter _bodyFilter;
        readonly Transform _nearWing;
        readonly Transform _farWing;
        readonly MeshFilter _nearWingFilter;
        readonly MeshFilter _farWingFilter;
        readonly Material _bodyMaterial;
        readonly Material _wingMaterial;
        readonly Mesh[] _bodyMeshes = new Mesh[3];
        readonly Mesh[] _wingMeshes = new Mesh[3];
        readonly Particle[] _puffs = new Particle[PuffCount];
        readonly Particle[] _feathers = new Particle[FeatherCount];
        readonly Material _featherMaterial;
        int _nextPuff;
        float _squash;
        bool _shimmering;

        public BirdView(Transform parent, MaterialLibrary materials, WorldSpace space)
        {
            _space = space;
            _cfg = space.Config;
            _root = new GameObject("Oiseau").transform;
            _root.SetParent(parent, false);
            _model = new GameObject("Modèle").transform;
            _model.SetParent(_root, false);

            for (int c = 0; c < 3; c++)
            {
                var colors = Palette.Bird((BirdColor)c);
                _bodyMeshes[c] = BuildBody(colors).Build("Oiseau " + (BirdColor)c);
                _wingMeshes[c] = new MeshBuilder()
                    .AddEllipsoid(new Vector3(-0.02f, 0f, -0.075f), new Vector3(0.095f, 0.028f, 0.075f), Color.Lerp(colors.Body, Color.white, 0.55f), 14, 8)
                    .AddEllipsoid(new Vector3(-0.05f, -0.005f, -0.095f), new Vector3(0.06f, 0.022f, 0.05f), colors.Body, 12, 8)
                    .Build("Aile " + (BirdColor)c);
            }

            _bodyMaterial = materials.Lit("Oiseau", Color.white, 0.45f, 0f, 0.5f);
            _bodyMaterial.SetFloat(MaterialLibrary.BreathStrength, 0.004f);
            _wingMaterial = materials.Lit("Ailes", Color.white, 0.4f, 0f, 0.5f);

            _bodyFilter = CreatePart(_model, "Corps", _bodyMaterial, out _);
            _nearWing = CreatePart(_model, "Aile proche", _wingMaterial, out _nearWingFilter).transform;
            _nearWing.localPosition = new Vector3(-0.01f, 0.01f, -0.105f);
            _farWing = CreatePart(_model, "Aile lointaine", _wingMaterial, out _farWingFilter).transform;
            _farWing.localPosition = new Vector3(-0.01f, 0.01f, 0.105f);
            _farWing.localScale = new Vector3(1f, 1f, -1f);

            var puffMesh = new MeshBuilder().AddSphere(Vector3.zero, 1f, Color.white, 12, 8).Build("Puff");
            var puffMaterial = materials.Lit("Puff", Color.white, 0.1f, 0f, 0.6f);
            puffMaterial.SetColor(MaterialLibrary.EmissionColor, new Color(0.25f, 0.25f, 0.25f));
            for (int i = 0; i < PuffCount; i++) _puffs[i].Transform = CreateParticle(parent, "Puff", puffMesh, puffMaterial);

            var featherMesh = new MeshBuilder().AddEllipsoid(Vector3.zero, new Vector3(1f, 0.25f, 0.45f), Color.white, 10, 6).Build("Plume");
            _featherMaterial = materials.Lit("Plumes", Color.white, 0.3f, 0f, 0.4f);
            for (int i = 0; i < FeatherCount; i++) _feathers[i].Transform = CreateParticle(parent, "Plume", featherMesh, _featherMaterial);

            SetColor(BirdColor.Yellow);
        }

        static MeshBuilder BuildBody(Palette.BirdColors c)
        {
            var b = new MeshBuilder();
            b.AddEllipsoid(Vector3.zero, new Vector3(0.17f, 0.13f, 0.14f), c.Body, 24, 16);
            b.AddEllipsoid(new Vector3(0.035f, -0.045f, 0f), new Vector3(0.125f, 0.085f, 0.122f), c.Belly, 20, 12);
            // Houppette et queue.
            b.AddSphere(new Vector3(0.02f, 0.13f, 0f), 0.035f, c.Shade, 10, 8);
            b.AddSphere(new Vector3(-0.025f, 0.135f, 0.01f), 0.03f, c.Shade, 10, 8);
            b.Append(new MeshBuilder().AddCone(Vector3.zero, 0.055f, 0.09f, c.Shade, 12),
                Matrix4x4.TRS(new Vector3(-0.14f, 0.02f, 0f), Quaternion.Euler(0f, 0f, 100f), Vector3.one));
            // Yeux des deux côtés.
            for (int side = -1; side <= 1; side += 2)
            {
                b.AddSphere(new Vector3(0.09f, 0.05f, 0.1f * side), 0.047f, Palette.White, 14, 10);
                b.AddSphere(new Vector3(0.112f, 0.056f, 0.132f * side), 0.022f, Palette.Pupil, 10, 8);
                b.AddSphere(new Vector3(0.118f, 0.066f, 0.141f * side), 0.007f, Palette.White, 6, 4);
            }
            // Bec en deux parties, orienté vers +x.
            b.Append(new MeshBuilder().AddCone(Vector3.zero, 0.042f, 0.1f, Palette.Beak, 14),
                Matrix4x4.TRS(new Vector3(0.14f, 0.005f, 0f), Quaternion.Euler(0f, 0f, -90f), new Vector3(1f, 1f, 0.9f)));
            b.Append(new MeshBuilder().AddCone(Vector3.zero, 0.03f, 0.075f, Palette.Hex("#D9452B"), 12),
                Matrix4x4.TRS(new Vector3(0.135f, -0.03f, 0f), Quaternion.Euler(0f, 0f, -98f), new Vector3(1f, 1f, 0.8f)));
            return b;
        }

        static MeshFilter CreatePart(Transform parent, string name, Material material, out MeshFilter filter)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            filter = go.AddComponent<MeshFilter>();
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.On;
            return filter;
        }

        static Transform CreateParticle(Transform parent, string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            go.SetActive(false);
            return go.transform;
        }

        public void SetColor(BirdColor color)
        {
            int c = (int)color;
            _bodyFilter.sharedMesh = _bodyMeshes[c];
            _nearWingFilter.sharedMesh = _wingMeshes[c];
            _farWingFilter.sharedMesh = _wingMeshes[c];
            _featherMaterial.SetColor(MaterialLibrary.BaseColor, Palette.Bird(color).Body);
        }

        public Vector3 Position => _root.position;

        /// <summary>
        /// Scintillement léger pendant l'accélération d'une étoile : lueur irisée qui change
        /// doucement de teinte, avec un frémissement rapide, et liseré renforcé.
        /// </summary>
        public void SetBoost(float amount, float time)
        {
            if (amount <= 0f)
            {
                if (!_shimmering) return;
                _shimmering = false;
                SetGlow(Color.black, 0.5f);
                return;
            }
            _shimmering = true;
            float hue = time * 0.7f % 1f;
            float flicker = 0.6f + 0.4f * Mathf.Sin(time * 30f) * Mathf.Sin(time * 11f);
            var glow = Color.HSVToRGB(hue, 0.45f, 1f) * (0.3f * flicker * amount);
            SetGlow(glow, 0.5f + 0.7f * amount);
        }

        void SetGlow(Color emission, float rim)
        {
            _bodyMaterial.SetColor(MaterialLibrary.EmissionColor, emission);
            _wingMaterial.SetColor(MaterialLibrary.EmissionColor, emission);
            _bodyMaterial.SetFloat(MaterialLibrary.RimStrength, rim);
            _wingMaterial.SetFloat(MaterialLibrary.RimStrength, rim);
        }

        public void OnFlap()
        {
            _squash = 1f;
            var origin = _root.position + new Vector3(-0.08f, -0.1f, 0f);
            for (int k = 0; k < 3; k++)
            {
                ref var p = ref _puffs[_nextPuff];
                _nextPuff = (_nextPuff + 1) % PuffCount;
                p.Age = 0f;
                p.Life = Random.Range(0.35f, 0.5f);
                p.Size = Random.Range(0.035f, 0.055f);
                p.Velocity = new Vector3(Random.Range(-0.6f, -0.2f), Random.Range(-0.5f, -0.1f), Random.Range(-0.2f, 0.2f));
                p.Transform.position = origin + Random.insideUnitSphere * 0.03f;
                p.Transform.gameObject.SetActive(true);
            }
        }

        public void OnHit()
        {
            var origin = _root.position;
            for (int i = 0; i < FeatherCount; i++)
            {
                ref var p = ref _feathers[i];
                p.Age = 0f;
                p.Life = Random.Range(0.7f, 1.1f);
                p.Size = Random.Range(0.025f, 0.04f);
                var dir = Random.insideUnitSphere;
                dir.z *= 0.4f;
                p.Velocity = dir.normalized * Random.Range(0.8f, 1.8f) + Vector3.up * 0.8f;
                p.Spin = new Vector3(Random.Range(-600f, 600f), Random.Range(-600f, 600f), Random.Range(-600f, 600f));
                p.Transform.position = origin;
                p.Transform.rotation = Random.rotation;
                p.Transform.gameObject.SetActive(true);
            }
        }

        public void Update(GameSimulation sim, float alpha, float deltaTime)
        {
            var bird = sim.Bird;
            float y = Mathf.LerpUnclamped(bird.PrevY, bird.Y, alpha) + _cfg.BirdHeight * 0.5f;
            _root.localPosition = _space.ToWorld(_cfg.BirdCenterX, y, 0f);
            _root.localRotation = Quaternion.Euler(0f, 0f, bird.DisplayRotation(_cfg)) * Quaternion.Euler(0f, Yaw, 0f);

            // Étirement au battement puis retour : la silhouette reste plus petite que la hitbox n'est grande.
            _squash = Mathf.Max(0f, _squash - deltaTime * 6f);
            float s = Mathf.Sin(_squash * Mathf.PI) * 0.12f;
            _model.localScale = new Vector3(1f - s * 0.5f, 1f + s, 1f - s * 0.5f);

            // Ailes : phase continue calée sur la séquence d'images de la spec, figées à la mort.
            float phase = bird.WingPhase(_cfg);
            float angle = WingAmplitude * Mathf.Cos(phase * Mathf.PI * 2f);
            _nearWing.localRotation = Quaternion.Euler(angle, 0f, 0f);
            _farWing.localRotation = Quaternion.Euler(-angle, 0f, 0f);
            bool flapping = sim.State != GameState.Dying && sim.State != GameState.Over;
            _wingMaterial.SetFloat(MaterialLibrary.BendAmount, flapping ? 2.5f * Mathf.Sin(phase * Mathf.PI * 2f) : 0f);

            UpdateParticles(_puffs, deltaTime, gravity: 0f, drag: 3f, grow: true);
            UpdateParticles(_feathers, deltaTime, gravity: -3.5f, drag: 1.5f, grow: false);
        }

        static void UpdateParticles(Particle[] particles, float dt, float gravity, float drag, bool grow)
        {
            for (int i = 0; i < particles.Length; i++)
            {
                ref var p = ref particles[i];
                if (!p.Transform.gameObject.activeSelf) continue;
                p.Age += dt;
                if (p.Age >= p.Life)
                {
                    p.Transform.gameObject.SetActive(false);
                    continue;
                }
                p.Velocity += Vector3.up * gravity * dt;
                p.Velocity *= Mathf.Max(0f, 1f - drag * dt);
                p.Transform.position += p.Velocity * dt;
                if (p.Spin != Vector3.zero) p.Transform.Rotate(p.Spin * dt, Space.Self);
                float t = p.Age / p.Life;
                float size = grow ? p.Size * Mathf.Sin(Mathf.Min(1f, t * 1.2f) * Mathf.PI) * 1.6f : p.Size * (1f - t * t);
                p.Transform.localScale = new Vector3(size, size, size);
            }
        }
    }
}
