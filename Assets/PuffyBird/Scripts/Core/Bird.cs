using System;

namespace PuffyBird.Core
{
    /// <summary>
    /// État de l'oiseau (§6). X est constant (<see cref="GameConfig.BirdX"/>) ; Y est le bord
    /// haut du rectangle 34 × 24, y vers le bas.
    /// </summary>
    public sealed class Bird
    {
        public float Y;
        public float PrevY;
        public float Vy;
        /// <summary>Angle interne en degrés, positif = nez vers le haut.</summary>
        public float Rot;
        public float WingTimer;
        public int WingIndex;

        public void Reset(float startY)
        {
            Y = startY;
            PrevY = startY;
            Vy = 0f;
            Rot = 0f;
            WingTimer = 0f;
            WingIndex = 0;
        }

        /// <summary>Le tap remplace la vitesse verticale, il ne s'y ajoute pas.</summary>
        public void Flap(GameConfig cfg)
        {
            Vy = cfg.FlapVelocity;
            Rot = cfg.RotOnFlap;
        }

        /// <summary>Euler semi-implicite : vitesse, puis position, puis rotation (§4.3, §6.4).</summary>
        public void Integrate(float dt, float rotSpeed, GameConfig cfg)
        {
            Vy = Clamp(Vy + cfg.Gravity * dt, cfg.MaxRiseSpeed, cfg.MaxFallSpeed);
            Y += Vy * dt;
            Rot = Math.Max(cfg.RotMin, Rot - rotSpeed * dt);
            if (Y < -cfg.BirdHeight) Y = -cfg.BirdHeight;
        }

        public void AnimateWings(float dt, GameConfig cfg)
        {
            WingTimer += dt;
            if (WingTimer >= cfg.WingFrameTime)
            {
                WingTimer -= cfg.WingFrameTime;
                WingIndex = (WingIndex + 1) % cfg.WingSequence.Length;
            }
        }

        /// <summary>Angle affiché : plafonné à +20° quand le nez monte (§6.5).</summary>
        public float DisplayRotation(GameConfig cfg) => Math.Min(Rot, cfg.RotVisibleMax);

        /// <summary>Image d'aile courante : 0 haute, 1 milieu, 2 basse.</summary>
        public int WingFrame(GameConfig cfg) => cfg.WingSequence[WingIndex];

        /// <summary>
        /// Phase continue du battement dans [0, 1) sur un cycle complet, pour une animation
        /// 3D fluide calée sur les images de la spec.
        /// </summary>
        public float WingPhase(GameConfig cfg)
        {
            int count = cfg.WingSequence.Length;
            return (WingIndex + WingTimer / cfg.WingFrameTime) / count;
        }

        public float CenterY(GameConfig cfg) => Y + cfg.BirdHeight * 0.5f;

        static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
    }
}
