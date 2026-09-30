using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PuffyBird.Rendering
{
    /// <summary>
    /// Construit des maillages procéduraux (sphères, cylindres, cônes, boîtes) avec couleurs par
    /// sommet, pour un décor et des personnages sans aucun fichier 3D. Les couleurs par sommet
    /// permettent de dessiner un objet multicolore avec un seul matériau.
    /// À n'utiliser qu'au chargement : chaque construction alloue.
    /// </summary>
    public sealed class MeshBuilder
    {
        readonly List<Vector3> _vertices = new List<Vector3>();
        readonly List<Vector3> _normals = new List<Vector3>();
        readonly List<Vector2> _uvs = new List<Vector2>();
        readonly List<Color> _colors = new List<Color>();
        readonly List<int> _indices = new List<int>();

        public int VertexCount => _vertices.Count;

        /// <summary>Ellipsoïde de centre et de rayons donnés.</summary>
        public MeshBuilder AddEllipsoid(Vector3 center, Vector3 radii, Color color, int longitude = 20, int latitude = 14)
        {
            int start = _vertices.Count;
            for (int lat = 0; lat <= latitude; lat++)
            {
                float v = (float)lat / latitude;
                float theta = v * Mathf.PI;
                float sinT = Mathf.Sin(theta);
                float cosT = Mathf.Cos(theta);
                for (int lon = 0; lon <= longitude; lon++)
                {
                    float u = (float)lon / longitude;
                    float phi = u * Mathf.PI * 2f;
                    var unit = new Vector3(sinT * Mathf.Cos(phi), cosT, sinT * Mathf.Sin(phi));
                    _vertices.Add(center + Vector3.Scale(unit, radii));
                    // Normale d'un ellipsoïde : gradient de (x/a)² + (y/b)² + (z/c)².
                    var n = new Vector3(unit.x / Mathf.Max(radii.x, 1e-5f), unit.y / Mathf.Max(radii.y, 1e-5f), unit.z / Mathf.Max(radii.z, 1e-5f));
                    _normals.Add(n.normalized);
                    _uvs.Add(new Vector2(u, 1f - v));
                    _colors.Add(color);
                }
            }
            int row = longitude + 1;
            for (int lat = 0; lat < latitude; lat++)
            {
                for (int lon = 0; lon < longitude; lon++)
                {
                    int a = start + lat * row + lon;
                    int b = a + row;
                    _indices.Add(a); _indices.Add(a + 1); _indices.Add(b);
                    _indices.Add(a + 1); _indices.Add(b + 1); _indices.Add(b);
                }
            }
            return this;
        }

        public MeshBuilder AddSphere(Vector3 center, float radius, Color color, int longitude = 20, int latitude = 14)
            => AddEllipsoid(center, Vector3.one * radius, color, longitude, latitude);

        /// <summary>Cylindre vertical (axe y), base à <paramref name="baseCenter"/>.</summary>
        public MeshBuilder AddCylinder(Vector3 baseCenter, float radius, float height, Color color, int segments = 24, bool caps = true)
        {
            return AddFrustum(baseCenter, radius, radius, height, color, segments, caps);
        }

        /// <summary>Tronc de cône vertical (axe y) : rayon bas, rayon haut.</summary>
        public MeshBuilder AddFrustum(Vector3 baseCenter, float bottomRadius, float topRadius, float height, Color color, int segments = 24, bool caps = true)
        {
            int start = _vertices.Count;
            float slope = (bottomRadius - topRadius) / Mathf.Max(height, 1e-5f);
            for (int i = 0; i <= segments; i++)
            {
                float u = (float)i / segments;
                float a = u * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var n = new Vector3(dir.x, slope, dir.z).normalized;
                _vertices.Add(baseCenter + dir * bottomRadius);
                _normals.Add(n);
                _uvs.Add(new Vector2(u, 0f));
                _colors.Add(color);
                _vertices.Add(baseCenter + dir * topRadius + Vector3.up * height);
                _normals.Add(n);
                _uvs.Add(new Vector2(u, 1f));
                _colors.Add(color);
            }
            for (int i = 0; i < segments; i++)
            {
                int b0 = start + i * 2;
                int t0 = b0 + 1;
                int b1 = b0 + 2;
                int t1 = b0 + 3;
                _indices.Add(b0); _indices.Add(t0); _indices.Add(b1);
                _indices.Add(b1); _indices.Add(t0); _indices.Add(t1);
            }
            if (caps)
            {
                if (topRadius > 0f) AddDisc(baseCenter + Vector3.up * height, topRadius, Vector3.up, color, segments);
                if (bottomRadius > 0f) AddDisc(baseCenter, bottomRadius, Vector3.down, color, segments);
            }
            return this;
        }

        /// <summary>Cône : base de rayon donné, pointe à <paramref name="height"/> le long de y.</summary>
        public MeshBuilder AddCone(Vector3 baseCenter, float radius, float height, Color color, int segments = 16)
            => AddFrustum(baseCenter, radius, 0f, height, color, segments, true);

        /// <summary>Disque horizontal tourné vers <paramref name="normal"/> (haut ou bas).</summary>
        public MeshBuilder AddDisc(Vector3 center, float radius, Vector3 normal, Color color, int segments = 24)
        {
            int start = _vertices.Count;
            _vertices.Add(center);
            _normals.Add(normal);
            _uvs.Add(new Vector2(0.5f, 0.5f));
            _colors.Add(color);
            for (int i = 0; i <= segments; i++)
            {
                float a = (float)i / segments * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                _vertices.Add(center + dir * radius);
                _normals.Add(normal);
                _uvs.Add(new Vector2(0.5f + dir.x * 0.5f, 0.5f + dir.z * 0.5f));
                _colors.Add(color);
            }
            bool up = normal.y > 0f;
            for (int i = 0; i < segments; i++)
            {
                int a = start + 1 + i;
                int b = a + 1;
                _indices.Add(start);
                if (up) { _indices.Add(b); _indices.Add(a); }
                else { _indices.Add(a); _indices.Add(b); }
            }
            return this;
        }

        /// <summary>
        /// Boîte alignée sur les axes. Les UV sont en unités monde divisées par
        /// <paramref name="uvScale"/>, pour qu'une texture se répète sans déformation.
        /// </summary>
        public MeshBuilder AddBox(Vector3 center, Vector3 size, Color color, float uvScale = 1f)
        {
            var h = size * 0.5f;
            var min = center - h;
            var max = center + h;
            // +X, −X, +Y, −Y, +Z, −Z
            AddQuad(new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z), new Vector3(max.x, max.y, max.z), new Vector3(max.x, min.y, max.z), Vector3.right, color, uvScale, 2, 1);
            AddQuad(new Vector3(min.x, min.y, max.z), new Vector3(min.x, max.y, max.z), new Vector3(min.x, max.y, min.z), new Vector3(min.x, min.y, min.z), Vector3.left, color, uvScale, 2, 1);
            AddQuad(new Vector3(min.x, max.y, min.z), new Vector3(min.x, max.y, max.z), new Vector3(max.x, max.y, max.z), new Vector3(max.x, max.y, min.z), Vector3.up, color, uvScale, 0, 2);
            AddQuad(new Vector3(min.x, min.y, max.z), new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z), Vector3.down, color, uvScale, 0, 2);
            AddQuad(new Vector3(max.x, min.y, max.z), new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z), new Vector3(min.x, min.y, max.z), Vector3.forward, color, uvScale, 0, 1);
            AddQuad(new Vector3(min.x, min.y, min.z), new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z), new Vector3(max.x, min.y, min.z), Vector3.back, color, uvScale, 0, 1);
            return this;
        }

        /// <summary>Quadrilatère a-b-c-d dans le sens horaire vu depuis <paramref name="normal"/>.</summary>
        void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Color color, float uvScale, int uAxis, int vAxis)
        {
            int start = _vertices.Count;
            _vertices.Add(a); _vertices.Add(b); _vertices.Add(c); _vertices.Add(d);
            for (int i = 0; i < 4; i++)
            {
                _normals.Add(normal);
                _colors.Add(color);
            }
            _uvs.Add(new Vector2(a[uAxis], a[vAxis]) / uvScale);
            _uvs.Add(new Vector2(b[uAxis], b[vAxis]) / uvScale);
            _uvs.Add(new Vector2(c[uAxis], c[vAxis]) / uvScale);
            _uvs.Add(new Vector2(d[uAxis], d[vAxis]) / uvScale);
            _indices.Add(start); _indices.Add(start + 1); _indices.Add(start + 2);
            _indices.Add(start); _indices.Add(start + 2); _indices.Add(start + 3);
        }

        /// <summary>
        /// Étoile bombée à facettes dans le plan XY : pointes alternant rayon extérieur et intérieur,
        /// sommets en relief de ± <paramref name="depth"/>/2 sur z, une normale plate par facette.
        /// </summary>
        public MeshBuilder AddStar(Vector3 center, float outerRadius, float innerRadius, float depth, Color color, int points = 5)
        {
            int ring = points * 2;
            var front = center + new Vector3(0f, 0f, -depth * 0.5f);
            var back = center + new Vector3(0f, 0f, depth * 0.5f);
            for (int i = 0; i < ring; i++)
            {
                var a = StarPoint(center, i, ring, outerRadius, innerRadius);
                var b = StarPoint(center, i + 1, ring, outerRadius, innerRadius);
                AddFacet(front, a, b, Vector3.back, color);
                AddFacet(back, a, b, Vector3.forward, color);
            }
            return this;
        }

        static Vector3 StarPoint(Vector3 center, int i, int ring, float outer, float inner)
        {
            float angle = Mathf.PI * 0.5f + i * Mathf.PI * 2f / ring;
            float r = i % 2 == 0 ? outer : inner;
            return center + new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, 0f);
        }

        /// <summary>Triangle à normale plate, retourné si besoin pour faire face à <paramref name="side"/>.</summary>
        void AddFacet(Vector3 a, Vector3 b, Vector3 c, Vector3 side, Color color)
        {
            var normal = Vector3.Cross(b - a, c - a).normalized;
            if (Vector3.Dot(normal, side) < 0f)
            {
                (b, c) = (c, b);
                normal = -normal;
            }
            int start = _vertices.Count;
            _vertices.Add(a); _vertices.Add(b); _vertices.Add(c);
            for (int i = 0; i < 3; i++)
            {
                _normals.Add(normal);
                _colors.Add(color);
                _uvs.Add(Vector2.zero);
            }
            _indices.Add(start); _indices.Add(start + 1); _indices.Add(start + 2);
        }

        /// <summary>
        /// Brin d'herbe : triangle effilé double face dans le plan XY, du pied (<paramref name="root"/>)
        /// à la pointe penchée de <paramref name="lean"/>, couleur dégradée du pied à la pointe.
        /// </summary>
        public MeshBuilder AddBlade(Vector3 root, float width, float height, float lean, Color rootColor, Color tipColor)
        {
            var a = root + new Vector3(-width * 0.5f, 0f, 0f);
            var b = root + new Vector3(width * 0.5f, 0f, 0f);
            var c = root + new Vector3(lean, height, 0f);
            // Normales inclinées vers le haut : les brins prennent la lumière comme le gazon.
            var front = new Vector3(0f, 0.6f, -0.8f);
            var back = new Vector3(0f, 0.6f, 0.8f);
            int start = _vertices.Count;
            _vertices.Add(a); _vertices.Add(c); _vertices.Add(b);
            _vertices.Add(a); _vertices.Add(b); _vertices.Add(c);
            for (int i = 0; i < 6; i++)
            {
                _normals.Add(i < 3 ? front : back);
                _uvs.Add(Vector2.zero);
            }
            _colors.Add(rootColor); _colors.Add(tipColor); _colors.Add(rootColor);
            _colors.Add(rootColor); _colors.Add(rootColor); _colors.Add(tipColor);
            for (int i = 0; i < 6; i++) _indices.Add(start + i);
            return this;
        }

        /// <summary>Ajoute un autre constructeur, transformé par <paramref name="matrix"/>.</summary>
        public MeshBuilder Append(MeshBuilder other, Matrix4x4 matrix)
        {
            int start = _vertices.Count;
            var normalMatrix = matrix.inverse.transpose;
            for (int i = 0; i < other._vertices.Count; i++)
            {
                _vertices.Add(matrix.MultiplyPoint3x4(other._vertices[i]));
                _normals.Add(normalMatrix.MultiplyVector(other._normals[i]).normalized);
                _uvs.Add(other._uvs[i]);
                _colors.Add(other._colors[i]);
            }
            for (int i = 0; i < other._indices.Count; i++) _indices.Add(start + other._indices[i]);
            return this;
        }

        public Mesh Build(string name)
        {
            var mesh = new Mesh { name = name };
            if (_vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(_vertices);
            mesh.SetNormals(_normals);
            mesh.SetUVs(0, _uvs);
            mesh.SetColors(_colors);
            mesh.SetTriangles(_indices, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
            return mesh;
        }
    }
}
