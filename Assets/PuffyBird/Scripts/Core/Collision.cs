namespace PuffyBird.Core
{
    /// <summary>Tests géométriques purs (§9).</summary>
    public static class Collision
    {
        /// <summary>Hauteur utilisée pour rendre le tuyau du haut infini vers le haut.</summary>
        public const float InfiniteHeight = 10000f;

        public static bool CircleRect(float cx, float cy, float r, float rx, float ry, float rw, float rh)
        {
            float nx = cx < rx ? rx : (cx > rx + rw ? rx + rw : cx);
            float ny = cy < ry ? ry : (cy > ry + rh ? ry + rh : cy);
            float dx = cx - nx;
            float dy = cy - ny;
            return dx * dx + dy * dy < r * r;
        }

        /// <summary>
        /// Collision entre l'oiseau (cercle) et une paire : le tuyau du haut est infini, on ne
        /// peut donc pas passer au-dessus ; le tuyau du bas descend jusqu'au sol. Un tuyau seul
        /// (<paramref name="kind"/>) n'a que l'un des deux.
        /// </summary>
        public static bool HitsPipe(float cx, float cy, float radius, float gapTop, float pipeX, GameConfig cfg, PipeKind kind = PipeKind.Pair)
        {
            return (kind != PipeKind.BottomOnly && CircleRect(cx, cy, radius, pipeX, -InfiniteHeight, cfg.PipeWidth, InfiniteHeight + gapTop))
                || (kind != PipeKind.TopOnly && CircleRect(cx, cy, radius, pipeX, gapTop + cfg.PipeGap, cfg.PipeWidth, cfg.GroundY));
        }

        /// <summary>
        /// Écart entre le bord du cercle et le rectangle (0 s'ils se touchent ou se recouvrent).
        /// </summary>
        public static float CircleRectGap(float cx, float cy, float r, float rx, float ry, float rw, float rh)
        {
            float nx = cx < rx ? rx : (cx > rx + rw ? rx + rw : cx);
            float ny = cy < ry ? ry : (cy > ry + rh ? ry + rh : cy);
            float dx = cx - nx;
            float dy = cy - ny;
            float gap = (float)System.Math.Sqrt(dx * dx + dy * dy) - r;
            return gap > 0f ? gap : 0f;
        }

        /// <summary>Écart entre l'oiseau et le tuyau le plus proche d'une paire (même géométrie que <see cref="HitsPipe"/>).</summary>
        public static float PipeGap(float cx, float cy, float radius, float gapTop, float pipeX, GameConfig cfg, PipeKind kind = PipeKind.Pair)
        {
            float top = kind == PipeKind.BottomOnly ? float.MaxValue
                : CircleRectGap(cx, cy, radius, pipeX, -InfiniteHeight, cfg.PipeWidth, InfiniteHeight + gapTop);
            float bottom = kind == PipeKind.TopOnly ? float.MaxValue
                : CircleRectGap(cx, cy, radius, pipeX, gapTop + cfg.PipeGap, cfg.PipeWidth, cfg.GroundY);
            return top < bottom ? top : bottom;
        }

        public static bool CircleCircle(float ax, float ay, float ar, float bx, float by, float br)
        {
            float dx = ax - bx;
            float dy = ay - by;
            float r = ar + br;
            return dx * dx + dy * dy < r * r;
        }
    }
}
