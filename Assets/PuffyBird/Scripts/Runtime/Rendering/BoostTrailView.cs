using PuffyBird.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace PuffyBird.Rendering
{
    /// <summary>
    /// Effets de l'étoile de vitesse : traînée arc-en-ciel derrière l'oiseau tant que dure
    /// l'accélération (petites billes lumineuses par bandes de couleur, emportées par le
    /// défilement), et gerbe d'étincelles à la prise d'une étoile. Pool fixe créé au chargement.
    /// </summary>
    public sealed class BoostTrailView
    {
        const int TrailCount = 56;
        const int BurstCount = 14;
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
        readonly Mesh[] _colorMeshes;
        readonly Particle[] _trail = new Particle[TrailCount];
        readonly Particle[] _burst = new Particle[BurstCount];
        int _nextTrail;
        int _emitted;
        float _emitAccumulator;

        public BoostTrailView(Transform parent, MaterialLibrary materials, WorldSpace space)
        {
            _cfg = space.Config;
            var root = new GameObject("Traînée").transform;
            root.SetParent(parent, false);

            _colorMeshes = new Mesh[Palette.Rainbow.Length];
            for (int c = 0; c < _colorMeshes.Length; c++)
                _colorMeshes[c] = new MeshBuilder().AddSphere(Vector3.zero, 1f, Palette.Rainbow[c], 10, 6).Build("Bille " + c);

            var material = materials.Lit("Traînée", Color.white, 0.2f, 0f, 0f);
            material.SetFloat(MaterialLibrary.VertexEmission, 1.8f);

            for (int i = 0; i < TrailCount; i++) _trail[i] = CreateParticle(root, material);
            for (int i = 0; i < BurstCount; i++) _burst[i] = CreateParticle(root, material);
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

        /// <summary>Gerbe d'étincelles multicolores à la prise d'une étoile.</summary>
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

        void UpdateBurst(float dt)
        {
            for (int i = 0; i < BurstCount; i++)
            {
                ref var p = ref _burst[i];
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
