using ElementalPhenix.Core;
using UnityEngine;

namespace ElementalPhenix
{
    /// <summary>Persistance via PlayerPrefs, avec les clés de la spec (§17).</summary>
    public sealed class PlayerPrefsScoreStorage : IScoreStorage
    {
        const string BestKey = "elementalphenix.best";
        const string MutedKey = "elementalphenix.muted";
        const int Unreadable = int.MinValue;

        public bool TryReadBest(out int best)
        {
            best = 0;
            if (!PlayerPrefs.HasKey(BestKey)) return true;
            int value = PlayerPrefs.GetInt(BestKey, Unreadable);
            // Clé présente mais d'un autre type : valeur illisible, à ne jamais écraser.
            if (value == Unreadable || value < 0) return false;
            best = value;
            return true;
        }

        public void WriteBest(int best)
        {
            PlayerPrefs.SetInt(BestKey, best);
            PlayerPrefs.Save();
        }

        bool? _muted;

        public bool Muted
        {
            get
            {
                if (!_muted.HasValue) _muted = PlayerPrefs.GetInt(MutedKey, 0) == 1;
                return _muted.Value;
            }
            set
            {
                _muted = value;
                PlayerPrefs.SetInt(MutedKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }
    }
}
