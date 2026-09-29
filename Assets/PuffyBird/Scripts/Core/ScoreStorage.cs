namespace PuffyBird.Core
{
    /// <summary>Persistance du meilleur score et de l'option muet (§17).</summary>
    public interface IScoreStorage
    {
        /// <summary>Faux si la valeur existe mais est illisible : elle ne doit alors jamais être écrasée.</summary>
        bool TryReadBest(out int best);
        void WriteBest(int best);
        bool Muted { get; set; }
    }

    public sealed class MemoryScoreStorage : IScoreStorage
    {
        public int Best;
        public bool Readable = true;
        public int Writes;

        public bool TryReadBest(out int best)
        {
            best = Best;
            return Readable;
        }

        public void WriteBest(int best)
        {
            Best = best;
            Writes++;
        }

        public bool Muted { get; set; }
    }
}
