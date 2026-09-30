using PuffyBird.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace PuffyBird.Rendering
{
    /// <summary>
    /// Étoiles de vitesse : étoiles dorées bombées, lumineuses (le bloom les fait briller), qui
    /// tournent et pulsent doucement. Une instance par emplacement de <see cref="StarField"/>,
    /// créée au chargement. La hitbox reste le cercle de la simulation.
    /// </summary>
    public sealed class StarView
    {
        const float SpinSpeed = 140f;
        const float VisualScale = 1.4f;

        readonly WorldSpace _space;
        readonly GameConfig _cfg;
        readonly GameObject[] _stars;

        public StarView(Transform parent, MaterialLibrary materials, WorldSpace space, int capacity)
        {
            _space = space;
            _cfg = space.Config;
            var root = new GameObject("Étoiles").transform;
            root.SetParent(parent, false);

            float outer = WorldSpace.Length(_cfg.StarRadius * VisualScale);
            var mesh = new MeshBuilder()
                .AddStar(Vector3.zero, outer, outer * 0.46f, outer * 0.55f, Palette.Star)
                .Build("Étoile");
            var material = materials.Lit("Étoile", Color.white, 0.8f, 0.25f, 0.7f);
            material.SetFloat(MaterialLibrary.VertexEmission, 1.1f);
            material.SetColor(MaterialLibrary.RimColor, Palette.StarRim);

            _stars = new GameObject[capacity];
            for (int i = 0; i < capacity; i++)
            {
                var go = new GameObject("Étoile " + i);
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = ShadowCastingMode.Off;
                go.SetActive(false);
                _stars[i] = go;
            }
        }

        public void Update(StarField stars, float alpha, float time)
        {
            for (int i = 0; i < _stars.Length; i++)
            {
                var go = _stars[i];
                ref var s = ref stars[i];
                if (!s.Active)
                {
                    if (go.activeSelf) go.SetActive(false);
                    continue;
                }
                float x = Mathf.LerpUnclamped(s.PrevX, s.X, alpha);
                float bob = Mathf.Sin(time * 3f + i) * 2f;
                var t = go.transform;
                t.localPosition = _space.ToWorld(x, s.Y + bob, 0f);
                t.localRotation = Quaternion.Euler(0f, time * SpinSpeed + i * 50f, Mathf.Sin(time * 2f + i) * 12f);
                float pulse = 1f + 0.08f * Mathf.Sin(time * 7f + i * 1.7f);
                t.localScale = new Vector3(pulse, pulse, pulse);
                if (!go.activeSelf) go.SetActive(true);
            }
        }
    }
}
