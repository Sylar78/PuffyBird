using System;
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
        const string DailyDayKey = "puffybird.dailyday";
        const string DailyBestKey = "puffybird.dailybest";
        const string AchievementsKey = "puffybird.ach";
        const string AchievementsReportedKey = "puffybird.achsent";
        const string AutoQualityKey = "puffybird.qualityauto";
        const string ReduceFlashKey = "puffybird.reduceflash";
        const string HighContrastKey = "puffybird.contrast";

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
        int _dailyDay;
        int _dailyBest;
        int _autoQuality;
        bool _reduceFlash;
        bool _highContrast;

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
            _dailyDay = PlayerPrefs.GetInt(DailyDayKey, 0);
            _dailyBest = PlayerPrefs.GetInt(DailyBestKey, 0);
            _autoQuality = PlayerPrefs.GetInt(AutoQualityKey, -1);
            _reduceFlash = PlayerPrefs.GetInt(ReduceFlashKey, 0) == 1;
            _highContrast = PlayerPrefs.GetInt(HighContrastKey, 0) == 1;
            Achievements = new AchievementTracker(PlayerPrefs.GetInt(AchievementsKey, 0), PlayerPrefs.GetInt(AchievementsReportedKey, 0));
        }

        /// <summary>Succès débloqués et envoyés. Après un changement, appeler <see cref="SaveAchievements"/>.</summary>
        public AchievementTracker Achievements { get; }

        public void SaveAchievements()
        {
            PlayerPrefs.SetInt(AchievementsKey, Achievements.Unlocked);
            PlayerPrefs.SetInt(AchievementsReportedKey, Achievements.Reported);
            PlayerPrefs.Save();
        }

        /// <summary>Meilleur score du défi du jour <paramref name="today"/> (0 si le joueur n'y a pas encore joué aujourd'hui).</summary>
        public int DailyBest(int today) => DailyChallenge.BestFor(_dailyDay, _dailyBest, today);

        /// <summary>Note le score d'une partie du défi du jour ; ne garde que le meilleur du jour.</summary>
        public void RecordDaily(int today, int score)
        {
            int current = DailyBest(today);
            if (_dailyDay == today && score <= current) return;
            _dailyDay = today;
            _dailyBest = Math.Max(score, current);
            PlayerPrefs.SetInt(DailyDayKey, _dailyDay);
            PlayerPrefs.SetInt(DailyBestKey, _dailyBest);
            PlayerPrefs.Save();
        }

        /// <summary>Qualité apprise par la mesure de fluidité (−1 : aucune) ; sert tant que le joueur n'a rien choisi.</summary>
        public int AutoQuality
        {
            get => _autoQuality;
            set => SetInt(AutoQualityKey, ref _autoQuality, value);
        }

        /// <summary>Réduire les flashs et les tremblements de caméra (accessibilité).</summary>
        public bool ReduceFlash
        {
            get => _reduceFlash;
            set => SetInt(ReduceFlashKey, ref _reduceFlash, value);
        }

        /// <summary>Tuyaux orange vif cerclés de clair, lisibles aussi pour les daltoniens (accessibilité).</summary>
        public bool HighContrast
        {
            get => _highContrast;
            set => SetInt(HighContrastKey, ref _highContrast, value);
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
