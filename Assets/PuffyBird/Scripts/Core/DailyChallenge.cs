namespace PuffyBird.Core
{
    /// <summary>
    /// Défi du jour : tous les joueurs reçoivent la même graine le même jour (numéro de jour de
    /// <see cref="DailyStreak.DayNumber"/>), donc les mêmes tuyaux, étoiles et décor.
    /// </summary>
    public static class DailyChallenge
    {
        /// <summary>Graine du jour : brassage du numéro de jour, jamais 0.</summary>
        public static uint SeedForDay(int day)
        {
            uint x = (uint)day * 0x9E3779B1u + 0x85EBCA6Bu;
            x ^= x >> 16;
            x *= 0x7FEB352Du;
            x ^= x >> 15;
            x *= 0x846CA68Bu;
            x ^= x >> 16;
            return x == 0 ? 1u : x;
        }

        /// <summary>Meilleur score du jour : celui qui est gardé s'il date d'aujourd'hui, sinon 0.</summary>
        public static int BestFor(int storedDay, int storedBest, int today) => storedDay == today ? storedBest : 0;
    }
}
