using UnityEngine;

namespace ElementalPhenix
{
    /// <summary>
    /// Reprise des réglages enregistrés avant le renommage (clés <c>puffybird.*</c>) : l'app garde le
    /// bundle <c>fr.puffybird.app</c>, donc une mise à jour retrouve ces valeurs sur le téléphone.
    /// Chaque ancienne valeur est copiée sous sa nouvelle clé si celle-ci n'existe pas encore, avec
    /// son type ; les anciennes clés sont gardées. Appelé avant toute lecture des PlayerPrefs.
    /// </summary>
    public static class LegacyPrefs
    {
        const string OldPrefix = "puffybird.";
        const string NewPrefix = "elementalphenix.";
        const int Unreadable = int.MinValue;

        static readonly string[] IntKeys =
        {
            "best", "muted", "music", "haptics", "quality", "consent", "streak", "streakday", "streakbest",
            "dailyday", "dailybest", "ach", "achsent", "qualityauto", "reduceflash", "contrast", "journey",
        };

        static readonly string[] StringKeys = { "skin", "owned" };

        static bool _done;

        public static void Migrate()
        {
            if (_done) return;
            _done = true;
            bool changed = false;
            foreach (string key in IntKeys)
            {
                if (!Movable(key)) continue;
                int value = PlayerPrefs.GetInt(OldPrefix + key, Unreadable);
                // Valeur d'un autre type : on la laisse sous l'ancienne clé plutôt que d'écrire un 0.
                if (value == Unreadable) continue;
                PlayerPrefs.SetInt(NewPrefix + key, value);
                changed = true;
            }
            foreach (string key in StringKeys)
            {
                if (!Movable(key)) continue;
                PlayerPrefs.SetString(NewPrefix + key, PlayerPrefs.GetString(OldPrefix + key));
                changed = true;
            }
            if (changed) PlayerPrefs.Save();
        }

        static bool Movable(string key) => PlayerPrefs.HasKey(OldPrefix + key) && !PlayerPrefs.HasKey(NewPrefix + key);
    }
}
