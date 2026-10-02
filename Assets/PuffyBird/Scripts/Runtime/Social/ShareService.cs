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

        public static void ShareScore(int score)
        {
            string text = ScoreMessage(score);
            try
            {
#if UNITY_EDITOR
                GUIUtility.systemCopyBuffer = $"{text} {SocialIds.ShareUrl}";
                Debug.Log($"PuffyBird : partage (copié dans le presse-papiers) : {text} {SocialIds.ShareUrl}");
#elif UNITY_IOS
                _Share_Text(text, SocialIds.ShareUrl);
#elif UNITY_ANDROID
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.SEND"))
                {
                    intent.Call<AndroidJavaObject>("setType", "text/plain").Dispose();
                    intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.TEXT", $"{text} {SocialIds.ShareUrl}").Dispose();
                    using (var intentClass = new AndroidJavaClass("android.content.Intent"))
                    using (var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, "PuffyBird"))
                    {
                        activity.Call("startActivity", chooser);
                    }
                }
#endif
            }
            catch (Exception e)
            {
                Debug.LogWarning($"PuffyBird : partage impossible ({e.Message}).");
            }
        }
    }
}
