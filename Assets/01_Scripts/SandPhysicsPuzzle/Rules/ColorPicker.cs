using System;
using SandPhysicsPuzzle.Core;
using Random = Unity.Mathematics.Random;

namespace SandPhysicsPuzzle.Rules
{
    // Deliberately not a uniform draw. A span needs one colour running unbroken wall to wall,
    // which takes a lot of sand of that colour; dealt evenly across six the board fills with
    // confetti and the run ends without a single clear.
    //
    // StickChance repeats the previous colour outright -- consecutive same-colour pours are what
    // build a mound wide enough to span. BoardBias then pulls the rest of the draw towards what is
    // already lying there: 0 is uniform, 1 is proportional to each colour's share.
    public sealed class ColorPicker
    {
        public readonly int ColorCount;

        public float StickChance;
        public float BoardBias;

        private Random _random;
        private byte _lastColor;

        // Reused every draw, so picking a colour allocates nothing.
        private readonly int[] _counts;

        public ColorPicker(uint seed, int colorCount, float stickChance = 0.45f, float boardBias = 0.6f)
        {
            if (colorCount < 1 || colorCount > SandGrid.MaxColorId)
                throw new ArgumentOutOfRangeException(nameof(colorCount),
                    $"Colour count {colorCount} is outside 1..{SandGrid.MaxColorId}.");

            ColorCount = colorCount;
            StickChance = stickChance;
            BoardBias = boardBias;

            _random = new Random(seed == 0 ? 1u : seed);
            _counts = new int[SandGrid.MaxColorId + 1];
            _lastColor = 0;
        }

        public byte Next(in SandGrid grid)
        {
            if (ColorCount == 1) return Remember(1);

            if (_lastColor != 0 && _random.NextFloat() < StickChance)
                return Remember(_lastColor);

            BuildHistogram(grid, out var total);

            var uniform = 1f / ColorCount;
            var bias = total > 0 ? BoardBias : 0f;

            var sum = 0f;
            for (var id = 1; id <= ColorCount; id++)
                sum += (1f - bias) * uniform + bias * (_counts[id] / (float)(total > 0 ? total : 1));

            // sum is 1 in exact arithmetic; rolling against the real total keeps rounding honest.
            var roll = _random.NextFloat() * sum;

            for (var id = 1; id <= ColorCount; id++)
            {
                var weight = (1f - bias) * uniform + bias * (_counts[id] / (float)(total > 0 ? total : 1));

                // Strictly less than, so a roll of exactly zero cannot be swallowed by a leading
                // colour carrying no weight.
                roll -= weight;
                if (roll < 0f) return Remember((byte)id);
            }

            return Remember((byte)ColorCount);
        }

        public void Reset() => _lastColor = 0;

        private byte Remember(byte color)
        {
            _lastColor = color;
            return color;
        }

        // O(width * height), but it runs once per piece dealt, not once per frame. Grains already
        // flagged for clearing are skipped: they are leaving, and counting them would bias the
        // next piece towards a colour that is about to disappear.
        private void BuildHistogram(in SandGrid grid, out int total)
        {
            Array.Clear(_counts, 0, _counts.Length);
            total = 0;

            var cells = grid.Cells;
            for (var i = 0; i < cells.Length; i++)
            {
                var cell = cells[i];
                if (cell == SandGrid.Empty || SandGrid.IsClearing(cell)) continue;

                var color = SandGrid.ColorOf(cell);
                if (color == 0 || color > ColorCount) continue;

                _counts[color]++;
                total++;
            }
        }
    }
}
