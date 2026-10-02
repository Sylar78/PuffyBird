using System;
using PuffyBird.Core;
using PuffyBird.UI;
using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace PuffyBird.Social
{
    /// <summary>
    /// « Partager mon score » : feuille de partage du système (iOS : <c>Plugins/iOS/ShareBridge.mm</c>,
    /// Android : intent ACTION_SEND). Dans l'éditeur, le texte est copié dans le presse-papiers.
    /// </summary>
    public static class ShareService
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void _Share_Text(string text, string url);
        [DllImport("__Internal")] static extern void _Share_Image(string text, string url, string imagePath);
#endif

        /// <summary>Message de partage, dans la langue de l'interface (anglais si elle n'est pas gérée).</summary>
        public static string ScoreMessage(int score) => ScoreMessage(score, Lang.Current);

        public static string ScoreMessage(int score, Language language)
        {
            bool one = score == 1;
            switch (language)
            {
                case Language.French:
                    return $"J'ai fait {score} {(one ? "point" : "points")} à PuffyBird ! Tu fais mieux ?";
                case Language.Spanish:
                    return $"¡Conseguí {score} {(one ? "punto" : "puntos")} en PuffyBird! ¿Puedes superarlo?";
                case Language.German:
                    return $"Ich habe {score} {(one ? "Punkt" : "Punkte")} bei PuffyBird! Schaffst du mehr?";
                case Language.Portuguese:
                    return $"Fiz {score} {(one ? "ponto" : "pontos")} no PuffyBird! Será que você consegue mais?";
                default:
                    return $"I scored {score} {(one ? "point" : "points")} in PuffyBird! Can you beat it?";
            }
        }

        /// <summary>Partage le score avec la capture de l'écran de fin (<paramref name="image"/>, JPEG) si elle existe, sinon le texte seul.</summary>
        public static void ShareScore(int score, byte[] image = null)
        {
            string text = ScoreMessage(score);
            try
            {
#if UNITY_EDITOR
                GUIUtility.systemCopyBuffer = $"{text} {SocialIds.ShareUrl}";
                string saved = "";
                if (image != null)
                {
                    System.IO.Directory.CreateDirectory("Captures");
                    saved = "Captures/PuffyBird-partage.jpg";
                    System.IO.File.WriteAllBytes(saved, image);
                }
                Debug.Log($"PuffyBird : partage (texte copié dans le presse-papiers{(saved.Length > 0 ? ", image dans " + saved : "")}) : {text} {SocialIds.ShareUrl}");
#elif UNITY_IOS
                if (image != null)
                {
                    string path = System.IO.Path.Combine(Application.temporaryCachePath, "puffybird-score.jpg");
                    System.IO.File.WriteAllBytes(path, image);
                    _Share_Image(text, SocialIds.ShareUrl, path);
                }
                else
                {
                    _Share_Text(text, SocialIds.ShareUrl);
                }
#elif UNITY_ANDROID
                if (image == null || !ShareImageAndroid($"{text} {SocialIds.ShareUrl}", image))
                    ShareTextAndroid($"{text} {SocialIds.ShareUrl}");
#endif
            }
            catch (Exception e)
            {
                Debug.LogWarning($"PuffyBird : partage impossible ({e.Message}).");
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static void ShareTextAndroid(string text)
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.SEND"))
            {
                intent.Call<AndroidJavaObject>("setType", "text/plain").Dispose();
                intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.TEXT", text).Dispose();
                StartChooser(activity, intent);
            }
        }

        /// <summary>
        /// Partage la capture : l'image est ajoutée à la galerie (MediaStore, dossier Pictures/PuffyBird,
        /// sans permission à partir d'Android 10) puis envoyée par son adresse. Faux si impossible (Android 9 ou moins).
        /// </summary>
        static bool ShareImageAndroid(string text, byte[] jpeg)
        {
            using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
            {
                if (version.GetStatic<int>("SDK_INT") < 29) return false;
            }
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var resolver = activity.Call<AndroidJavaObject>("getContentResolver"))
            using (var values = new AndroidJavaObject("android.content.ContentValues"))
            using (var media = new AndroidJavaClass("android.provider.MediaStore$Images$Media"))
            using (var collection = media.GetStatic<AndroidJavaObject>("EXTERNAL_CONTENT_URI"))
            {
                values.Call("put", "_display_name", "PuffyBird-score.jpg");
                values.Call("put", "mime_type", "image/jpeg");
                values.Call("put", "relative_path", "Pictures/PuffyBird");
                using (var item = resolver.Call<AndroidJavaObject>("insert", collection, values))
                {
                    if (item == null) return false;
                    using (var stream = resolver.Call<AndroidJavaObject>("openOutputStream", item))
                    {
                        stream.Call("write", jpeg);
                        stream.Call("close");
                    }
                    using (var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.SEND"))
                    {
                        intent.Call<AndroidJavaObject>("setType", "image/jpeg").Dispose();
                        intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.STREAM", item).Dispose();
                        intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.TEXT", text).Dispose();
                        intent.Call<AndroidJavaObject>("addFlags", 1).Dispose(); // FLAG_GRANT_READ_URI_PERMISSION
                        StartChooser(activity, intent);
                    }
                }
            }
            return true;
        }

        static void StartChooser(AndroidJavaObject activity, AndroidJavaObject intent)
        {
            using (var intentClass = new AndroidJavaClass("android.content.Intent"))
            using (var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, "PuffyBird"))
            {
                activity.Call("startActivity", chooser);
            }
        }
#endif
    }
}
