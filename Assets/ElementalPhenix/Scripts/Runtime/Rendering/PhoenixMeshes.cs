using UnityEngine;

namespace ElementalPhenix.Rendering
{
    /// <summary>
    /// Maillages du phénix, dans un style d'oiseau de proie : buste élancé au plastron de plumes
    /// écailleuses, petite tête au bec crochu, regard sous l'arcade, aigrette de plumes fines
    /// rabattue vers l'arrière, ailes en couches (couvertures, secondaires, rémiges en doigts)
    /// aux pointes de flammes, longue traîne de plumes. Repère du modèle : bec vers +x, dos vers +y.
    /// Construits au chargement seulement ; purement visuels, la hitbox reste le cercle de la simulation.
    /// </summary>
    public static class PhoenixMeshes
    {
        /// <summary>Épaule de l'aile lointaine (l'aile proche est symétrique en z).</summary>
        public static readonly Vector3 Shoulder = new Vector3(0.012f, 0.034f, 0.04f);
        /// <summary>Attache de la queue.</summary>
        public static readonly Vector3 TailRoot = new Vector3(-0.088f, -0.006f, 0f);

        /// <summary>
        /// Ellipsoïde allongé le long de <paramref name="dir"/>, de <paramref name="start"/> à
        /// <paramref name="end"/> depuis <paramref name="origin"/>, aplati selon <paramref name="normal"/>.
        /// </summary>
        public static void AddSegment(MeshBuilder b, Vector3 origin, Vector3 dir, Vector3 normal, float start, float end,
            float width, float thickness, Color color, int longitude = 10, int latitude = 6)
        {
            var center = origin + dir * ((start + end) * 0.5f);
            var piece = new MeshBuilder().AddEllipsoid(Vector3.zero, new Vector3(width, thickness, (end - start) * 0.5f), color, longitude, latitude);
            b.Append(piece, Matrix4x4.TRS(center, Quaternion.LookRotation(dir, normal), Vector3.one));
        }

        /// <summary>Pointe conique de la base <paramref name="basePos"/> vers <paramref name="dir"/>.</summary>
        public static void AddSpike(MeshBuilder b, Vector3 basePos, Vector3 dir, float radius, float length, Color color, int segments = 8)
        {
            b.Append(new MeshBuilder().AddCone(Vector3.zero, radius, length, color, segments),
                Matrix4x4.TRS(basePos, Quaternion.FromToRotation(Vector3.up, dir.normalized), Vector3.one));
        }

        /// <summary>
        /// Ruban qui se courbe dans le plan de profil : direction de <paramref name="fromDeg"/> à
        /// <paramref name="toDeg"/> (degrés, 0 = vers le bec, 90 = vers le haut), largeur et couleur
        /// interpolées de la base au bout. Aplati face à la caméra. Renvoie la position du bout.
        /// </summary>
        public static Vector3 AddCurve(MeshBuilder b, Vector3 start, float fromDeg, float toDeg, float length,
            float width0, float width1, Color color0, Color color1, float thickness = 0.006f, int segments = 6)
        {
            var previous = start;
            float step = length / segments;
            for (int i = 1; i <= segments; i++)
            {
                float u = (float)i / segments;
                float a = Mathf.Lerp(fromDeg, toDeg, (i - 0.5f) / segments) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                AddSegment(b, previous, dir, Vector3.back, -0.006f, step + 0.006f, Mathf.Lerp(width0, width1, u), thickness,
                    Color.Lerp(color0, color1, u));
                previous += dir * step;
            }
            return previous;
        }

        public static Vector3 Direction(float degrees)
        {
            float a = degrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
        }

        /// <summary>Couleur sans lueur (alpha des couleurs de sommet à <paramref name="glow"/>).</summary>
        static Color WithGlow(Color c, float glow)
        {
            c.a = glow;
            return c;
        }

