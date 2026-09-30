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
            var bodyMesh = new MeshBuilder()
                .AddCylinder(Vector3.zero, bodyRadius, 1f, Palette.PipeBody, 28, caps: false)
                .Build("Fût");
            // Chapeau : lèvre jade avec une bague dorée et un léger biseau, base à l'origine.
            var capMesh = new MeshBuilder()
                .AddFrustum(Vector3.zero, capRadius * 0.96f, capRadius, capHeight * 0.18f, Palette.PipeShade, 28)
                .AddCylinder(new Vector3(0f, capHeight * 0.18f, 0f), capRadius, capHeight * 0.5f, Palette.PipeBody, 28)
                .AddCylinder(new Vector3(0f, capHeight * 0.68f, 0f), capRadius * 1.03f, capHeight * 0.14f, Palette.PipeRim, 28)
                .AddFrustum(new Vector3(0f, capHeight * 0.82f, 0f), capRadius, capRadius * 0.9f, capHeight * 0.18f, Palette.PipeBody, 28)
                .Build("Chapeau");

            for (int i = 0; i < _pairs.Length; i++)
            {
                var pair = new Pair { Root = new GameObject("Paire " + i) };
                pair.Root.transform.SetParent(root, false);
                pair.TopBody = CreatePart(pair.Root.transform, "Fût haut", bodyMesh, material);
                pair.TopCap = CreatePart(pair.Root.transform, "Chapeau haut", capMesh, material);
                pair.BottomBody = CreatePart(pair.Root.transform, "Fût bas", bodyMesh, material);
                pair.BottomCap = CreatePart(pair.Root.transform, "Chapeau bas", capMesh, material);
                // Le chapeau du haut est retourné : lèvre vers le bas (§7.1).
                pair.TopCap.localRotation = Quaternion.Euler(180f, 0f, 0f);
                pair.Renderers = pair.Root.GetComponentsInChildren<Renderer>();
                pair.Root.SetActive(false);
                _pairs[i] = pair;
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
                float gapTop = p.GapTop + Mathf.LerpUnclamped(p.PrevShift, p.Shift, alpha);
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
