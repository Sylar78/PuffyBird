#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace PuffyBird.Editor
{
    /// <summary>
    /// Complète le projet Xcode généré. Le jeu n'utilise aucun chiffrement (pas de réseau) :
    /// le déclarer dans Info.plist évite la question « conformité export » à chaque build TestFlight.
    /// </summary>
    static class IosPostBuild
    {
        [PostProcessBuild(100)]
        static void OnPostProcessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            string plistPath = Path.Combine(path, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
            plist.WriteToFile(plistPath);
        }
    }
}
#endif
