using ElementalPhenix.Core;
using UnityEngine;

namespace ElementalPhenix.Rendering
{
    /// <summary>
    /// Formes des obstacles de chaque décor, à la place d'un tuyau à chapeau : rochers de grès
    /// empilés, cristaux de lune, tour néon aux fenêtres allumées, pagode à toits relevés, orgues de
    /// basalte fissurés d'éclairs, glace hérissée de givre, tronc à lianes et à feuilles, colonne de
    /// marbre dans les nuages, récif de corail. Repère de l'ouverture : y = 0 au bord de l'ouverture,
    /// l'obstacle s'étend vers −y (l'obstacle du haut est le même maillage, retourné). Chaque forme
    /// contient le rectangle de collision sur toute sa longueur (noyau de demi-largeur ≥ la moitié de
    /// <see cref="GameConfig.PipeWidth"/>) et ne dépasse jamais dans l'ouverture : la mort n'arrive
    /// jamais dans le vide. Construits au chargement seulement.
    /// </summary>
    public static class ObstacleMeshes
    {
        /// <summary>Longueur, depuis l'ouverture, sur laquelle les détails sont construits (au-delà : hors champ).</summary>
        public const float DetailLength = 4.6f;
        /// <summary>Avancée maximale d'un détail vers la caméra, pour qu'il ne masque jamais l'oiseau.</summary>
        const float MaxForward = 0.42f;

        sealed class Kit
        {
            public readonly MeshBuilder B = new MeshBuilder();
            public readonly float Half;
            readonly ObstacleStyles.Style _style;
            readonly bool _contrast;
            readonly System.Random _random;

            public Kit(ObstacleStyles.Style style, bool contrast, float half, int seed)
            {
                _style = style;
                _contrast = contrast;
                Half = half;
                _random = new System.Random(seed);
            }

            public Color C(ObstacleStyles.Role role) => ObstacleStyles.ColorOf(_style, role, _contrast);

            public float R(float min, float max) => min + (float)_random.NextDouble() * (max - min);

            public bool Chance(float p) => _random.NextDouble() < p;

            /// <summary>Rayon d'un prisme à <paramref name="sides"/> faces dont la silhouette couvre au moins la demi-largeur donnée.</summary>
            public static float Cover(float halfWidth, int sides) => halfWidth / Mathf.Cos(Mathf.PI / sides);

            /// <summary>
            /// Prisme de la distance <paramref name="d"/> (côté ouverture) à <paramref name="d"/> + <paramref name="h"/>,
            /// rayon <paramref name="rNear"/> côté ouverture et <paramref name="rFar"/> de l'autre côté.
            /// </summary>
            public void Prism(float d, float h, float rNear, float rFar, int sides, ObstacleStyles.Role role,
                float yaw = 0f, float x = 0f, float z = 0f)
            {
                var piece = new MeshBuilder().AddPrism(new Vector3(0f, -(d + h), 0f), rFar, rNear, h, C(role), sides);
                B.Append(piece, Matrix4x4.TRS(new Vector3(x, 0f, z), Quaternion.Euler(0f, yaw, 0f), Vector3.one));
            }

            /// <summary>Prisme à base carrée face à la caméra, de demi-largeurs données.</summary>
            public void Square(float d, float h, float halfNear, float halfFar, ObstacleStyles.Role role)
                => Prism(d, h, halfNear * 1.41421356f, halfFar * 1.41421356f, 4, role, 45f);

            public void Box(float dCenter, Vector3 size, float x, float z, ObstacleStyles.Role role, float roll = 0f)
            {
                var piece = new MeshBuilder().AddBox(Vector3.zero, size, C(role));
                B.Append(piece, Matrix4x4.TRS(new Vector3(x, -dCenter, z), Quaternion.Euler(0f, 0f, roll), Vector3.one));
            }

            public void Sphere(float d, float x, float z, float radius, ObstacleStyles.Role role, int longitude = 8, int latitude = 6)
                => B.AddSphere(new Vector3(x, -d, Mathf.Max(z, -MaxForward)), radius, C(role), longitude, latitude);

            public void Ellipsoid(float d, float x, float z, Vector3 radii, ObstacleStyles.Role role, float yaw = 0f)
            {
                var piece = new MeshBuilder().AddEllipsoid(Vector3.zero, radii, C(role), 10, 6);
                B.Append(piece, Matrix4x4.TRS(new Vector3(x, -d, Mathf.Max(z, -MaxForward)), Quaternion.Euler(0f, yaw, 0f), Vector3.one));
            }

            /// <summary>Direction vers l'extérieur à l'angle <paramref name="angle"/> (degrés), qui s'éloigne de l'ouverture de <paramref name="away"/>, sans trop avancer vers la caméra.</summary>
            public static Vector3 Outward(float angle, float away)
            {
                float a = angle * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(a), -away, Mathf.Sin(a));
                dir.z = Mathf.Max(dir.z, -0.45f);
                return dir.normalized;
            }

