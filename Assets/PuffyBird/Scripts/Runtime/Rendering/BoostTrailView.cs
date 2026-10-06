using PuffyBird.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace PuffyBird.Rendering
{
    /// <summary>
    /// Effets de l'étoile de vitesse : traînée aux couleurs du phénix choisi derrière l'oiseau tant
    /// que dure l'accélération (petites billes lumineuses par bandes de couleur, emportées par le
    /// défilement), et gerbe d'étincelles à la prise d'une étoile. Pool fixe et maillages de chaque
    /// oiseau créés au chargement.
    /// </summary>
    public sealed class BoostTrailView
    {
        const int TrailCount = 56;
        const int BurstCount = 14;
        const int SparkCount = 28;
        const float EmitRate = 80f;
        const float TrailLife = 0.5f;
        const float TrailSize = 0.055f;
        const int BandLength = 3;

        struct Particle
        {
            public Transform Transform;
            public MeshFilter Filter;
            public Vector3 Velocity;
            public float Age;
            public float Life;
            public float Size;
        }

        readonly GameConfig _cfg;
        /// <summary>Billes de couleur de chaque oiseau du catalogue (<see cref="Palette.TrailColors"/>).</summary>
        readonly Mesh[][] _skinMeshes = new Mesh[Skins.Count][];
        Mesh[] _colorMeshes;
        readonly Particle[] _trail = new Particle[TrailCount];
        readonly Particle[] _burst = new Particle[BurstCount];
        readonly Particle[] _sparks = new Particle[SparkCount];
        readonly Mesh[] _sparkMeshes = new Mesh[2 + 4];
        int _nextSpark;
        int _nextTrail;
        int _emitted;
        float _emitAccumulator;

        public BoostTrailView(Transform parent, MaterialLibrary materials, WorldSpace space)
        {
            _cfg = space.Config;
            var root = new GameObject("Traînée").transform;
            root.SetParent(parent, false);

            for (int i = 0; i < Skins.Count; i++)
            {
                var skin = Skins.Get(i);
                var colors = Palette.TrailColors(Palette.Skin(skin.Id).Phoenix);
                _skinMeshes[i] = new Mesh[colors.Length];
                for (int c = 0; c < colors.Length; c++)
                    _skinMeshes[i][c] = new MeshBuilder().AddSphere(Vector3.zero, 1f, colors[c], 10, 6).Build("Bille " + skin.Id + " " + c);
            }
            _colorMeshes = _skinMeshes[0];

            var material = materials.Lit("Traînée", Color.white, 0.2f, 0f, 0f);
            material.SetFloat(MaterialLibrary.VertexEmission, 1.8f);

            for (int i = 0; i < TrailCount; i++) _trail[i] = CreateParticle(root, material);
            for (int i = 0; i < BurstCount; i++) _burst[i] = CreateParticle(root, material);

            // Étincelles des frôlements (blanc, cyan) et des paliers (bronze, argent, or, platine).
            var sparkColors = new[]
            {
                Color.white, Palette.Hex("#8FE6FF"),
                Palette.MedalColor(Medal.Bronze), Palette.MedalColor(Medal.Silver),
                Palette.MedalColor(Medal.Gold), Palette.MedalColor(Medal.Platinum),
            };
            for (int c = 0; c < _sparkMeshes.Length; c++)
                _sparkMeshes[c] = new MeshBuilder().AddSphere(Vector3.zero, 1f, sparkColors[c], 10, 6).Build("Étincelle " + c);
            for (int i = 0; i < SparkCount; i++) _sparks[i] = CreateParticle(root, material);
        }

        Particle CreateParticle(Transform parent, Material material)
        {
            var go = new GameObject("Bille");
            go.transform.SetParent(parent, false);
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = _colorMeshes[0];
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.SetActive(false);
            return new Particle { Transform = go.transform, Filter = filter };
        }

        /// <summary>Oiseau choisi : la traînée et la gerbe de l'étoile prennent ses couleurs.</summary>
        public void SetSkin(int index) => _colorMeshes = _skinMeshes[Mathf.Clamp(index, 0, Skins.Count - 1)];

        /// <summary>Gerbe d'étincelles aux couleurs de l'oiseau à la prise d'une étoile.</summary>
        public void OnStar(Vector3 birdPosition)
        {
            var origin = birdPosition + new Vector3(0.08f, 0f, 0f);
            for (int i = 0; i < BurstCount; i++)
            {
                ref var p = ref _burst[i];
                float a = (i + Random.value * 0.5f) / BurstCount * Mathf.PI * 2f;
                p.Velocity = new Vector3(Mathf.Cos(a), Mathf.Sin(a), Random.Range(-0.3f, 0.3f)) * Random.Range(0.9f, 1.5f);
                p.Age = 0f;
                p.Life = Random.Range(0.35f, 0.55f);
                p.Size = Random.Range(0.025f, 0.04f);
                p.Filter.sharedMesh = _colorMeshes[i % _colorMeshes.Length];
                p.Transform.position = origin;
                p.Transform.gameObject.SetActive(true);
            }
        }

        /// <summary>Petite gerbe blanche et cyan quand l'oiseau frôle un tuyau.</summary>
        public void OnNearMiss(Vector3 birdPosition)
        {
            const int count = 8;
            for (int i = 0; i < count; i++)
            {
                float a = (i + Random.value * 0.6f) / count * Mathf.PI * 2f;
                Spark(birdPosition + new Vector3(0.05f, 0f, 0.05f), a, Random.Range(0.8f, 1.4f), i % 2, Random.Range(0.02f, 0.032f), Random.Range(0.25f, 0.4f));
            }
        }

        /// <summary>Couronne d'étincelles aux couleurs de la médaille qui vient d'être atteinte.</summary>
        public void OnMilestone(Vector3 birdPosition, Medal medal)
        {
            int color = 2 + Mathf.Clamp((int)medal - (int)Medal.Bronze, 0, 3);
            const int count = 20;
            for (int i = 0; i < count; i++)
            {
                float a = (i + Random.value * 0.3f) / count * Mathf.PI * 2f;
                Spark(birdPosition, a, Random.Range(1.3f, 1.9f), i % 5 == 0 ? 0 : color, Random.Range(0.03f, 0.05f), Random.Range(0.5f, 0.75f));
            }
        }

        void Spark(Vector3 origin, float angle, float speed, int mesh, float size, float life)
        {
            ref var p = ref _sparks[_nextSpark];
            _nextSpark = (_nextSpark + 1) % SparkCount;
            p.Velocity = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), Random.Range(-0.2f, 0.2f)) * speed;
            p.Age = 0f;
            p.Life = life;
            p.Size = size;
            p.Filter.sharedMesh = _sparkMeshes[mesh];
            p.Transform.position = origin;
            p.Transform.gameObject.SetActive(true);
        }

        public void Update(GameSimulation sim, Vector3 birdPosition, float deltaTime, float time)
        {
            float amount = Mathf.Clamp01(sim.BoostAmount);
            // Le décor défile vers la gauche : la traînée reste accrochée au monde.
            float scroll = sim.State == GameState.Playing ? _cfg.ScrollSpeed * sim.SpeedFactor / WorldSpace.PixelsPerUnit : 0f;

            if (amount > 0f && sim.State == GameState.Playing)
            {
                _emitAccumulator += EmitRate * deltaTime;
                while (_emitAccumulator >= 1f)
                {
                    _emitAccumulator -= 1f;
                    Emit(birdPosition, amount, scroll, time);
                }
            }
            else
            {
                _emitAccumulator = 0f;
            }

            UpdateTrail(deltaTime);
            UpdateBurst(deltaTime);
            UpdateBurst(_sparks, deltaTime);
        }

        void Emit(Vector3 birdPosition, float amount, float scroll, float time)
        {
            ref var p = ref _trail[_nextTrail];
            _nextTrail = (_nextTrail + 1) % TrailCount;
            int band = (_emitted++ / BandLength) % _colorMeshes.Length;
            p.Filter.sharedMesh = _colorMeshes[band];
            p.Age = 0f;
            p.Life = TrailLife;
            p.Size = TrailSize * (0.5f + 0.5f * amount);
            float wave = Mathf.Sin(time * 18f) * 0.012f;
            p.Transform.position = birdPosition + new Vector3(-0.13f, -0.01f + wave, 0.03f);
            p.Velocity = new Vector3(-scroll, Random.Range(-0.04f, 0.04f), 0f);
            p.Transform.gameObject.SetActive(true);
        }

        void UpdateTrail(float dt)
        {
            for (int i = 0; i < TrailCount; i++)
            {
                ref var p = ref _trail[i];
                if (!Tick(ref p, dt)) continue;
                float t = p.Age / p.Life;
                float s = p.Size * (1f - t) * (1f - t * 0.3f);
                p.Transform.localScale = new Vector3(s * 1.3f, s, s);
            }
        }

        void UpdateBurst(float dt) => UpdateBurst(_burst, dt);

        static void UpdateBurst(Particle[] pool, float dt)
        {
            for (int i = 0; i < pool.Length; i++)
            {
                ref var p = ref pool[i];
                p.Velocity *= Mathf.Max(0f, 1f - 4f * dt);
                if (!Tick(ref p, dt)) continue;
                float t = p.Age / p.Life;
                float s = p.Size * Mathf.Sin(Mathf.Min(1f, t * 1.5f + 0.2f) * Mathf.PI);
                p.Transform.localScale = new Vector3(s, s, s);
            }
        }

        static bool Tick(ref Particle p, float dt)
        {
            if (!p.Transform.gameObject.activeSelf) return false;
            p.Age += dt;
            if (p.Age >= p.Life)
            {
                p.Transform.gameObject.SetActive(false);
                return false;
            }
            p.Transform.position += p.Velocity * dt;
            return true;
        }
    }
}
