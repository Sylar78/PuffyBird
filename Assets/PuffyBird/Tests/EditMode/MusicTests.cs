using System;
using NUnit.Framework;
using PuffyBird.Core;

namespace PuffyBird.Tests
{
    /// <summary>Musique de synthèse : chaque décor joue, sans saturation ni valeur invalide.</summary>
    public class MusicTests
    {
        const int SampleRate = 48000;
        const int Block = 1024;

        static float[] Render(Theme theme, double seconds, float volume = 1f)
        {
            var synth = new MusicSynth(SampleRate) { Volume = volume };
            synth.SetTheme(theme);
            int frames = (int)(seconds * SampleRate);
            var output = new float[frames * 2];
            var block = new float[Block * 2];
            for (int done = 0; done < frames; done += Block)
            {
                synth.Render(block, 2);
                Array.Copy(block, 0, output, done * 2, Math.Min(Block, frames - done) * 2);
            }
            return output;
        }

        [Test]
        public void OneThemePerDecor()
        {
            Assert.AreEqual(Enum.GetValues(typeof(Theme)).Length, MusicThemes.Count);
            foreach (Theme theme in Enum.GetValues(typeof(Theme)))
            {
                var t = MusicThemes.For(theme);
                Assert.AreEqual(8, t.ChordRoots.Length, theme.ToString());
                Assert.AreEqual(8, t.ChordIntervals.Length, theme.ToString());
                Assert.AreEqual(16, t.Arp.Length, theme.ToString());
                if (t.ArpB != null) Assert.AreEqual(16, t.ArpB.Length, theme.ToString());
                if (t.Bass != null) Assert.AreEqual(16, t.Bass.Length, theme.ToString());
                if (t.Kick != null) Assert.AreEqual(16, t.Kick.Length, theme.ToString());
                if (t.Snare != null) Assert.AreEqual(16, t.Snare.Length, theme.ToString());
                if (t.Hat != null) Assert.AreEqual(16, t.Hat.Length, theme.ToString());
            }
        }

        [Test]
        public void EveryThemeIsAudibleAndNeverClips()
        {
            foreach (Theme theme in Enum.GetValues(typeof(Theme)))
            {
                var samples = Render(theme, 10.0);
                double sum = 0.0;
                float peak = 0f;
                foreach (float s in samples)
                {
                    Assert.IsFalse(float.IsNaN(s) || float.IsInfinity(s), theme.ToString());
                    peak = Math.Max(peak, Math.Abs(s));
                    sum += s * s;
                }
                double rms = Math.Sqrt(sum / samples.Length);
                Assert.LessOrEqual(peak, 0.95f, $"{theme} : crête {peak}");
                Assert.Greater(rms, 0.01, $"{theme} : trop faible ({rms})");
            }
        }

        [Test]
        public void RenderingIsDeterministic()
        {
            var a = Render(Theme.Storm, 2.0);
            var b = Render(Theme.Storm, 2.0);
            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void ZeroVolumeIsSilent()
        {
            foreach (float s in Render(Theme.Day, 1.0, 0f)) Assert.AreEqual(0f, s);
        }

        [Test]
        public void ThemeSwitchFadesWithoutClicks()
        {
            var synth = new MusicSynth(SampleRate);
            synth.SetTheme(Theme.Storm);
            var block = new float[Block];
            for (int i = 0; i < 100; i++) synth.Render(block, 1);
            synth.SetTheme(Theme.Snow);
            float previous = block[Block - 1];
            for (int i = 0; i < 100; i++)
            {
                synth.Render(block, 1);
                foreach (float s in block)
                {
                    Assert.Less(Math.Abs(s - previous), 0.25f);
                    previous = s;
                }
            }
        }

        [Test]
        public void LoopLastsEightBars()
        {
            Assert.AreEqual(8 * 4 * 60.0 / 112.0, MusicSynth.LoopSeconds(Theme.Day), 1e-9);
        }
    }
}