            /// <summary>Point du pourtour à l'angle donné (degrés), à la distance <paramref name="d"/> de l'ouverture.</summary>
            public static Vector3 Around(float angle, float radius, float d)
            {
                float a = angle * Mathf.Deg2Rad;
                return new Vector3(Mathf.Cos(a) * radius, -d, Mathf.Sin(a) * radius);
            }

            /// <summary>Cristal hexagonal à pointe, de la base vers <paramref name="dir"/>.</summary>
            public void Crystal(Vector3 basePos, Vector3 dir, float radius, float length, ObstacleStyles.Role role)
            {
                var piece = new MeshBuilder()
                    .AddPrism(Vector3.zero, radius, radius, length * 0.72f, C(role), 6)
                    .AddPrism(new Vector3(0f, length * 0.72f, 0f), radius, 0f, length * 0.28f, C(role), 6, caps: false);
                B.Append(piece, Matrix4x4.TRS(basePos, Quaternion.FromToRotation(Vector3.up, dir), Vector3.one));
            }

            /// <summary>Cône de la base vers <paramref name="dir"/>.</summary>
            public void Spike(Vector3 basePos, Vector3 dir, float radius, float length, ObstacleStyles.Role role, int sides = 7)
            {
                var piece = new MeshBuilder().AddCone(Vector3.zero, radius, length, C(role), sides);
                B.Append(piece, Matrix4x4.TRS(basePos, Quaternion.FromToRotation(Vector3.up, dir), Vector3.one));
            }

            /// <summary>Feuille ou plume aplatie, de la base vers <paramref name="dir"/>.</summary>
            public void Leaf(Vector3 basePos, Vector3 dir, float length, float width, ObstacleStyles.Role role)
                => PhoenixMeshes.AddSegment(B, basePos, dir, Vector3.up, 0f, length, width, 0.008f, C(role), 8, 4);
        }

        /// <summary>
        /// Obstacle du décor <paramref name="theme"/>, de l'ouverture (y = 0) jusqu'à <paramref name="length"/>
        /// vers −y ; <paramref name="half"/> est la demi-largeur du rectangle de collision. <paramref name="hanging"/> :
        /// variante de l'obstacle du haut, qui pend (enseigne suspendue, lanternes, nuage d'orage, banc de
        /// nuages, surplomb à méduses, racines) ; ce qui pend reste plaqué contre lui et s'arrête au bord de l'ouverture.
        /// </summary>
        public static Mesh Build(Theme theme, bool contrast, float half, float length, bool hanging, string name)
        {
            var k = new Kit(ObstacleStyles.For(theme), contrast, half, 101 + (int)theme * 7919 + (hanging ? 31 : 0));
            float detail = Mathf.Min(DetailLength, length);
            if (hanging)
            {
                switch (theme)
                {
                    case Theme.City: Billboard(k, length, detail); return k.B.Build(name);
                    case Theme.Japan: Lanterns(k, length, detail); return k.B.Build(name);
                    case Theme.Storm: StormCloud(k, length, detail); return k.B.Build(name);
                    case Theme.Sky: CloudBank(k, length, detail); return k.B.Build(name);
                    case Theme.Ocean: JellyOverhang(k, length, detail); return k.B.Build(name);
                    case Theme.Day: Boulders(k, length, detail); Roots(k, detail); return k.B.Build(name);
                }
            }
            switch (theme)
            {
                case Theme.Night: MoonCrystals(k, length, detail); break;
                case Theme.City: NeonTower(k, length, detail); break;
                case Theme.Japan: Pagoda(k, length, detail); break;
                case Theme.Storm: Basalt(k, length, detail); break;
                case Theme.Snow: Ice(k, length, detail); break;
                case Theme.Jungle: Trunk(k, length, detail); break;
                case Theme.Sky: CloudColumn(k, length, detail); break;
                case Theme.Ocean: Reef(k, length, detail); break;
                default: Boulders(k, length, detail); break;
            }
            return k.B.Build(name);
        }

        /// <summary>Prairie : rochers de grès empilés, chacun tourné et taillé autrement, coiffés de mousse.</summary>
        static void Boulders(Kit k, float length, float detail)
        {
            float r = Kit.Cover(k.Half, 7);
            k.Prism(0f, 0.035f, r * 1.1f, r * 1.14f, 7, ObstacleStyles.Role.Rim, 8f);
            k.Prism(0.035f, 0.1f, r * 1.16f, r * 1.08f, 7, ObstacleStyles.Role.Body, 8f);
            float d = 0.135f;
            int n = 0;
            while (d < detail)
            {
                float h = k.R(0.26f, 0.42f);
                int sides = k.Chance(0.5f) ? 6 : 7;
                float rr = Kit.Cover(k.Half, sides) * k.R(1.06f, 1.2f);
                var role = n % 2 == 1 ? ObstacleStyles.Role.Shade : ObstacleStyles.Role.Body;
                k.Prism(d, h, rr * k.R(0.9f, 0.97f), rr, sides, role, k.R(0f, 360f), k.R(-0.015f, 0.015f));
                if (k.Chance(0.55f))
                {
                    var p = Kit.Around(k.R(0f, 360f), rr * 0.75f, d + 0.02f);
                    k.Ellipsoid(d + 0.022f, p.x, p.z, new Vector3(0.09f, 0.022f, 0.07f), ObstacleStyles.Role.Rim, k.R(0f, 180f));
                }
                d += h;
                n++;
            }
            k.Prism(0f, length, r, r, 8, ObstacleStyles.Role.Shade);
        }

