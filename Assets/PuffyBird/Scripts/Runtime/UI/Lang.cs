using UnityEngine;

namespace PuffyBird.UI
{
    /// <summary>
    /// Langue de l'interface : français si le téléphone est en français, anglais sinon. Les
    /// libellés sont choisis au chargement (<see cref="T"/>), puis construits une seule fois.
    /// </summary>
    public static class Lang
    {
        public static readonly bool French = Application.systemLanguage == SystemLanguage.French;

        public static string T(string french, string english) => French ? french : english;
    }
}
