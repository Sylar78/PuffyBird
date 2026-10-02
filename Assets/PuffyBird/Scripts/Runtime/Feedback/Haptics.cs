using System;
using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace PuffyBird.Feedback
{
    public enum HapticKind
    {
        /// <summary>Battement d'ailes : à peine perceptible.</summary>
        Light,
        /// <summary>Étoile ramassée, frôlement d'un tuyau ou palier de médaille.</summary>
        Medium,
        /// <summary>Choc contre un tuyau ou le sol.</summary>
        Heavy,
    }

    /// <summary>
    /// Vibrations courtes. iOS : générateurs d'impact natifs (<c>Plugins/iOS/HapticsBridge.mm</c>).
    /// Android : effets prédéfinis (Android 10+) ou vibrations brèves dosées en intensité
    /// (Android 8+), appelés par JNI sans allocation. Ailleurs : rien.
    /// </summary>
    public sealed class Haptics
    {
        readonly IBackend _backend;

        public Haptics()
        {
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                _backend = new AndroidBackend();
#elif UNITY_IOS && !UNITY_EDITOR
                _backend = new IosBackend();
#endif
            }
            catch (Exception e)
            {
                Debug.LogWarning($"PuffyBird : vibrations indisponibles ({e.Message}).");
                _backend = null;
            }
        }

        public bool Enabled { get; set; } = true;

        public void Play(HapticKind kind)
        {
            if (Enabled) _backend?.Play(kind);
        }

        interface IBackend
        {
            void Play(HapticKind kind);
        }

#if UNITY_IOS && !UNITY_EDITOR
        sealed class IosBackend : IBackend
        {
            [DllImport("__Internal")] static extern void _Haptics_Prepare();
            [DllImport("__Internal")] static extern void _Haptics_Impact(int style);

            public IosBackend() => _Haptics_Prepare();

            // UIImpactFeedbackStyle : 0 = léger, 1 = moyen, 2 = fort.
            public void Play(HapticKind kind) => _Haptics_Impact((int)kind);
        }
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        sealed class AndroidBackend : IBackend
        {
            // VibrationEffect.EFFECT_TICK, EFFECT_CLICK, EFFECT_HEAVY_CLICK.
            static readonly int[] Predefined = { 2, 0, 5 };
            // Repli Android 8 et 9 : durée (ms) et intensité (1 à 255).
            static readonly long[] Durations = { 12, 22, 45 };
            static readonly int[] Amplitudes = { 70, 140, 255 };

            readonly AndroidJavaObject _vibrator;
            readonly AndroidJavaObject[] _effects = new AndroidJavaObject[3];
            readonly IntPtr _vibrateMethod;
            readonly jvalue[] _args = new jvalue[1];
            readonly bool _legacy;

            public AndroidBackend()
            {
                int sdk;
                using (var version = new AndroidJavaClass("android.os.Build$VERSION")) sdk = version.GetStatic<int>("SDK_INT");
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }
                if (_vibrator == null || !_vibrator.Call<bool>("hasVibrator")) throw new InvalidOperationException("pas de vibreur");

                IntPtr vibratorClass = AndroidJNI.GetObjectClass(_vibrator.GetRawObject());
                if (sdk >= 26)
                {
                    using (var effect = new AndroidJavaClass("android.os.VibrationEffect"))
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            _effects[i] = sdk >= 29
                                ? effect.CallStatic<AndroidJavaObject>("createPredefined", Predefined[i])
                                : effect.CallStatic<AndroidJavaObject>("createOneShot", Durations[i], Amplitudes[i]);
                        }
                    }
                    _vibrateMethod = AndroidJNI.GetMethodID(vibratorClass, "vibrate", "(Landroid/os/VibrationEffect;)V");
                }
                else
                {
                    _legacy = true;
                    _vibrateMethod = AndroidJNI.GetMethodID(vibratorClass, "vibrate", "(J)V");
                }
                AndroidJNI.DeleteLocalRef(vibratorClass);
            }

            public void Play(HapticKind kind)
            {
                int i = (int)kind;
                if (_legacy) _args[0].j = Durations[i];
                else _args[0].l = _effects[i].GetRawObject();
                AndroidJNI.CallVoidMethod(_vibrator.GetRawObject(), _vibrateMethod, _args);
                if (AndroidJNI.ExceptionOccurred() != IntPtr.Zero) AndroidJNI.ExceptionClear();
            }
        }
#endif
    }
}
