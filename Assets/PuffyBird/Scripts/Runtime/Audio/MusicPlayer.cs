using PuffyBird.Core;
using UnityEngine;

namespace PuffyBird.Audio
{
    /// <summary>
    /// Musique d'ambiance du décor, synthétisée en temps réel par <see cref="MusicSynth"/> (aucun
    /// fichier audio). Une source joue en boucle un clip silencieux pour que Unity appelle
    /// <see cref="OnAudioFilterRead"/>, qui remplace ce silence par la musique.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MusicPlayer : MonoBehaviour
    {
        /// <summary>Niveau de la musique, sous celui des bruitages.</summary>
        const float Level = 0.7f;

        MusicSynth _synth;

        public static MusicPlayer Create(Transform parent)
        {
            var go = new GameObject("Musique");
            go.transform.SetParent(parent, false);
            var player = go.AddComponent<MusicPlayer>();
            player.Init();
            return player;
        }

        void Init()
        {
            int sampleRate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
            _synth = new MusicSynth(sampleRate) { Volume = 0f };

            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.loop = true;
            source.priority = 0;
            source.clip = AudioClip.Create("Silence", sampleRate, 1, sampleRate, false);
            source.Play();
        }

        public void SetTheme(Theme theme) => _synth.SetTheme(theme);

        /// <summary>Volume dans [0, 1] (0 = coupée), atteint en douceur.</summary>
        public void SetVolume(float volume) => _synth.Volume = volume * Level;

        // Fil audio : aucune allocation.
        void OnAudioFilterRead(float[] data, int channels) => _synth?.Render(data, channels);
    }
}
