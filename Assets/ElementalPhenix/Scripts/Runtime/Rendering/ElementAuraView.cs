using UnityEngine;
using UnityEngine.Rendering;

namespace ElementalPhenix.Rendering
{
    /// <summary>
    /// Aura de l'élément du phénix, présente en permanence et plus dense sous étoile : flammes et
    /// braises (feu), éclats de glace et flocons en orbite (eau), filets de vent qui s'enroulent
    /// (vent), couronne de rayons et grains de lumière (lumière), fumée d'ombre, étincelles violettes
    /// et couronne d'épines sombres (ténèbres). Chaque battement d'ailes en lâche quelques-unes.
    /// Tout est créé au chargement puis seulement déplacé, affiché ou masqué ; purement visuel,
    /// la hitbox ne change pas.
    /// </summary>
    public sealed class ElementAuraView
    {
        const int PoolSize = 36;
        const int BurstCount = 4;
        /// <summary>Sous étoile, le débit est multiplié jusqu'à 1 + BoostRate.</summary>
        const float BoostRate = 2.5f;
        /// <summary>Couronne (lumière, ténèbres) : profondeur derrière l'oiseau.</summary>
        const float CrownDepth = 0.09f;

        enum Motion
        {
            Rise,
            Orbit,
            Gust,
            Twinkle,
            Smoke,
        }

        struct Recipe
        {
            public Mesh Main;
            public Mesh Alt;
            public float AltChance;
            public Color Start;
            public Color End;
            public Color AltColor;
            public float Rate;
            public float LifeMin;
            public float LifeMax;
            public float SizeMin;
            public float SizeMax;
            public Vector3 Stretch;
            public Motion Motion;
            public float VertexEmission;
            public Color Emission;
        }

        struct Particle
        {
            public Transform Transform;
            public MeshFilter Filter;
            public MeshRenderer Renderer;
            public Vector3 Origin;
            public Vector3 Velocity;
            /// <summary>Orbite : angle, rayon, hauteur ; vent : phase, rayon de la spirale.</summary>
            public Vector3 Orbit;
            public float Age;
            public float Life;
            public float Size;
            public float Spin;
            public bool Alt;
        }

        readonly Particle[] _pool = new Particle[PoolSize];
        readonly Material _material;
        readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        readonly Transform _crown;
        readonly MeshFilter _crownFilter;
        readonly Material _crownMaterial;
        readonly Mesh _flame;
        readonly Mesh _ember;
        readonly Mesh _crystal;
        readonly Mesh _flake;
        readonly Mesh _streak;
        readonly Mesh _mote;
        readonly Mesh _smoke;
        readonly Mesh _rays;
        readonly Mesh _thorns;
        Recipe _recipe;
        Palette.Element _element;
        bool _configured;
        int _next;
        float _accumulator;
        float _crownSpin;

        public ElementAuraView(Transform parent, MaterialLibrary materials)
        {
            var glow = new Color(1f, 1f, 1f, 1f);
            var matte = new Color(1f, 1f, 1f, 0f);
            _flame = new MeshBuilder()
                .AddSphere(Vector3.zero, 0.42f, glow, 10, 8)
                .AddCone(new Vector3(0f, 0.05f, 0f), 0.4f, 1.05f, glow, 10)
                .Build("Flamme");
            _ember = new MeshBuilder().AddSphere(Vector3.zero, 0.5f, glow, 8, 6).Build("Braise");
            _crystal = BuildCrystal(glow);
            _flake = new MeshBuilder().AddStar(Vector3.zero, 0.5f, 0.12f, 0.08f, glow, 6).Build("Flocon");
            _streak = new MeshBuilder().AddEllipsoid(Vector3.zero, new Vector3(0.5f, 0.05f, 0.05f), glow, 12, 6).Build("Filet de vent");
            _mote = new MeshBuilder().AddStar(Vector3.zero, 0.5f, 0.14f, 0.1f, glow, 4).Build("Grain de lumière");
            _smoke = new MeshBuilder().AddSphere(Vector3.zero, 0.5f, matte, 10, 8).Build("Fumée d'ombre");
            _rays = BuildCrown(12, 0.012f, 0.15f, 0.11f, glow);
            _thorns = BuildCrown(10, 0.028f, 0.12f, 0.09f, new Color(1f, 1f, 1f, 0.2f));

            _material = materials.Lit("Aura", Color.white, 0.6f, 0f, 0.4f);
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("Aura");
                go.transform.SetParent(parent, false);
                ref var p = ref _pool[i];
                p.Transform = go.transform;
                p.Filter = go.AddComponent<MeshFilter>();
                p.Renderer = go.AddComponent<MeshRenderer>();
                p.Renderer.sharedMaterial = _material;
                p.Renderer.shadowCastingMode = ShadowCastingMode.Off;
                go.SetActive(false);
            }

