using System;

namespace PuffyBird.Core
{
    /// <summary>Une paire de tuyaux (§7.5). X est le bord gauche, chapeau inclus.</summary>
    public struct PipePair
    {
        public int Id;
        public float X;
        public float PrevX;
        public int GapTop;
        public bool Scored;
    }

    /// <summary>
    /// Paires de tuyaux actives, dans un tampon circulaire de taille fixe :
    /// aucune allocation pendant la partie (§18.2). Au plus 3 paires sont visibles.
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
        }

        public void Spawn(float x, int gapTop)
        {
            if (_count == _pairs.Length) RemoveFirst();
            int slot = (_head + _count) % _pairs.Length;
            _pairs[slot] = new PipePair { Id = _nextId++, X = x, PrevX = x, GapTop = gapTop, Scored = false };
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
            for (int i = 0; i < _count; i++) this[i].PrevX = this[i].X;
        }

        /// <summary>
        /// Défilement, suppression et apparition pour un pas (§7.3, §7.4). L'apparition se
        /// base sur la position du dernier tuyau, pas sur un minuteur : l'espacement reste
        /// exactement constant.
        /// </summary>
        public void Advance(float dt, Rng rng, GameConfig cfg)
        {
            float dx = cfg.ScrollSpeed * dt;
            for (int i = 0; i < _count; i++) this[i].X -= dx;
            if (_count > 0 && this[0].X + cfg.PipeWidth < -cfg.PipeDespawnMargin) RemoveFirst();
            if (_count > 0 && Last.X <= cfg.Width + cfg.SpawnLookahead - cfg.PipeSpacing)
                SpawnRandom(Last.X + cfg.PipeSpacing, rng, cfg);
        }
    }
}
