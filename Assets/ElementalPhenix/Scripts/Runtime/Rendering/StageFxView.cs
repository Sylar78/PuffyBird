using ElementalPhenix.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace ElementalPhenix.Rendering
{
    /// <summary>
    /// Effets qui montrent la règle de l'étape du Voyage tant qu'elle agit :
    /// rafales (traits de vent qui montent ou descendent avec la rafale, flèches près de l'oiseau),
    /// ailes gelées (éclats de glace qui tombent lourdement des ailes), course folle (traits de
    /// vitesse), air léger (plumes qui s'envolent sous l'oiseau) et nage au ralenti (bulles qui
    /// s'échappent du bec, en gerbe à chaque battement). Purement visuel : les traits restent
    /// derrière le plan de jeu, les particules près de l'oiseau sont minuscules et ne masquent
    /// aucun tuyau. Pools fixes créés au chargement.
    /// </summary>
    public sealed class StageFxView
    {
        const int StreakCount = 30;
        const int MoteCount = 48;
        const float NearZ = 0.6f;
        const float FarZ = 3f;
        const float SpawnFov = 40f;
        /// <summary>En dessous de cette part de la rafale la plus forte, les flèches disparaissent.</summary>
        const float ArrowThreshold = 0.25f;

        struct Particle
        {
            public Transform Transform;
            public MeshFilter Filter;
            public Vector3 Velocity;
            public float Age;
            public float Life;
            public float Size;
            public float Fall;
            public float Phase;
            public float Spin;
        }

        readonly WorldSpace _space;
        readonly Particle[] _streaks = new Particle[StreakCount];
        readonly Particle[] _motes = new Particle[MoteCount];
        readonly Transform[] _arrows = new Transform[2];
        readonly Mesh _streakMesh;
        readonly Mesh _speedMesh;
        readonly Mesh _shardMesh;
        readonly Mesh _featherMesh;
        readonly Mesh _bubbleMesh;
        StageRule _rule;
        int _nextMote;
        float _emitAccumulator;

        public StageFxView(Transform parent, MaterialLibrary materials, WorldSpace space)
        {
            _space = space;
            var root = new GameObject("Effets d'étape").transform;
            root.SetParent(parent, false);

            _streakMesh = new MeshBuilder().AddBox(Vector3.zero, Vector3.one, Palette.Hex("#DCEBFF")).Build("Trait de vent");
            _speedMesh = new MeshBuilder().AddBox(Vector3.zero, Vector3.one, Palette.Hex("#FFF4D6")).Build("Trait de vitesse");
            _shardMesh = new MeshBuilder().AddEllipsoid(Vector3.zero, new Vector3(0.45f, 1f, 0.3f), Palette.Hex("#BDEBFF"), 4, 3).Build("Éclat de glace");
            _featherMesh = new MeshBuilder()
                .AddEllipsoid(Vector3.zero, new Vector3(1f, 0.16f, 0.4f), Color.white, 8, 5)
                .AddBox(new Vector3(0.1f, 0f, 0f), new Vector3(2f, 0.08f, 0.08f), Palette.Hex("#E6E0FF"))
                .Build("Plume");
            _bubbleMesh = new MeshBuilder()
                .AddSphere(Vector3.zero, 1f, Palette.Hex("#D8F6FF"), 10, 8)
                .AddSphere(new Vector3(-0.35f, 0.4f, -0.6f), 0.28f, Color.white, 6, 4)
                .Build("Bulle du phénix");

            var glow = materials.Lit("Effets d'étape", Color.white, 0.6f, 0f, 0.5f);
            glow.SetFloat(MaterialLibrary.VertexEmission, 1.1f);
            for (int i = 0; i < StreakCount; i++) _streaks[i] = Create(root, "Trait", _streakMesh, glow);
            for (int i = 0; i < MoteCount; i++) _motes[i] = Create(root, "Particule", _shardMesh, glow);

            // Flèche en chevron : deux branches inclinées, pointe vers le haut (tournée pour descendre).
            var arrowColor = Palette.Hex("#BFE4FF");
            var arrow = new MeshBuilder()
                .Append(new MeshBuilder().AddBox(Vector3.zero, new Vector3(0.11f, 0.03f, 0.03f), arrowColor), Matrix4x4.TRS(new Vector3(-0.038f, -0.025f, 0f), Quaternion.Euler(0f, 0f, 40f), Vector3.one))
                .Append(new MeshBuilder().AddBox(Vector3.zero, new Vector3(0.11f, 0.03f, 0.03f), arrowColor), Matrix4x4.TRS(new Vector3(0.038f, -0.025f, 0f), Quaternion.Euler(0f, 0f, -40f), Vector3.one))
                .Build("Flèche de rafale");
            var arrowMaterial = materials.Lit("Flèche de rafale", Color.white, 0.4f, 0f, 0.4f);
            arrowMaterial.SetFloat(MaterialLibrary.VertexEmission, 1.6f);
            for (int i = 0; i < _arrows.Length; i++) _arrows[i] = Create(root, "Flèche", arrow, arrowMaterial).Transform;
        }

        static Particle Create(Transform parent, string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.SetActive(false);
            return new Particle { Transform = go.transform, Filter = filter };
        }

        /// <summary>Battement d'ailes : quelques éclats, plumes ou bulles de plus selon la règle.</summary>
        public void OnFlap(Vector3 birdPosition)
        {
            int count = _rule == StageRule.SlowSwim ? 4 : (_rule == StageRule.FrozenWings ? 5 : (_rule == StageRule.LightAir ? 3 : 0));
            for (int i = 0; i < count; i++) EmitMote(birdPosition, 0f, burst: true);
        }

        /// <param name="rule">Règle de l'étape en cours (<see cref="StageRule.FirstFlight"/> hors Voyage : aucun effet).</param>
        public void Update(GameSimulation sim, StageRule rule, Vector3 birdPosition, float deltaTime)
        {
            if (rule != _rule)
            {
                _rule = rule;
                Clear();
            }
            // En pause, tout reste figé.
            if (sim.State == GameState.Paused) return;

            var state = sim.State;
            bool running = state == GameState.Ready || state == GameState.Playing;
            var cfg = sim.Config;
            float scroll = running ? cfg.ScrollSpeed * sim.SpeedFactor / WorldSpace.PixelsPerUnit : 0f;
            // Rafale en cours, de −1 (pousse vers le haut) à 1 (pousse vers le bas) ; seulement en jeu, comme la simulation.
            float gust = rule == StageRule.Gusts && state == GameState.Playing && cfg.GustAcceleration != 0f
                ? cfg.GustAt(sim.Time) / cfg.GustAcceleration : 0f;
            // Courant porteur (pouvoir du vent) : les flèches montrent où le souffle emmène le phénix.
            if (sim.Guiding) gust = GuidePull(sim);

            if (running && (rule == StageRule.FrozenWings || rule == StageRule.LightAir || rule == StageRule.SlowSwim))
            {
                float rate = rule == StageRule.FrozenWings ? 16f : (rule == StageRule.LightAir ? 9f : 5f);
                _emitAccumulator += rate * deltaTime;
                while (_emitAccumulator >= 1f)
                {
                    _emitAccumulator -= 1f;
                    EmitMote(birdPosition, scroll, burst: false);
                }
            }
            else
            {
                _emitAccumulator = 0f;
            }

            bool streaks = rule == StageRule.Gusts || rule == StageRule.WildRush;
            UpdateStreaks(streaks && running, scroll, gust, deltaTime);
            UpdateMotes(scroll, deltaTime, sim.Time);
            UpdateArrows(birdPosition, gust, sim.Time);
        }

        /// <summary>Direction du courant porteur, de −1 (vers le haut) à 1 (vers le bas), comme une rafale.</summary>
        static float GuidePull(GameSimulation sim)
        {
            var cfg = sim.Config;
            for (int i = 0; i < sim.Pipes.Count; i++)
            {
                ref var p = ref sim.Pipes[i];
                if (p.Id != sim.GuideTarget) continue;
                float offset = ElementPowers.OpeningCenter(p, cfg) - sim.Bird.CenterY(cfg);
                return Mathf.Clamp(offset / 30f, -1f, 1f);
            }
            return 0f;
        }

        void Clear()
        {
            for (int i = 0; i < StreakCount; i++)
            {
                _streaks[i].Transform.gameObject.SetActive(false);
                _streaks[i].Age = 0f;
            }
            for (int i = 0; i < MoteCount; i++) _motes[i].Transform.gameObject.SetActive(false);
            for (int i = 0; i < _arrows.Length; i++) _arrows[i].gameObject.SetActive(false);
            _emitAccumulator = 0f;
        }

        // ───────── traits de vent et de vitesse (derrière le plan de jeu) ─────────

        float TopAt(float z) => _space.CameraPosition.y + (z + WorldSpace.CameraDistance) * Mathf.Tan(SpawnFov * 0.5f * Mathf.Deg2Rad);

        void UpdateStreaks(bool emit, float scroll, float gust, float dt)
        {
            float strength = Mathf.Abs(gust);
            for (int i = 0; i < StreakCount; i++)
            {
                ref var p = ref _streaks[i];
                var t = p.Transform;
                if (!t.gameObject.activeSelf)
                {
                    if (emit) SpawnStreak(ref p, anywhere: p.Age == 0f);
                    continue;
                }

                // Rafales : le vent suit la rafale (y monde vers le haut, la rafale est en y logique vers le bas).
                if (_rule == StageRule.Gusts)
                {
                    p.Velocity = new Vector3(-(1.6f + scroll * 1.5f) * p.Fall, -gust * 2.4f * p.Fall, 0f);
                    float length = p.Size * (0.35f + 0.65f * strength);
                    t.localScale = new Vector3(length, 0.008f, 0.008f);
                    t.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(p.Velocity.y, p.Velocity.x) * Mathf.Rad2Deg);
                }

                var pos = t.localPosition + p.Velocity * dt;
                float halfWidth = CameraRig.HalfWidthAt(pos.z, SpawnFov, CameraRig.MaxAspect) + 0.8f;
                if (pos.x < -halfWidth || pos.y < -0.2f || pos.y > TopAt(pos.z) + 0.4f)
                {
                    t.gameObject.SetActive(false);
                    if (emit) SpawnStreak(ref p, anywhere: false);
                    continue;
                }
                t.localPosition = pos;
            }
        }

        void SpawnStreak(ref Particle p, bool anywhere)
        {
            float z = Random.Range(NearZ, FarZ);
            float halfWidth = CameraRig.HalfWidthAt(z, SpawnFov, CameraRig.MaxAspect);
            float top = TopAt(z);
            var t = p.Transform;
            p.Age = 1f;
            p.Fall = Random.Range(0.75f, 1.25f);
            if (_rule == StageRule.WildRush)
            {
                p.Filter.sharedMesh = _speedMesh;
                p.Size = Random.Range(0.35f, 0.8f);
                p.Velocity = new Vector3(-Random.Range(4.5f, 7f), 0f, 0f);
                t.localScale = new Vector3(p.Size, 0.007f, 0.007f);
                t.localRotation = Quaternion.identity;
            }
            else
            {
                p.Filter.sharedMesh = _streakMesh;
                p.Size = Random.Range(0.3f, 0.6f);
                p.Velocity = new Vector3(-2f, 0f, 0f);
            }
            float x = anywhere ? Random.Range(-halfWidth, halfWidth) : halfWidth + Random.Range(0.2f, 1.2f);
            t.localPosition = new Vector3(x, Random.Range(0.15f, top), z);
            t.gameObject.SetActive(true);
        }

        // ───────── particules autour de l'oiseau ─────────

        void EmitMote(Vector3 bird, float scroll, bool burst)
        {
            ref var p = ref _motes[_nextMote];
            _nextMote = (_nextMote + 1) % MoteCount;
            var t = p.Transform;
            p.Age = 0f;
            p.Phase = Random.Range(0f, 100f);
            switch (_rule)
            {
                case StageRule.FrozenWings:
                    // Éclats de glace secoués des ailes : ils tombent vite, comme l'oiseau alourdi.
                    p.Filter.sharedMesh = _shardMesh;
                    p.Life = Random.Range(0.5f, 0.8f);
                    p.Size = Random.Range(0.012f, 0.022f);
                    p.Fall = -5f;
                    p.Spin = Random.Range(-360f, 360f);
                    p.Velocity = new Vector3(-scroll * 0.5f + Random.Range(-0.3f, 0.3f), burst ? Random.Range(0.1f, 0.5f) : -Random.Range(0.1f, 0.4f), 0f);
                    t.position = bird + new Vector3(Random.Range(-0.08f, 0.04f), Random.Range(-0.02f, 0.06f), 0.04f);
                    t.localRotation = Random.rotation;
                    break;
                case StageRule.LightAir:
                    // Plumes qui s'envolent : l'air porte, tout monte doucement.
                    p.Filter.sharedMesh = _featherMesh;
                    p.Life = Random.Range(1.1f, 1.6f);
                    p.Size = Random.Range(0.016f, 0.026f);
                    p.Fall = 0.25f;
                    p.Spin = Random.Range(-90f, 90f);
                    p.Velocity = new Vector3(-scroll * 0.6f + Random.Range(-0.15f, 0.15f), Random.Range(0.15f, 0.35f), 0f);
                    t.position = bird + new Vector3(Random.Range(-0.12f, 0.05f), -Random.Range(0.06f, 0.12f), 0.05f);
                    t.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), Random.Range(-30f, 30f));
                    break;
                default:
                    // Bulles du bec : elles remontent lentement, en gerbe à chaque battement.
                    p.Filter.sharedMesh = _bubbleMesh;
                    p.Life = Random.Range(1.2f, 1.8f);
                    p.Size = Random.Range(0.008f, 0.018f);
                    p.Fall = 0.4f;
                    p.Spin = 0f;
                    p.Velocity = new Vector3(-scroll * 0.4f + Random.Range(-0.05f, 0.08f), Random.Range(0.2f, 0.4f), 0f);
                    t.position = bird + new Vector3(0.13f + Random.Range(-0.02f, 0.03f), Random.Range(0f, 0.04f), 0.03f);
                    t.localRotation = Quaternion.identity;
                    break;
            }
            t.localScale = Vector3.zero;
            t.gameObject.SetActive(true);
        }

        void UpdateMotes(float scroll, float dt, float time)
        {
            for (int i = 0; i < MoteCount; i++)
            {
                ref var p = ref _motes[i];
                var t = p.Transform;
                if (!t.gameObject.activeSelf) continue;
                p.Age += dt;
                if (p.Age >= p.Life)
                {
                    t.gameObject.SetActive(false);
                    continue;
                }
                p.Velocity.y += p.Fall * dt;
                var pos = t.position + p.Velocity * dt;
                if (_rule != StageRule.FrozenWings) pos.x += Mathf.Sin(time * 2.2f + p.Phase) * 0.12f * dt;
                t.position = pos;
                if (p.Spin != 0f) t.Rotate(p.Spin * dt * 0.4f, p.Spin * dt, 0f, Space.Self);
                // Apparition rapide, disparition en douceur.
                float k = p.Age / p.Life;
                float s = p.Size * Mathf.Min(1f, k * 6f) * (1f - k * k);
                t.localScale = new Vector3(s, s, s);
            }
        }

        // ───────── flèches des rafales ─────────

        void UpdateArrows(Vector3 bird, float gust, float time)
        {
            float strength = Mathf.Abs(gust);
            bool show = strength >= ArrowThreshold;
            for (int i = 0; i < _arrows.Length; i++)
            {
                var a = _arrows[i];
                if (a.gameObject.activeSelf != show) a.gameObject.SetActive(show);
                if (!show) continue;
                // La rafale pousse vers le bas en y logique quand elle est positive : flèches vers le bas.
                float dir = gust > 0f ? -1f : 1f;
                float travel = (time * 1.8f + i * 0.5f) % 1f;
                float k = (strength - ArrowThreshold) / (1f - ArrowThreshold);
                float s = 0.6f + 0.6f * k;
                a.position = bird + new Vector3(-0.3f, dir * (-0.05f + travel * 0.12f), 0.04f);
                a.localRotation = Quaternion.Euler(0f, 0f, dir > 0f ? 0f : 180f);
                a.localScale = new Vector3(s, s, s) * Mathf.Sin(travel * Mathf.PI);
            }
        }
    }
}
