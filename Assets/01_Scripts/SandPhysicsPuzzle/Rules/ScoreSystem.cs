namespace SandPhysicsPuzzle.Rules
{
    // A combo here is a cascade, not a streak across turns: clearing a span drops the sand resting
    // on it, which can complete another by itself. Each clear inside that chain scores higher, so
    // the reward goes to setting up a collapse rather than to clearing often.
    public sealed class ScoreSystem
    {
        private readonly int _pointsPerGrain;
        private readonly int _maxComboMultiplier;

        public ScoreSystem(int pointsPerGrain = 1, int maxComboMultiplier = 8)
        {
            _pointsPerGrain = pointsPerGrain;
            _maxComboMultiplier = maxComboMultiplier;
        }

        public int Score { get; private set; }

        /// <summary>Clears so far in the current cascade; 0 between placements.</summary>
        public int Combo { get; private set; }

        public int BestCombo { get; private set; }
        public int GrainsCleared { get; private set; }

        public int RegisterClear(int grainCount)
        {
            if (grainCount <= 0) return 0;

            Combo++;
            if (Combo > BestCombo) BestCombo = Combo;

            var multiplier = Combo < _maxComboMultiplier ? Combo : _maxComboMultiplier;
            var points = grainCount * _pointsPerGrain * multiplier;

            Score += points;
            GrainsCleared += grainCount;
            return points;
        }

        /// <summary>A placement that cleared nothing still calls this.</summary>
        public void EndChain() => Combo = 0;

        public void Reset()
        {
            Score = 0;
            Combo = 0;
            BestCombo = 0;
            GrainsCleared = 0;
        }
    }
}
