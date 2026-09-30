#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace PuffyBird.Editor
{
    /// <summary>
    /// Complète le projet Xcode généré :
    /// - chiffrement exempté (HTTPS standard seulement) : évite la question « conformité export »
    ///   à chaque build TestFlight ;
    /// - texte de la demande de suivi (ATT) exigé par Apple pour la pub LevelPlay, et lien faible
    ///   vers AppTrackingTransparency pour le pont natif <c>Plugins/iOS/ATTRequester.mm</c> ;
    /// - chargements HTTP autorisés : certaines créations publicitaires ne sont pas en HTTPS.
    /// Les identifiants SKAdNetwork sont ajoutés par le package LevelPlay lui-même.
    /// </summary>
    static class IosPostBuild
    {
        const string TrackingUsage =
            "Your data will be used to show you more relevant ads, which keeps PuffyBird free.";

        [PostProcessBuild(100)]
        static void OnPostProcessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;

            string plistPath = Path.Combine(path, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
            plist.root.SetString("NSUserTrackingUsageDescription", TrackingUsage);
            var ats = plist.root["NSAppTransportSecurity"]?.AsDict() ?? plist.root.CreateDict("NSAppTransportSecurity");
            ats.SetBoolean("NSAllowsArbitraryLoads", true);
            plist.WriteToFile(plistPath);

            string projectPath = PBXProject.GetPBXProjectPath(path);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);
            project.AddFrameworkToProject(project.GetUnityFrameworkTargetGuid(), "AppTrackingTransparency.framework", true);
            project.WriteToFile(projectPath);
        }
    }
}
#endif