        /// <summary>
        /// Corps : buste sombre incliné vers l'avant, plastron de plumes écailleuses qui passent du
        /// plumage lumineux au sombre, cou et collerette de plumes effilées, petite tête, bec crochu
        /// de métal clair, œil de gemme sous une arcade sombre, aigrette rabattue et serres repliées.
        /// </summary>
        public static MeshBuilder Body(Palette.PhoenixColors c)
        {
            var b = new MeshBuilder();
            var dark = c.Body;
            var mid = WithGlow(Color.Lerp(c.Body, c.Plume, 0.45f), c.Plume.a * 0.5f);

            // Buste : long, la poitrine relevée vers l'avant, le dos qui file vers la queue.
            b.Append(new MeshBuilder().AddEllipsoid(Vector3.zero, new Vector3(0.105f, 0.04f, 0.04f), dark, 22, 14),
                Matrix4x4.TRS(new Vector3(-0.015f, 0f, 0f), Quaternion.Euler(0f, 0f, 9f), Vector3.one));
            b.Append(new MeshBuilder().AddEllipsoid(Vector3.zero, new Vector3(0.056f, 0.036f, 0.037f), mid, 18, 12),
                Matrix4x4.TRS(new Vector3(0.036f, 0.004f, 0f), Quaternion.Euler(0f, 0f, 26f), Vector3.one));

            // Plastron : rangées de plumes en écailles, pointes vers le bas et l'arrière.
            const int rows = 5;
            for (int r = 0; r < rows; r++)
            {
                float t = (float)r / (rows - 1);
                var rowCenter = Vector3.Lerp(new Vector3(0.07f, 0.026f, 0f), new Vector3(-0.004f, -0.034f, 0f), t);
                var color = WithGlow(Color.Lerp(mid, c.Body, t * 0.8f), Mathf.Lerp(c.Plume.a * 0.45f, 0f, t));
                for (int k = -2; k <= 2; k++)
                {
                    float side = k / 2f;
                    var pos = rowCenter + new Vector3(-Mathf.Abs(side) * 0.012f, -Mathf.Abs(side) * 0.006f, side * 0.03f);
                    var dir = new Vector3(-0.55f, -1f, side * 0.4f).normalized;
                    var normal = new Vector3(1f, -0.3f, side * 0.8f).normalized;
                    AddSegment(b, pos, dir, normal, -0.004f, 0.022f, 0.01f, 0.0025f, color, 8, 4);
                }
            }

            // Cou : penché vers l'avant, collerette de plumes effilées sur la nuque.
            var head = new Vector3(0.124f, 0.078f, 0f);
            b.Append(new MeshBuilder().AddEllipsoid(Vector3.zero, new Vector3(0.023f, 0.05f, 0.025f), mid, 14, 10),
                Matrix4x4.TRS(new Vector3(0.094f, 0.046f, 0f), Quaternion.Euler(0f, 0f, -44f), Vector3.one));
            for (int k = 0; k < 7; k++)
            {
                float t = k / 6f;
                float z = Mathf.Lerp(-0.022f, 0.022f, t);
                var root = head + new Vector3(-0.022f, -0.008f - Mathf.Abs(z) * 0.3f, z);
                var color = k % 2 == 0 ? c.FlameA : WithGlow(Color.Lerp(c.Plume, c.FlameB, 0.5f), c.FlameB.a);
                AddSegment(b, root, new Vector3(-0.75f, -0.62f, z * 6f).normalized, Vector3.up, 0f, 0.055f, 0.007f, 0.004f, color, 8, 4);
            }

            // Tête : petite, front plat, joue sombre.
            b.AddEllipsoid(head, new Vector3(0.029f, 0.021f, 0.021f), mid, 16, 12);
            b.AddEllipsoid(head + new Vector3(0.006f, -0.007f, 0f), new Vector3(0.02f, 0.013f, 0.022f), dark, 12, 8);

            // Bec crochu : mandibule supérieure qui s'incurve vers le bas, mandibule inférieure courte, cire à la base.
            b.AddEllipsoid(head + new Vector3(0.022f, 0.001f, 0f), new Vector3(0.011f, 0.0095f, 0.011f), c.Armor, 10, 8);
            AddCurve(b, head + new Vector3(0.024f, 0.003f, 0f), -2f, -92f, 0.044f, 0.0095f, 0.002f, c.ArmorTrim, c.ArmorTrim, 0.009f, 5);
            AddSpike(b, head + new Vector3(0.026f, -0.007f, 0f), Direction(-22f), 0.006f, 0.022f, c.Armor, 8);

            // Regard : œil de gemme en amande sous une arcade sombre qui descend vers le bec.
            for (int side = -1; side <= 1; side += 2)
            {
                var eye = head + new Vector3(0.012f, 0.004f, 0.019f * side);
                b.Append(new MeshBuilder().AddEllipsoid(Vector3.zero, new Vector3(0.0058f, 0.003f, 0.003f), c.Gem, 10, 6),
                    Matrix4x4.TRS(eye, Quaternion.Euler(0f, 0f, 8f), Vector3.one));
                b.Append(new MeshBuilder().AddEllipsoid(Vector3.zero, new Vector3(0.013f, 0.004f, 0.006f), dark, 10, 6),
                    Matrix4x4.TRS(eye + new Vector3(0.002f, 0.0048f, 0.001f * side), Quaternion.Euler(0f, 0f, -16f), Vector3.one));
            }

            // Aigrette : longues plumes fines rabattues vers l'arrière, de la flamme vers la pointe claire,
            // et quelques filaments plus longs.
            const int crest = 6;
            for (int k = 0; k < crest; k++)
            {
                float t = (float)k / (crest - 1);
                var root = head + new Vector3(-0.004f + t * 0.01f, 0.02f, (t - 0.5f) * 0.016f);
                float from = Mathf.Lerp(150f, 128f, t);
                float length = Mathf.Lerp(0.11f, 0.075f, t);
                AddCurve(b, root, from, from + 30f, length, 0.0065f, 0.0015f, c.FlameA, c.FlameTip, 0.004f, 5);
            }
            for (int k = 0; k < 3; k++)
                AddCurve(b, head + new Vector3(0.002f, 0.022f, (k - 1) * 0.01f), 140f + k * 6f, 172f + k * 4f, 0.15f, 0.0022f, 0.001f,
                    c.FlameB, c.FlameTip, 0.002f, 6);

            // Pattes repliées sous le ventre, serres de métal clair.
            for (int side = -1; side <= 1; side += 2)
            {
                var hip = new Vector3(-0.005f, -0.034f, 0.02f * side);
                AddSegment(b, hip, Direction(-140f), Vector3.back, 0f, 0.03f, 0.011f, 0.011f, dark, 8, 6);
                var ankle = hip + Direction(-140f) * 0.03f;
                AddSegment(b, ankle, Direction(-170f), Vector3.back, 0f, 0.03f, 0.005f, 0.005f, c.Armor, 6, 4);
                var foot = ankle + Direction(-170f) * 0.03f;
                for (int k = -1; k <= 1; k++)
                    AddCurve(b, foot + new Vector3(0f, 0f, k * 0.006f), -150f + k * 20f, -230f + k * 20f, 0.018f, 0.0028f, 0.001f,
                        c.ArmorTrim, c.ArmorTrim, 0.0028f, 3);
            }
            return b;
        }

