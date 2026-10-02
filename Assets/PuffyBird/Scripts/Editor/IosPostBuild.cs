#if UNITY_IOS
using System.IO;
using System.Text;
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
    /// - langues de l'app (français, anglais, espagnol, allemand, portugais) : <c>CFBundleLocalizations</c> et
    ///   texte ATT traduit dans un <c>InfoPlist.strings</c> par langue ;
    /// Les identifiants SKAdNetwork sont ajoutés par le package LevelPlay lui-même.
    /// </summary>
    static class IosPostBuild
    {
        const string TrackingUsage =
            "Your data will be used to show you more relevant ads, which keeps PuffyBird free.";

        /// <summary>Langues du jeu : code de dossier .lproj et texte de la demande de suivi (ATT) dans cette langue.</summary>
        sealed class Localization
        {
            public readonly string Code;
            public readonly string Tracking;

            public Localization(string code, string tracking)
            {
                Code = code;
                Tracking = tracking;
            }
        }

        static readonly Localization[] Localizations =
        {
            new Localization("en", TrackingUsage),
            new Localization("fr", "Vos données serviront à vous proposer des publicités plus pertinentes, ce qui permet à PuffyBird de rester gratuit."),
            new Localization("es", "Tus datos se usarán para mostrarte anuncios más relevantes, lo que mantiene PuffyBird gratis."),
            new Localization("de", "Deine Daten werden verwendet, um dir passendere Werbung zu zeigen. So bleibt PuffyBird kostenlos."),
            new Localization("pt", "Seus dados serão usados para mostrar anúncios mais relevantes, o que mantém o PuffyBird gratuito."),
        };

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
            // Langues gérées : l'App Store les affiche, et la demande de suivi s'adapte à la langue du téléphone.
            plist.root.SetString("CFBundleDevelopmentRegion", "en");
            var languages = plist.root.CreateArray("CFBundleLocalizations");
            foreach (var localization in Localizations) languages.AddString(localization.Code);
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

            // Un dossier <langue>.lproj par langue, avec la demande de suivi traduite (InfoPlist.strings).
            foreach (var localization in Localizations)
            {
                string folder = localization.Code + ".lproj";
                string folderPath = Path.Combine(path, folder);
                Directory.CreateDirectory(folderPath);
                File.WriteAllText(Path.Combine(folderPath, "InfoPlist.strings"),
                    "\"NSUserTrackingUsageDescription\" = \"" + localization.Tracking + "\";\n", new UTF8Encoding(false));
                string guid = project.AddFolderReference(folderPath, folder);
                project.AddFileToBuild(main, guid);
            }
            project.WriteToFile(projectPath);
        }
    }
}
#endif