        /// <summary>Nuit : pilier de pierre de lune hérissé de cristaux, veines de runes lumineuses.</summary>
        static void MoonCrystals(Kit k, float length, float detail)
        {
            float r = Kit.Cover(k.Half, 6);
            k.Prism(0f, length, r, r, 6, ObstacleStyles.Role.Body, 30f);
            k.Prism(0f, 0.06f, r * 1.2f, r * 1.16f, 6, ObstacleStyles.Role.Shade, 30f);
            k.Prism(0.06f, 0.022f, r * 1.08f, r * 1.08f, 6, ObstacleStyles.Role.Glow, 30f);
            for (float d = 0.65f; d < detail; d += 0.7f) k.Prism(d, 0.02f, r * 1.03f, r * 1.03f, 6, ObstacleStyles.Role.Glow, 30f);
            for (float d = 0.22f; d < detail; d += k.R(0.3f, 0.45f))
            {
                for (int i = 0; i < 2; i++)
                {
                    float angle = k.R(0f, 360f);
                    var dir = Kit.Outward(angle, k.R(0.25f, 0.6f));
                    var basePos = Kit.Around(angle, r * 0.7f, d);
                    k.Crystal(basePos, dir, k.R(0.045f, 0.075f), k.R(0.22f, 0.42f), k.Chance(0.7f) ? ObstacleStyles.Role.Rim : ObstacleStyles.Role.Accent);
                }
            }
        }

        /// <summary>Ville : tour de béton, corniches, fenêtres allumées au hasard et enseignes de néon.</summary>
        static void NeonTower(Kit k, float length, float detail)
        {
            float half = k.Half;
            k.Square(0f, length, half, half, ObstacleStyles.Role.Shade);
            k.Box(0.03f, new Vector3(half * 2.24f, 0.06f, 0.6f), 0f, 0f, ObstacleStyles.Role.Rim);
            k.Box(0.07f, new Vector3(half * 2.12f, 0.02f, 0.02f), 0f, -0.29f, ObstacleStyles.Role.Glow);
            const float floor = 0.3f;
            int n = 0;
            for (float d = 0.08f; d < detail; d += floor, n++)
            {
                k.Box(d + floor * 0.5f, new Vector3(half * 2.04f, floor - 0.03f, 0.52f), 0f, 0f, ObstacleStyles.Role.Body);
                k.Box(d + floor - 0.012f, new Vector3(half * 2.2f, 0.024f, 0.56f), 0f, 0f, ObstacleStyles.Role.Rim);
                for (int side = -1; side <= 1; side += 2)
                {
                    float z = 0.262f * side;
                    if (n % 5 == 3)
                    {
                        k.Box(d + floor * 0.5f, new Vector3(half * 1.8f, 0.07f, 0.014f), 0f, z, n % 2 == 0 ? ObstacleStyles.Role.Glow : ObstacleStyles.Role.Accent);
                        continue;
                    }
                    for (int w = 0; w < 4; w++)
                    {
                        float roll = k.R(0f, 1f);
                        var role = roll < 0.45f ? ObstacleStyles.Role.Glow : (roll < 0.7f ? ObstacleStyles.Role.Accent : ObstacleStyles.Role.Shade);
                        k.Box(d + floor * 0.5f, new Vector3(0.085f, 0.13f, 0.012f), -0.18f + w * 0.12f, z, role);
                    }
                }
            }
        }

