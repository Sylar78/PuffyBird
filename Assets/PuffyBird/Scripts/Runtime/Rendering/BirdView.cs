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
        const int SparkleCount = 14;
        const float SparkleRate = 22f;
        /// <summary>Aperçu du menu des oiseaux : centre (px logiques) et agrandissement.</summary>
        const float PreviewX = 144f;
        const float PreviewY = 182f;
        const float PreviewScale = 2.6f;

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
        readonly Material[] _plumage;
        readonly Mesh[] _bodyMeshes = new Mesh[Skins.Count];
        readonly Mesh[] _wingMeshes = new Mesh[Skins.Count];
        readonly Palette.SkinLook[] _looks = new Palette.SkinLook[Skins.Count];
        readonly Mesh _goldBody;
        readonly Mesh _goldWing;
        readonly Particle[] _sparkles = new Particle[SparkleCount];
        readonly Particle[] _puffs = new Particle[PuffCount];
        readonly Particle[] _feathers = new Particle[FeatherCount];
        readonly Material _featherMaterial;
        int _nextPuff;
        int _nextSparkle;
        float _sparkleAccumulator;
        float _squash;
        bool _golden;
        int _skin;
        bool _previewing;
        float _preview;

        public BirdView(Transform parent, MaterialLibrary materials, WorldSpace space)
        {
            _space = space;
            _cfg = space.Config;
            _root = new GameObject("Oiseau").transform;
            _root.SetParent(parent, false);
            _model = new GameObject("Modèle").transform;
            _model.SetParent(_root, false);

            for (int i = 0; i < Skins.Count; i++)
            {
                var skin = Skins.Get(i);
                _looks[i] = Palette.Skin(skin.Id);
                _bodyMeshes[i] = BuildBody(_looks[i].Colors, _looks[i].Accessory).Build("Oiseau " + skin.Id);
                _wingMeshes[i] = BuildWing(_looks[i].Colors).Build("Aile " + skin.Id);
            }
            _goldBody = BuildBody(Palette.GoldBird, Palette.Accessory.None).Build("Oiseau doré");
            _goldWing = BuildWing(Palette.GoldBird).Build("Aile dorée");

            _bodyMaterial = materials.Lit("Oiseau", Color.white, 0.45f, 0f, 0.5f);
            _bodyMaterial.SetFloat(MaterialLibrary.BreathStrength, 0.004f);
            _wingMaterial = materials.Lit("Ailes", Color.white, 0.4f, 0f, 0.5f);
            _plumage = new[] { _bodyMaterial, _wingMaterial };
            foreach (var m in _plumage)
            {
                m.SetFloat(MaterialLibrary.GlitterScale, 70f);
                m.SetColor(MaterialLibrary.GlitterColor, Palette.Glitter);
            }

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

            var sparkleMesh = new MeshBuilder().AddStar(Vector3.zero, 1f, 0.28f, 0.25f, Palette.Glitter, 4).Build("Éclat");
            var sparkleMaterial = materials.Lit("Éclats", Color.white, 0.2f, 0f, 0f);
            sparkleMaterial.SetFloat(MaterialLibrary.VertexEmission, 2.6f);
            for (int i = 0; i < SparkleCount; i++) _sparkles[i].Transform = CreateParticle(parent, "Éclat", sparkleMesh, sparkleMaterial);

            SetSkin(0);
        }

        static MeshBuilder BuildWing(Palette.BirdColors colors)
        {
            return new MeshBuilder()
                .AddEllipsoid(new Vector3(-0.02f, 0f, -0.075f), new Vector3(0.095f, 0.028f, 0.075f), Color.Lerp(colors.Body, Color.white, 0.55f), 14, 8)
                .AddEllipsoid(new Vector3(-0.05f, -0.005f, -0.095f), new Vector3(0.06f, 0.022f, 0.05f), colors.Body, 12, 8);
        }

        static MeshBuilder BuildBody(Palette.BirdColors c, Palette.Accessory accessory)
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
            AddAccessory(b, accessory, c);
            return b;
        }

        static void AddAccessory(MeshBuilder b, Palette.Accessory accessory, Palette.BirdColors c)
        {
            switch (accessory)
            {
                case Palette.Accessory.Sunglasses:
                {
                    var lens = Palette.Hex("#1B1B26");
                    for (int side = -1; side <= 1; side += 2)
                    {
                        b.AddEllipsoid(new Vector3(0.105f, 0.058f, 0.128f * side), new Vector3(0.05f, 0.036f, 0.016f), lens, 14, 8);
                        // Reflet sur le verre.
                        b.AddEllipsoid(new Vector3(0.118f, 0.07f, 0.142f * side), new Vector3(0.016f, 0.008f, 0.004f), Palette.White, 8, 4);
                    }
                    // Monture : barre sur le haut de la tête.
                    b.AddEllipsoid(new Vector3(0.07f, 0.085f, 0f), new Vector3(0.09f, 0.012f, 0.148f), lens, 16, 6);
                    break;
                }
                case Palette.Accessory.Crown:
                {
                    var gold = Palette.Hex("#F3C22B");
                    var origin = new Vector3(0f, 0.125f, 0f);
                    b.AddFrustum(origin, 0.05f, 0.058f, 0.04f, gold, 16);
                    for (int k = 0; k < 5; k++)
                    {
                        float a = k * Mathf.PI * 2f / 5f;
                        var tip = origin + new Vector3(Mathf.Cos(a) * 0.052f, 0.052f, Mathf.Sin(a) * 0.052f);
                        b.AddSphere(tip, 0.011f, gold, 8, 6);
                    }
                    b.AddSphere(origin + new Vector3(0.056f, 0.02f, 0f), 0.012f, Palette.Hex("#E2457A"), 8, 6);
                    break;
                }
                case Palette.Accessory.Headband:
                {
                    var red = Palette.Hex("#E23E3E");
                    b.AddEllipsoid(new Vector3(0f, 0.065f, 0f), new Vector3(0.158f, 0.026f, 0.132f), red, 24, 8);
                    // Pans du bandeau qui flottent derrière la tête.
                    b.Append(new MeshBuilder().AddEllipsoid(Vector3.zero, new Vector3(0.06f, 0.012f, 0.02f), red, 10, 6),
                        Matrix4x4.TRS(new Vector3(-0.19f, 0.08f, 0.02f), Quaternion.Euler(0f, 0f, 18f), Vector3.one));
                    b.Append(new MeshBuilder().AddEllipsoid(Vector3.zero, new Vector3(0.05f, 0.011f, 0.018f), red, 10, 6),
                        Matrix4x4.TRS(new Vector3(-0.18f, 0.05f, -0.02f), Quaternion.Euler(0f, 0f, -12f), Vector3.one));
                    break;
                }
                case Palette.Accessory.Antenna:
                    b.AddCylinder(new Vector3(0.01f, 0.12f, 0f), 0.008f, 0.075f, c.Shade, 8);
                    b.AddSphere(new Vector3(0.01f, 0.2f, 0f), 0.018f, Palette.Hex("#FF4D6D"), 10, 8);
                    // Rivets sur les joues.
                    for (int side = -1; side <= 1; side += 2) b.AddSphere(new Vector3(0.02f, -0.01f, 0.136f * side), 0.012f, c.Shade, 8, 6);
                    break;
                case Palette.Accessory.FlameCrest:
                {
                    var flame = Palette.Hex("#FFD23F");
                    for (int k = 0; k < 3; k++)
                    {
                        float x = 0.03f - k * 0.04f;
                        float h = 0.09f - k * 0.015f;
                        b.Append(new MeshBuilder().AddCone(Vector3.zero, 0.028f, h, k == 1 ? c.Belly : flame, 10),
                            Matrix4x4.TRS(new Vector3(x, 0.115f, 0f), Quaternion.Euler(0f, 0f, 20f + k * 12f), Vector3.one));
                    }
                    break;
                }
            }
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

        /// <summary>Oiseau du catalogue (<see cref="Skins"/>) : purement visuel, la hitbox ne change pas.</summary>
        public void SetSkin(int index)
        {
            _skin = Mathf.Clamp(index, 0, Skins.Count - 1);
            _golden = false;
            var look = _looks[_skin];
            ApplyMeshes(_bodyMeshes[_skin], _wingMeshes[_skin]);
            SetFinish(look.Emission, look.Glitter, look.Smoothness, look.Metallic, look.Rim, look.RimStrength);
            _featherMaterial.SetColor(MaterialLibrary.BaseColor, look.Colors.Body);
        }

        /// <summary>Menu des oiseaux ouvert : l'oiseau vient au centre de l'écran, agrandi, et tourne doucement.</summary>
        public void SetPreview(bool previewing) => _previewing = previewing;

        void ApplyMeshes(Mesh body, Mesh wing)
        {
            _bodyFilter.sharedMesh = body;
            _nearWingFilter.sharedMesh = wing;
            _farWingFilter.sharedMesh = wing;
        }

        public Vector3 Position => _root.position;

        /// <summary>
        /// Pendant l'accélération d'une étoile, l'oiseau devient jaune doré brillant et pailleté :
        /// plumage or lustré, paillettes qui scintillent sur tout le corps (shader), lueur dorée
        /// qui pulse et petits éclats en étoile autour de lui. Il reprend sa couleur à la fin.
        /// </summary>
        public void SetBoost(float amount, float time, float deltaTime)
        {
            bool golden = amount > 0.02f;
            if (golden != _golden)
            {
                _golden = golden;
                if (golden) ApplyMeshes(_goldBody, _goldWing);
                else SetSkin(_skin);
            }
            if (golden)
            {
                float pulse = 0.75f + 0.25f * Mathf.Sin(time * 9f);
                SetFinish(Palette.GoldGlow * (0.22f * pulse * amount), 1.4f * amount, 0.85f, 0.3f, Palette.Glitter, 0.5f + 0.8f * amount);
                _featherMaterial.SetColor(MaterialLibrary.BaseColor, Palette.GoldBird.Body);

                _sparkleAccumulator += SparkleRate * amount * deltaTime;
                while (_sparkleAccumulator >= 1f)
                {
                    _sparkleAccumulator -= 1f;
                    EmitSparkle();
                }
            }
            else
            {
                _sparkleAccumulator = 0f;
            }
            UpdateSparkles(deltaTime);
        }

        void SetFinish(Color emission, float glitter, float smoothness, float metallic, Color rimColor, float rim)
        {
            foreach (var m in _plumage)
            {
                m.SetColor(MaterialLibrary.EmissionColor, emission);
                m.SetFloat(MaterialLibrary.Glitter, glitter);
                m.SetFloat(MaterialLibrary.Smoothness, smoothness);
                m.SetFloat(MaterialLibrary.Metallic, metallic);
                m.SetColor(MaterialLibrary.RimColor, rimColor);
                m.SetFloat(MaterialLibrary.RimStrength, rim);
            }
        }

        void EmitSparkle()
        {
            ref var p = ref _sparkles[_nextSparkle];
            _nextSparkle = (_nextSparkle + 1) % SparkleCount;
            var offset = Random.insideUnitSphere * 0.17f;
            offset.z = -Mathf.Abs(offset.z) - 0.05f; // devant l'oiseau, côté caméra
            p.Age = 0f;
            p.Life = Random.Range(0.3f, 0.45f);
            p.Size = Random.Range(0.018f, 0.032f);
            p.Velocity = new Vector3(Random.Range(-0.3f, 0f), Random.Range(-0.05f, 0.15f), 0f);
            p.Transform.position = _root.position + offset;
            p.Transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 90f));
            p.Transform.gameObject.SetActive(true);
        }

        void UpdateSparkles(float dt)
        {
            for (int i = 0; i < SparkleCount; i++)
            {
                ref var p = ref _sparkles[i];
                if (!p.Transform.gameObject.activeSelf) continue;
                p.Age += dt;
                if (p.Age >= p.Life)
                {
                    p.Transform.gameObject.SetActive(false);
                    continue;
                }
                p.Transform.position += p.Velocity * dt;
                float size = p.Size * Mathf.Sin(p.Age / p.Life * Mathf.PI);
                p.Transform.localScale = new Vector3(size, size, size);
            }
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

            _preview = Mathf.MoveTowards(_preview, _previewing ? 1f : 0f, deltaTime * 4f);
            if (_preview > 0f)
            {
                float k = _preview * _preview * (3f - 2f * _preview);
                float bob = y - (_cfg.BirdStartY + _cfg.BirdHeight * 0.5f);
                var target = _space.ToWorld(PreviewX, PreviewY + bob, 0f);
                _root.localPosition = Vector3.Lerp(_root.localPosition, target, k);
                float turn = Mathf.Sin(Time.unscaledTime * 0.8f) * 35f;
                _root.localRotation = Quaternion.Slerp(_root.localRotation, Quaternion.Euler(0f, Yaw + turn, 0f), k);
            }
            _root.localScale = Vector3.one * Mathf.Lerp(1f, PreviewScale, _preview * _preview * (3f - 2f * _preview));

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