        /// <summary>Plume d'aile à plat (plan xz) : tige sombre, flamme qui dépasse au bout, pointe claire.</summary>
        static void AddFeather(MeshBuilder b, Palette.PhoenixColors c, Vector3 root, Vector3 dir, float length, float width,
            Color shaft, float flameFrom, float lift)
        {
            var up = Vector3.up;
            var flame = Color.Lerp(c.FlameA, c.FlameB, flameFrom);
            AddSegment(b, root + up * (lift - 0.002f), dir, up, length * flameFrom, length * 1.02f, width * 1.15f, 0.004f, flame, 8, 4);
            AddSegment(b, root + up * (lift - 0.001f), dir, up, length * 0.86f, length * 1.08f, width * 0.6f, 0.004f, c.FlameTip, 8, 4);
            AddSegment(b, root + up * lift, dir, up, 0f, length * Mathf.Lerp(0.9f, 0.75f, flameFrom), width, 0.005f, shaft, 8, 4);
        }

        /// <summary>
        /// Aile au repos : à plat (plan xz), étendue vers −z depuis l'épaule, bord d'attaque vers +x.
        /// Bras sombre au liseré lumineux, petites et grandes couvertures, secondaires puis rémiges
        /// primaires séparées en doigts, toutes à pointe de flammes.
        /// </summary>
        public static MeshBuilder Wing(Palette.PhoenixColors c)
        {
            var b = new MeshBuilder();
            var dark = c.Body;
            var mid = WithGlow(Color.Lerp(c.Body, c.Plume, 0.55f), c.Plume.a * 0.6f);
            var arm = new Vector3(0.12f, 0f, -1f).normalized;
            const float armLength = 0.19f;
            var wrist = arm * armLength;

            // Bras et bord d'attaque.
            AddSegment(b, Vector3.zero, arm, Vector3.up, -0.01f, armLength + 0.01f, 0.026f, 0.014f, dark, 12, 8);
            AddSegment(b, new Vector3(0.016f, 0.006f, 0f), arm, Vector3.up, 0f, armLength, 0.009f, 0.012f, mid, 10, 6);

            // Petites couvertures : écailles lumineuses le long du bras.
            for (int k = 0; k < 8; k++)
            {
                float t = (k + 0.5f) / 8f;
                var root = arm * (armLength * t) + new Vector3(0.006f, 0.008f, 0f);
                AddSegment(b, root, new Vector3(-1f, 0f, -0.25f).normalized, Vector3.up, 0f, 0.04f, 0.013f, 0.005f,
                    WithGlow(Color.Lerp(c.Plume, c.Body, 0.25f), c.Plume.a * 0.8f), 8, 4);
            }
            // Grandes couvertures.
            for (int k = 0; k < 9; k++)
            {
                float t = (k + 0.5f) / 9f;
                var root = arm * (armLength * t) + new Vector3(-0.012f, 0.005f, 0f);
                AddSegment(b, root, new Vector3(-1f, 0f, -0.3f).normalized, Vector3.up, 0f, 0.07f, 0.017f, 0.005f, mid, 8, 4);
            }
            // Secondaires : du corps jusqu'au poignet, vers l'arrière.
            const int secondaries = 9;
            for (int k = 0; k < secondaries; k++)
            {
                float t = (float)k / (secondaries - 1);
                var root = arm * (armLength * Mathf.Lerp(0.05f, 0.95f, t)) + new Vector3(-0.02f, 0f, 0f);
                var dir = new Vector3(-1f, 0f, -Mathf.Lerp(0.05f, 0.45f, t)).normalized;
                AddFeather(b, c, root, dir, Mathf.Lerp(0.105f, 0.125f, t), 0.017f, dark, 0.55f, 0.002f);
            }
            // Rémiges primaires : en éventail depuis la main, pointes séparées comme des doigts.
            const int primaries = 7;
            for (int k = 0; k < primaries; k++)
            {
                float t = (float)k / (primaries - 1);
                var root = wrist + arm * (0.012f * (1f - t)) + new Vector3(-0.012f * t, 0.001f, 0f);
                float a = Mathf.Lerp(-8f, 52f, t) * Mathf.Deg2Rad;
                var dir = new Vector3(-Mathf.Sin(a), 0f, -Mathf.Cos(a));
                float length = Mathf.Lerp(0.2f, 0.15f, Mathf.Abs(t - 0.25f) * 1.33f);
                AddFeather(b, c, root, dir, length, Mathf.Lerp(0.013f, 0.016f, t), dark, 0.5f, 0.003f + 0.0006f * k);
            }
            return b;
        }

