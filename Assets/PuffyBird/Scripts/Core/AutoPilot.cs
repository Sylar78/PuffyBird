using System.Collections.Generic;

namespace PuffyBird.Core
{
    /// <summary>
    /// Pilote automatique (§23.3), version « qui anticipe » : à chaque pas, il cherche s'il existe
    /// une suite de battements qui évite toute collision sur l'horizon d'anticipation (assez long
    /// pour franchir l'ouverture courante et préparer la suivante). Il ne bat des ailes que si
    /// aucune trajectoire sûre ne commence sans battement.
    /// S'il meurt, une combinaison d'ouvertures est infranchissable : les constantes rendent
    /// alors le jeu injuste (§16.2). Sert de test de non-régression des réglages et de mode démo
    /// (il alloue au premier appel, à ne pas utiliser dans la boucle de jeu normale).
    /// </summary>
    public static class AutoPilot
    {
        /// <summary>Horizon d'anticipation, en pas de simulation (≈ 1,7 s, plus d'une paire de tuyaux).</summary>
        public const int Horizon = 100;

        static readonly HashSet<long> Explored = new HashSet<long>();
        public static bool ShouldFlap(GameSimulation sim)
        {
            if (sim.State == GameState.Ready) return true;
            if (sim.State != GameState.Playing) return false;
            Explored.Clear();
            var start = new Motion { BoostTime = sim.BoostTime, Factor = sim.SpeedFactor };
            return !Survives(sim, sim.Bird.Y, sim.Bird.Vy, 0, flap: false, start);
        }

        /// <summary>Défilement et accélération le long d'une trajectoire (une étoile prise l'accélère).</summary>
        struct Motion
        {
            public float Scroll;
            public float BoostTime;
            public float Factor;
            public int TakenStars;
            public int PickStep;
        }

        /// <summary>
        /// Rejoue un pas de <see cref="GameSimulation.Step"/> (accélération, battement éventuel,
        /// oiseau, étoiles, puis tuyaux et collisions) et renvoie vrai s'il reste une trajectoire sûre
        /// jusqu'à l'horizon.
        /// </summary>
        static bool Survives(GameSimulation sim, float y, float vy, int depth, bool flap, Motion m)
        {
            var cfg = sim.Config;
            float dt = cfg.Step;
            int step = depth + 1;
            GameSimulation.StepBoost(ref m.BoostTime, ref m.Factor, dt, cfg);
            m.Scroll += cfg.ScrollSpeed * m.Factor * dt;

            if (flap) vy = cfg.FlapVelocity;
            vy += cfg.Gravity * dt;
            if (vy < cfg.MaxRiseSpeed) vy = cfg.MaxRiseSpeed;
            if (vy > cfg.MaxFallSpeed) vy = cfg.MaxFallSpeed;
            y += vy * dt;
            if (y < -cfg.BirdHeight) y = -cfg.BirdHeight;
            if (y + cfg.BirdHeight >= cfg.GroundY) return false;

            float time = sim.Time + dt * step;
            float cx = cfg.BirdCenterX;
            float cy = y + cfg.BirdHeight * 0.5f;
            var stars = sim.Stars;
            for (int i = 0; i < stars.Capacity; i++)
            {
                ref var s = ref stars[i];
                if (!s.Active || (m.TakenStars & (1 << i)) != 0) continue;
                if (!Collision.CircleCircle(cx, cy, cfg.BirdRadius, s.X - m.Scroll, s.Y, cfg.StarRadius)) continue;
                m.TakenStars |= 1 << i;
                m.BoostTime = cfg.StarBoostDuration;
                m.PickStep = step;
            }
            var pipes = sim.Pipes;
            for (int i = 0; i < pipes.Count; i++)
            {
                ref var p = ref pipes[i];
                if (Collision.HitsPipe(cx, cy, cfg.BirdRadius, p.BaseTop + p.ShiftAt(time, cfg), p.X - m.Scroll, cfg, p.Kind)) return false;
            }
            if (step >= Horizon) return true;

            // Un état déjà exploré sans succès n'a pas besoin d'être revisité. L'accélération fait
            // partie de l'état : étoiles prises et pas de la dernière prise.
            long key = ((long)step << 42) | ((long)m.PickStep << 34) | ((long)m.TakenStars << 30)
                | ((long)(int)(y + 1000f) << 15) | (long)(int)(vy + 1000f);
            if (!Explored.Add(key)) return false;
            return Survives(sim, y, vy, step, flap: false, m) || Survives(sim, y, vy, step, flap: true, m);
        }
    }
}
