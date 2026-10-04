using System.IO;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEngine;

namespace PuffyBird.Editor
{
    /// <summary>
    /// Compile les scripts du jeu comme pour un build, sans le faire, avec les symboles des packages
    /// installés (<see cref="PackageDefines"/>) : le code de la boutique et de la mesure d'audience
    /// est donc compilé dès que son package est présent. Utilisé par le workflow « Compilation Unity » :
    ///   Unity -batchmode -quit -projectPath . -executeMethod PuffyBird.Editor.CompileCheck.Run
    /// </summary>
    public static class CompileCheck
    {
        const string OutputFolder = "Temp/PuffyBirdCompileCheck";

        [MenuItem("PuffyBird/Vérifier la compilation (Android)", priority = 30)]
        public static void Run()
        {
            var defines = PackageDefines.InstalledSymbols();
            Debug.Log("PuffyBird : compilation Android avec les symboles [" + string.Join(", ", defines) + "].");

            var settings = new ScriptCompilationSettings
            {
                target = BuildTarget.Android,
                group = BuildTargetGroup.Android,
                options = ScriptCompilationOptions.None,
                extraScriptingDefines = defines,
            };
            Directory.CreateDirectory(OutputFolder);
            var result = PlayerBuildInterface.CompilePlayerScripts(settings, OutputFolder);

            bool ok = result.assemblies != null && result.assemblies.Count > 0
                && result.assemblies.Contains("Assembly-CSharp.dll");
            if (ok)
            {
                Debug.Log($"PuffyBird : compilation réussie ({result.assemblies.Count} assemblies).");
                if (Application.isBatchMode) EditorApplication.Exit(0);
                return;
            }
            Debug.LogError("PuffyBird : compilation échouée (erreurs ci-dessus).");
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
