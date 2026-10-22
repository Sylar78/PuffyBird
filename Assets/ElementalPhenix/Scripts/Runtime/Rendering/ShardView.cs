using ElementalPhenix.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace ElementalPhenix.Rendering
{
    /// <summary>
    /// Éclats d'élément : petits cristaux taillés en double pointe, cyan et violet, qui tournent et
    /// flottent. Une instance par emplacement de <see cref="GameSimulation.Shards"/>, créée au
    /// chargement. La hitbox reste le cercle de la simulation.
    /// </summary>
    public sealed class ShardView
    {
        const float SpinSpeed = 160f;

        readonly WorldSpace _space;
        readonly GameObject[] _shards;

        public ShardView(Transform parent, MaterialLibrary materials, WorldSpace space, int capacity)
        {
            _space = space;
            var cfg = space.Config;
            var root = new GameObject("Éclats").transform;
            root.SetParent(parent, false);

            float r = WorldSpace.Length(cfg.ShardRadius) * 0.9f;
            float h = r * 1.5f;
            var top = Palette.Hex("#8FE6FF");
            var bottom = Palette.Hex("#B07CFF");
            var mesh = new MeshBuilder()
                .AddCone(Vector3.zero, r, h, top, 6)
                .Append(new MeshBuilder().AddCone(Vector3.zero, r, h, bottom, 6), Matrix4x4.Rotate(Quaternion.Euler(180f, 0f, 0f)))
                .Build("Éclat");
            var material = materials.Lit("Éclat", Color.white, 0.9f, 0.2f, 0.8f);
            material.SetFloat(MaterialLibrary.VertexEmission, 1.3f);

            _shards = new GameObject[capacity];
            for (int i = 0; i < capacity; i++)
            {
                var go = new GameObject("Éclat " + i);
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                go.SetActive(false);
                _shards[i] = go;
            }
        }

        public void Update(StarField shards, float alpha, float time)
        {
            for (int i = 0; i < _shards.Length; i++)
            {
                var go = _shards[i];
                ref var s = ref shards[i];
                if (!s.Active)
                {
                    if (go.activeSelf) go.SetActive(false);
                    continue;
                }
                float x = Mathf.LerpUnclamped(s.PrevX, s.X, alpha);
                float bob = Mathf.Sin(time * 2.6f + i * 1.3f) * 2.5f;
                var t = go.transform;
                t.localPosition = _space.ToWorld(x, s.Y + bob, 0f);
                t.localRotation = Quaternion.Euler(0f, time * SpinSpeed + i * 70f, 10f);
                float pulse = 1f + 0.1f * Mathf.Sin(time * 6f + i);
                t.localScale = new Vector3(pulse, pulse, pulse);
                if (!go.activeSelf) go.SetActive(true);
            }
        }
    }
}
