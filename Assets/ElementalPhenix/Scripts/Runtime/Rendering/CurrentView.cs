using ElementalPhenix.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace ElementalPhenix.Rendering
{
    /// <summary>
    /// Courants ascendants des abysses : colonnes de bulles qui montent du sable jusqu'en haut de
    /// l'écran, sur la largeur exacte du courant de la simulation. Les bulles suivent une trajectoire
    /// calculée à partir du temps (aucun état, aucune allocation) et passent juste derrière le plan de jeu.
    /// </summary>
    public sealed class CurrentView
    {
        const int BubblesPerColumn = 16;
        const float RiseSpeed = 1.6f;
        const float BubbleZ = 0.12f;

        readonly WorldSpace _space;
        readonly GameConfig _cfg;
        readonly Transform[] _columns;
        readonly Transform[] _bubbles;
        readonly float[] _phases = new float[BubblesPerColumn];
        readonly float[] _offsets = new float[BubblesPerColumn];
        readonly float[] _sizes = new float[BubblesPerColumn];

        public CurrentView(Transform parent, MaterialLibrary materials, WorldSpace space, int capacity)
        {
            _space = space;
            _cfg = space.Config;
            var root = new GameObject("Courants").transform;
            root.SetParent(parent, false);

            var mesh = new MeshBuilder()
                .AddSphere(Vector3.zero, 1f, Palette.Hex("#D8F6FF"), 10, 8)
                .AddSphere(new Vector3(-0.35f, 0.4f, -0.6f), 0.28f, Color.white, 6, 4)
                .Build("Bulle de courant");
            var material = materials.Lit("Courant", Color.white, 0.95f, 0f, 1f);
            material.SetFloat(MaterialLibrary.VertexEmission, 0.7f);

            var random = new System.Random(11);
            for (int k = 0; k < BubblesPerColumn; k++)
            {
                _phases[k] = (float)random.NextDouble();
                _offsets[k] = (float)(random.NextDouble() * 2.0 - 1.0);
                _sizes[k] = 0.012f + (float)random.NextDouble() * 0.02f;
            }

            _columns = new Transform[capacity];
            _bubbles = new Transform[capacity * BubblesPerColumn];
            for (int c = 0; c < capacity; c++)
            {
                _columns[c] = new GameObject("Courant " + c).transform;
                _columns[c].SetParent(root, false);
                for (int k = 0; k < BubblesPerColumn; k++)
                {
                    var go = new GameObject("Bulle");
                    go.transform.SetParent(_columns[c], false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var r = go.AddComponent<MeshRenderer>();
                    r.sharedMaterial = material;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    r.receiveShadows = false;
                    _bubbles[c * BubblesPerColumn + k] = go.transform;
                }
                _columns[c].gameObject.SetActive(false);
            }
        }

        public void Update(StarField currents, float alpha, float time)
        {
            float height = WorldSpace.Length(_cfg.GroundY) + 0.6f;
            float halfWidth = WorldSpace.Length(_cfg.CurrentHalfWidth);
            for (int c = 0; c < _columns.Length; c++)
            {
                var column = _columns[c];
                ref var current = ref currents[c];
                if (!current.Active)
                {
                    if (column.gameObject.activeSelf) column.gameObject.SetActive(false);
                    continue;
                }
                float x = Mathf.LerpUnclamped(current.PrevX, current.X, alpha);
                column.localPosition = new Vector3(_space.X(x), 0f, BubbleZ);
                if (!column.gameObject.activeSelf) column.gameObject.SetActive(true);
                for (int k = 0; k < BubblesPerColumn; k++)
                {
                    float y = Mathf.Repeat(time * RiseSpeed * (0.8f + 0.4f * _phases[k]) + _phases[k] * height, height);
                    float wobble = Mathf.Sin(time * 3f + k * 1.7f) * 0.15f;
                    float s = _sizes[k] * (0.6f + 0.4f * Mathf.Min(1f, y * 2f));
                    var b = _bubbles[c * BubblesPerColumn + k];
                    b.localPosition = new Vector3((_offsets[k] + wobble) * halfWidth, y, 0f);
                    b.localScale = new Vector3(s, s, s);
                }
            }
        }
    }
}
