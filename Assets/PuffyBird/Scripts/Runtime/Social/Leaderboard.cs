using System;
using PuffyBird.Core;
using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace PuffyBird.Social
{
    /// <summary>
    /// Classement en ligne du meilleur score : Game Center sur iOS
    /// (<c>Plugins/iOS/GameCenterBridge.mm</c>), Play Games Services v2 sur Android (appelé par
    /// JNI, bibliothèque ajoutée au build par <c>AndroidPostBuild</c> si
    /// <see cref="SocialIds.PlayGamesConfigured"/>). La connexion se fait au lancement, sans
    /// fenêtre : si le joueur doit se connecter, la fenêtre ne s'ouvre qu'au tap sur le trophée.
    /// </summary>
    public sealed class Leaderboard
    {
        readonly IBackend _backend;
        int _submitted;
        int _pending;

        public Leaderboard()
        {
            try
            {
#if UNITY_EDITOR
                _backend = new EditorBackend();
#elif UNITY_IOS
                _backend = new GameCenterBackend();
#elif UNITY_ANDROID
                if (SocialIds.PlayGamesConfigured) _backend = new PlayGamesBackend();
#endif
            }
            catch (Exception e)
            {
                Debug.LogWarning($"PuffyBird : classement indisponible ({e.Message}).");
                _backend = null;
            }
        }

        /// <summary>Le bouton du classement a un sens sur cette plateforme.</summary>
        public bool Available => _backend != null;

        /// <summary>
        /// Envoie le meilleur score s'il est plus haut que le dernier envoyé ; attend la connexion
        /// du joueur si besoin (<see cref="Update"/>).
        /// </summary>
        public void SubmitBest(int best)
        {
            if (_backend == null || best <= _submitted || best <= 0) return;
            _pending = best;
            Update();
        }

        /// <summary>À appeler à chaque image : envoie le score en attente dès que le joueur est connecté.</summary>
        public void Update()
        {
            if (_pending <= _submitted || !_backend.SignedIn) return;
            try
            {
                _backend.Submit(_pending);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"PuffyBird : envoi du score impossible ({e.Message}).");
            }
            _submitted = _pending;
        }

        /// <summary>
        /// Envoie un succès gagné. Vrai si la plateforme l'a reçu (ou n'a pas de succès à lui donner) ;
        /// faux si le joueur n'est pas encore connecté : à réessayer plus tard.
        /// </summary>
        public bool Unlock(Achievement achievement)
        {
            if (_backend == null) return true;
            if (!_backend.SignedIn) return false;
            try
            {
                _backend.Unlock(achievement);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"PuffyBird : succès impossible à envoyer ({e.Message}).");
            }
            return true;
        }

        /// <summary>Ouvre le classement, ou la connexion si le joueur n'est pas connecté.</summary>
        public void Show()
        {
            try
            {
                _backend?.Show();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"PuffyBird : classement indisponible ({e.Message}).");
            }
        }

        interface IBackend
        {
            bool SignedIn { get; }
            void Submit(int score);
            void Unlock(Achievement achievement);
            void Show();
        }

#if UNITY_EDITOR
        sealed class EditorBackend : IBackend
        {
            public bool SignedIn => true;
            public void Submit(int score) => Debug.Log($"PuffyBird : score {score} envoyé au classement (simulation éditeur).");
            public void Unlock(Achievement achievement) => Debug.Log($"PuffyBird : succès « {AchievementTracker.Key(achievement)} » débloqué (simulation éditeur).");
            public void Show() => Debug.Log("PuffyBird : ouverture du classement (Game Center ou Play Games sur l'appareil).");
        }
#endif

