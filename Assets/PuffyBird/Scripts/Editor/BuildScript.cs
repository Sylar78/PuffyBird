using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PuffyBird.Editor
{
    /// <summary>
    /// Builds mobiles, depuis le menu ou en ligne de commande :
    ///   Unity -batchmode -quit -projectPath . -executeMethod PuffyBird.Editor.BuildScript.BuildAndroid
    ///   Unity -batchmode -quit -projectPath . -executeMethod PuffyBird.Editor.BuildScript.BuildIOS
    /// Ajouter « -release » pour un bundle Android (.aab) destiné au Play Store.
    /// </summary>
    public static class BuildScript
    {
        [MenuItem("PuffyBird/Build Android (APK)", priority = 20)]
        public static void BuildAndroid()
        {
            bool release = Environment.GetCommandLineArgs().Contains("-release");
            EditorUserBuildSettings.buildAppBundle = release;
            string path = release ? "Builds/Android/PuffyBird.aab" : "Builds/Android/PuffyBird.apk";
            Build(BuildTarget.Android, path);
        }

        /// <summary>Génère le projet Xcode ; la signature et l'envoi se font ensuite dans Xcode, sur un Mac.</summary>
        [MenuItem("PuffyBird/Build iOS (projet Xcode)", priority = 21)]
        public static void BuildIOS()
        {
            Build(BuildTarget.iOS, "Builds/iOS");
        }

        static void Build(BuildTarget target, string path)
        {
            ProjectSetup.Configure(openScene: false);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.ScenePath },
                locationPathName = path,
                target = target,
                options = BuildOptions.None,
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
    }
}