        /// <summary>Japon : pagode, murs vermillon, toits sombres qui s'évasent, avant-toits dorés relevés aux coins.</summary>
        static void Pagoda(Kit k, float length, float detail)
        {
            float half = k.Half;
            k.Prism(0f, length, Kit.Cover(half, 8), Kit.Cover(half, 8), 8, ObstacleStyles.Role.Body, 22.5f);
            k.Square(0f, 0.04f, half * 1.2f, half * 1.2f, ObstacleStyles.Role.Rim);
            k.Square(0.04f, 0.03f, half * 1.1f, half * 1.1f, ObstacleStyles.Role.Shade);
            for (float d = 0.07f; d < detail; d += 0.52f)
            {
                k.Square(d, 0.34f, half * 1.02f, half * 1.02f, ObstacleStyles.Role.Body);
                for (int w = -1; w <= 1; w += 2)
                {
                    k.Box(d + 0.17f, new Vector3(0.07f, 0.12f, 0.012f), w * half * 0.45f, -half * 1.02f - 0.006f, ObstacleStyles.Role.Glow);
                    k.Box(d + 0.17f, new Vector3(0.07f, 0.12f, 0.012f), w * half * 0.45f, half * 1.02f + 0.006f, ObstacleStyles.Role.Glow);
                }
                k.Square(d + 0.34f, 0.11f, half * 1.08f, half * 1.65f, ObstacleStyles.Role.Shade);
                k.Square(d + 0.45f, 0.025f, half * 1.7f, half * 1.7f, ObstacleStyles.Role.Rim);
                k.Square(d + 0.475f, 0.045f, half * 0.98f, half * 0.98f, ObstacleStyles.Role.Shade);
                for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    var corner = new Vector3(sx * half * 1.7f, -(d + 0.46f), Mathf.Max(sz * half * 1.7f, -MaxForward));
                    k.Spike(corner, new Vector3(sx * 0.7f, 0.55f, sz * 0.45f).normalized, 0.022f, 0.1f, ObstacleStyles.Role.Rim, 6);
                }
            }
        }

        /// <summary>Orage : orgues de basalte, colonnes de hauteurs différentes, bagues de cuivre, fissures d'éclairs.</summary>
        static void Basalt(Kit k, float length, float detail)
        {
            float r = Kit.Cover(k.Half, 6);
            k.Prism(0f, length, r, r, 6, ObstacleStyles.Role.Body);
            k.Prism(0f, 0.05f, r * 1.12f, r * 1.12f, 6, ObstacleStyles.Role.Rim);
            k.Prism(0.05f, 0.02f, r * 1.05f, r * 1.05f, 6, ObstacleStyles.Role.Glow);
            for (int i = 0; i < 5; i++)
            {
                float angle = i * 72f + k.R(-15f, 15f);
                var p = Kit.Around(angle, 0.15f, 0f);
                float start = k.R(0.12f, 0.55f);
                float rr = k.R(0.11f, 0.15f);
                k.Prism(start, length - start, rr, rr, 6, i % 2 == 0 ? ObstacleStyles.Role.Shade : ObstacleStyles.Role.Accent, k.R(0f, 60f), p.x, Mathf.Max(p.z, -0.2f));
                k.Prism(start + 0.03f, 0.03f, rr * 1.08f, rr * 1.08f, 6, ObstacleStyles.Role.Rim, 0f, p.x, Mathf.Max(p.z, -0.2f));
            }
            // Fissures en zigzag sur la face avant, chargées d'éclairs.
            float front = -Mathf.Max(r * 0.866f, 0.2f + 0.15f) - 0.008f;
            for (int crack = 0; crack < 2; crack++)
            {
                float x0 = k.R(-0.12f, 0.12f);
                const float step = 0.12f;
                for (float d = 0.15f + crack * 0.9f; d < detail; d += step)
                {
                    float x1 = Mathf.Clamp(x0 + k.R(-0.09f, 0.09f), -0.16f, 0.16f);
                    float len = Mathf.Sqrt(step * step + (x1 - x0) * (x1 - x0));
                    float roll = Mathf.Atan2(x1 - x0, step) * Mathf.Rad2Deg;
                    k.Box(d + step * 0.5f, new Vector3(0.022f, len + 0.01f, 0.012f), (x0 + x1) * 0.5f, front, ObstacleStyles.Role.Glow, roll);
                    x0 = x1;
                }
            }
        }

        /// <summary>Grand Nord : pilier de glace, chapeau de givre d'où pendent des glaçons, éclats de cristal.</summary>
        static void Ice(Kit k, float length, float detail)
        {
            float r = Kit.Cover(k.Half, 6);
            k.Prism(0f, length, r, r, 6, ObstacleStyles.Role.Body, 30f);
            k.Prism(0f, 0.06f, r * 1.18f, r * 1.12f, 6, ObstacleStyles.Role.Rim, 30f);
            k.Prism(0.06f, 0.02f, r * 1.08f, r * 1.08f, 6, ObstacleStyles.Role.Glow, 30f);
            for (int i = 0; i < 9; i++)
            {
                float angle = i * 40f + k.R(-10f, 10f);
                var p = Kit.Around(angle, r * 1.08f, 0.07f);
                p.z = Mathf.Max(p.z, -MaxForward);
                k.Spike(p, Vector3.down, k.R(0.02f, 0.034f), k.R(0.08f, 0.2f), ObstacleStyles.Role.Rim, 6);
            }
            for (float d = 0.9f; d < detail; d += 0.8f) k.Prism(d, 0.03f, r * 1.04f, r * 1.04f, 6, ObstacleStyles.Role.Accent, 30f);
            for (float d = 0.35f; d < detail; d += k.R(0.25f, 0.4f))
            {
                for (int i = 0; i < 3; i++)
                {
                    float angle = k.R(0f, 360f);
                    k.Crystal(Kit.Around(angle, r * 0.7f, d), Kit.Outward(angle, k.R(0.3f, 0.7f)), k.R(0.03f, 0.055f), k.R(0.14f, 0.28f),
                        k.Chance(0.6f) ? ObstacleStyles.Role.Rim : ObstacleStyles.Role.Accent);
                }
            }
        }

        /// <summary>Jungle : tronc géant coupé net, écorce striée, deux lianes enroulées et des feuilles.</summary>
        static void Trunk(Kit k, float length, float detail)
        {
            float r = Kit.Cover(k.Half, 11);
            k.Prism(0f, length, r, r, 11, ObstacleStyles.Role.Body);
            k.Prism(0f, 0.03f, r * 1.0f, r * 1.0f, 11, ObstacleStyles.Role.Accent);
            k.Prism(0.03f, 0.05f, r * 1.05f, r * 1.05f, 11, ObstacleStyles.Role.Shade);
            for (int i = 0; i < 4; i++)
            {
                var p = Kit.Around(k.R(0f, 360f), r * 0.9f, 0.04f);
                k.Ellipsoid(0.05f, p.x, p.z, new Vector3(0.07f, 0.02f, 0.06f), ObstacleStyles.Role.Rim, k.R(0f, 180f));
            }
            for (int i = 0; i < 12; i++)
            {
                float angle = i * 30f + k.R(-8f, 8f);
                var p = Kit.Around(angle, r * 0.97f, 0f);
                float start = k.R(0.08f, 0.3f);
                var piece = new MeshBuilder().AddBox(new Vector3(0f, -(start + detail) * 0.5f, 0f), new Vector3(0.035f, detail - start, 0.035f), k.C(ObstacleStyles.Role.Shade));
                k.B.Append(piece, Matrix4x4.TRS(new Vector3(p.x, 0f, p.z), Quaternion.Euler(0f, -angle, 0f), Vector3.one));
            }
            for (int vine = 0; vine < 2; vine++)
            {
                float phase = vine * 180f + k.R(0f, 60f);
                for (float d = 0.12f; d < detail; d += 0.06f)
                {
                    float angle = phase + d * 230f;
                    var p = Kit.Around(angle, r * 1.03f, d);
                    k.Sphere(d, p.x, p.z, 0.03f, ObstacleStyles.Role.Rim, 6, 4);
                    if (k.Chance(0.18f))
                        k.Leaf(new Vector3(p.x, -d, Mathf.Max(p.z, -MaxForward)), Kit.Outward(angle + k.R(-30f, 30f), k.R(0.2f, 0.6f)),
                            k.R(0.12f, 0.18f), 0.055f, k.Chance(0.5f) ? ObstacleStyles.Role.Glow : ObstacleStyles.Role.Rim);
                }
            }
        }

        /// <summary>Ciel : colonne de marbre cannelée à chapiteau à volutes, bagues d'or, nuages accrochés.</summary>
        static void CloudColumn(Kit k, float length, float detail)
        {
            float half = k.Half;
            float r = Kit.Cover(half, 16);
            k.Prism(0f, length, r, r, 16, ObstacleStyles.Role.Body);
            k.Square(0f, 0.05f, half * 1.32f, half * 1.32f, ObstacleStyles.Role.Shade);
            k.Prism(0.05f, 0.09f, r * 1.22f, r * 1.02f, 16, ObstacleStyles.Role.Body);
            for (int side = -1; side <= 1; side += 2)
            {
                var volute = new MeshBuilder().AddCylinder(new Vector3(0f, -0.22f, 0f), 0.05f, 0.44f, k.C(ObstacleStyles.Role.Rim), 14);
                k.B.Append(volute, Matrix4x4.TRS(new Vector3(side * half * 1.18f, -0.1f, 0f), Quaternion.Euler(90f, 0f, 0f), Vector3.one));
            }
            for (int i = 0; i < 8; i++)
            {
                float angle = 202.5f + i * 22.5f;
                var p = Kit.Around(angle, r * 0.99f, 0f);
                var piece = new MeshBuilder().AddBox(new Vector3(0f, -(0.2f + detail) * 0.5f, 0f), new Vector3(0.026f, detail - 0.2f, 0.012f), k.C(ObstacleStyles.Role.Shade));
                k.B.Append(piece, Matrix4x4.TRS(new Vector3(p.x, 0f, p.z), Quaternion.Euler(0f, -angle - 90f, 0f), Vector3.one));
            }
            for (float d = 0.45f; d < detail; d += 1.1f) k.Prism(d, 0.04f, r * 1.05f, r * 1.05f, 16, ObstacleStyles.Role.Rim);
            for (float d = 0.75f; d < detail; d += k.R(0.85f, 1.15f))
            {
                for (int i = 0; i < 6; i++)
                {
                    float angle = k.R(0f, 360f);
                    float radius = k.R(0.11f, 0.17f);
                    var p = Kit.Around(angle, r * 0.95f, d);
                    k.Sphere(d + k.R(-0.08f, 0.08f), p.x, p.z, radius, ObstacleStyles.Role.Accent, 10, 8);
                }
            }
        }

        /// <summary>Abysses : récif de corail bosselé, chapeau de corail cerveau, branches à polypes lumineux.</summary>
        static void Reef(Kit k, float length, float detail)
        {
            float r = Kit.Cover(k.Half, 9);
            k.Prism(0f, length, r, r, 9, ObstacleStyles.Role.Body);
            k.Prism(0f, 0.07f, r * 1.14f, r * 1.1f, 9, ObstacleStyles.Role.Rim);
            for (int i = 0; i < 10; i++)
            {
                var p = Kit.Around(i * 36f, r * 1.12f, 0f);
                k.Sphere(0.045f, p.x, p.z, 0.04f, i % 2 == 0 ? ObstacleStyles.Role.Rim : ObstacleStyles.Role.Glow, 6, 4);
            }
            for (float d = 0.26f; d < detail; d += 0.22f)
            {
                for (int i = 0; i < 3; i++)
                {
                    float angle = k.R(0f, 360f);
                    var p = Kit.Around(angle, r * 0.75f, d);
                    k.Sphere(d, p.x, p.z, k.R(0.1f, 0.16f), k.Chance(0.5f) ? ObstacleStyles.Role.Body : ObstacleStyles.Role.Shade);
                }
            }
            for (float d = 0.35f; d < detail; d += k.R(0.35f, 0.5f))
            {
                for (int i = 0; i < 2; i++)
                {
                    float angle = k.R(0f, 360f);
                    var basePos = Kit.Around(angle, r * 0.8f, d);
                    var dir = Kit.Outward(angle, k.R(0.3f, 0.8f));
                    float len = k.R(0.18f, 0.3f);
                    var piece = new MeshBuilder().AddCylinder(Vector3.zero, 0.032f, len, k.C(ObstacleStyles.Role.Rim), 8);
                    k.B.Append(piece, Matrix4x4.TRS(basePos, Quaternion.FromToRotation(Vector3.up, dir), Vector3.one));
                    var tip = basePos + dir * len;
                    k.B.AddSphere(new Vector3(tip.x, tip.y, Mathf.Max(tip.z, -MaxForward)), 0.04f, k.C(ObstacleStyles.Role.Glow), 8, 6);
                }
            }
            for (int i = 0; i < 24; i++)
            {
                float d = k.R(0.2f, detail);
                var p = Kit.Around(k.R(0f, 360f), r * 1.02f, d);
                k.Sphere(d, p.x, p.z, 0.018f, ObstacleStyles.Role.Glow, 6, 4);
            }
        }
            /// <summary>Ville (haut) : enseigne lumineuse suspendue à un pylône d'acier, câbles et croisillons.</summary>
        static void Billboard(Kit k, float length, float detail)
        {
            float half = k.Half;
            // L'enseigne, épaisse, porte la collision près de l'ouverture ; le pylône prend le relais au-delà.
            k.Square(0.6f, length - 0.6f, half, half, ObstacleStyles.Role.Shade);
            k.Box(0.012f, new Vector3(half * 2.8f, 0.024f, 0.6f), 0f, 0f, ObstacleStyles.Role.Rim);
            k.Box(0.33f, new Vector3(half * 2.7f, 0.62f, 0.56f), 0f, 0f, ObstacleStyles.Role.Body);
            for (int side = -1; side <= 1; side += 2)
            {
                float z = 0.285f * side;
                k.Box(0.33f, new Vector3(half * 2.5f, 0.52f, 0.014f), 0f, z, ObstacleStyles.Role.Glow);
                k.Box(0.25f, new Vector3(half * 1.9f, 0.05f, 0.016f), 0f, z * 1.04f, ObstacleStyles.Role.Accent);
                k.Box(0.42f, new Vector3(half * 1.3f, 0.05f, 0.016f), -half * 0.4f, z * 1.04f, ObstacleStyles.Role.Accent);
                k.Box(0.5f, new Vector3(half * 0.7f, 0.04f, 0.016f), half * 0.5f, z * 1.04f, ObstacleStyles.Role.Shade);
            }
            for (int side = -1; side <= 1; side += 2)
                k.Box(0.63f + (detail - 0.63f) * 0.5f, new Vector3(0.025f, detail - 0.63f, 0.025f), side * half * 1.1f, -0.06f, ObstacleStyles.Role.Rim);
            for (float d = 0.8f; d < detail; d += 0.32f)
            {
                k.Box(d, new Vector3(half * 2.1f, 0.02f, 0.02f), 0f, -half - 0.012f, ObstacleStyles.Role.Rim);
                k.Box(d + 0.16f, new Vector3(0.02f, 0.4f, 0.02f), 0f, -half - 0.012f, ObstacleStyles.Role.Rim, 50f);
            }
        }

        /// <summary>Japon (haut) : poutre laquée à embout doré, corde sacrée à papiers pliés, lanternes de papier.</summary>
        static void Lanterns(Kit k, float length, float detail)
        {
            float half = k.Half;
            float r = Kit.Cover(half, 8);
            k.Prism(0f, length, r, r, 8, ObstacleStyles.Role.Shade, 22.5f);
            k.Square(0f, 0.06f, half * 1.25f, half * 1.25f, ObstacleStyles.Role.Rim);
            k.Square(0.06f, 0.1f, half * 1.1f, half * 1.1f, ObstacleStyles.Role.Body);
            // Corde sacrée et papiers en zigzag qui pendent vers l'ouverture, sans l'atteindre.
            for (int i = 0; i < 16; i++)
            {
                var p = Kit.Around(i * 22.5f, half * 1.12f, 0.28f);
                k.Sphere(0.28f, p.x, p.z, 0.04f, ObstacleStyles.Role.Accent, 6, 4);
            }
            for (int i = -1; i <= 1; i++)
            {
                float x = i * half * 0.6f;
                for (int z = 0; z < 3; z++)
                    k.Box(0.215f - z * 0.03f, new Vector3(0.05f, 0.032f, 0.008f), x + (z % 2 == 0 ? 0.012f : -0.012f), -half * 1.16f, ObstacleStyles.Role.Accent);
            }
            for (float d = 0.62f; d < detail; d += 0.55f)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    float z = (half + 0.085f) * side;
                    k.Box(d - 0.15f, new Vector3(0.01f, 0.1f, 0.01f), 0f, z, ObstacleStyles.Role.Shade);
                    var cap = new MeshBuilder().AddCylinder(Vector3.zero, 0.04f, 0.022f, k.C(ObstacleStyles.Role.Shade), 10);
                    k.B.Append(cap, Matrix4x4.Translate(new Vector3(0f, -(d - 0.1f) - 0.022f, z)));
                    k.Ellipsoid(d, 0f, z, new Vector3(0.075f, 0.1f, 0.075f), ObstacleStyles.Role.Glow);
                    k.B.Append(new MeshBuilder().AddCylinder(Vector3.zero, 0.035f, 0.02f, k.C(ObstacleStyles.Role.Shade), 10),
                        Matrix4x4.Translate(new Vector3(0f, -(d + 0.1f), z)));
                    k.Box(d + 0.15f, new Vector3(0.012f, 0.08f, 0.012f), 0f, z, ObstacleStyles.Role.Body);
                }
            }
        }

        /// <summary>Orage (haut) : nuage noir au ventre plat, bourrelets sombres, éclairs qui le zèbrent.</summary>
        static void StormCloud(Kit k, float length, float detail)
        {
            float r = Kit.Cover(k.Half, 10);
            k.Prism(0f, length, r, r, 10, ObstacleStyles.Role.Shade);
            k.Prism(0f, 0.08f, r * 1.25f, r * 1.32f, 10, ObstacleStyles.Role.Shade);
            for (int i = 0; i < 10; i++)
            {
                var p = Kit.Around(i * 36f + 18f, r * 1.2f, 0f);
                k.Sphere(0.16f, p.x, p.z, 0.1f, ObstacleStyles.Role.Accent);
            }
            for (float d = 0.3f; d < detail; d += 0.2f)
            {
                for (int i = 0; i < 3; i++)
                {
                    float angle = k.R(0f, 360f);
                    float radius = k.R(0.14f, 0.22f);
                    var p = Kit.Around(angle, r * 0.85f, d);
                    k.Sphere(d, p.x, p.z, radius, k.Chance(0.5f) ? ObstacleStyles.Role.Accent : ObstacleStyles.Role.Shade, 10, 7);
                }
            }
            float front = -MaxForward + 0.01f;
            float x0 = k.R(-0.1f, 0.1f);
            const float step = 0.12f;
            for (float d = 0.12f; d < detail; d += step)
            {
                float x1 = Mathf.Clamp(x0 + k.R(-0.1f, 0.1f), -0.18f, 0.18f);
                float len = Mathf.Sqrt(step * step + (x1 - x0) * (x1 - x0));
                float roll = Mathf.Atan2(x1 - x0, step) * Mathf.Rad2Deg;
                k.Box(d + step * 0.5f, new Vector3(0.024f, len + 0.01f, 0.012f), (x0 + x1) * 0.5f, front, ObstacleStyles.Role.Glow, roll);
                x0 = x1;
            }
        }

        /// <summary>Ciel (haut) : banc de nuages au ventre plat, gonflé de bourrelets, quelques paillettes d'or.</summary>
        static void CloudBank(Kit k, float length, float detail)
        {
            float r = Kit.Cover(k.Half, 12);
            k.Prism(0f, length, r, r, 12, ObstacleStyles.Role.Accent);
            k.Prism(0f, 0.07f, r * 1.2f, r * 1.26f, 12, ObstacleStyles.Role.Accent);
            for (int i = 0; i < 10; i++)
            {
                var p = Kit.Around(i * 36f, r * 1.18f, 0f);
                k.Sphere(0.15f, p.x, p.z, 0.09f, ObstacleStyles.Role.Accent, 10, 7);
            }
            for (float d = 0.3f; d < detail; d += 0.2f)
            {
                for (int i = 0; i < 3; i++)
                {
                    float angle = k.R(0f, 360f);
                    var p = Kit.Around(angle, r * 0.85f, d);
                    k.Sphere(d, p.x, p.z, k.R(0.14f, 0.22f), k.Chance(0.25f) ? ObstacleStyles.Role.Shade : ObstacleStyles.Role.Accent, 10, 7);
                }
            }
            for (int i = 0; i < 14; i++)
            {
                float d = k.R(0.3f, detail);
                var p = Kit.Around(k.R(0f, 360f), r * 1.15f, d);
                k.Sphere(d, p.x, p.z, 0.02f, ObstacleStyles.Role.Rim, 6, 4);
            }
        }

        /// <summary>Abysses (haut) : surplomb de roche sombre croûté de corail, varech plaqué, méduses lumineuses.</summary>
        static void JellyOverhang(Kit k, float length, float detail)
        {
            float r = Kit.Cover(k.Half, 9);
            k.Prism(0f, length, r, r, 9, ObstacleStyles.Role.Shade);
            k.Prism(0f, 0.06f, r * 1.14f, r * 1.1f, 9, ObstacleStyles.Role.Body);
            for (int i = 0; i < 9; i++)
            {
                var p = Kit.Around(i * 40f, r * 1.1f, 0f);
                k.Sphere(0.04f, p.x, p.z, 0.04f, ObstacleStyles.Role.Rim, 6, 4);
            }
            // Varech : rubans ondulés plaqués contre la roche, qui retombent jusqu'au bord.
            for (int strand = 0; strand < 5; strand++)
            {
                float angle = 200f + strand * 35f + k.R(-8f, 8f);
                float start = k.R(1.6f, 2.6f);
                for (float d = start; d > 0.1f; d -= 0.12f)
                {
                    float wave = Mathf.Sin(d * 9f + strand) * 10f;
                    var p = Kit.Around(angle + wave, r * 1.04f, d);
                    k.Ellipsoid(d - 0.06f, p.x, p.z, new Vector3(0.022f, 0.075f, 0.012f), ObstacleStyles.Role.Accent, -angle - wave);
                }
            }
            // Méduses : cloche lumineuse et tentacules vers l'ouverture, sur les côtés.
            for (float d = 0.7f; d < detail; d += k.R(0.6f, 0.9f))
            {
                float x = (k.Chance(0.5f) ? 1f : -1f) * k.Half * k.R(1.25f, 1.45f);
                float z = k.R(-0.2f, 0.2f);
                k.Ellipsoid(d, x, z, new Vector3(0.09f, 0.06f, 0.09f), ObstacleStyles.Role.Glow);
                for (int t = 0; t < 4; t++)
                    k.Box(d - 0.16f, new Vector3(0.008f, 0.2f, 0.008f), x - 0.045f + t * 0.03f, z, ObstacleStyles.Role.Rim);
            }
            for (int i = 0; i < 20; i++)
            {
                float d = k.R(0.2f, detail);
                var p = Kit.Around(k.R(0f, 360f), r * 1.02f, d);
                k.Sphere(d, p.x, p.z, 0.018f, ObstacleStyles.Role.Glow, 6, 4);
            }
        }

        /// <summary>Prairie (haut) : racines qui courent sur la roche et s'arrêtent au bord du surplomb.</summary>
        static void Roots(Kit k, float detail)
        {
            float front = -0.37f;
            for (int root = 0; root < 3; root++)
            {
                float x0 = (root - 1) * k.Half * 0.6f + k.R(-0.04f, 0.04f);
                const float step = 0.1f;
                for (float d = Mathf.Min(detail, k.R(1.6f, 2.8f)); d > 0.14f; d -= step)
                {
                    float x1 = Mathf.Clamp(x0 + k.R(-0.05f, 0.05f), -k.Half * 1.1f, k.Half * 1.1f);
                    float roll = Mathf.Atan2(x1 - x0, step) * Mathf.Rad2Deg;
                    k.Box(d - step * 0.5f, new Vector3(0.026f, step + 0.012f, 0.03f), (x0 + x1) * 0.5f, Mathf.Max(front, -MaxForward), ObstacleStyles.Role.Accent, roll);
                    x0 = x1;
                }
            }
        }
    }
}