#if UNITY_IOS && !UNITY_EDITOR
        sealed class GameCenterBackend : IBackend
        {
            [DllImport("__Internal")] static extern void _GC_Authenticate();
            [DllImport("__Internal")] static extern bool _GC_IsAuthenticated();
            [DllImport("__Internal")] static extern void _GC_Submit(string leaderboard, long score);
            [DllImport("__Internal")] static extern void _GC_Unlock(string achievement);
            [DllImport("__Internal")] static extern void _GC_Show(string leaderboard);

            public GameCenterBackend() => _GC_Authenticate();

            public bool SignedIn => _GC_IsAuthenticated();
            public void Submit(int score) => _GC_Submit(SocialIds.GameCenterLeaderboard, score);
            public void Unlock(Achievement achievement) => _GC_Unlock(SocialIds.GameCenterAchievement(achievement));
            public void Show() => _GC_Show(SocialIds.GameCenterLeaderboard);
        }
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>Play Games Services v2 : connexion automatique au lancement, classement par intent.</summary>
        sealed class PlayGamesBackend : IBackend
        {
            const int RequestCode = 9004;

            readonly AndroidJavaObject _activity;
            readonly AndroidJavaClass _playGames;
            volatile bool _signedIn;

            public PlayGamesBackend()
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    _activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                }
                using (var sdk = new AndroidJavaClass("com.google.android.gms.games.PlayGamesSdk"))
                {
                    sdk.CallStatic("initialize", _activity);
                }
                _playGames = new AndroidJavaClass("com.google.android.gms.games.PlayGames");
                using (var client = _playGames.CallStatic<AndroidJavaObject>("getGamesSignInClient", _activity))
                using (var task = client.Call<AndroidJavaObject>("isAuthenticated"))
                {
                    task.Call<AndroidJavaObject>("addOnCompleteListener", new OnComplete(OnAuthenticated, null)).Dispose();
                }
            }

            public bool SignedIn => _signedIn;

            public void Submit(int score)
            {
                using (var client = _playGames.CallStatic<AndroidJavaObject>("getLeaderboardsClient", _activity))
                {
                    client.Call("submitScore", SocialIds.PlayGamesLeaderboard, (long)score);
                }
            }

            public void Unlock(Achievement achievement)
            {
                string id = SocialIds.PlayGamesAchievement(achievement);
                if (string.IsNullOrEmpty(id)) return;
                using (var client = _playGames.CallStatic<AndroidJavaObject>("getAchievementsClient", _activity))
                {
                    client.Call("unlock", id);
                }
            }

            public void Show()
            {
                if (_signedIn)
                {
                    OpenLeaderboard();
                    return;
                }
                // Connexion à la demande (le joueur a refusé ou ignoré la connexion automatique).
                using (var client = _playGames.CallStatic<AndroidJavaObject>("getGamesSignInClient", _activity))
                using (var task = client.Call<AndroidJavaObject>("signIn"))
                {
                    task.Call<AndroidJavaObject>("addOnCompleteListener", new OnComplete(OnAuthenticated, () =>
                    {
                        if (_signedIn) OpenLeaderboard();
                    })).Dispose();
                }
            }

            void OpenLeaderboard()
            {
                using (var client = _playGames.CallStatic<AndroidJavaObject>("getLeaderboardsClient", _activity))
                using (var task = client.Call<AndroidJavaObject>("getLeaderboardIntent", SocialIds.PlayGamesLeaderboard))
                {
                    task.Call<AndroidJavaObject>("addOnSuccessListener", new OnSuccess(intent =>
                        _activity.Call("startActivityForResult", intent, RequestCode))).Dispose();
                }
            }

            void OnAuthenticated(AndroidJavaObject task)
            {
                if (!task.Call<bool>("isSuccessful")) return;
                using (var result = task.Call<AndroidJavaObject>("getResult"))
                {
                    _signedIn = result != null && result.Call<bool>("isAuthenticated");
                }
            }

            /// <summary>com.google.android.gms.tasks.OnCompleteListener (appelé sur le fil UI d'Android).</summary>
            sealed class OnComplete : AndroidJavaProxy
            {
                readonly Action<AndroidJavaObject> _handler;
                readonly Action _then;

                public OnComplete(Action<AndroidJavaObject> handler, Action then) : base("com.google.android.gms.tasks.OnCompleteListener")
                {
                    _handler = handler;
                    _then = then;
                }

                [UnityEngine.Scripting.Preserve]
                void onComplete(AndroidJavaObject task)
                {
                    try
                    {
                        _handler(task);
                        _then?.Invoke();
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"PuffyBird : Play Games ({e.Message}).");
                    }
                }
            }

            /// <summary>com.google.android.gms.tasks.OnSuccessListener.</summary>
            sealed class OnSuccess : AndroidJavaProxy
            {
                readonly Action<AndroidJavaObject> _handler;

                public OnSuccess(Action<AndroidJavaObject> handler) : base("com.google.android.gms.tasks.OnSuccessListener") => _handler = handler;

                [UnityEngine.Scripting.Preserve]
                void onSuccess(AndroidJavaObject result)
                {
                    try
                    {
                        _handler(result);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"PuffyBird : Play Games ({e.Message}).");
                    }
                }
            }
        }
#endif
    }
}
