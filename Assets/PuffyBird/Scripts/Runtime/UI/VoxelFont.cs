using System.Collections.Generic;
using UnityEngine;

namespace PuffyBird.UI
{
    public enum TextAlign
    {
        Left,
        Center,
        Right,
    }

    /// <summary>
    /// Police 5 × 7 (§14.5) transformée en texte en volume : chaque pixel allumé devient un petit
    /// cube éclairé, doublé d'un cube sombre en retrait qui fait office de contour. Aucune police
    /// ni atlas à importer. Les maillages sont construits une fois au chargement.
    /// </summary>
    public static class VoxelFont
    {
        public const int GlyphWidth = 5;
        public const int GlyphHeight = 7;
        public const int Advance = 6;
        const float OutlineGrow = 0.3f;

        static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
        {
            // Chiffres de la spec (§14.5).
            ['0'] = new[] { ".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###." },
            ['1'] = new[] { "..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###." },
            ['2'] = new[] { ".###.", "#...#", "....#", "..##.", ".#...", "#....", "#####" },
            ['3'] = new[] { "####.", "....#", "....#", ".###.", "....#", "....#", "####." },
            ['4'] = new[] { "...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#." },
            ['5'] = new[] { "#####", "#....", "####.", "....#", "....#", "#...#", ".###." },
            ['6'] = new[] { "..##.", ".#...", "#....", "####.", "#...#", "#...#", ".###." },
            ['7'] = new[] { "#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#..." },
            ['8'] = new[] { ".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###." },
            ['9'] = new[] { ".###.", "#...#", "#...#", ".####", "....#", "...#.", ".##.." },
            ['A'] = new[] { ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
            ['B'] = new[] { "####.", "#...#", "#...#", "####.", "#...#", "#...#", "####." },
            ['C'] = new[] { ".###.", "#...#", "#....", "#....", "#....", "#...#", ".###." },
            ['D'] = new[] { "####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####." },
            ['E'] = new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#####" },
            ['F'] = new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#...." },
            ['G'] = new[] { ".###.", "#...#", "#....", "#.###", "#...#", "#...#", ".####" },
            ['H'] = new[] { "#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
            ['I'] = new[] { ".###.", "..#..", "..#..", "..#..", "..#..", "..#..", ".###." },
            ['L'] = new[] { "#....", "#....", "#....", "#....", "#....", "#....", "#####" },
            ['M'] = new[] { "#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#" },
            ['N'] = new[] { "#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#" },
            ['O'] = new[] { ".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
            ['P'] = new[] { "####.", "#...#", "#...#", "####.", "#....", "#....", "#...." },
            ['R'] = new[] { "####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#" },
            ['S'] = new[] { ".####", "#....", "#....", ".###.", "....#", "....#", "####." },
            ['T'] = new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.." },
            ['U'] = new[] { "#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
            ['V'] = new[] { "#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#.." },
            ['W'] = new[] { "#...#", "#...#", "#...#", "#.#.#", "#.#.#", "##.##", "#...#" },
            ['Y'] = new[] { "#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.." },
            ['!'] = new[] { "..#..", "..#..", "..#..", "..#..", "..#..", ".....", "..#.." },
            ['?'] = new[] { ".###.", "#...#", "....#", "..##.", "..#..", ".....", "..#.." },
            ['^'] = new[] { "..#..", ".###.", "#####", "..#..", "..#..", "..#..", "....." },
        };

        public static float Width(string text) => text.Length == 0 ? 0f : text.Length * Advance - 1;

        /// <summary>
        /// Maillage d'un texte, en unités de « voxel » (1 = un pixel de la police). L'origine est
        /// au milieu du bord haut pour <see cref="TextAlign.Center"/>, au coin haut gauche ou haut
        /// droit sinon ; le texte descend vers −y et fait face à −z (la caméra).
        /// </summary>
        public static Mesh Build(string text, TextAlign align, Color front, Color outline)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var colors = new List<Color>();
            var indices = new List<int>();

            float width = Width(text);
            float offsetX = align == TextAlign.Left ? 0f : (align == TextAlign.Center ? -width * 0.5f : -width);

            for (int c = 0; c < text.Length; c++)
            {
                if (!Glyphs.TryGetValue(char.ToUpperInvariant(text[c]), out var rows)) continue;
                for (int row = 0; row < GlyphHeight; row++)
                {
                    for (int col = 0; col < GlyphWidth; col++)
                    {
                        if (rows[row][col] != '#') continue;
                        var min = new Vector3(offsetX + c * Advance + col, -(row + 1), -0.5f);
                        AddCube(vertices, normals, colors, indices, min, min + new Vector3(1f, 1f, 1f), front);
                        var grow = new Vector3(OutlineGrow, OutlineGrow, 0f);
                        AddCube(vertices, normals, colors, indices, min - grow + new Vector3(0f, 0f, 0.6f), min + new Vector3(1f, 1f, 1.3f) + grow, outline);
                    }
                }
            }

            var mesh = new Mesh { name = "Texte " + text };
            if (vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.uv = new Vector2[vertices.Count];
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }

        static void AddCube(List<Vector3> v, List<Vector3> n, List<Color> c, List<int> idx, Vector3 min, Vector3 max, Color color)
        {
            // Faces : −z (face avant), +x, −x, +y, −y. La face arrière n'est jamais vue.
            AddFace(v, n, c, idx, new Vector3(min.x, min.y, min.z), new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z), new Vector3(max.x, min.y, min.z), Vector3.back, color);
            AddFace(v, n, c, idx, new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z), new Vector3(max.x, max.y, max.z), new Vector3(max.x, min.y, max.z), Vector3.right, color);
            AddFace(v, n, c, idx, new Vector3(min.x, min.y, max.z), new Vector3(min.x, max.y, max.z), new Vector3(min.x, max.y, min.z), new Vector3(min.x, min.y, min.z), Vector3.left, color);
            AddFace(v, n, c, idx, new Vector3(min.x, max.y, min.z), new Vector3(min.x, max.y, max.z), new Vector3(max.x, max.y, max.z), new Vector3(max.x, max.y, min.z), Vector3.up, color);
            AddFace(v, n, c, idx, new Vector3(min.x, min.y, max.z), new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z), Vector3.down, color);
        }

        static void AddFace(List<Vector3> v, List<Vector3> n, List<Color> c, List<int> idx, Vector3 a, Vector3 b, Vector3 cc, Vector3 d, Vector3 normal, Color color)
        {
            int start = v.Count;
            v.Add(a); v.Add(b); v.Add(cc); v.Add(d);
            for (int i = 0; i < 4; i++)
            {
                n.Add(normal);
                c.Add(color);
            }
            idx.Add(start); idx.Add(start + 1); idx.Add(start + 2);
            idx.Add(start); idx.Add(start + 2); idx.Add(start + 3);
        }
    }
}
