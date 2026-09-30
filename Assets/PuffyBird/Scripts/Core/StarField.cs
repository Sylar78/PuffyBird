namespace PuffyBird.Core
{
    /// <summary>Une étoile de vitesse, centre en px logiques (y vers le bas).</summary>
    public struct Star
    {
        public int Id;
        public bool Active;
        public float X;
        public float PrevX;
        public float Y;
    }

    /// <summary>
    /// Étoiles de vitesse actives, dans un tableau de taille fixe (aucune allocation en jeu).
    /// Elles défilent avec les tuyaux.
    /// </summary>
    public sealed class StarField
    {
        readonly Star[] _stars;
        int _nextId;

        public StarField(int capacity = 4)
        {
            _stars = new Star[capacity];
        }

        public int Capacity => _stars.Length;

        /// <summary>Emplacement i (0 à Capacity − 1), actif ou non.</summary>
        public ref Star this[int index] => ref _stars[index];

        public int ActiveCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _stars.Length; i++) if (_stars[i].Active) n++;
                return n;
            }
        }

        public void Clear()
        {
            for (int i = 0; i < _stars.Length; i++) _stars[i].Active = false;
        }

        public void Spawn(float x, float y)
        {
            int slot = -1;
            for (int i = 0; i < _stars.Length; i++)
            {
                if (!_stars[i].Active)
                {
                    slot = i;
                    break;
                }
            }
            if (slot < 0) return;
            _stars[slot] = new Star { Id = _nextId++, Active = true, X = x, PrevX = x, Y = y };
        }

        public void SavePrevious()
        {
            for (int i = 0; i < _stars.Length; i++) _stars[i].PrevX = _stars[i].X;
        }

        public void Advance(float dx, GameConfig cfg, float viewMargin)
        {
            for (int i = 0; i < _stars.Length; i++)
            {
                ref var s = ref _stars[i];
                if (!s.Active) continue;
                s.X -= dx;
                if (s.X + cfg.StarRadius < -cfg.PipeDespawnMargin - viewMargin) s.Active = false;
            }
        }
    }
}
