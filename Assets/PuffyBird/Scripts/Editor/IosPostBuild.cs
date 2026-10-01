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
    /// - chargements HTTP autorisés : certaines créations publicitaires ne sont pas en HTTPS ;
    /// - Game Center (classement, <c>Plugins/iOS/GameCenterBridge.mm</c>) : framework GameKit et
    ///   droit <c>com.apple.developer.game-center</c>. Volontairement sans
    ///   <c>ProjectCapabilityManager.AddGameCenter</c>, qui ajouterait « gamekit » aux capacités
    ///   requises de l'appareil, interdit d'ajout dans une mise à jour sur l'App Store.
    /// Les identifiants SKAdNetwork sont ajoutés par le package LevelPlay lui-même.
    /// </summary>
    static class IosPostBuild
    {
        const string TrackingUsage =
            "Your data will be used to show you more relevant ads, which keeps PuffyBird free.";

        const string EntitlementsFile = "Unity-iPhone/PuffyBird.entitlements";

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
            string framework = project.GetUnityFrameworkTargetGuid();
            project.AddFrameworkToProject(framework, "AppTrackingTransparency.framework", true);
            project.AddFrameworkToProject(framework, "GameKit.framework", false);

            var entitlements = new PlistDocument();
            string entitlementsPath = Path.Combine(path, EntitlementsFile);
            if (File.Exists(entitlementsPath)) entitlements.ReadFromFile(entitlementsPath);
            entitlements.root.SetBoolean("com.apple.developer.game-center", true);
            entitlements.WriteToFile(entitlementsPath);
            string main = project.GetUnityMainTargetGuid();
            if (project.FindFileGuidByProjectPath(EntitlementsFile) == null) project.AddFile(EntitlementsFile, EntitlementsFile);
            project.SetBuildProperty(main, "CODE_SIGN_ENTITLEMENTS", EntitlementsFile);
            project.WriteToFile(projectPath);
        }
    }
}
#endif
