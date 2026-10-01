using System;
using UnityEditor;
using UnityEditor.Build;

namespace PuffyBird.Editor
{
    /// <summary>
    /// Active le code des achats intégrés (<c>Monetization/UnityIapStore.cs</c>, compilé sous
    /// <see cref="Symbol"/>) seulement quand le package Unity IAP (« In App Purchasing »,
    /// <c>com.unity.purchasing</c> 5.x) est installé : sans lui, le jeu compile et tourne sans boutique.
    /// </summary>
    [InitializeOnLoad]
    public static class PurchasingDefine
    {
        public const string Symbol = "PUFFYBIRD_IAP";

        static readonly NamedBuildTarget[] Targets = { NamedBuildTarget.Android, NamedBuildTarget.iOS, NamedBuildTarget.Standalone };

        static PurchasingDefine()
        {
            EditorApplication.delayCall += Sync;
        }

        /// <summary>Le package est chargé dans l'éditeur (type d'entrée de l'API v5 présent).</summary>
        public static bool PackageInstalled
        {
            get
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetType("UnityEngine.Purchasing.UnityIAPServices", false) != null) return true;
                }
                return false;
            }
        }

        /// <summary>Ajoute ou retire le symbole selon la présence du package, pour chaque plateforme.</summary>
        public static void Sync()
        {
            bool installed = PackageInstalled;
            foreach (var target in Targets)
            {
                string defines = PlayerSettings.GetScriptingDefineSymbols(target);
                var list = new System.Collections.Generic.List<string>(defines.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
                bool present = list.Contains(Symbol);
                if (present == installed) continue;
                if (installed) list.Add(Symbol);
                else list.Remove(Symbol);
                PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", list));
            }
        }
    }
}
