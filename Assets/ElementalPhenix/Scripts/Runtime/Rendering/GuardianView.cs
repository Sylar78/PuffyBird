using ElementalPhenix.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace ElementalPhenix.Rendering
{
    /// <summary>
    /// Gardien d'un monde du Voyage : un grand esprit élémentaire, loin derrière le plan de jeu à
    /// droite, cœur lumineux à la couleur du monde, couronne de pointes, éclats en orbite et deux
    /// yeux. Il surgit quand ses paires apparaissent, se cabre à chacune (il les « lance »), puis se
    /// dissout en lumière quand l'étape est réussie. Purement visuel, jamais devant un obstacle.
    /// </summary>
    public sealed class GuardianView
    {
        const int OrbitCount = 8;
        const float AppearTime = 0.8f;
        const float VanishTime = 0.9f;
        const float ThrowTime = 0.35f;

        readonly Transform _root;
        readonly Transform _body;
        readonly Transform _orbit;
        readonly Material _material;
        readonly Vector3 _home;
        float _presence;
        float _throw;
        int _lastPipeId = -1;

        public GuardianView(Transform parent, MaterialLibrary materials, WorldSpace space)
        {
            _root = new GameObject("Gardien").transform;
            _root.SetParent(parent, false);
            // À droite, au-dessus des obstacles, loin derrière le plan de jeu.
            var cfg = space.Config;
            _home = new Vector3(space.X(cfg.Width * 0.82f), space.Y(cfg.GroundY * 0.32f), 3.2f);

            _material = materials.Lit("Gardien", Color.white, 0.7f, 0.1f, 0.9f);
            _material.SetFloat(MaterialLibrary.VertexEmission, 1.4f);
            var eyeMaterial = materials.Lit("Yeux du gardien", Color.white, 0.6f, 0f, 0.2f);
            eyeMaterial.SetFloat(MaterialLibrary.VertexEmission, 2.4f);

            // Cœur : sphère claire (teintée par le matériau), noyau blanc, couronne de pointes.
            var light = new Color(0.9f, 0.9f, 0.9f, 1f);
            var dim = new Color(0.55f, 0.55f, 0.55f, 0.5f);
            var core = new MeshBuilder()
                .AddSphere(Vector3.zero, 0.42f, light, 18, 12)
                .AddSphere(new Vector3(0f, 0f, -0.12f), 0.3f, Color.white, 12, 8);
            for (int i = 0; i < 5; i++)
            {
                float a = (i - 2) * 24f;
                core.Append(new MeshBuilder().AddCone(Vector3.zero, 0.08f, 0.34f - Mathf.Abs(i - 2) * 0.05f, dim, 6),
                    Matrix4x4.TRS(Quaternion.Euler(0f, 0f, -a) * new Vector3(0f, 0.36f, 0f), Quaternion.Euler(0f, 0f, -a), Vector3.one));
            }
            _body = Create(_root, "Corps", core.Build("Gardien"), _material);

            var eyes = new MeshBuilder()
                .AddEllipsoid(new Vector3(-0.14f, 0.06f, -0.38f), new Vector3(0.07f, 0.1f, 0.04f), Color.white, 10, 6)
                .AddEllipsoid(new Vector3(0.14f, 0.06f, -0.38f), new Vector3(0.07f, 0.1f, 0.04f), Color.white, 10, 6)
                .Build("Yeux du gardien");
            Create(_body, "Yeux", eyes, eyeMaterial);

            // Éclats en orbite, pointe vers l'extérieur.
            var shards = new MeshBuilder();
            for (int i = 0; i < OrbitCount; i++)
            {
                float a = i * 360f / OrbitCount;
                var rot = Quaternion.Euler(0f, 0f, a - 90f);
                shards.Append(new MeshBuilder().AddCone(Vector3.zero, 0.06f, 0.2f, light, 5),
                    Matrix4x4.TRS(rot * new Vector3(0f, 0.72f, 0f), rot, Vector3.one));
            }
            _orbit = Create(_root, "Orbite", shards.Build("Éclats du gardien"), _material);
            _root.gameObject.SetActive(false);
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
            return go.transform;
        }

        /// <summary>Couleur du gardien selon le monde (nouvelle partie, hors du jeu).</summary>
        public void SetTheme(Theme theme)
        {
            _material.SetColor(MaterialLibrary.BaseColor, ColorOf(theme));
            _presence = 0f;
            _lastPipeId = -1;
            _root.gameObject.SetActive(false);
        }

        static Color ColorOf(Theme theme)
        {
            switch (theme)
            {
                case Theme.Night: return Palette.Hex("#9AA8FF");
                case Theme.City: return Palette.Hex("#FF5FD2");
                case Theme.Japan: return Palette.Hex("#FF6A4D");
                case Theme.Storm: return Palette.Hex("#B9A8FF");
                case Theme.Snow: return Palette.Hex("#A8E8FF");
                case Theme.Jungle: return Palette.Hex("#7CFF8A");
                case Theme.Sky: return Palette.Hex("#FFD27A");
                case Theme.Ocean: return Palette.Hex("#5FE0FF");
                default: return Palette.Hex("#C8FF7A");
            }
        }

        public void Update(GameSimulation sim, float deltaTime, float time)
        {
            bool active = sim.GuardianActive;
            float target = active ? 1f : 0f;
            float rate = active ? 1f / AppearTime : 1f / VanishTime;
            _presence = Mathf.MoveTowards(_presence, target, rate * deltaTime);

            // Il se cabre à chaque paire lancée.
            if (active && sim.Pipes.Count > 0 && sim.Pipes.Last.Id != _lastPipeId)
            {
                _lastPipeId = sim.Pipes.Last.Id;
                _throw = ThrowTime;
            }
            if (_throw > 0f) _throw = Mathf.Max(0f, _throw - deltaTime);

            bool visible = _presence > 0.001f;
            if (_root.gameObject.activeSelf != visible) _root.gameObject.SetActive(visible);
            if (!visible) return;

            // Apparition : grossit en montant ; disparition (étape réussie) : gonfle et s'efface.
            float ease = _presence * _presence * (3f - 2f * _presence);
            float vanishing = active ? 0f : 1f - _presence;
            float scale = ease * (1f + vanishing * 0.8f);
            float lunge = _throw > 0f ? Mathf.Sin(_throw / ThrowTime * Mathf.PI) : 0f;
            float hover = Mathf.Sin(time * 1.4f) * 0.08f;
            _root.localPosition = _home + new Vector3(-lunge * 0.25f, hover + (1f - ease) * -0.6f, 0f);
            _root.localScale = new Vector3(scale, scale, scale) * (1f + lunge * 0.12f);
            _body.localRotation = Quaternion.Euler(Mathf.Sin(time * 0.9f) * 6f, Mathf.Sin(time * 0.7f) * 12f, lunge * 10f);
            _orbit.localRotation = Quaternion.Euler(0f, 0f, time * 50f);
        }
    }
}
