using ElementalPhenix.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace ElementalPhenix.Rendering
{
    /// <summary>
    /// Obstacles en 3D, propres à chaque décor (<see cref="ObstacleMeshes"/>) : en bas, une forme qui
    /// pousse du sol ; en haut, une variante qui pend. Recyclés dans un pool de 4 paires (aucune
    /// allocation en jeu ; les maillages des 9 décors sont construits au chargement, ceux de la palette
    /// contrastée à la première demande, hors partie). Chaque forme contient le rectangle de collision
    /// de 52 px de large et s'arrête au bord de l'ouverture. L'obstacle touché vibre (vertex shader), sans
    /// jamais modifier la hitbox. Franchi, il s'illumine : ses bandeaux (lanternes, néons, runes,
    /// glace...) s'embrasent puis restent allumés, comme si le phénix le rallumait au passage.
    /// </summary>
    public sealed class PipeView
    {
        const float TopLength = 14f;
        /// <summary>Longueur de l'obstacle du bas : jusque sous le sol, quelle que soit l'ouverture.</summary>
        const float BottomLength = 6f;
        const float WobbleDuration = 0.6f;
        const float WobbleAmplitude = 0.012f;
        const float BaseEmission = 1.8f;
        const float LitEmission = 2.8f;
        const float FlashEmission = 4f;
        const float LightUpTime = 0.5f;

        sealed class Pair
        {
            public GameObject Root;
            public Transform Top;
            public Transform Bottom;
            public Renderer[] Renderers;
            public int Id = -1;
            public float Wobble;
            /// <summary>Paire déjà allumée (identifiant de la paire de la simulation), −1 sinon.</summary>
            public int LitId = -1;
            public float LightUp;
        }

        readonly WorldSpace _space;
        readonly GameConfig _cfg;
        readonly Pair[] _pairs = new Pair[4];
        readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        // Maillages par décor : [thème * 2 + contrasté] ; ceux de la palette contrastée sont faits à la première demande.
        readonly Mesh[] _bottomMeshes;
        readonly Mesh[] _topMeshes;
        readonly float _half;
        Theme _theme;
        bool _contrast;
        readonly MeshFilter[] _bottomFilters = new MeshFilter[4];
        readonly MeshFilter[] _topFilters = new MeshFilter[4];

        public PipeView(Transform parent, MaterialLibrary materials, WorldSpace space)
        {
            _space = space;
            _cfg = space.Config;
            var root = new GameObject("Obstacles").transform;
            root.SetParent(parent, false);

            var material = materials.Lit("Obstacle", Color.white, 0.6f, 0f, 0.45f);
            material.SetFloat(MaterialLibrary.WobbleFrequency, 38f);
            // Bandeaux lumineux (néons, runes, glace) : l'alpha des couleurs de sommet dose la lueur.
            material.SetFloat(MaterialLibrary.VertexEmission, BaseEmission);

            // Demi-largeur du rectangle de collision, avec une marge d'arrondi : la forme le recouvre toujours.
            _half = WorldSpace.Length(_cfg.PipeWidth) * 0.5f + 0.004f;
            int themes = System.Enum.GetValues(typeof(Theme)).Length;
            _bottomMeshes = new Mesh[themes * 2];
            _topMeshes = new Mesh[themes * 2];
            for (int t = 0; t < themes; t++) BuildMeshes((Theme)t, false);

            for (int i = 0; i < _pairs.Length; i++)
            {
                var pair = new Pair { Root = new GameObject("Paire " + i) };
                pair.Root.transform.SetParent(root, false);
                pair.Bottom = CreatePart(pair.Root.transform, "Obstacle bas", _bottomMeshes[0], material);
                pair.Top = CreatePart(pair.Root.transform, "Obstacle haut", _topMeshes[0], material);
                // L'obstacle du haut est retourné : il s'étend vers le haut depuis le bord de l'ouverture.
                pair.Top.localScale = new Vector3(1f, -1f, 1f);
                _bottomFilters[i] = pair.Bottom.GetComponent<MeshFilter>();
                _topFilters[i] = pair.Top.GetComponent<MeshFilter>();
                pair.Renderers = pair.Root.GetComponentsInChildren<Renderer>();
                pair.Root.SetActive(false);
                _pairs[i] = pair;
            }
        }

        void BuildMeshes(Theme theme, bool contrast)
        {
            int index = (int)theme * 2 + (contrast ? 1 : 0);
            if (_bottomMeshes[index] != null) return;
            string suffix = theme + (contrast ? " contrasté" : "");
            _bottomMeshes[index] = ObstacleMeshes.Build(theme, contrast, _half, BottomLength, false, "Obstacle bas " + suffix);
            _topMeshes[index] = ObstacleMeshes.Build(theme, contrast, _half, TopLength, true, "Obstacle haut " + suffix);
        }

        /// <summary>Obstacles du décor de la partie. Hors partie (changement de décor).</summary>
        public void SetTheme(Theme theme)
        {
            _theme = theme;
            ApplyMeshes();
        }

        /// <summary>Palette contrastée des obstacles (accessibilité) : change seulement les maillages, jamais la hitbox. Rare, hors partie.</summary>
        public void SetHighContrast(bool on)
        {
            _contrast = on;
            ApplyMeshes();
        }

        void ApplyMeshes()
        {
            // Palette contrastée : construite la première fois qu'on la demande (réglage, hors partie).
            if (_contrast) BuildMeshes(_theme, true);
            int index = (int)_theme * 2 + (_contrast ? 1 : 0);
            for (int i = 0; i < _bottomFilters.Length; i++)
            {
                _bottomFilters[i].sharedMesh = _bottomMeshes[index];
                _topFilters[i].sharedMesh = _topMeshes[index];
            }
        }

        static Transform CreatePart(Transform parent, string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.On;
            r.receiveShadows = true;
            return go.transform;
        }

        /// <summary>Déclenche la vibration du tuyau touché.</summary>
        public void Hit(int pipeId)
        {
            if (pipeId < 0) return;
            var pair = _pairs[pipeId % _pairs.Length];
            if (pair.Id == pipeId) pair.Wobble = WobbleDuration;
        }

        /// <param name="pipeGap">Ouverture de la partie en cours (elle change selon l'étape du Voyage).</param>
        public void Update(PipeField pipes, float pipeGap, float alpha, float deltaTime)
        {
            for (int i = 0; i < _pairs.Length; i++) _pairs[i].Id = -1;

            for (int i = 0; i < pipes.Count; i++)
            {
                ref var p = ref pipes[i];
                var pair = _pairs[p.Id % _pairs.Length];
                if (pair.Id != -1) continue;
                pair.Id = p.Id;
                // Franchie (point marqué) : l'obstacle s'allume une fois pour toutes.
                if (p.Scored && pair.LitId != p.Id)
                {
                    pair.LitId = p.Id;
                    pair.LightUp = LightUpTime;
                }

                float x = Mathf.LerpUnclamped(p.PrevX, p.X, alpha) + _cfg.PipeWidth * 0.5f;
                // Paires mobiles : les deux tuyaux se décalent ensemble, l'ouverture garde sa hauteur.
                float gapTop = p.BaseTop + Mathf.LerpUnclamped(p.PrevShift, p.Shift, alpha);
                float gapTopY = _space.Y(gapTop);
                float gapBottomY = _space.Y(gapTop + pipeGap);
                pair.Root.transform.localPosition = new Vector3(_space.X(x), 0f, 0f);

                // Bas : du bord de l'ouverture jusque sous le sol. Haut : du bord de l'ouverture jusque
                // bien au-dessus de l'écran (obstacle « infini »).
                pair.Bottom.localPosition = new Vector3(0f, gapBottomY, 0f);
                pair.Top.localPosition = new Vector3(0f, gapTopY, 0f);

                // Obstacle seul : l'autre est masqué (l'ouverture touche le haut de l'écran ou le sol).
                SetActive(pair.Top, p.Kind != PipeKind.BottomOnly);
                SetActive(pair.Bottom, p.Kind != PipeKind.TopOnly);

                if (!pair.Root.activeSelf) pair.Root.SetActive(true);
            }

            for (int i = 0; i < _pairs.Length; i++)
            {
                var pair = _pairs[i];
                if (pair.Id == -1)
                {
                    if (pair.Root.activeSelf) pair.Root.SetActive(false);
                    if (pair.Wobble > 0f || pair.LitId != -1)
                    {
                        pair.Wobble = 0f;
                        pair.LitId = -1;
                        pair.LightUp = 0f;
                        for (int r = 0; r < pair.Renderers.Length; r++) pair.Renderers[r].SetPropertyBlock(null);
                    }
                    continue;
                }
                UpdateBlock(pair, deltaTime);
            }
        }

        static void SetActive(Transform part, bool active)
        {
            if (part.gameObject.activeSelf != active) part.gameObject.SetActive(active);
        }

        /// <summary>Vibration de l'obstacle touché et lueur de l'obstacle franchi, par bloc de propriétés.</summary>
        void UpdateBlock(Pair pair, float deltaTime)
        {
            // Emplacement repris par une nouvelle paire pas encore franchie : lueur d'origine.
            if (pair.LitId != -1 && pair.LitId != pair.Id)
            {
                pair.LitId = -1;
                pair.LightUp = 0f;
                for (int r = 0; r < pair.Renderers.Length; r++) pair.Renderers[r].SetPropertyBlock(null);
            }
            bool lit = pair.LitId == pair.Id;
            if (pair.Wobble <= 0f && !lit) return;
            pair.Wobble = Mathf.Max(0f, pair.Wobble - deltaTime);
            if (pair.LightUp > 0f) pair.LightUp = Mathf.Max(0f, pair.LightUp - deltaTime);
            float k = pair.Wobble / WobbleDuration;
            // Embrasement bref, puis lueur un peu plus vive qu'avant le passage.
            float flash = pair.LightUp / LightUpTime;
            float emission = lit ? Mathf.Lerp(LitEmission, FlashEmission, flash * flash) : BaseEmission;
            for (int r = 0; r < pair.Renderers.Length; r++)
            {
                pair.Renderers[r].GetPropertyBlock(_block);
                _block.SetFloat(MaterialLibrary.WobbleAmount, WobbleAmplitude * k * k);
                _block.SetFloat(MaterialLibrary.VertexEmission, emission);
                pair.Renderers[r].SetPropertyBlock(_block);
            }
        }
    }
}
