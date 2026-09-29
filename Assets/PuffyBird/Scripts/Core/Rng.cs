namespace PuffyBird.Core
{
    /// <summary>
    /// Générateur pseudo-aléatoire à graine (Mulberry32). Injecté dans la simulation
    /// pour des parties reproductibles : tests, rediffusions, défi quotidien.
    /// </summary>
    public sealed class Rng
    {
        uint _state;

        public Rng(uint seed)
        {
            _state = seed;
        }

        public uint State => _state;

        public uint NextUInt()
        {
            uint z = _state += 0x6D2B79F5u;
            z = (z ^ (z >> 15)) * (z | 1u);
            z ^= z + (z ^ (z >> 7)) * (z | 61u);
            return z ^ (z >> 14);
        }

        /// <summary>Flottant uniforme dans [0, 1).</summary>
        public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

        /// <summary>Entier uniforme, bornes incluses.</summary>
        public int Range(int minInclusive, int maxInclusive)
        {
            if (maxInclusive <= minInclusive) return minInclusive;
            uint span = (uint)(maxInclusive - minInclusive + 1);
            return minInclusive + (int)(NextUInt() % span);
        }
    }
}
