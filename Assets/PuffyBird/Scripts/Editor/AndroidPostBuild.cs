#if UNITY_ANDROID
using System.IO;
using System.Xml;
using UnityEditor.Android;

namespace PuffyBird.Editor
{
    /// <summary>
    /// Complète le manifeste du projet Gradle généré : permission AD_ID, sans laquelle
    /// l'identifiant publicitaire est inaccessible à la pub LevelPlay à partir d'Android 13.
    /// </summary>
    sealed class AndroidPostBuild : IPostGenerateGradleAndroidProject
    {
        const string AndroidNs = "http://schemas.android.com/apk/res/android";
        const string AdIdPermission = "com.google.android.gms.permission.AD_ID";

        public int callbackOrder => 100;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifestPath)) return;

            var doc = new XmlDocument();
            doc.Load(manifestPath);
            var manifest = doc.DocumentElement;
            foreach (XmlNode node in manifest.SelectNodes("uses-permission"))
            {
                if (node.Attributes?["android:name"]?.Value == AdIdPermission) return;
            }

            var permission = doc.CreateElement("uses-permission");
            permission.SetAttribute("name", AndroidNs, AdIdPermission);
            manifest.PrependChild(permission);
            doc.Save(manifestPath);
        }
    }
}
#endif
