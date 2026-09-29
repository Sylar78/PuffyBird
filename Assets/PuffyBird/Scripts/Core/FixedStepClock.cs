namespace PuffyBird.Core
{
    /// <summary>
    /// Accumulateur de temps pour la simulation à pas fixe (§4.2) : même comportement
    /// à 30, 60, 120 ou 144 Hz.
    /// </summary>
    public sealed class FixedStepClock
    {
        readonly double _step;
        readonly double _maxFrameDelta;
        double _accumulator;

        public FixedStepClock(float step, float maxFrameDelta)
        {
            _step = step;
            _maxFrameDelta = maxFrameDelta;
        }

        /// <summary>Ajoute la durée d'une image et renvoie le nombre de pas à simuler.</summary>
        public int Advance(double frameDelta)
        {
            if (frameDelta < 0) frameDelta = 0;
            if (frameDelta > _maxFrameDelta) frameDelta = _maxFrameDelta;
            _accumulator += frameDelta;
            int steps = 0;
            // Petite tolérance pour absorber l'erreur d'arrondi (1/60 n'est pas exact en binaire).
            while (_accumulator + 1e-6 >= _step)
            {
                _accumulator -= _step;
                steps++;
            }
            if (_accumulator < 0) _accumulator = 0;
            return steps;
        }

        /// <summary>Fraction du pas suivant déjà écoulée, pour interpoler le rendu.</summary>
        public float Alpha => (float)(_accumulator / _step);

        public void Reset() => _accumulator = 0;
    }
}