        /// <summary>
        /// Queue vers −x : longue traîne de plumes en éventail, sombres à la base, de la couleur des
        /// flammes jusqu'à leur pointe claire, et deux longues plumes centrales qui ondulent.
        /// </summary>
        public static MeshBuilder Tail(Palette.PhoenixColors c)
        {
            var b = new MeshBuilder();
            const int count = 9;
            for (int k = 0; k < count; k++)
            {
                float t = (float)k / (count - 1);
                float from = Mathf.Lerp(182f, 210f, t);
                float bend = Mathf.Lerp(10f, 26f, t);
                float length = Mathf.Lerp(0.22f, 0.29f, Mathf.Sin(t * Mathf.PI));
                float depth = (t - 0.5f) * 0.03f;
                // Base sombre qui s'embrase vers le milieu, puis flamme jusqu'à la pointe claire.
                var middle = AddCurve(b, new Vector3(0f, 0f, depth), from, from + bend * 0.5f, length * 0.5f, 0.012f, 0.009f,
                    c.Body, c.FlameA, 0.0045f, 4);
                AddCurve(b, middle, from + bend * 0.5f, from + bend, length * 0.5f, 0.009f, 0.003f, c.FlameA, c.FlameTip, 0.0045f, 4);
            }
            for (int side = -1; side <= 1; side += 2)
                AddCurve(b, new Vector3(0f, 0f, 0.004f * side), 190f + side * 3f, 226f - side * 8f, 0.4f, 0.0055f, 0.0015f,
                    c.Ribbon, c.FlameTip, 0.004f, 10);
            return b;
        }
    }
}
