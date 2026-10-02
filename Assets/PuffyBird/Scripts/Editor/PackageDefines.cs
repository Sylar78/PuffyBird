using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;

namespace PuffyBird.Editor
{
    /// <summary>
    /// Active le code qui dépend d'un package facultatif seulement quand ce package est installé :
    /// sans lui, le jeu compile et tourne sans la fonction. Achats intégrés (<c>Monetization/UnityIapStore.cs</c>,
    /// package « In App Purchasing » <c>com.unity.purchasing</c> 5.x) et mesure d'audience
    /// (<c>Monetization/UnityAnalyticsMetrics.cs</c>, package « Analytics » <c>com.unity.services.analytics</c> 6.x).
    /// </summary>
    [InitializeOnLoad]
    public static class PackageDefines
    {
        public const string PurchasingSymbol = "PUFFYBIRD_IAP";
        public const string AnalyticsSymbol = "PUFFYBIRD_ANALYTICS";

        /// <summary>Symbole et type d'entrée du package qui le déclenche.</summary>
        static readonly (string Symbol, string Type)[] Packages =
        {
            (PurchasingSymbol, "UnityEngine.Purchasing.UnityIAPServices"),
            (AnalyticsSymbol, "Unity.Services.Analytics.AnalyticsService"),
        };

        static readonly NamedBuildTarget[] Targets = { NamedBuildTarget.Android, NamedBuildTarget.iOS, NamedBuildTarget.Standalone };

        static PackageDefines()
        {
            EditorApplication.delayCall += Sync;
        }

        /// <summary>Symboles des packages chargés dans l'éditeur.</summary>
        public static string[] InstalledSymbols()
        {
            var symbols = new List<string>();
            foreach (var (symbol, type) in Packages)
            {
                if (TypeLoaded(type)) symbols.Add(symbol);
            }
            return symbols.ToArray();
        }

        static bool TypeLoaded(string typeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetType(typeName, false) != null) return true;
            }
            return false;
        }

        /// <summary>Ajoute ou retire chaque symbole selon la présence de son package, pour chaque plateforme.</summary>
        public static void Sync()
        {
            var installed = new HashSet<string>(InstalledSymbols());
            foreach (var target in Targets)
            {
                string defines = PlayerSettings.GetScriptingDefineSymbols(target);
                var list = new List<string>(defines.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
                bool changed = false;
                foreach (var (symbol, _) in Packages)
                {
                    bool want = installed.Contains(symbol);
                    if (list.Contains(symbol) == want) continue;
                    if (want) list.Add(symbol);
                    else list.Remove(symbol);
                    changed = true;
                }
                if (changed) PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", list));
            }
        }
    }
}
