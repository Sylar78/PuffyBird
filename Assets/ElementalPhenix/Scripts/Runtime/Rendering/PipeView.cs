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
    /// jamais modifier la hitbox. Franchi, il s'illumine : ses bandeaux (lanternes, néons, runes,
    /// glace...) s'embrasent puis restent allumés, comme si le phénix le rallumait au passage.
    /// Pouvoirs d'élément : l'obstacle brûlé par le feu flambe puis se réduit en cendres (il ne
    /// touche déjà plus), l'ouverture écartée par la lumière s'agrandit avec sa hitbox.
    /// </summary>
    public sealed class PipeView
    {
        const float TopPipeHeight = 14f;
        const float BuriedDepth = 0.4f;
        const float WobbleDuration = 0.6f;
        const float WobbleAmplitude = 0.012f;
        const float BaseEmission = 1.8f;
        const float LitEmission = 2.8f;
        const float FlashEmission = 4f;
        const float LightUpTime = 0.5f;
        const float BurnTime = 0.45f;
        const float BurnEmission = 7f;

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
            /// <summary>Paire déjà allumée (identifiant de la paire de la simulation), −1 sinon.</summary>
            public int LitId = -1;
            public float LightUp;
            /// <summary>Paire brûlée par le feu (identifiant de la simulation), −1 sinon ; temps écoulé depuis.</summary>
            public int BurnId = -1;
            public float Burn;
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
            material.SetFloat(MaterialLibrary.VertexEmission, BaseEmission);

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

        /// <param name="pipeGap">Ouverture de la partie en cours (elle change selon l'étape du Voyage), sans l'élargissement de la lumière.</param>
        public void Update(PipeField pipes, float pipeGap, float alpha, float deltaTime)
        {
            for (int i = 0; i < _pairs.Length; i++) _pairs[i].Id = -1;

            float capHeight = WorldSpace.Length(_cfg.PipeCapHeight);
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
                // Paires mobiles : les deux tuyaux se décalent ensemble, l'ouverture garde sa hauteur
                // (agrandie autour de son milieu par la lumière, comme la hitbox).
                float widen = Mathf.LerpUnclamped(p.PrevWiden, p.Widen, alpha);
                float gapTop = p.BaseTop + Mathf.LerpUnclamped(p.PrevShift, p.Shift, alpha) - widen * 0.5f;
                float gapTopY = _space.Y(gapTop);
                float gapBottomY = _space.Y(gapTop + pipeGap + widen);
                pair.Root.transform.localPosition = new Vector3(_space.X(x), 0f, 0f);
                // Brûlé : l'obstacle flambe puis se réduit en cendres.
                if (p.Burned && pair.BurnId != p.Id)
                {
                    pair.BurnId = p.Id;
                    pair.Burn = 0f;
                }
                float ash = pair.BurnId == p.Id ? Mathf.Clamp01(pair.Burn / BurnTime) : 0f;
                float width = 1f - ash * ash;
                pair.Root.transform.localScale = new Vector3(width, 1f, width);

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
                bool gone = ash >= 1f;
                SetActive(pair.TopBody, p.Kind != PipeKind.BottomOnly && !gone);
                SetActive(pair.TopCap, p.Kind != PipeKind.BottomOnly && !gone);
                SetActive(pair.BottomBody, p.Kind != PipeKind.TopOnly && !gone);
                SetActive(pair.BottomCap, p.Kind != PipeKind.TopOnly && !gone);

                if (!pair.Root.activeSelf) pair.Root.SetActive(true);
            }

            for (int i = 0; i < _pairs.Length; i++)
            {
                var pair = _pairs[i];
                if (pair.Id == -1)
                {
                    if (pair.Root.activeSelf) pair.Root.SetActive(false);
                    if (pair.Wobble > 0f || pair.LitId != -1 || pair.BurnId != -1)
                    {
                        pair.Wobble = 0f;
                        pair.LitId = -1;
                        pair.LightUp = 0f;
                        pair.BurnId = -1;
                        pair.Root.transform.localScale = Vector3.one;
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
            // Emplacement repris par une paire qui n'a pas brûlé : pleine largeur.
            if (pair.BurnId != -1 && pair.BurnId != pair.Id)
            {
                pair.BurnId = -1;
                pair.Root.transform.localScale = Vector3.one;
                for (int r = 0; r < pair.Renderers.Length; r++) pair.Renderers[r].SetPropertyBlock(null);
            }
            bool lit = pair.LitId == pair.Id;
            bool burning = pair.BurnId == pair.Id;
            if (pair.Wobble <= 0f && !lit && !burning) return;
            pair.Wobble = Mathf.Max(0f, pair.Wobble - deltaTime);
            if (pair.LightUp > 0f) pair.LightUp = Mathf.Max(0f, pair.LightUp - deltaTime);
            if (burning) pair.Burn += deltaTime;
            float k = pair.Wobble / WobbleDuration;
            // Embrasement bref, puis lueur un peu plus vive qu'avant le passage.
            float flash = pair.LightUp / LightUpTime;
            float emission = lit ? Mathf.Lerp(LitEmission, FlashEmission, flash * flash) : BaseEmission;
            if (burning) emission = BurnEmission;
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
