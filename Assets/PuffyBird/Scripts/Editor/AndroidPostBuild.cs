#if UNITY_ANDROID
using System.IO;
using System.Xml;
using PuffyBird.Social;
using UnityEditor.Android;
using UnityEngine;

namespace PuffyBird.Editor
{
    /// <summary>
    /// Complète le manifeste du projet Gradle généré : permission AD_ID, sans laquelle
    /// l'identifiant publicitaire est inaccessible à la pub LevelPlay à partir d'Android 13,
    /// et VIBRATE pour les vibrations du jeu (<c>Feedback/Haptics</c>, appelées par JNI : Unity
    /// ne la déduit pas seul).
    /// Si Play Games est configuré (<see cref="SocialIds.PlayGamesConfigured"/>) : bibliothèque
    /// Play Games Services v2 et ID du projet dans le manifeste, pour le classement en ligne. Sans
    /// ID, rien n'est ajouté (la bibliothèque planterait au lancement sans son ID).
    /// </summary>
    sealed class AndroidPostBuild : IPostGenerateGradleAndroidProject
    {
        const string AndroidNs = "http://schemas.android.com/apk/res/android";
        static readonly string[] Permissions =
        {
            "com.google.android.gms.permission.AD_ID",
            "android.permission.VIBRATE",
        };

        const string PlayGamesLibrary = "com.google.android.gms:play-services-games-v2:22.1.0";
        const string PlayGamesAppIdKey = "com.google.android.gms.games.APP_ID";
        const string PlayGamesAppIdResource = "game_services_project_id";

        public int callbackOrder => 100;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifestPath)) return;

            var doc = new XmlDocument();
            doc.Load(manifestPath);
            var manifest = doc.DocumentElement;
            bool changed = false;
            if (SocialIds.PlayGamesConfigured) changed |= AddPlayGames(path, doc);
            foreach (string name in Permissions)
            {
                if (HasPermission(manifest, name)) continue;
                var permission = doc.CreateElement("uses-permission");
                permission.SetAttribute("name", AndroidNs, name);
                manifest.PrependChild(permission);
                changed = true;
            }
            if (!changed) return;
            doc.Save(manifestPath);
        }

        static bool AddPlayGames(string path, XmlDocument doc)
        {
            // ID numérique : en ressource texte, sinon Android le lirait comme un nombre.
            string values = Path.Combine(path, "src", "main", "res", "values");
            Directory.CreateDirectory(values);
            File.WriteAllText(Path.Combine(values, "puffybird_games.xml"),
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<resources>\n" +
                $"    <string name=\"{PlayGamesAppIdResource}\" translatable=\"false\">{SocialIds.PlayGamesAppId}</string>\n" +
                "</resources>\n");

            string gradle = Path.Combine(path, "build.gradle");
            string gradleKts = gradle + ".kts";
            if (File.Exists(gradleKts)) AppendDependency(gradleKts, $"    implementation(\"{PlayGamesLibrary}\")");
            else if (File.Exists(gradle)) AppendDependency(gradle, $"    implementation '{PlayGamesLibrary}'");
            else Debug.LogWarning("PuffyBird : build.gradle introuvable, Play Games non ajouté.");

            var application = doc.DocumentElement["application"];
            if (application == null) return false;
            foreach (XmlNode node in application.SelectNodes("meta-data"))
            {
                if (node.Attributes?["android:name"]?.Value == PlayGamesAppIdKey) return false;
            }
            var meta = doc.CreateElement("meta-data");
            meta.SetAttribute("name", AndroidNs, PlayGamesAppIdKey);
            meta.SetAttribute("value", AndroidNs, "@string/" + PlayGamesAppIdResource);
            application.AppendChild(meta);
            return true;
        }

        static void AppendDependency(string gradleFile, string line)
        {
            string text = File.ReadAllText(gradleFile);
            if (text.Contains("play-services-games-v2")) return;
            File.AppendAllText(gradleFile, "\n// PuffyBird : classement Play Games (AndroidPostBuild).\ndependencies {\n" + line + "\n}\n");
        }

        static bool HasPermission(XmlElement manifest, string name)
        {
            foreach (XmlNode node in manifest.SelectNodes("uses-permission"))
            {
                if (node.Attributes?["android:name"]?.Value == name) return true;
            }
            return false;
        }
    }
}
#endif