            var crown = new GameObject("Couronne");
            crown.transform.SetParent(parent, false);
            _crown = crown.transform;
            _crownFilter = crown.AddComponent<MeshFilter>();
            var renderer = crown.AddComponent<MeshRenderer>();
            _crownMaterial = materials.Lit("Couronne", Color.white, 0.5f, 0f, 0.3f);
            renderer.sharedMaterial = _crownMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            crown.SetActive(false);
        }

        /// <summary>Cristal hexagonal pointu aux deux bouts, le long de y.</summary>
        static Mesh BuildCrystal(Color color)
        {
            var tip = new MeshBuilder().AddPrism(Vector3.zero, 0.2f, 0f, 0.32f, color, 6, false);
            return new MeshBuilder()
                .AddPrism(new Vector3(0f, -0.3f, 0f), 0.2f, 0.2f, 0.6f, color, 6, false)
                .Append(tip, Matrix4x4.Translate(new Vector3(0f, 0.3f, 0f)))
                .Append(tip, Matrix4x4.TRS(new Vector3(0f, -0.3f, 0f), Quaternion.Euler(180f, 0f, 0f), Vector3.one))
                .Build("Éclat de glace");
        }

        /// <summary>
        /// Couronne de <paramref name="count"/> pointes dans le plan de l'écran, longueurs alternées,
        /// qui partent à <paramref name="inner"/> du centre.
        /// </summary>
        static Mesh BuildCrown(int count, float width, float length, float inner, Color color)
        {
            var b = new MeshBuilder();
            for (int i = 0; i < count; i++)
            {
                float l = i % 2 == 0 ? length : length * 0.62f;
                var spike = new MeshBuilder().AddPrism(new Vector3(0f, inner, 0f), width, 0f, l, color, 4, false);
                b.Append(spike, Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, i * 360f / count)));
            }
            return b.Build("Couronne");
        }

        /// <summary>Choisit l'aura du phénix affiché ; aucune pour un oiseau sans élément.</summary>
        public void SetLook(Palette.SkinLook look)
        {
            var element = look.Shape == Palette.BodyShape.Phoenix ? look.Element : Palette.Element.None;
            // Fin de l'étoile : même phénix, l'aura continue sans repartir de zéro.
            if (_configured && element == _element) return;
            _configured = true;
            _element = element;
            var c = look.Phoenix;
            _recipe = default;
            _recipe.Stretch = Vector3.one;
            switch (_element)
            {
                case Palette.Element.Fire:
                    _recipe.Main = _flame;
                    _recipe.Alt = _ember;
                    _recipe.AltChance = 0.35f;
                    _recipe.Start = Opaque(c.FlameTip);
                    _recipe.End = Opaque(c.FlameA);
                    _recipe.AltColor = Opaque(c.FlameB);
                    _recipe.Rate = 16f;
                    _recipe.LifeMin = 0.35f;
                    _recipe.LifeMax = 0.6f;
                    _recipe.SizeMin = 0.03f;
                    _recipe.SizeMax = 0.05f;
                    _recipe.Stretch = new Vector3(0.75f, 1.35f, 0.75f);
                    _recipe.Motion = Motion.Rise;
                    _recipe.VertexEmission = 2.4f;
                    break;
                case Palette.Element.Ice:
                    _recipe.Main = _crystal;
                    _recipe.Alt = _flake;
                    _recipe.AltChance = 0.4f;
                    _recipe.Start = Palette.Hex("#F2FDFF");
                    _recipe.End = Palette.Hex("#8FDCFF");
                    _recipe.AltColor = Palette.Hex("#FFFFFF");
                    _recipe.Rate = 6f;
                    _recipe.LifeMin = 1.3f;
                    _recipe.LifeMax = 1.9f;
                    _recipe.SizeMin = 0.022f;
                    _recipe.SizeMax = 0.036f;
                    _recipe.Stretch = new Vector3(0.8f, 1.4f, 0.8f);
                    _recipe.Motion = Motion.Orbit;
                    _recipe.VertexEmission = 1.1f;
                    break;
                case Palette.Element.Wind:
                    _recipe.Main = _streak;
                    _recipe.Alt = _mote;
                    _recipe.AltChance = 0.2f;
                    _recipe.Start = Palette.Hex("#F4FFF8");
                    _recipe.End = Palette.Hex("#9FF0C8");
                    _recipe.AltColor = Opaque(c.Gem);
                    _recipe.Rate = 8f;
                    _recipe.LifeMin = 0.35f;
                    _recipe.LifeMax = 0.55f;
                    _recipe.SizeMin = 0.16f;
                    _recipe.SizeMax = 0.26f;
                    _recipe.Motion = Motion.Gust;
                    _recipe.VertexEmission = 1.3f;
                    break;
                case Palette.Element.Light:
                    _recipe.Main = _mote;
                    _recipe.Alt = _ember;
                    _recipe.AltChance = 0.3f;
                    _recipe.Start = Palette.Hex("#FFFFFF");
                    _recipe.End = Palette.Hex("#FFE38A");
                    _recipe.AltColor = Opaque(c.Gem);
                    _recipe.Rate = 9f;
                    _recipe.LifeMin = 0.6f;
                    _recipe.LifeMax = 1f;
                    _recipe.SizeMin = 0.02f;
                    _recipe.SizeMax = 0.034f;
                    _recipe.Motion = Motion.Twinkle;
                    _recipe.VertexEmission = 2.6f;
                    break;
                case Palette.Element.Shadow:
                    _recipe.Main = _smoke;
                    _recipe.Alt = _mote;
                    _recipe.AltChance = 0.25f;
                    _recipe.Start = Palette.Hex("#0E0614");
                    _recipe.End = Palette.Hex("#2C0F44");
                    _recipe.AltColor = Opaque(c.FlameTip);
                    _recipe.Rate = 12f;
                    _recipe.LifeMin = 0.6f;
                    _recipe.LifeMax = 0.9f;
                    _recipe.SizeMin = 0.04f;
                    _recipe.SizeMax = 0.065f;
                    _recipe.Motion = Motion.Smoke;
                    _recipe.VertexEmission = 2f;
                    _recipe.Emission = Palette.Hex("#3A0F66") * 0.25f;
                    break;
            }
            _material.SetFloat(MaterialLibrary.VertexEmission, _recipe.VertexEmission);
            _material.SetColor(MaterialLibrary.EmissionColor, _recipe.Emission);
            for (int i = 0; i < PoolSize; i++) _pool[i].Transform.gameObject.SetActive(false);
            _accumulator = 0f;

            bool light = _element == Palette.Element.Light;
            bool shadow = _element == Palette.Element.Shadow;
            _crown.gameObject.SetActive(light || shadow);
            if (light)
            {
                _crownFilter.sharedMesh = _rays;
                _crownMaterial.SetColor(MaterialLibrary.BaseColor, Palette.Hex("#FFF2C0"));
                _crownMaterial.SetFloat(MaterialLibrary.VertexEmission, 2.2f);
                _crownMaterial.SetColor(MaterialLibrary.EmissionColor, Color.black);
            }
            else if (shadow)
            {
                _crownFilter.sharedMesh = _thorns;
                _crownMaterial.SetColor(MaterialLibrary.BaseColor, Palette.Hex("#1A0A26"));
                _crownMaterial.SetFloat(MaterialLibrary.VertexEmission, 0f);
                _crownMaterial.SetColor(MaterialLibrary.EmissionColor, Palette.Hex("#4A148A") * 0.35f);
            }
        }

        /// <summary>
        /// Rythme de la lueur du plumage : flamme qui vacille (feu), éclat qui respire (lumière),
        /// pulsation lente et sourde (ténèbres), stable pour la glace et le vent.
        /// </summary>
        public float GlowPulse(float time)
        {
            switch (_element)
            {
                case Palette.Element.Fire: return 0.75f + 0.5f * Mathf.PerlinNoise(time * 7f, 0.3f);
                case Palette.Element.Ice: return 0.92f + 0.08f * Mathf.Sin(time * 1.3f);
                case Palette.Element.Light: return 0.85f + 0.3f * (0.5f + 0.5f * Mathf.Sin(time * 2.2f));
                case Palette.Element.Shadow: return 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(time * 1.6f));
                default: return 1f;
            }
        }

        /// <summary>Teinte et lueur des petits nuages lâchés à chaque battement d'ailes.</summary>
        public void PuffLook(out Color color, out Color emission)
        {
            switch (_element)
            {
                case Palette.Element.Fire: color = Palette.Hex("#FFB070"); emission = Palette.Hex("#FF6A1A") * 0.5f; break;
                case Palette.Element.Ice: color = Palette.Hex("#E8F8FF"); emission = Palette.Hex("#9FE4FF") * 0.3f; break;
                case Palette.Element.Wind: color = Palette.Hex("#F0FFF6"); emission = Palette.Hex("#B8F5D8") * 0.25f; break;
                case Palette.Element.Light: color = Palette.Hex("#FFF6D8"); emission = Palette.Hex("#FFE38A") * 0.6f; break;
                case Palette.Element.Shadow: color = Palette.Hex("#2A1838"); emission = Palette.Hex("#3A1060") * 0.3f; break;
                default: color = Color.white; emission = new Color(0.25f, 0.25f, 0.25f); break;
            }
        }

        /// <summary>Quelques particules de l'élément au battement d'ailes.</summary>
        public void Burst(Vector3 center, float scale)
        {
            if (_element == Palette.Element.None) return;
            for (int i = 0; i < BurstCount; i++) Emit(center, scale, true);
        }

        /// <summary>
        /// Avance l'aura autour de <paramref name="center"/> (position de l'oiseau, à l'échelle
        /// <paramref name="scale"/> de son modèle). <paramref name="boost"/> (0 à 1) densifie l'aura
        /// sous étoile ; <paramref name="emitting"/> faux (oiseau tombé) laisse finir les particules.
        /// </summary>
        public void Update(Vector3 center, float scale, float boost, bool emitting, float dt, float time)
        {
            if (_element != Palette.Element.None && emitting)
            {
                _accumulator += _recipe.Rate * (1f + BoostRate * boost) * dt;
                while (_accumulator >= 1f)
                {
                    _accumulator -= 1f;
                    Emit(center, scale, false);
                }
            }
            else
            {
                _accumulator = 0f;
            }

            for (int i = 0; i < PoolSize; i++)
            {
                ref var p = ref _pool[i];
                if (!p.Transform.gameObject.activeSelf) continue;
                p.Age += dt;
                if (p.Age >= p.Life)
                {
                    p.Transform.gameObject.SetActive(false);
                    continue;
                }
                Animate(ref p, center, scale, dt, time);
                _block.SetColor(MaterialLibrary.BaseColor, p.Alt ? _recipe.AltColor : Color.Lerp(_recipe.Start, _recipe.End, p.Age / p.Life));
                p.Renderer.SetPropertyBlock(_block);
            }

            if (_crown.gameObject.activeSelf)
            {
                bool light = _element == Palette.Element.Light;
                _crownSpin += dt * (light ? 18f : -26f) * (1f + boost);
                float pulse = light ? 1f + 0.08f * Mathf.Sin(time * 2.2f) : 1f + 0.06f * Mathf.Sin(time * 3.1f);
                float size = scale * pulse * (1f + 0.35f * boost);
                _crown.position = center + new Vector3(0f, 0f, CrownDepth * scale);
                _crown.rotation = Quaternion.Euler(0f, 0f, _crownSpin);
                _crown.localScale = new Vector3(size, size, size);
            }
        }

        void Emit(Vector3 center, float scale, bool burst)
        {
            ref var p = ref _pool[_next];
            _next = (_next + 1) % PoolSize;
            p.Alt = Random.value < _recipe.AltChance;
            p.Filter.sharedMesh = p.Alt ? _recipe.Alt : _recipe.Main;
            p.Age = 0f;
            p.Life = Random.Range(_recipe.LifeMin, _recipe.LifeMax);
            p.Size = Random.Range(_recipe.SizeMin, _recipe.SizeMax) * (p.Alt ? 0.6f : 1f);
            p.Spin = Random.Range(-240f, 240f);
            p.Orbit = Vector3.zero;
            p.Velocity = Vector3.zero;
            switch (_recipe.Motion)
            {
                case Motion.Rise:
                    p.Origin = center + scale * Scatter(0.12f, 0.06f, 0.06f, -0.04f);
                    p.Velocity = new Vector3(Random.Range(-0.45f, -0.2f), Random.Range(0.25f, 0.5f), 0f);
                    if (p.Alt) p.Velocity += new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(0.1f, 0.3f), 0f);
                    if (burst) p.Velocity += new Vector3(Random.Range(-0.4f, 0f), Random.Range(-0.6f, -0.3f), 0f);
                    break;
                case Motion.Orbit:
                    p.Orbit = new Vector3(Random.Range(0f, Mathf.PI * 2f), Random.Range(0.15f, 0.2f), Random.Range(-0.06f, 0.06f));
                    // Au battement, les éclats s'écartent de l'oiseau au lieu de tourner sur place.
                    p.Velocity = new Vector3(burst ? Random.Range(0.25f, 0.45f) : 0f, 0f, 0f);
                    break;
                case Motion.Gust:
                    p.Origin = center + scale * new Vector3(Random.Range(0.18f, 0.3f), Random.Range(-0.13f, 0.13f), Random.Range(-0.08f, 0.04f));
                    p.Velocity = new Vector3(Random.Range(-1.7f, -1.1f) * (burst ? 1.4f : 1f), 0f, 0f);
                    p.Orbit = new Vector3(Random.Range(0f, Mathf.PI * 2f), Random.Range(0.03f, 0.07f), 0f);
                    break;
                case Motion.Twinkle:
                    p.Origin = center + scale * Scatter(0.2f, 0.15f, 0.06f, 0f);
                    p.Velocity = new Vector3(Random.Range(-0.2f, -0.05f), Random.Range(0.05f, 0.2f), 0f);
                    if (burst) p.Velocity += Random.insideUnitSphere * 0.4f;
                    break;
                case Motion.Smoke:
                    p.Origin = center + scale * Scatter(0.1f, 0.07f, 0.04f, -0.03f) + new Vector3(0f, 0f, 0.03f * scale);
                    p.Velocity = new Vector3(Random.Range(-0.4f, -0.2f), Random.Range(0.02f, 0.15f), 0f);
                    if (p.Alt) p.Velocity += Random.insideUnitSphere * 0.3f;
                    if (burst) p.Velocity += new Vector3(Random.Range(-0.3f, 0f), Random.Range(-0.4f, -0.2f), 0f);
                    break;
            }
            p.Transform.position = p.Origin;
            p.Transform.localScale = Vector3.zero;
            p.Transform.gameObject.SetActive(true);
        }

        /// <summary>Point au hasard dans un ellipsoïde de demi-axes donnés, décalé de <paramref name="back"/> en x.</summary>
        static Vector3 Scatter(float x, float y, float z, float back)
        {
            var r = Random.insideUnitSphere;
            return new Vector3(r.x * x + back, r.y * y, r.z * z);
        }

        void Animate(ref Particle p, Vector3 center, float scale, float dt, float time)
        {
            float t = p.Age / p.Life;
            float size = p.Size * scale;
            switch (_recipe.Motion)
            {
                case Motion.Rise:
                {
                    var pos = p.Origin + p.Velocity * (p.Age * scale);
                    p.Transform.position = pos;
                    // La flamme pointe vers où elle file, grandit vite puis s'éteint en rétrécissant.
                    p.Transform.rotation = Quaternion.FromToRotation(Vector3.up, p.Velocity);
                    size *= Mathf.Min(1f, t * 6f) * (1f - t);
                    break;
                }
                case Motion.Orbit:
                {
                    p.Orbit.x += 1.7f * dt;
                    p.Orbit.y += p.Velocity.x * dt;
                    float a = p.Orbit.x;
                    var offset = new Vector3(Mathf.Cos(a) * p.Orbit.y, p.Orbit.z + 0.03f * Mathf.Sin(a * 2f) - 0.04f * t, Mathf.Sin(a) * p.Orbit.y * 0.8f);
                    p.Transform.position = center + offset * scale;
                    p.Transform.rotation = Quaternion.Euler(p.Age * p.Spin, p.Age * p.Spin * 0.6f, a * Mathf.Rad2Deg);
                    size *= Mathf.Sin(t * Mathf.PI);
                    break;
                }
                case Motion.Gust:
                {
                    float phase = p.Orbit.x + p.Age * 9f;
                    float r = p.Orbit.y * scale;
                    var pos = p.Origin + p.Velocity * (p.Age * scale) + new Vector3(0f, Mathf.Sin(phase) * r, Mathf.Cos(phase) * r);
                    p.Transform.position = pos;
                    var dir = new Vector3(p.Velocity.x, Mathf.Cos(phase) * p.Orbit.y * 9f, -Mathf.Sin(phase) * p.Orbit.y * 9f);
                    p.Transform.rotation = Quaternion.FromToRotation(Vector3.right, dir);
                    size *= Mathf.Sin(t * Mathf.PI);
                    break;
                }
                case Motion.Twinkle:
                {
                    p.Transform.position = p.Origin + p.Velocity * (p.Age * scale);
                    p.Transform.rotation = Quaternion.Euler(0f, 0f, p.Age * p.Spin);
                    size *= Mathf.Sin(t * Mathf.PI) * (0.7f + 0.3f * Mathf.Sin(time * 20f + p.Spin));
                    break;
                }
                case Motion.Smoke:
                {
                    p.Transform.position = p.Origin + p.Velocity * (p.Age * scale);
                    p.Transform.rotation = Quaternion.Euler(0f, 0f, p.Age * p.Spin);
                    // La fumée gonfle en s'éloignant puis se résorbe.
                    size *= p.Alt ? Mathf.Sin(t * Mathf.PI) : (0.5f + 0.9f * t) * (1f - t * t * t);
                    break;
                }
            }
            var s = p.Alt ? Vector3.one : _recipe.Stretch;
            p.Transform.localScale = s * size;
        }

        static Color Opaque(Color c) => new Color(c.r, c.g, c.b, 1f);
    }
}
