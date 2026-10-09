using PuffyBird.Core;
using UnityEngine;

namespace PuffyBird.Audio
{
    /// <summary>
    /// Joue les 5 sons de la spec et celui de l'étoile, synthétisés au démarrage (aucun fichier audio). Plusieurs
    /// sources permettent aux sons de se superposer (taps rapides, §13.2).
    /// </summary>
    public sealed class SfxPlayer
    {
        const int SampleRate = 44100;
        const int Voices = 6;

        readonly AudioClip[] _clips;
        readonly AudioSource[] _sources = new AudioSource[Voices];
        int _next;

        public SfxPlayer(Transform parent)
        {
            var go = new GameObject("Sons");
            go.transform.SetParent(parent, false);
            for (int i = 0; i < Voices; i++)
            {
                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.loop = false;
                _sources[i] = source;
            }

            var ids = (SoundId[])System.Enum.GetValues(typeof(SoundId));
            _clips = new AudioClip[ids.Length];
            foreach (var id in ids)
            {
                var samples = SfxRecipes.Generate(id, SampleRate, (uint)id + 1u);
                var clip = AudioClip.Create(id.ToString(), samples.Length, 1, SampleRate, false);
                clip.SetData(samples, 0);
                _clips[(int)id] = clip;
            }
        }

        /// <summary>Coupe les bruitages seulement : la musique a son propre réglage.</summary>
        public bool Muted
        {
            set
            {
                foreach (var source in _sources) source.mute = value;
            }
        }

        public void Play(SoundId id)
        {
            var source = _sources[_next];
            _next = (_next + 1) % Voices;
            source.PlayOneShot(_clips[(int)id], SfxRecipes.Volume(id) * 1.6f);
        }

        /// <summary>Joue les sons correspondant aux événements d'une image.</summary>
        public void Play(GameEvents events)
        {
            if ((events & GameEvents.Flap) != 0) Play(SoundId.Wing);
            if ((events & GameEvents.Point) != 0) Play(SoundId.Point);
            if ((events & (GameEvents.Hit | GameEvents.StarShield)) != 0) Play(SoundId.Hit);
            if ((events & GameEvents.Die) != 0) Play(SoundId.Die);
            if ((events & GameEvents.Swoosh) != 0) Play(SoundId.Swoosh);
            if ((events & GameEvents.Star) != 0) Play(SoundId.Star);
            if ((events & GameEvents.NearMiss) != 0) Play(SoundId.Graze);
            if ((events & GameEvents.Milestone) != 0) Play(SoundId.Milestone);
        }
    }
}
