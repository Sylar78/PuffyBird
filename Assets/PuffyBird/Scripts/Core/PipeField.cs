using System;

namespace PuffyBird.Core
{
    /// <summary>Forme d'une paire : deux tuyaux, ou un seul tuyau plus long (extension).</summary>
    public enum PipeKind : byte
    {
        /// <summary>Tuyau du haut (infini) et tuyau du bas, ouverture tirée (§7.2).</summary>
        Pair,
        /// <summary>Tuyau du haut seul : l'ouverture est en bas, entre le tuyau et le sol.</summary>
        TopOnly,
        /// <summary>Tuyau du bas seul : l'ouverture est en haut, entre le tuyau et le haut de l'écran.</summary>
        BottomOnly,
    }

    /// <summary>Une paire de tuyaux (§7.5). X est le bord gauche, chapeau inclus.</summary>
    public struct PipePair
    {
        public int Id;
        public float X;
        public float PrevX;
        /// <summary>Valeur tirée pour le haut de l'ouverture (§7.2), même pour un tuyau seul : la suite des tirages reste celle de la spec.</summary>
        public int GapTop;
        /// <summary>Haut de l'ouverture au moment de l'apparition : <see cref="GapTop"/> pour une paire, 0 ou le sol moins l'ouverture pour un tuyau seul.</summary>
        public float BaseTop;
        public PipeKind Kind;
        /// <summary>Distance horizontale depuis la paire précédente (bord gauche à bord gauche), 0 pour la première.</summary>
        public float SpacingBefore;
        public bool Scored;
        /// <summary>L'oiseau est passé à moins de <see cref="GameConfig.NearMissDistance"/> d'un tuyau de cette paire.</summary>
        public bool Grazed;
        /// <summary>Le frôlement a déjà été signalé (une seule fois par paire).</summary>
        public bool GrazeReported;
        /// <summary>L'étoile a protégé l'oiseau contre cette paire : elle ne peut plus le toucher.</summary>
        public bool Pierced;
        /// <summary>Amplitude du mouvement vertical en px (0 = paire fixe).</summary>
        public float MoveAmplitude;
        public float MovePhase;
        /// <summary>Décalage vertical courant des deux tuyaux, y vers le bas.</summary>
        public float Shift;
        public float PrevShift;

        /// <summary>Haut de l'ouverture à cet instant.</summary>
        public float OpeningTop => BaseTop + Shift;

        /// <summary>Décalage vertical au temps de simulation <paramref name="time"/> (paires mobiles).</summary>
        public float ShiftAt(float time, GameConfig cfg)
        {
            if (MoveAmplitude == 0f) return 0f;
            return MoveAmplitude * (float)Math.Sin(MovePhase + time * 2.0 * Math.PI / cfg.PipeMovePeriod);
        }
    }

    /// <summary>
    /// Paires de tuyaux actives, dans un tampon circulaire de taille fixe :
    /// aucune allocation pendant la partie (§18.2). Au plus 3 paires sont visibles en 9:16, 4 en 3:4.
    /// </summary>
    public sealed class PipeField
    {
        readonly PipePair[] _pairs;
        int _head;
        int _count;
        int _nextId;

        public PipeField(int capacity = 4)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            _pairs = new PipePair[capacity];
        }

        /// <summary>
        /// Distance entre la dernière paire et la prochaine (bord gauche à bord gauche), choisie par la
        /// simulation à l'apparition de la dernière ; 0 = <see cref="GameConfig.PipeSpacing"/>.
        /// </summary>
        public float NextSpacing;

        public int Count => _count;
        public int Capacity => _pairs.Length;

        /// <summary>Paire i, de la plus ancienne (0, la plus à gauche) à la plus récente.</summary>
        public ref PipePair this[int index]
        {
            get
            {
                if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));
                return ref _pairs[(_head + index) % _pairs.Length];
            }
        }

        public ref PipePair Last => ref this[_count - 1];

        public void Clear()
        {
            _head = 0;
            _count = 0;
            NextSpacing = 0f;
        }

        public void Spawn(float x, int gapTop)
        {
            if (_count == _pairs.Length) RemoveFirst();
            int slot = (_head + _count) % _pairs.Length;
            _pairs[slot] = new PipePair { Id = _nextId++, X = x, PrevX = x, GapTop = gapTop, BaseTop = gapTop, Scored = false };
            _count++;
        }

        public void SpawnRandom(float x, Rng rng, GameConfig cfg) => Spawn(x, rng.Range(cfg.GapTopMin, cfg.GapTopMax));

        public void RemoveFirst()
        {
            if (_count == 0) return;
            _head = (_head + 1) % _pairs.Length;
            _count--;
        }

        public void SavePrevious()
        {
            for (int i = 0; i < _count; i++)
            {
                ref var p = ref this[i];
                p.PrevX = p.X;
                p.PrevShift = p.Shift;
            }
        }

        /// <summary>Met à jour le décalage vertical des paires mobiles.</summary>
        public void UpdateMotion(float time, GameConfig cfg)
        {
            for (int i = 0; i < _count; i++)
            {
                ref var p = ref this[i];
                p.Shift = p.ShiftAt(time, cfg);
            }
        }

        /// <summary>
        /// Défilement, suppression et apparition pour un pas (§7.3, §7.4). L'apparition se
        /// base sur la position du dernier tuyau, pas sur un minuteur : l'espacement est
        /// exactement <see cref="NextSpacing"/> (<see cref="GameConfig.PipeSpacing"/> par défaut).
        /// </summary>
        /// <param name="viewMargin">
        /// Largeur visible au-delà de l'écran logique de chaque côté, en px (0 en 9:16). Les tuyaux
        /// apparaissent et disparaissent d'autant plus loin, donc toujours hors champ. Les positions
        /// et l'ordre des tirages ne changent pas : seule l'apparition est plus précoce.
        /// </param>
        /// <param name="speedFactor">Multiplicateur du défilement (étoile de vitesse).</param>
        /// <returns>Vrai si une nouvelle paire est apparue (c'est alors <see cref="Last"/>).</returns>
        public bool Advance(float dt, Rng rng, GameConfig cfg, float viewMargin = 0f, float speedFactor = 1f)
        {
            float dx = cfg.ScrollSpeed * speedFactor * dt;
            for (int i = 0; i < _count; i++) this[i].X -= dx;
            if (_count > 0 && this[0].X + cfg.PipeWidth < -cfg.PipeDespawnMargin - viewMargin) RemoveFirst();
            float spacing = NextSpacing > 0f ? NextSpacing : cfg.PipeSpacing;
            if (_count > 0 && Last.X <= cfg.Width + cfg.SpawnLookahead + viewMargin - spacing)
            {
                SpawnRandom(Last.X + spacing, rng, cfg);
                Last.SpacingBefore = spacing;
                NextSpacing = 0f;
                return true;
            }
            return false;
        }
    }
}
