using PuffyBird.Core;
using UnityEngine;

namespace PuffyBird
{
    /// <summary>
    /// Réglages du joueur gardés d'une session à l'autre (PlayerPrefs, clés <c>puffybird.*</c>).
    /// Les valeurs sont lues une fois puis gardées en mémoire ; l'écriture est immédiate.
    /// Le meilleur score et le muet restent dans <see cref="PlayerPrefsScoreStorage"/>.
    /// </summary>
    public sealed class GamePrefs
    {
        const string HapticsKey = "puffybird.haptics";
        const string MusicKey = "puffybird.music";
        const string QualityKey = "puffybird.quality";
        const string ConsentKey = "puffybird.consent";
        const string SkinKey = "puffybird.skin";
        const string StreakKey = "puffybird.streak";
        const string StreakDayKey = "puffybird.streakday";
        const string LongestStreakKey = "puffybird.streakbest";

        /// <summary>Consentement aux pubs personnalisées pas encore demandé.</summary>
        public const int ConsentUnknown = -1;
        public const int ConsentRefused = 0;
        public const int ConsentGranted = 1;

        bool _haptics;
        bool _music;
        int _quality;
        int _consent;
        string _skin;
        int _streak;
        int _streakDay;
        int _longestStreak;

        public GamePrefs()
        {
            _haptics = PlayerPrefs.GetInt(HapticsKey, 1) == 1;
            _music = PlayerPrefs.GetInt(MusicKey, 1) == 1;
            _quality = PlayerPrefs.GetInt(QualityKey, -1);
            _consent = PlayerPrefs.GetInt(ConsentKey, ConsentUnknown);
            _skin = PlayerPrefs.GetString(SkinKey, "");
            _streak = PlayerPrefs.GetInt(StreakKey, 0);
            _streakDay = PlayerPrefs.GetInt(StreakDayKey, 0);
            _longestStreak = PlayerPrefs.GetInt(LongestStreakKey, 0);
        }

        /// <summary>Série de jours consécutifs en cours (à relire avec <see cref="DailyStreak.Current"/> pour l'affichage).</summary>
        public int Streak => _streak;

        /// <summary>Numéro du dernier jour joué (0 = jamais).</summary>
        public int StreakDay => _streakDay;

        /// <summary>Plus longue série atteinte : ne baisse jamais.</summary>
        public int LongestStreak => _longestStreak;

        /// <summary>Note une partie lancée le jour <paramref name="today"/>. Retourne vrai si la plus longue série vient de grandir.</summary>
        public bool RecordPlayDay(int today)
        {
            int streak = DailyStreak.Advance(_streakDay, _streak, today);
            int day = DailyStreak.LastDayAfter(_streakDay, today);
            if (streak != _streak || day != _streakDay)
            {
                _streak = streak;
                _streakDay = day;
                PlayerPrefs.SetInt(StreakKey, _streak);
                PlayerPrefs.SetInt(StreakDayKey, _streakDay);
            }
            if (_streak <= _longestStreak)
            {
                PlayerPrefs.Save();
                return false;
            }
            _longestStreak = _streak;
            PlayerPrefs.SetInt(LongestStreakKey, _longestStreak);
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>Vibrations (activées par défaut).</summary>
        public bool Haptics
        {
            get => _haptics;
            set => SetInt(HapticsKey, ref _haptics, value);
        }

        /// <summary>Musique (activée par défaut ; les bruitages dépendent du muet, à part).</summary>
        public bool Music
        {
            get => _music;
            set => SetInt(MusicKey, ref _music, value);
        }

        /// <summary>Niveau de qualité graphique choisi, ou −1 tant que le joueur n'a rien choisi (réglage automatique).</summary>
        public int Quality
        {
            get => _quality;
            set => SetInt(QualityKey, ref _quality, value);
        }

        /// <summary>Consentement RGPD aux pubs personnalisées : <see cref="ConsentUnknown"/>, <see cref="ConsentRefused"/> ou <see cref="ConsentGranted"/>.</summary>
        public int Consent
        {
            get => _consent;
            set => SetInt(ConsentKey, ref _consent, value);
        }

        /// <summary>Identifiant du skin choisi (vide = oiseau par défaut).</summary>
        public string Skin
        {
            get => _skin;
            set
            {
                if (_skin == value) return;
                _skin = value ?? "";
                PlayerPrefs.SetString(SkinKey, _skin);
                PlayerPrefs.Save();
            }
        }

        static void SetInt(string key, ref bool field, bool value)
        {
            if (field == value) return;
            field = value;
            PlayerPrefs.SetInt(key, value ? 1 : 0);
            PlayerPrefs.Save();
        }

        static void SetInt(string key, ref int field, int value)
        {
            if (field == value) return;
            field = value;
            PlayerPrefs.SetInt(key, value);
            PlayerPrefs.Save();
        }
    }
}
