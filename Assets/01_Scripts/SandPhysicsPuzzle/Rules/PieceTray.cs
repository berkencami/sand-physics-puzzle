using System;
using SandPhysicsPuzzle.Core;
using Random = Unity.Mathematics.Random;

namespace SandPhysicsPuzzle.Rules
{
    public struct TraySlot
    {
        public int Shape;
        public int Rotation;
        public byte ColorId;
        public bool IsFilled;

        public PieceState ToPiece(int x, int y, int blockSize) =>
            new PieceState(Shape, Rotation, x, y, ColorId, blockSize);
    }

    // All three slots refill together, once the last is used. Refilling each the instant it empties
    // would leave the player three choices forever; committing to a whole set is what creates the
    // "one bad piece left" pressure.
    //
    // The refill is not automatic -- IsExhausted reports, Refill deals -- so the caller owns the
    // timing. Same split as ClearDetector's mark/commit, for the same reason.
    //
    // The player cannot rotate, so each piece is dealt at a random rotation instead. That turns
    // seven shapes into nineteen silhouettes without handing back any control.
    public sealed class PieceTray
    {
        public const int SlotCount = 3;

        private readonly TraySlot[] _slots = new TraySlot[SlotCount];
        private readonly ColorPicker _colors;
        private Bag7 _bag;
        private Random _random;

        public PieceTray(uint seed, ColorPicker colors)
        {
            _colors = colors ?? throw new ArgumentNullException(nameof(colors));
            _bag = new Bag7(seed);

            // A separate stream, so rotations do not consume the shape sequence.
            var rotationSeed = seed * 747796405u + 2891336453u;
            _random = new Random(rotationSeed == 0 ? 1u : rotationSeed);
        }

        public int FilledCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < SlotCount; i++)
                    if (_slots[i].IsFilled) count++;
                return count;
            }
        }

        public bool IsExhausted => FilledCount == 0;

        public TraySlot GetSlot(int index)
        {
            if ((uint)index >= SlotCount)
                throw new ArgumentOutOfRangeException(nameof(index), $"Slot {index} does not exist.");

            return _slots[index];
        }

        /// <summary>False when the slot was already empty, which rejects a double tap.</summary>
        public bool TryConsume(int index)
        {
            if ((uint)index >= SlotCount)
                throw new ArgumentOutOfRangeException(nameof(index), $"Slot {index} does not exist.");

            if (!_slots[index].IsFilled) return false;

            _slots[index] = default;
            return true;
        }

        public void Refill(in SandGrid grid)
        {
            for (var i = 0; i < SlotCount; i++)
            {
                if (_slots[i].IsFilled) continue;

                _slots[i] = new TraySlot
                {
                    Shape = _bag.Next(),
                    Rotation = _random.NextInt(0, Tetromino.Rotations),
                    ColorId = _colors.Next(grid),
                    IsFilled = true,
                };
            }
        }

        public void Clear()
        {
            for (var i = 0; i < SlotCount; i++) _slots[i] = default;
        }
    }
}
