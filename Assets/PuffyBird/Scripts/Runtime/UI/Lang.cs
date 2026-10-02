using PuffyBird.Core;
using UnityEngine;

namespace PuffyBird.UI
{
    /// <summary>
    /// Langue de l'interface : celle du téléphone si le jeu la parle (français, anglais, espagnol,
    /// allemand, portugais), anglais sinon. Les libellés sont choisis au chargement
    /// (<see cref="T"/>), puis construits une seule fois.
    /// </summary>
    public static class Lang
    {
        public static readonly Language Current = From(Application.systemLanguage);

        public static Language From(SystemLanguage system)
        {
            switch (system)
            {
                case SystemLanguage.French: return Language.French;
                case SystemLanguage.Spanish: return Language.Spanish;
                case SystemLanguage.German: return Language.German;
                case SystemLanguage.Portuguese: return Language.Portuguese;
                default: return Language.English;
            }
        }

        /// <summary>Le libellé dans la langue courante, parmi les cinq traductions.</summary>
        public static string T(string french, string english, string spanish, string german, string portuguese)
            => Pick(Current, french, english, spanish, german, portuguese);

        public static string Pick(Language language, string french, string english, string spanish, string german, string portuguese)
        {
            switch (language)
            {
                case Language.French: return french;
                case Language.Spanish: return spanish;
                case Language.German: return german;
                case Language.Portuguese: return portuguese;
                default: return english;
            }
        }
    }
}
