using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PuffyBird.Editor
{
    /// <summary>
    /// Builds mobiles, depuis le menu ou en ligne de commande :
    ///   Unity -batchmode -quit -projectPath . -executeMethod PuffyBird.Editor.BuildScript.BuildAndroid
    ///   Unity -batchmode -quit -projectPath . -executeMethod PuffyBird.Editor.BuildScript.BuildIOS
    /// Options : « -release » pour un bundle Android (.aab) destiné au Play Store,
    /// « -buildNumber N » (numéro de build, unique par envoi sur les stores),
    /// « -appleTeamId ID » (équipe Apple pour la signature automatique, ou variable APPLE_TEAM_ID),
    /// « -customBuildPath chemin » (dossier de sortie, fourni par GameCI dans GitHub Actions).
    /// Signature Android (fournie par GameCI, voir docs/publication-android-google-play.md) :
    /// « -androidKeystoreName », « -androidKeystorePass », « -androidKeyaliasName »,
    /// « -androidKeyaliasPass » ; « -androidExportType androidAppBundle » équivaut à « -release ».
    /// </summary>
    public static class BuildScript
    {
        [MenuItem("PuffyBird/Build Android (APK)", priority = 20)]
        public static void BuildAndroid()
        {
            bool release = HasArg("-release") || Arg("-androidExportType") == "androidAppBundle";
            EditorUserBuildSettings.buildAppBundle = release;
            string path = release ? "Builds/Android/PuffyBird.aab" : "Builds/Android/PuffyBird.apk";
            Build(BuildTarget.Android, Arg("-customBuildPath") ?? path);
        }

        /// <summary>Génère le projet Xcode ; la signature et l'envoi se font ensuite sur un Mac.</summary>
        [MenuItem("PuffyBird/Build iOS (projet Xcode)", priority = 21)]
        public static void BuildIOS()
        {
            Build(BuildTarget.iOS, Arg("-customBuildPath") ?? "Builds/iOS");
        }

        static void Build(BuildTarget target, string path)
        {
            ProjectSetup.Configure(openScene: false);
            ApplyCommandLineOverrides();
            PackageDefines.Sync();
            var defines = PackageDefines.InstalledSymbols();
            bool purchasing = System.Array.IndexOf(defines, PackageDefines.PurchasingSymbol) >= 0;
            bool analytics = System.Array.IndexOf(defines, PackageDefines.AnalyticsSymbol) >= 0;
            Debug.Log(purchasing
                ? "PuffyBird : achats intégrés activés (package Unity IAP présent)."
                : "PuffyBird : package Unity IAP absent, build sans boutique.");
            Debug.Log(analytics
                ? "PuffyBird : mesure d'audience activée (package Unity Analytics présent)."
                : "PuffyBird : package Unity Analytics absent, build sans mesure d'audience.");

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.ScenePath },
                locationPathName = path,
                target = target,
                options = BuildOptions.None,
                extraScriptingDefines = defines.Length > 0 ? defines : null,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"PuffyBird : build {target} réussi ({summary.totalSize / (1024 * 1024)} Mo) dans {path}");
                return;
            }
            Debug.LogError($"PuffyBird : build {target} échoué ({summary.totalErrors} erreurs)");
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }

        static void ApplyCommandLineOverrides()
        {
            string buildNumber = Arg("-buildNumber");
            if (int.TryParse(buildNumber, out int number) && number > 0)
            {
                PlayerSettings.iOS.buildNumber = number.ToString();
                PlayerSettings.Android.bundleVersionCode = number;
            }

            string team = Arg("-appleTeamId") ?? Environment.GetEnvironmentVariable("APPLE_TEAM_ID");
            if (!string.IsNullOrEmpty(team))
            {
                PlayerSettings.iOS.appleDeveloperTeamID = team;
                PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            }

            // Clé d'envoi Google Play. Sans elle, l'APK reste signé avec la clé de débogage,
            // que Google Play refuse : une clé demandée mais introuvable fait échouer le build.
            string keystore = Arg("-androidKeystoreName");
            if (!string.IsNullOrEmpty(keystore))
            {
                if (!File.Exists(keystore))
                    throw new Exception($"PuffyBird : clé d'envoi introuvable ({Path.GetFullPath(keystore)}).");
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = Path.GetFullPath(keystore);
                PlayerSettings.Android.keystorePass = RawArg("-androidKeystorePass");
                PlayerSettings.Android.keyaliasName = Arg("-androidKeyaliasName");
                PlayerSettings.Android.keyaliasPass = RawArg("-androidKeyaliasPass");
                Debug.Log($"PuffyBird : signature avec la clé d'envoi {keystore} (alias {PlayerSettings.Android.keyaliasName}).");
            }
        }

        static bool HasArg(string name) => Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length && !args[i + 1].StartsWith("-") ? args[i + 1] : null;
        }

        // Valeur brute, même si elle commence par « - » (mot de passe).
        static string RawArg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
