using ElementalPhenix.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace ElementalPhenix.Rendering
{
    /// <summary>
    /// Sillage choisi par le joueur (<see cref="Wakes"/>) : petites lueurs semées derrière le phénix
    /// en vol, emportées par le défilement, qui rapetissent et s'éteignent. Braises, écume, aurore,
    /// arc-en-ciel ou poussière d'étoiles ; « aucun » n'émet rien. Pool fixe et maillages créés au
    /// chargement. Purement visuel, derrière l'oiseau.
    /// </summary>
    public sealed class WakeTrailView
    {
        const int Count = 48;
        const float EmitRate = 26f;
        const float Life = 0.75f;

        struct Particle
        {
            public Transform Transform;
            public MeshFilter Filter;
            public Vector3 Velocity;
            public float Age;
            public float Size;
            public float Rise;
        }

        readonly Particle[] _particles = new Particle[Count];
        // Maillages de chaque sillage (vide pour « aucun »).
        readonly Mesh[][] _meshes = new Mesh[Wakes.Count][];
        int _wake;
        int _next;
        int _emitted;
        float _accumulator;

        public WakeTrailView(Transform parent, MaterialLibrary materials)
        {
            var root = new GameObject("Sillage").transform;
            root.SetParent(parent, false);
            for (int w = 0; w < Wakes.Count; w++)
            {
                var colors = ColorsOf(Wakes.Get(w).Id);
                bool stars = Wakes.Get(w).Id == "stars";
                _meshes[w] = new Mesh[colors.Length];
                for (int c = 0; c < colors.Length; c++)
                {
                    var b = new MeshBuilder();
                    if (stars) b.AddStar(Vector3.zero, 1f, 0.45f, 0.4f, colors[c]);
                    else b.AddSphere(Vector3.zero, 1f, colors[c], 8, 6);
                    _meshes[w][c] = b.Build("Sillage " + Wakes.Get(w).Id + " " + c);
                }
            }
            var material = materials.Lit("Sillage", Color.white, 0.3f, 0f, 0.2f);
            material.SetFloat(MaterialLibrary.VertexEmission, 1.6f);
            for (int i = 0; i < Count; i++)
            {
                var go = new GameObject("Lueur");
                go.transform.SetParent(root, false);
                var filter = go.AddComponent<MeshFilter>();
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                go.SetActive(false);
                _particles[i] = new Particle { Transform = go.transform, Filter = filter };
            }
        }

        static Color[] ColorsOf(string id)
        {
            switch (id)
            {
                case "embers": return new[] { Palette.Hex("#FF7A1A"), Palette.Hex("#FFC24D"), Palette.Hex("#FF3D1F") };
                case "foam": return new[] { Palette.Hex("#E8FBFF"), Palette.Hex("#7FDBFF"), Palette.Hex("#3FA9F5") };
                case "aurora": return new[] { Palette.Hex("#5CFFB0"), Palette.Hex("#4DD6FF"), Palette.Hex("#B07CFF") };
                case "rainbow": return new[] { Palette.Hex("#FF4D4D"), Palette.Hex("#FFA64D"), Palette.Hex("#FFE14D"), Palette.Hex("#5CE65C"), Palette.Hex("#4DA6FF"), Palette.Hex("#9B5CFF") };
                case "stars": return new[] { Palette.Hex("#FFF4B0"), Palette.Hex("#FFFFFF"), Palette.Hex("#FFD24D") };
                default: return new Color[0];
            }
        }

        /// <summary>Sillage affiché (indice du catalogue).</summary>
        public void SetWake(int index)
        {
            _wake = Mathf.Clamp(index, 0, Wakes.Count - 1);
        }

        /// <param name="emit">Le phénix vole (partie, attente du premier tap ou aperçu du menu).</param>
        /// <param name="scroll">Défilement du monde, en unités par seconde.</param>
        public void Update(Vector3 birdPosition, bool emit, float scroll, float deltaTime)
        {
            var meshes = _meshes[_wake];
            if (emit && meshes.Length > 0)
            {
                _accumulator += EmitRate * deltaTime;
                while (_accumulator >= 1f)
                {
                    _accumulator -= 1f;
                    Emit(birdPosition, meshes, scroll);
                }
            }
            else
            {
                _accumulator = 0f;
            }

            for (int i = 0; i < Count; i++)
            {
                ref var p = ref _particles[i];
                var t = p.Transform;
                if (!t.gameObject.activeSelf) continue;
                p.Age += deltaTime;
                if (p.Age >= Life)
                {
                    t.gameObject.SetActive(false);
                    continue;
                }
                p.Velocity.y += p.Rise * deltaTime;
                t.position += p.Velocity * deltaTime;
                float k = p.Age / Life;
                float s = p.Size * (1f - k) * Mathf.Min(1f, k * 8f);
                t.localScale = new Vector3(s, s, s);
                t.Rotate(0f, 0f, 120f * deltaTime, Space.Self);
            }
        }

        void Emit(Vector3 bird, Mesh[] meshes, float scroll)
        {
            ref var p = ref _particles[_next];
            _next = (_next + 1) % Count;
            p.Filter.sharedMesh = meshes[(_emitted++ / 2) % meshes.Length];
            p.Age = 0f;
            p.Size = Random.Range(0.018f, 0.03f);
            p.Rise = Random.Range(-0.1f, 0.25f);
            p.Velocity = new Vector3(-scroll * 0.9f + Random.Range(-0.1f, 0.05f), Random.Range(-0.08f, 0.08f), 0f);
            p.Transform.position = bird + new Vector3(-0.14f, Random.Range(-0.03f, 0.02f), 0.04f);
            p.Transform.localRotation = Quaternion.identity;
            p.Transform.localScale = Vector3.zero;
            p.Transform.gameObject.SetActive(true);
        }
    }
}
