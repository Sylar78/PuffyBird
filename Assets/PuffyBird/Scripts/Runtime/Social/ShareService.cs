using System;
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

        /// <summary>Message de partage, en français si l'appareil l'est, sinon en anglais.</summary>
        public static string ScoreMessage(int score)
        {
            bool french = Application.systemLanguage == SystemLanguage.French;
            string points = score == 1 ? "point" : "points";
            return french
                ? $"J'ai fait {score} {points} à PuffyBird ! Tu fais mieux ?"
                : $"I scored {score} {points} in PuffyBird! Can you beat it?";
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
