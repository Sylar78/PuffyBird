using System;

namespace PuffyBird.Core
{
    public enum SoundId
    {
        Wing,
        Point,
        Hit,
        Die,
        Swoosh,
    }

    /// <summary>
    /// Synthèse procédurale des 5 sons (§13.3), sans aucun fichier audio : mêmes recettes que
    /// l'implémentation HTML de référence (bruit filtré passe-bande, oscillateurs carré et
    /// triangle, glissandos et enveloppes exponentiels).
    /// </summary>
    public static class SfxRecipes
    {
        public static float[] Generate(SoundId id, int sampleRate, uint seed = 1)
        {
            var rng = new Rng(seed);
            switch (id)
            {
                case SoundId.Wing:
                {
                    var buf = Alloc(0.12f, sampleRate);
                    Noise(buf, sampleRate, 0f, 0.12f, 0.25f, 2500f, 700f, rng);
                    return buf;
                }
                case SoundId.Point:
                {
                    var buf = Alloc(0.34f, sampleRate);
                    Tone(buf, sampleRate, 0f, 0.09f, Wave.Square, 0.08f, 1046.5f, 1046.5f);
                    Tone(buf, sampleRate, 0.09f, 0.25f, Wave.Square, 0.08f, 1568f, 1568f);
                    return buf;
                }
                case SoundId.Hit:
                {
                    var buf = Alloc(0.12f, sampleRate);
                    Noise(buf, sampleRate, 0f, 0.1f, 0.5f, 900f, 120f, rng);
                    Tone(buf, sampleRate, 0f, 0.12f, Wave.Triangle, 0.3f, 140f, 60f);
                    return buf;
                }
                case SoundId.Die:
                {
                    var buf = Alloc(0.45f, sampleRate);
                    Tone(buf, sampleRate, 0f, 0.45f, Wave.Triangle, 0.15f, 700f, 120f);
                    return buf;
                }
                case SoundId.Swoosh:
                {
                    var buf = Alloc(0.25f, sampleRate);
                    Noise(buf, sampleRate, 0f, 0.25f, 0.18f, 600f, 3000f, rng);
                    return buf;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(id));
            }
        }

        /// <summary>Volume relatif conseillé (§13.2).</summary>
        public static float Volume(SoundId id)
        {
            switch (id)
            {
                case SoundId.Wing: return 0.6f;
                case SoundId.Point: return 0.7f;
                case SoundId.Hit: return 1f;
                case SoundId.Die: return 0.7f;
                default: return 0.5f;
            }
        }

        enum Wave
        {
            Square,
            Triangle,
        }

        static float[] Alloc(float seconds, int sampleRate) => new float[(int)Math.Ceiling(seconds * sampleRate)];

        /// <summary>Interpolation exponentielle, comme exponentialRampToValueAtTime de WebAudio.</summary>
        static float ExpRamp(float from, float to, float t) => from * (float)Math.Pow(to / from, t);

        static void Tone(float[] buf, int sampleRate, float start, float duration, Wave wave, float volume, float freqFrom, float freqTo)
        {
            int first = (int)(start * sampleRate);
            int count = (int)(duration * sampleRate);
            double phase = 0;
            for (int i = 0; i < count && first + i < buf.Length; i++)
            {
                float t = (float)i / count;
                float freq = ExpRamp(freqFrom, freqTo, t);
                phase += freq / sampleRate;
                phase -= Math.Floor(phase);
                float s = wave == Wave.Square
                    ? (phase < 0.5 ? 1f : -1f)
                    : (float)(1.0 - 4.0 * Math.Abs(phase - 0.5));
                float gain = ExpRamp(volume, 0.001f, t);
                buf[first + i] = Clamp(buf[first + i] + s * gain);
            }
        }

        /// <summary>Bruit blanc filtré par un passe-bande biquad (Q = 1) dont la fréquence glisse.</summary>
        static void Noise(float[] buf, int sampleRate, float start, float duration, float volume, float filterFrom, float filterTo, Rng rng)
        {
            int first = (int)(start * sampleRate);
            int count = (int)(duration * sampleRate);
            double x1 = 0, x2 = 0, y1 = 0, y2 = 0;
            const double q = 1.0;
            for (int i = 0; i < count && first + i < buf.Length; i++)
            {
                float t = (float)i / count;
                double f0 = ExpRamp(filterFrom, filterTo, t);
                double w0 = 2.0 * Math.PI * f0 / sampleRate;
                double alpha = Math.Sin(w0) / (2.0 * q);
                double a0 = 1.0 + alpha;
                double b0 = alpha / a0;
                double b2 = -alpha / a0;
                double a1 = -2.0 * Math.Cos(w0) / a0;
                double a2 = (1.0 - alpha) / a0;

                double x0 = rng.NextFloat() * 2.0 - 1.0;
                double y0 = b0 * x0 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1; x1 = x0;
                y2 = y1; y1 = y0;

                float gain = ExpRamp(volume, 0.001f, t);
                buf[first + i] = Clamp(buf[first + i] + (float)y0 * gain);
            }
        }

        static float Clamp(float v) => v < -1f ? -1f : (v > 1f ? 1f : v);
    }
}
