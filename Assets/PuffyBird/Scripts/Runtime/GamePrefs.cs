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

        /// <summary>Consentement aux pubs personnalisées pas encore demandé.</summary>
        public const int ConsentUnknown = -1;
        public const int ConsentRefused = 0;
        public const int ConsentGranted = 1;

        bool _haptics;
        bool _music;
        int _quality;
        int _consent;
        string _skin;

        public GamePrefs()
        {
            _haptics = PlayerPrefs.GetInt(HapticsKey, 1) == 1;
            _music = PlayerPrefs.GetInt(MusicKey, 1) == 1;
            _quality = PlayerPrefs.GetInt(QualityKey, -1);
            _consent = PlayerPrefs.GetInt(ConsentKey, ConsentUnknown);
            _skin = PlayerPrefs.GetString(SkinKey, "");
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
