using System;

namespace PuffyBird.Core
{
    /// <summary>
    /// Petit synthétiseur de musique en temps réel, sans aucun fichier audio : joue en boucle la
    /// partition du décor (<see cref="MusicThemes"/>) avec un arpège, une nappe, une basse et des
    /// percussions de synthèse. <see cref="Render"/> est appelé par le fil audio et n'alloue rien ;
    /// le fil principal ne fait que demander un décor et un volume. Changement de décor : fondu
    /// sortant, puis la nouvelle boucle repart du début.
    /// </summary>
    public sealed class MusicSynth
    {
        const int MaxVoices = 24;
        const int TableSize = 4096;
        const int StepsPerBar = 16;
        const int Bars = 8;
        const float MasterGain = 0.55f;
        const float SwitchFadeSeconds = 0.25f;
        const float VolumeSmoothingSeconds = 0.15f;
        const float LowPassHz = 7000f;

        static readonly float[] SineTable = BuildSineTable();

        struct Voice
        {
            public bool Active;
            public bool Pad;
            public MusicWave Wave;
            public double Phase;
            public double Increment;
            public double Phase2;
            public double Increment2;
            public float Volume;
            public float Envelope;
            public float AttackStep;
            public float DecayFactor;
            public float ReleaseFactor;
            public int Hold;
            public int Stage; // 0 attaque, 1 décroissance, 2 relâchement
        }

        readonly Voice[] _voices = new Voice[MaxVoices];
        readonly int _sampleRate;
        readonly float _lowPass;
        readonly float _volumeSmoothing;
        readonly float _switchStep;

        MusicTheme _theme;
        int _playingTheme = -1;
        volatile int _requestedTheme;
        volatile float _targetVolume = 1f;
        float _volume;
        float _switchGain;
        int _step;
        double _samplesPerStep;
        double _untilNextStep;
        float _filtered;
        float _gain = MasterGain;

        // Percussions.
        uint _noise = 0x12345678u;
        float _kickEnvelope;
        double _kickPhase;
        double _kickIncrement;
        double _kickIncrementMin;
        float _kickSweep;
        float _kickDecay;
        float _hatEnvelope;
        float _hatDecay;
        float _hatPrevious;
        float _snareEnvelope;
        float _snareDecay;
        float _snareLowPass;
        double _snarePhase;

        public MusicSynth(int sampleRate)
        {
            if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            _sampleRate = sampleRate;
            _lowPass = 1f - (float)Math.Exp(-2.0 * Math.PI * LowPassHz / sampleRate);
            _volumeSmoothing = 1f / (VolumeSmoothingSeconds * sampleRate);
            _switchStep = 1f / (SwitchFadeSeconds * sampleRate);
            _kickSweep = (float)Math.Pow(50.0 / 150.0, 1.0 / (0.08 * sampleRate));
            _kickDecay = DecayFactor(0.18f);
            _hatDecay = DecayFactor(0.025f);
            _snareDecay = DecayFactor(0.08f);
            _requestedTheme = 0;
            _volume = 0f;
        }

        public int SampleRate => _sampleRate;

        /// <summary>Décor dont la musique doit jouer (fondu au changement).</summary>
        public void SetTheme(Theme theme) => _requestedTheme = (int)theme;

        /// <summary>Volume visé dans [0, 1], atteint en douceur (0 = musique coupée).</summary>
        public float Volume
        {
            get => _targetVolume;
            set => _targetVolume = value < 0f ? 0f : (value > 1f ? 1f : value);
        }

        /// <summary>Durée d'une boucle complète en secondes, pour le décor donné.</summary>
        public static double LoopSeconds(Theme theme) => Bars * 4 * 60.0 / MusicThemes.For(theme).Bpm;

        /// <summary>
        /// Remplit <paramref name="data"/> (échantillons entrelacés sur <paramref name="channels"/> canaux).
        /// Appelé par le fil audio : aucune allocation.
        /// </summary>
        public void Render(float[] data, int channels)
        {
            if (data == null || channels <= 0) return;
            int frames = data.Length / channels;
            float target = _targetVolume;
            if (target <= 0f && _volume < 1e-4f)
            {
                _volume = 0f;
                Array.Clear(data, 0, frames * channels);
                return;
            }

            for (int f = 0; f < frames; f++)
            {
                UpdateTheme();
                if (_theme != null)
                {
                    _untilNextStep -= 1.0;
                    if (_untilNextStep <= 0.0)
                    {
                        TriggerStep(_step);
                        _step = (_step + 1) % (StepsPerBar * Bars);
                        _untilNextStep += _samplesPerStep;
                    }
                }

                _volume += (target - _volume) * _volumeSmoothing;
                float x = MixVoices() + MixDrums();
                _filtered += (x - _filtered) * _lowPass;
                float y = SoftClip(_filtered * _gain * _switchGain * _volume);

                int i = f * channels;
                for (int c = 0; c < channels; c++) data[i + c] = y;
            }
        }

