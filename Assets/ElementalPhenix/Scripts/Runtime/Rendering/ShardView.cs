using ElementalPhenix.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace ElementalPhenix.Rendering
{
    /// <summary>
    /// Éclats d'élément : petits cristaux taillés en double pointe, cyan et violet, qui tournent et
    /// flottent. Une instance par emplacement de <see cref="GameSimulation.Shards"/>, créée au
    /// chargement. La hitbox reste le cercle de la simulation. Ramassé, l'éclat file vers le phénix
    /// et s'y fond (<see cref="AbsorbTime"/>) : la prise se fait au bord du cercle de la simulation,
    /// plus large que le corps élancé du phénix, et sans ce trajet l'éclat disparaîtrait à distance.
    /// </summary>
    public sealed class ShardView
    {
        const float SpinSpeed = 160f;
        const float AbsorbTime = 0.14f;
        /// <summary>Marge sur la distance de prise (flottement, interpolation) pour reconnaître un éclat ramassé.</summary>
        const float CatchMargin = 1.6f;

        readonly WorldSpace _space;
        readonly Transform _root;
        readonly GameObject[] _shards;
        readonly bool[] _wasActive;
        readonly Vector3[] _lastPosition;
        readonly float[] _absorb;
        readonly float _catchDistance;

        public ShardView(Transform parent, MaterialLibrary materials, WorldSpace space, int capacity)
        {
            _space = space;
            var cfg = space.Config;
            var root = new GameObject("Éclats").transform;
            root.SetParent(parent, false);
            _root = root;

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

            _catchDistance = WorldSpace.Length(cfg.BirdRadius + cfg.ShardRadius) * CatchMargin;
            _wasActive = new bool[capacity];
            _lastPosition = new Vector3[capacity];
            _absorb = new float[capacity];
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

        /// <param name="birdWorld">Position du phénix (repère du monde), vers laquelle filent les éclats ramassés.</param>
        public void Update(StarField shards, float alpha, float time, float deltaTime, Vector3 birdWorld)
        {
            var bird = _root.InverseTransformPoint(birdWorld);
            for (int i = 0; i < _shards.Length; i++)
            {
                var go = _shards[i];
                var t = go.transform;
                ref var s = ref shards[i];
                if (!s.Active)
                {
                    // Disparu près du phénix : ramassé, il file vers lui (sinon sorti de l'écran ou terrain vidé).
                    if (_wasActive[i] && (_lastPosition[i] - bird).sqrMagnitude <= _catchDistance * _catchDistance)
                        _absorb[i] = AbsorbTime;
                    _wasActive[i] = false;
                    if (_absorb[i] > 0f)
                    {
                        _absorb[i] = Mathf.Max(0f, _absorb[i] - deltaTime);
                        float k = 1f - _absorb[i] / AbsorbTime;
                        t.localPosition = Vector3.Lerp(_lastPosition[i], bird, k * (2f - k));
                        float size = 1f - k * k;
                        t.localScale = new Vector3(size, size, size);
                        if (_absorb[i] > 0f) continue;
                    }
                    if (go.activeSelf) go.SetActive(false);
                    continue;
                }
                _absorb[i] = 0f;
                _wasActive[i] = true;
                float x = Mathf.LerpUnclamped(s.PrevX, s.X, alpha);
                float bob = Mathf.Sin(time * 2.6f + i * 1.3f) * 2.5f;
                t.localPosition = _space.ToWorld(x, s.Y + bob, 0f);
                _lastPosition[i] = t.localPosition;
                t.localRotation = Quaternion.Euler(0f, time * SpinSpeed + i * 70f, 10f);
                float pulse = 1f + 0.1f * Mathf.Sin(time * 6f + i);
                t.localScale = new Vector3(pulse, pulse, pulse);
                if (!go.activeSelf) go.SetActive(true);
            }
        }
    }
}
