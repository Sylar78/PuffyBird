using ElementalPhenix.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace ElementalPhenix.Rendering
{
    /// <summary>
    /// Obstacles en 3D, propres à chaque décor (<see cref="ObstacleStyles"/>) : fût à facettes et
    /// chapeau en couches, recyclés dans un pool de 4 paires (aucune allocation en jeu ; les maillages
    /// des 9 décors sont construits au chargement). La géométrie visible correspond aux rectangles de
    /// collision : chapeau de 52 px, fût de 48 px. L'obstacle touché vibre (vertex shader), sans
    /// jamais modifier la hitbox.
    /// </summary>
    public sealed class PipeView
    {
        const float TopPipeHeight = 14f;
        const float BuriedDepth = 0.4f;
        const float WobbleDuration = 0.6f;
        const float WobbleAmplitude = 0.012f;

        sealed class Pair
        {
            public GameObject Root;
            public Transform TopBody;
            public Transform TopCap;
            public Transform BottomBody;
            public Transform BottomCap;
            public Renderer[] Renderers;
            public int Id = -1;
            public float Wobble;
        }

        readonly WorldSpace _space;
        readonly GameConfig _cfg;
        readonly Pair[] _pairs = new Pair[4];
        readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        // Maillages par décor : [thème * 2 + contrasté].
        readonly Mesh[] _bodyMeshes;
        readonly Mesh[] _capMeshes;
        Theme _theme;
        bool _contrast;
        readonly MeshFilter[] _bodyFilters = new MeshFilter[8];
        readonly MeshFilter[] _capFilters = new MeshFilter[8];

        public PipeView(Transform parent, MaterialLibrary materials, WorldSpace space)
        {
            _space = space;
            _cfg = space.Config;
            var root = new GameObject("Tuyaux").transform;
            root.SetParent(parent, false);

            var material = materials.Lit("Obstacle", Color.white, 0.6f, 0f, 0.45f);
            material.SetFloat(MaterialLibrary.WobbleFrequency, 38f);
            // Bandeaux lumineux (néons, runes, glace) : l'alpha des couleurs de sommet dose la lueur.
            material.SetFloat(MaterialLibrary.VertexEmission, 1.8f);

            float bodyRadius = WorldSpace.Length(_cfg.PipeBodyWidth) * 0.5f;
            float capRadius = WorldSpace.Length(_cfg.PipeWidth) * 0.5f;
            float capHeight = WorldSpace.Length(_cfg.PipeCapHeight);

            // Fût : prisme unitaire (hauteur 1, base à l'origine), étiré à la bonne longueur.
            // Chapeau : couches empilées, base côté fût, bord côté ouverture. Deux palettes par décor : normale et contrastée.
            var themes = (Theme[])System.Enum.GetValues(typeof(Theme));
            _bodyMeshes = new Mesh[themes.Length * 2];
            _capMeshes = new Mesh[themes.Length * 2];
            foreach (var theme in themes)
            {
                var style = ObstacleStyles.For(theme);
                for (int c = 0; c < 2; c++)
                {
                    bool contrast = c == 1;
                    string suffix = theme + (contrast ? " contrasté" : "");
                    _bodyMeshes[(int)theme * 2 + c] = BuildBody(bodyRadius, style, contrast, "Fût " + suffix);
                    _capMeshes[(int)theme * 2 + c] = BuildCap(capRadius, capHeight, style, contrast, "Chapeau " + suffix);
                }
            }

            for (int i = 0; i < _pairs.Length; i++)
            {
                var pair = new Pair { Root = new GameObject("Paire " + i) };
                pair.Root.transform.SetParent(root, false);
                pair.TopBody = CreatePart(pair.Root.transform, "Fût haut", _bodyMeshes[0], material);
                pair.TopCap = CreatePart(pair.Root.transform, "Chapeau haut", _capMeshes[0], material);
                pair.BottomBody = CreatePart(pair.Root.transform, "Fût bas", _bodyMeshes[0], material);
                pair.BottomCap = CreatePart(pair.Root.transform, "Chapeau bas", _capMeshes[0], material);
                // Le chapeau du haut est retourné : lèvre vers le bas (§7.1).
                pair.TopCap.localRotation = Quaternion.Euler(180f, 0f, 0f);
                _bodyFilters[i * 2] = pair.TopBody.GetComponent<MeshFilter>();
                _bodyFilters[i * 2 + 1] = pair.BottomBody.GetComponent<MeshFilter>();
                _capFilters[i * 2] = pair.TopCap.GetComponent<MeshFilter>();
                _capFilters[i * 2 + 1] = pair.BottomCap.GetComponent<MeshFilter>();
                pair.Renderers = pair.Root.GetComponentsInChildren<Renderer>();
                pair.Root.SetActive(false);
                _pairs[i] = pair;
            }
        }

        static Mesh BuildBody(float radius, ObstacleStyles.Style style, bool contrast, string name)
        {
            var color = ObstacleStyles.ColorOf(style, ObstacleStyles.Role.Body, contrast);
            return new MeshBuilder().AddPrism(Vector3.zero, radius, radius, 1f, color, style.Sides, caps: false).Build(name);
        }

        static Mesh BuildCap(float radius, float height, ObstacleStyles.Style style, bool contrast, string name)
        {
            var builder = new MeshBuilder();
            foreach (var layer in style.Cap)
            {
                var color = ObstacleStyles.ColorOf(style, layer.Role, contrast);
                builder.AddPrism(new Vector3(0f, height * layer.From, 0f), radius * layer.Bottom, radius * layer.Top, height * layer.Height, color, style.Sides);
            }
            return builder.Build(name);
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
            int index = (int)_theme * 2 + (_contrast ? 1 : 0);
            for (int i = 0; i < _bodyFilters.Length; i++)
            {
                _bodyFilters[i].sharedMesh = _bodyMeshes[index];
                _capFilters[i].sharedMesh = _capMeshes[index];
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

        public void Update(PipeField pipes, float alpha, float deltaTime)
        {
            for (int i = 0; i < _pairs.Length; i++) _pairs[i].Id = -1;

            float capHeight = WorldSpace.Length(_cfg.PipeCapHeight);
            for (int i = 0; i < pipes.Count; i++)
            {
                ref var p = ref pipes[i];
                var pair = _pairs[p.Id % _pairs.Length];
                if (pair.Id != -1) continue;
                pair.Id = p.Id;

                float x = Mathf.LerpUnclamped(p.PrevX, p.X, alpha) + _cfg.PipeWidth * 0.5f;
                // Paires mobiles : les deux tuyaux se décalent ensemble, l'ouverture garde sa hauteur.
                float gapTop = p.BaseTop + Mathf.LerpUnclamped(p.PrevShift, p.Shift, alpha);
                float gapTopY = _space.Y(gapTop);
                float gapBottomY = _space.Y(gapTop + _cfg.PipeGap);
                pair.Root.transform.localPosition = new Vector3(_space.X(x), 0f, 0f);

                // Bas : du fond du sol jusqu'au bas de l'ouverture, chapeau en haut.
                float bottomLength = gapBottomY + BuriedDepth;
                pair.BottomBody.localPosition = new Vector3(0f, -BuriedDepth, 0f);
                pair.BottomBody.localScale = new Vector3(1f, Mathf.Max(0.01f, bottomLength), 1f);
                pair.BottomCap.localPosition = new Vector3(0f, gapBottomY - capHeight, 0f);

                // Haut : du haut de l'ouverture jusque bien au-dessus de l'écran (tuyau « infini »).
                pair.TopBody.localPosition = new Vector3(0f, gapTopY, 0f);
                pair.TopBody.localScale = new Vector3(1f, TopPipeHeight, 1f);
                pair.TopCap.localPosition = new Vector3(0f, gapTopY + capHeight, 0f);

                // Tuyau seul : l'autre est masqué (l'ouverture touche le haut de l'écran ou le sol).
                SetActive(pair.TopBody, p.Kind != PipeKind.BottomOnly);
                SetActive(pair.TopCap, p.Kind != PipeKind.BottomOnly);
                SetActive(pair.BottomBody, p.Kind != PipeKind.TopOnly);
                SetActive(pair.BottomCap, p.Kind != PipeKind.TopOnly);

                if (!pair.Root.activeSelf) pair.Root.SetActive(true);
            }

            for (int i = 0; i < _pairs.Length; i++)
            {
                var pair = _pairs[i];
                if (pair.Id == -1)
                {
                    if (pair.Root.activeSelf) pair.Root.SetActive(false);
                    if (pair.Wobble > 0f)
                    {
                        pair.Wobble = 0f;
                        for (int r = 0; r < pair.Renderers.Length; r++) pair.Renderers[r].SetPropertyBlock(null);
                    }
                    continue;
                }
                UpdateWobble(pair, deltaTime);
            }
        }

        static void SetActive(Transform part, bool active)
        {
            if (part.gameObject.activeSelf != active) part.gameObject.SetActive(active);
        }

        void UpdateWobble(Pair pair, float deltaTime)
        {
            if (pair.Wobble <= 0f) return;
            pair.Wobble = Mathf.Max(0f, pair.Wobble - deltaTime);
            float k = pair.Wobble / WobbleDuration;
            for (int r = 0; r < pair.Renderers.Length; r++)
            {
                if (pair.Wobble > 0f)
                {
                    pair.Renderers[r].GetPropertyBlock(_block);
                    _block.SetFloat(MaterialLibrary.WobbleAmount, WobbleAmplitude * k * k);
                    pair.Renderers[r].SetPropertyBlock(_block);
                }
                else
                {
                    // Sans bloc de propriétés, le rendu reste compatible avec le SRP Batcher.
                    pair.Renderers[r].SetPropertyBlock(null);
                }
            }
        }
    }
}
