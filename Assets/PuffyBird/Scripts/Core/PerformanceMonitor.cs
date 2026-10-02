namespace PuffyBird.Core
{
    /// <summary>
    /// Mesure la fluidité pendant la partie et dit quand baisser la qualité graphique : si la cadence
    /// moyenne reste sous <see cref="LowFpsRatio"/> de la cadence visée pendant
    /// <see cref="BadWindowsToDrop"/> fenêtres de <see cref="WindowSeconds"/> secondes de suite, la qualité doit baisser.
    /// Les images très longues (mise en arrière-plan, chargement) sont ignorées.
    /// </summary>
    public sealed class PerformanceMonitor
    {
        public const float WindowSeconds = 3f;
        public const float LowFpsRatio = 0.8f;
        public const int BadWindowsToDrop = 2;
        /// <summary>Une image plus longue que cela n'est pas une mesure de fluidité.</summary>
        public const float MaxFrameSeconds = 0.25f;

        readonly float _targetFps;
        float _time;
        int _frames;
        int _badWindows;

        public PerformanceMonitor(float targetFps)
        {
            _targetFps = targetFps;
        }

        /// <summary>Cadence moyenne de la dernière fenêtre terminée (0 avant la première).</summary>
        public float LastFps { get; private set; }

        /// <summary>
        /// À appeler à chaque image de jeu (pas aux menus) avec sa durée réelle. Renvoie vrai quand la
        /// qualité doit baisser ; le compte repart de zéro à chaque décision.
        /// </summary>
        public bool Record(float frameSeconds)
        {
            if (frameSeconds <= 0f || frameSeconds > MaxFrameSeconds) return false;
            _time += frameSeconds;
            _frames++;
            if (_time < WindowSeconds) return false;

            LastFps = _frames / _time;
            _time = 0f;
            _frames = 0;
            _badWindows = LastFps < _targetFps * LowFpsRatio ? _badWindows + 1 : 0;
            if (_badWindows < BadWindowsToDrop) return false;
            _badWindows = 0;
            return true;
        }

        /// <summary>Repart de zéro (changement de qualité : l'ancienne mesure ne vaut plus).</summary>
        public void Reset()
        {
            _time = 0f;
            _frames = 0;
            _badWindows = 0;
        }
    }
}
