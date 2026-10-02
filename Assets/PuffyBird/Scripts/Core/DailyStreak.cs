using System;

namespace PuffyBird.Core
{
    /// <summary>
    /// Série de jours consécutifs où le joueur lance au moins une partie. Les jours sont des numéros
    /// (jours écoulés depuis le 01/01/2000, heure locale) : la logique ne lit jamais l'horloge elle-même.
    /// </summary>
    public static class DailyStreak
    {
        static readonly DateTime Epoch = new DateTime(2000, 1, 1);

        /// <summary>Numéro du jour d'une date locale.</summary>
        public static int DayNumber(DateTime local) => (int)(local.Date - Epoch).TotalDays;

        /// <summary>
        /// Série après une partie lancée le jour <paramref name="today"/>.
        /// <paramref name="lastDay"/> vaut 0 tant que le joueur n'a jamais joué.
        /// Première partie : 1 ; même jour : inchangée ; lendemain : +1 ; jour manqué : retour à 1.
        /// Une horloge reculée (<paramref name="today"/> avant <paramref name="lastDay"/>) ne casse pas la série.
        /// </summary>
        public static int Advance(int lastDay, int streak, int today)
        {
            if (lastDay <= 0 || streak <= 0) return 1;
            if (today <= lastDay) return streak;
            return today == lastDay + 1 ? streak + 1 : 1;
        }

        /// <summary>Jour à garder en mémoire après une partie : jamais en arrière.</summary>
        public static int LastDayAfter(int lastDay, int today) => today > lastDay ? today : lastDay;

        /// <summary>Série à afficher : retombe à 0 si le joueur a laissé passer un jour entier.</summary>
        public static int Current(int lastDay, int streak, int today)
        {
            if (lastDay <= 0 || streak <= 0) return 0;
            return today - lastDay > 1 ? 0 : streak;
        }
    }
}
