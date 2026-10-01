#if UNITY_ANDROID
using System.IO;
using System.Xml;
using UnityEditor.Android;

namespace PuffyBird.Editor
{
    /// <summary>
    /// Complète le manifeste du projet Gradle généré : permission AD_ID, sans laquelle
    /// l'identifiant publicitaire est inaccessible à la pub LevelPlay à partir d'Android 13,
    /// et VIBRATE pour les vibrations du jeu (<c>Feedback/Haptics</c>, appelées par JNI : Unity
    /// ne la déduit pas seul).
    /// </summary>
    sealed class AndroidPostBuild : IPostGenerateGradleAndroidProject
    {
        const string AndroidNs = "http://schemas.android.com/apk/res/android";
        static readonly string[] Permissions =
        {
            "com.google.android.gms.permission.AD_ID",
            "android.permission.VIBRATE",
        };

        public int callbackOrder => 100;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifestPath)) return;

            var doc = new XmlDocument();
            doc.Load(manifestPath);
            var manifest = doc.DocumentElement;
            bool changed = false;
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
