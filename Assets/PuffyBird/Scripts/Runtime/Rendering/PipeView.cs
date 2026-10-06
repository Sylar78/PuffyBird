using PuffyBird.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace PuffyBird.Rendering
{
    /// <summary>
    /// Tuyaux en 3D : fûts cylindriques et chapeaux à bague dorée, recyclés dans un pool de 4
    /// paires (aucune allocation en jeu). La géométrie visible correspond aux rectangles de
    /// collision : chapeau de 52 px, fût de 48 px. Le tuyau touché vibre (vertex shader), sans
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
        readonly Mesh _bodyMesh;
        readonly Mesh _capMesh;
        readonly Mesh _bodyMeshContrast;
        readonly Mesh _capMeshContrast;
        readonly MeshFilter[] _bodyFilters = new MeshFilter[8];
        readonly MeshFilter[] _capFilters = new MeshFilter[8];

        public PipeView(Transform parent, MaterialLibrary materials, WorldSpace space)
        {
            _space = space;
            _cfg = space.Config;
            var root = new GameObject("Tuyaux").transform;
            root.SetParent(parent, false);

            var material = materials.Lit("Tuyau", Color.white, 0.6f, 0f, 0.45f);
            material.SetFloat(MaterialLibrary.WobbleFrequency, 38f);

            float bodyRadius = WorldSpace.Length(_cfg.PipeBodyWidth) * 0.5f;
            float capRadius = WorldSpace.Length(_cfg.PipeWidth) * 0.5f;
            float capHeight = WorldSpace.Length(_cfg.PipeCapHeight);

            // Fût : cylindre unitaire (hauteur 1, base à l'origine), étiré à la bonne longueur.
            // Chapeau : lèvre avec une bague et un léger biseau, base à l'origine. Deux palettes : normale et contrastée.
            _bodyMesh = BuildBody(bodyRadius, Palette.PipeBody, "Fût");
            _capMesh = BuildCap(capRadius, capHeight, Palette.PipeBody, Palette.PipeShade, Palette.PipeRim, "Chapeau");
            _bodyMeshContrast = BuildBody(bodyRadius, Palette.PipeBodyContrast, "Fût contrasté");
            _capMeshContrast = BuildCap(capRadius, capHeight, Palette.PipeBodyContrast, Palette.PipeShadeContrast, Palette.PipeRimContrast, "Chapeau contrasté");

            for (int i = 0; i < _pairs.Length; i++)
            {
                var pair = new Pair { Root = new GameObject("Paire " + i) };
                pair.Root.transform.SetParent(root, false);
                pair.TopBody = CreatePart(pair.Root.transform, "Fût haut", _bodyMesh, material);
                pair.TopCap = CreatePart(pair.Root.transform, "Chapeau haut", _capMesh, material);
                pair.BottomBody = CreatePart(pair.Root.transform, "Fût bas", _bodyMesh, material);
                pair.BottomCap = CreatePart(pair.Root.transform, "Chapeau bas", _capMesh, material);
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

        static Mesh BuildBody(float radius, Color body, string name)
        {
            return new MeshBuilder().AddCylinder(Vector3.zero, radius, 1f, body, 28, caps: false).Build(name);
        }

        static Mesh BuildCap(float radius, float height, Color body, Color shade, Color rim, string name)
        {
            return new MeshBuilder()
                .AddFrustum(Vector3.zero, radius * 0.96f, radius, height * 0.18f, shade, 28)
                .AddCylinder(new Vector3(0f, height * 0.18f, 0f), radius, height * 0.5f, body, 28)
                .AddCylinder(new Vector3(0f, height * 0.68f, 0f), radius * 1.03f, height * 0.14f, rim, 28)
                .AddFrustum(new Vector3(0f, height * 0.82f, 0f), radius, radius * 0.9f, height * 0.18f, body, 28)
                .Build(name);
        }

        /// <summary>Palette contrastée des tuyaux (accessibilité) : change seulement les maillages, jamais la hitbox. Rare, hors partie.</summary>
        public void SetHighContrast(bool on)
        {
            for (int i = 0; i < _bodyFilters.Length; i++)
            {
                _bodyFilters[i].sharedMesh = on ? _bodyMeshContrast : _bodyMesh;
                _capFilters[i].sharedMesh = on ? _capMeshContrast : _capMesh;
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
