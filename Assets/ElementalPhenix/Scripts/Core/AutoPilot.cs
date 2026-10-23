using System.Collections.Generic;

namespace ElementalPhenix.Core
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
            var start = new Motion
            {
                BoostTime = sim.BoostTime, Factor = sim.SpeedFactor, Time = sim.Time,
                SlowTime = sim.SlowTime, Scale = sim.TimeScale, GuideTime = sim.GuideTime, GuideTarget = sim.GuideTarget,
            };
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
            /// <summary>Temps de jeu (ralenti par la marée lente), ralenti et courant porteur en cours.</summary>
            public float Time;
            public float SlowTime;
            public float Scale;
            public float GuideTime;
            public int GuideTarget;
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
            ElementPowers.StepSlow(ref m.SlowTime, ref m.Scale, dt, cfg);
            float gdt = dt * m.Scale;
            m.Time += gdt;
            float time = m.Time;
            GameSimulation.StepBoost(ref m.BoostTime, ref m.Factor, gdt, cfg);
            var pipes = sim.Pipes;
            if (flap) vy = cfg.FlapVelocity;
            // Les courants et le souffle sont calculés avant le défilement du pas, comme dans la simulation.
            float accel = cfg.GustAt(time) + GameSimulation.CurrentAt(cfg.BirdCenterX, sim.Currents, m.Scroll, cfg);
            if (m.GuideTime > 0f)
            {
                m.GuideTime = System.Math.Max(0f, m.GuideTime - gdt);
                int target = -1;
                for (int i = 0; i < pipes.Count; i++) if (pipes[i].Id == m.GuideTarget) target = i;
                if (target >= 0 && cfg.BirdCenterX - cfg.BirdRadius < pipes[target].X - m.Scroll + cfg.PipeWidth)
                {
                    ref var g = ref pipes[target];
                    float center = g.BaseTop + g.ShiftAt(time - gdt, cfg) - g.Widen * 0.5f + g.Gap(cfg) * 0.5f;
                    accel = ElementPowers.GuideAcceleration(y + cfg.BirdHeight * 0.5f, vy, center, cfg);
                }
                else m.GuideTime = 0f;
            }
            m.Scroll += cfg.ScrollSpeed * m.Factor * gdt;
            dt = gdt;
            vy += (cfg.Gravity + accel) * dt;
            if (vy < cfg.MaxRiseSpeed) vy = cfg.MaxRiseSpeed;
            if (vy > cfg.MaxFallSpeed) vy = cfg.MaxFallSpeed;
            y += vy * dt;
            if (y < -cfg.BirdHeight) y = -cfg.BirdHeight;
            if (y + cfg.BirdHeight >= cfg.GroundY) return false;

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
            for (int i = 0; i < pipes.Count; i++)
            {
                ref var p = ref pipes[i];
                // Paire déjà traversable (étoile, feu, ombre). L'élargissement en cours est pris tel quel :
                // l'ouverture ne fait que grandir, la prévision reste prudente.
                if (p.Pierced) continue;
                if (Collision.HitsPipe(cx, cy, cfg.BirdRadius, p.BaseTop + p.ShiftAt(time, cfg) - p.Widen * 0.5f, p.X - m.Scroll, cfg, p.Kind, p.Gap(cfg))) return false;
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
