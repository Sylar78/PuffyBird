using System;

namespace PuffyBird.Core
{
    /// <summary>Succès du jeu (Game Center et Play Games). L'ordre fixe le bit de chacun : ne jamais réordonner.</summary>
    public enum Achievement
    {
        Score10,
        Score20,
        Score30,
        Score40,
        FirstStar,
        CloseCalls,
        Streak7,
        Streak30,
        DailyChallenge,
    }

    /// <summary>
    /// Succès débloqués, gardés en mémoire (un bit chacun) avec ceux déjà envoyés à la plateforme :
    /// un succès gagné hors connexion est envoyé plus tard, jamais deux fois.
    /// </summary>
    public sealed class AchievementTracker
    {
        public static readonly int Count = Enum.GetValues(typeof(Achievement)).Length;

        /// <summary>Frôlements dans une même partie pour le succès <see cref="Achievement.CloseCalls"/>.</summary>
        public const int CloseCallsRequired = 5;

        public AchievementTracker(int unlocked = 0, int reported = 0)
        {
            Unlocked = unlocked;
            Reported = reported;
        }

        /// <summary>Masque des succès gagnés.</summary>
        public int Unlocked { get; private set; }

        /// <summary>Masque des succès déjà envoyés à la plateforme.</summary>
        public int Reported { get; private set; }

        public bool IsUnlocked(Achievement a) => (Unlocked & Bit(a)) != 0;

        /// <summary>Débloque ; vrai seulement la première fois.</summary>
        public bool Unlock(Achievement a)
        {
            if (IsUnlocked(a)) return false;
            Unlocked |= Bit(a);
            return true;
        }

        /// <summary>Succès gagné mais pas encore envoyé, ou null s'il n'y en a plus.</summary>
        public Achievement? NextToReport()
        {
            int pending = Unlocked & ~Reported;
            if (pending == 0) return null;
            for (int i = 0; i < Count; i++)
            {
                if ((pending & (1 << i)) != 0) return (Achievement)i;
            }
            return null;
        }

        public void MarkReported(Achievement a) => Reported |= Bit(a);

        /// <summary>Succès de score franchi par <paramref name="score"/> (le plus haut seuil atteint ne dispense pas des autres).</summary>
        public void UnlockForScore(int score, GameConfig cfg)
        {
            if (score >= cfg.MedalBronze) Unlock(Achievement.Score10);
            if (score >= cfg.MedalSilver) Unlock(Achievement.Score20);
            if (score >= cfg.MedalGold) Unlock(Achievement.Score30);
            if (score >= cfg.MedalPlatinum) Unlock(Achievement.Score40);
        }

        /// <summary>Succès de série pour une série de <paramref name="days"/> jours consécutifs.</summary>
        public void UnlockForStreak(int days, GameConfig cfg)
        {
            if (days >= 7) Unlock(Achievement.Streak7);
            if (days >= cfg.StreakDaysForSkin) Unlock(Achievement.Streak30);
        }

        /// <summary>Clé stable du succès, base des identifiants Game Center (<c>puffybird.ach.&lt;clé&gt;</c>).</summary>
        public static string Key(Achievement a)
        {
            switch (a)
            {
                case Achievement.Score10: return "score10";
                case Achievement.Score20: return "score20";
                case Achievement.Score30: return "score30";
                case Achievement.Score40: return "score40";
                case Achievement.FirstStar: return "star";
                case Achievement.CloseCalls: return "closecalls";
                case Achievement.Streak7: return "streak7";
                case Achievement.Streak30: return "streak30";
                default: return "daily";
            }
        }

        static int Bit(Achievement a) => 1 << (int)a;
    }
}