        void UpdateTheme()
        {
            int requested = _requestedTheme;
            if (requested == _playingTheme)
            {
                if (_switchGain < 1f) _switchGain = Math.Min(1f, _switchGain + _switchStep);
                return;
            }
            if (_switchGain > 0f && _theme != null)
            {
                _switchGain = Math.Max(0f, _switchGain - _switchStep);
                return;
            }

            // Silence atteint : on change de partition.
            for (int v = 0; v < MaxVoices; v++) _voices[v].Active = false;
            _kickEnvelope = _hatEnvelope = _snareEnvelope = 0f;
            _playingTheme = requested;
            _theme = MusicThemes.For((Theme)requested);
            _gain = MasterGain * _theme.Gain;
            _samplesPerStep = _sampleRate * 60.0 / _theme.Bpm / 4.0;
            _step = 0;
            _untilNextStep = 0.0;
            _switchGain = 0f;
        }

        void TriggerStep(int step)
        {
            var t = _theme;
            int bar = step / StepsPerBar;
            int s = step % StepsPerBar;
            int root = t.ChordRoots[bar];
            var chord = t.ChordIntervals[bar];

            if (s == 0 && t.PadVolume > 0f)
            {
                for (int v = 0; v < MaxVoices; v++)
                {
                    if (_voices[v].Active && _voices[v].Pad) _voices[v].Stage = 2;
                }
                int hold = (int)(_samplesPerStep * StepsPerBar);
                for (int n = 0; n < chord.Length && n < 3; n++)
                {
                    StartVoice(root + 12 + chord[n], t.PadWave, t.PadVolume, 0.3f, 0f, 0.6f, hold, pad: true);
                }
            }

            var arp = bar >= Bars / 2 && t.ArpB != null ? t.ArpB : t.Arp;
            if (arp != null && arp[s] >= 0)
            {
                int k = arp[s];
                int note = root + t.ArpTranspose + chord[k % chord.Length] + 12 * (k / chord.Length);
                StartVoice(note, t.ArpWave, t.ArpVolume, 0.004f, t.ArpDecay, 0.05f, int.MaxValue, pad: false);
            }

            if (t.Bass != null && t.Bass[s] >= 0)
            {
                int offset = t.Bass[s] == 1 ? 7 : (t.Bass[s] == 2 ? 12 : 0);
                StartVoice(root + offset, t.BassWave, t.BassVolume, 0.006f, t.BassDecay, 0.05f, int.MaxValue, pad: false);
            }

            float drums = t.DrumVolume;
            if (t.Kick != null && t.Kick[s] == 1)
            {
                _kickEnvelope = 0.9f * drums;
                _kickIncrement = 150.0 / _sampleRate;
                _kickIncrementMin = 50.0 / _sampleRate;
            }
            if (t.Snare != null && t.Snare[s] == 1) _snareEnvelope = 0.35f * drums;
            if (t.Hat != null && t.Hat[s] == 1) _hatEnvelope = 0.22f * drums;
        }

        void StartVoice(int midi, MusicWave wave, float volume, float attack, float decay, float release, int hold, bool pad)
        {
            int slot = -1;
            float quietest = float.MaxValue;
            for (int v = 0; v < MaxVoices; v++)
            {
                if (!_voices[v].Active)
                {
                    slot = v;
                    break;
                }
                if (_voices[v].Envelope < quietest)
                {
                    quietest = _voices[v].Envelope;
                    slot = v;
                }
            }

            double freq = 440.0 * Math.Pow(2.0, (midi - 69) / 12.0);
            ref var voice = ref _voices[slot];
            voice.Active = true;
            voice.Pad = pad;
            voice.Wave = wave;
            voice.Phase = 0.0;
            voice.Increment = freq / _sampleRate;
            voice.Phase2 = 0.0;
            voice.Increment2 = freq * 2.76 / _sampleRate;
            voice.Volume = volume;
            voice.Envelope = 0f;
            voice.AttackStep = 1f / Math.Max(1f, attack * _sampleRate);
            // Note tenue (nappe) : pas de décroissance avant le relâchement.
            voice.DecayFactor = decay > 0f ? DecayFactor(decay) : 1f;
            voice.ReleaseFactor = DecayFactor(release);
            voice.Hold = hold;
            voice.Stage = 0;
        }

