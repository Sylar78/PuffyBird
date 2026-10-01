namespace PuffyBird.Core
{
    /// <summary>Timbres du synthétiseur de musique.</summary>
    public enum MusicWave
    {
        Sine,
        Triangle,
        /// <summary>Carré adouci (trois harmoniques impaires) : son « chiptune » rond.</summary>
        SoftSquare,
        /// <summary>Cloche de boîte à musique : sinus et partiel inharmonique qui s'éteint plus vite.</summary>
        Bell,
    }

    /// <summary>
    /// Partition d'une boucle de 8 mesures à 4 temps, sur une grille de 16 doubles-croches par
    /// mesure. Les notes sont en demi-tons MIDI (60 = do central).
    /// </summary>
    public sealed class MusicTheme
    {
        public float Bpm;
        /// <summary>Fondamentale de l'accord de chaque mesure (8 valeurs).</summary>
        public int[] ChordRoots;
        /// <summary>Intervalles de l'accord de chaque mesure, depuis la fondamentale.</summary>
        public int[][] ChordIntervals;

        /// <summary>Arpège : indice de la note de l'accord par double-croche (−1 = silence), étendu aux octaves supérieures.</summary>
        public int[] Arp;
        /// <summary>Variante de l'arpège pour les mesures 5 à 8 (null = même motif).</summary>
        public int[] ArpB;
        public int ArpTranspose = 12;
        public MusicWave ArpWave = MusicWave.SoftSquare;
        public float ArpDecay = 0.18f;
        public float ArpVolume = 0.14f;

        public MusicWave PadWave = MusicWave.Triangle;
        /// <summary>Volume de chaque note de la nappe tenue (0 = pas de nappe).</summary>
        public float PadVolume = 0.045f;

        /// <summary>Basse par double-croche : −1 silence, 0 fondamentale, 1 quinte, 2 octave.</summary>
        public int[] Bass;
        public MusicWave BassWave = MusicWave.Triangle;
        public float BassVolume = 0.2f;
        public float BassDecay = 0.3f;

        /// <summary>Percussions par double-croche (1 = coup), null = aucune.</summary>
        public int[] Kick;
        public int[] Snare;
        public int[] Hat;
        public float DrumVolume = 1f;

        /// <summary>Gain global de la boucle, pour égaliser le volume perçu d'un décor à l'autre.</summary>
        public float Gain = 1f;
    }

    /// <summary>Une boucle par décor (<see cref="Theme"/>), composée à la main.</summary>
    public static class MusicThemes
    {
        static readonly int[] Major = { 0, 4, 7 };
        static readonly int[] Minor = { 0, 3, 7 };
        static readonly int[] Maj7 = { 0, 4, 7, 11 };
        static readonly int[] Min7 = { 0, 3, 7, 10 };
        static readonly int[] Sus2 = { 0, 2, 7 };

        static readonly MusicTheme[] All =
        {
            // Jour : majeur et sautillant.
            new MusicTheme
            {
                Bpm = 112f,
                ChordRoots = new[] { 48, 43, 45, 41, 48, 43, 41, 43 },
                ChordIntervals = new[] { Major, Major, Minor, Major, Major, Major, Major, Major },
                Arp = new[] { 0, -1, 1, -1, 2, -1, 3, -1, 2, -1, 1, -1, 2, -1, 4, -1 },
                ArpB = new[] { 3, -1, 2, -1, 1, -1, 2, -1, 4, -1, 3, -1, 2, -1, 1, -1 },
                ArpTranspose = 12,
                ArpWave = MusicWave.SoftSquare,
                ArpDecay = 0.16f,
                ArpVolume = 0.12f,
                PadVolume = 0.035f,
                Bass = new[] { 0, -1, -1, -1, 1, -1, -1, -1, 0, -1, -1, 2, 1, -1, -1, -1 },
                Kick = new[] { 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0 },
                Hat = new[] { 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0 },
                DrumVolume = 0.8f,
            },
            // Nuit : mineur, lent, cloches clairsemées.
            new MusicTheme
            {
                Bpm = 80f,
                ChordRoots = new[] { 45, 41, 48, 43, 45, 41, 38, 40 },
                ChordIntervals = new[] { Minor, Major, Major, Major, Minor, Major, Minor, Major },
                Arp = new[] { 0, -1, -1, -1, 2, -1, -1, -1, 4, -1, -1, -1, 2, -1, -1, -1 },
                ArpB = new[] { 4, -1, -1, -1, 3, -1, -1, -1, 2, -1, -1, -1, 1, -1, -1, -1 },
                ArpTranspose = 24,
                ArpWave = MusicWave.Bell,
                ArpDecay = 0.9f,
                ArpVolume = 0.1f,
                PadWave = MusicWave.Sine,
                PadVolume = 0.05f,
                Bass = new[] { 0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
                BassWave = MusicWave.Sine,
                BassDecay = 1.2f,
                BassVolume = 0.18f,
                Gain = 1.6f,
            },
            // Ville au crépuscule : accords de septième, rythme feutré.
            new MusicTheme
            {
                Bpm = 86f,
                ChordRoots = new[] { 41, 40, 38, 36, 41, 40, 38, 43 },
                ChordIntervals = new[] { Maj7, Min7, Min7, Maj7, Maj7, Min7, Min7, Major },
                Arp = new[] { 0, -1, 2, -1, -1, 3, -1, 1, -1, -1, 2, -1, 4, -1, -1, -1 },
                ArpTranspose = 12,
                ArpWave = MusicWave.Triangle,
                ArpDecay = 0.35f,
                ArpVolume = 0.12f,
                PadWave = MusicWave.Triangle,
                PadVolume = 0.04f,
                Bass = new[] { 0, -1, -1, -1, -1, -1, 0, -1, -1, -1, 1, -1, -1, -1, -1, -1 },
                BassWave = MusicWave.Sine,
                BassDecay = 0.5f,
                Kick = new[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0 },
                Snare = new[] { 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0 },
                Hat = new[] { 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 1 },
                DrumVolume = 0.6f,
                Gain = 1.2f,
            },
            // Japon médiéval : gamme pentatonique de ré, pincé façon koto.
            new MusicTheme
            {
                Bpm = 92f,
                ChordRoots = new[] { 38, 46, 36, 38, 38, 43, 45, 38 },
                ChordIntervals = new[] { Minor, Major, Sus2, Minor, Minor, Minor, Sus2, Minor },
                Arp = new[] { 0, -1, 1, 2, -1, -1, 3, -1, 2, -1, 1, -1, 0, -1, -1, -1 },
                ArpB = new[] { 3, -1, 4, -1, 3, 2, -1, -1, 1, -1, 2, -1, 0, -1, -1, -1 },
                ArpTranspose = 24,
                ArpWave = MusicWave.Triangle,
                ArpDecay = 0.28f,
                ArpVolume = 0.15f,
                PadWave = MusicWave.Sine,
                PadVolume = 0.03f,
                Bass = new[] { 0, -1, -1, -1, -1, -1, -1, -1, 1, -1, -1, -1, -1, -1, -1, -1 },
                BassWave = MusicWave.Sine,
                BassDecay = 0.6f,
                Kick = new[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 0 },
                DrumVolume = 0.7f,
            },
            // Orage : mineur, basse pulsée en croches, tension.
            new MusicTheme
            {
                Bpm = 132f,
                ChordRoots = new[] { 38, 46, 48, 45, 38, 46, 43, 45 },
                ChordIntervals = new[] { Minor, Major, Major, Minor, Minor, Major, Minor, Major },
                Arp = new[] { 0, 1, 2, 1, 0, 1, 2, 3, 0, 1, 2, 1, 0, 2, 3, 2 },
                ArpTranspose = 12,
                ArpWave = MusicWave.SoftSquare,
                ArpDecay = 0.09f,
                ArpVolume = 0.08f,
                PadWave = MusicWave.Triangle,
                PadVolume = 0.04f,
                Bass = new[] { 0, -1, 0, -1, 0, -1, 0, -1, 0, -1, 0, -1, 0, -1, 2, -1 },
                BassWave = MusicWave.SoftSquare,
                BassDecay = 0.12f,
                BassVolume = 0.16f,
                Kick = new[] { 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0 },
                Snare = new[] { 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1 },
                Hat = new[] { 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0 },
                DrumVolume = 0.75f,
                Gain = 0.8f,
            },
            // Neige : boîte à musique douce.
            new MusicTheme
            {
                Bpm = 72f,
                ChordRoots = new[] { 48, 40, 41, 43, 45, 40, 41, 43 },
                ChordIntervals = new[] { Maj7, Minor, Maj7, Major, Minor, Minor, Major, Major },
                Arp = new[] { 0, -1, 1, -1, 2, -1, 3, -1, 4, -1, 3, -1, 2, -1, 1, -1 },
                ArpTranspose = 24,
                ArpWave = MusicWave.Bell,
                ArpDecay = 0.7f,
                ArpVolume = 0.09f,
                PadWave = MusicWave.Sine,
                PadVolume = 0.04f,
                Bass = new[] { 0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
                BassWave = MusicWave.Sine,
                BassDecay = 1.5f,
                BassVolume = 0.16f,
                Gain = 1.9f,
            },
        };

        public static int Count => All.Length;

        public static MusicTheme For(Theme theme)
        {
            int i = (int)theme;
            return All[i >= 0 && i < All.Length ? i : 0];
        }
    }
}