        float MixVoices()
        {
            float sum = 0f;
            for (int v = 0; v < MaxVoices; v++)
            {
                ref var voice = ref _voices[v];
                if (!voice.Active) continue;

                switch (voice.Stage)
                {
                    case 0:
                        voice.Envelope += voice.AttackStep;
                        if (voice.Envelope >= 1f)
                        {
                            voice.Envelope = 1f;
                            voice.Stage = 1;
                        }
                        break;
                    case 1:
                        voice.Envelope *= voice.DecayFactor;
                        if (--voice.Hold <= 0) voice.Stage = 2;
                        break;
                    default:
                        voice.Envelope *= voice.ReleaseFactor;
                        break;
                }
                if (voice.Stage > 0 && voice.Envelope < 1e-4f)
                {
                    voice.Active = false;
                    continue;
                }

                float s = Wave(voice.Wave, voice.Phase);
                if (voice.Wave == MusicWave.Bell) s = s * 0.8f + Sine(voice.Phase2) * 0.35f * voice.Envelope;
                sum += s * voice.Envelope * voice.Volume;

                voice.Phase += voice.Increment;
                if (voice.Phase >= 1.0) voice.Phase -= 1.0;
                voice.Phase2 += voice.Increment2;
                if (voice.Phase2 >= 1.0) voice.Phase2 -= 1.0;
            }
            return sum;
        }

        float MixDrums()
        {
            float sum = 0f;
            if (_kickEnvelope > 1e-4f)
            {
                sum += Sine(_kickPhase) * _kickEnvelope;
                _kickPhase += _kickIncrement;
                if (_kickPhase >= 1.0) _kickPhase -= 1.0;
                _kickIncrement = Math.Max(_kickIncrementMin, _kickIncrement * _kickSweep);
                _kickEnvelope *= _kickDecay;
            }
            if (_hatEnvelope > 1e-4f || _snareEnvelope > 1e-4f)
            {
                float n = NextNoise();
                float highPass = n - _hatPrevious;
                _hatPrevious = n;
                sum += highPass * 0.5f * _hatEnvelope;
                _hatEnvelope *= _hatDecay;

                _snareLowPass += (n - _snareLowPass) * 0.35f;
                sum += (_snareLowPass * 0.8f + Sine(_snarePhase) * 0.3f) * _snareEnvelope;
                _snarePhase += 190.0 / _sampleRate;
                if (_snarePhase >= 1.0) _snarePhase -= 1.0;
                _snareEnvelope *= _snareDecay;
            }
            return sum;
        }

        static float Wave(MusicWave wave, double phase)
        {
            switch (wave)
            {
                case MusicWave.Triangle:
                    return 1f - 4f * Math.Abs((float)phase - 0.5f);
                case MusicWave.SoftSquare:
                    return (Sine(phase) + Sine(Frac(phase * 3.0)) / 3f + Sine(Frac(phase * 5.0)) / 5f) * 0.9f;
                default:
                    return Sine(phase);
            }
        }

        static double Frac(double x) => x - Math.Floor(x);

        static float Sine(double phase)
        {
            double index = phase * TableSize;
            int i = (int)index;
            float frac = (float)(index - i);
            i &= TableSize - 1;
            float a = SineTable[i];
            float b = SineTable[(i + 1) & (TableSize - 1)];
            return a + (b - a) * frac;
        }

        float NextNoise()
        {
            uint x = _noise;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _noise = x;
            return (x >> 8) * (2f / 16777216f) - 1f;
        }

        float DecayFactor(float seconds) => (float)Math.Exp(-1.0 / (Math.Max(0.001f, seconds) * _sampleRate));

        /// <summary>Approximation douce de tanh : pas d'écrêtage brutal si plusieurs notes s'additionnent.</summary>
        static float SoftClip(float x)
        {
            if (x > 3f) return 1f;
            if (x < -3f) return -1f;
            float x2 = x * x;
            return x * (27f + x2) / (27f + 9f * x2);
        }

        static float[] BuildSineTable()
        {
            var table = new float[TableSize];
            for (int i = 0; i < TableSize; i++) table[i] = (float)Math.Sin(2.0 * Math.PI * i / TableSize);
            return table;
        }
    }
}
