using System;

namespace SandPhysicsPuzzle.Rules
{
    // Seven shapes, four rotations, as 4x4 bitmasks. Bit (y * 4 + x) is set when filled, with y
    // growing upward to match SandGrid, so stamping needs no vertical flip.
    //
    // Each shape rotates inside its own box -- 2 for O, 4 for I, 3 for the rest -- which is what
    // stops a rotation drifting across the board. Masks are generated from the artwork below at
    // static init, so the table cannot disagree with the picture a human reads.
    public static class Tetromino
    {
        public const int Count = 7;
        public const int Rotations = 4;
        public const int BoxWidth = 4;

        public const int I = 0;
        public const int O = 1;
        public const int T = 2;
        public const int S = 3;
        public const int Z = 4;
        public const int J = 5;
        public const int L = 6;

        private static readonly int[] _boxSizes = { 4, 2, 3, 3, 3, 3, 3 };

        // Artwork is written the way it looks on screen: the FIRST string is the TOP row.
        private static readonly string[][] _art =
        {
            // I
            new[] { "....",
                    "####",
                    "....",
                    "...." },
            // O
            new[] { "##",
                    "##" },
            // T
            new[] { ".#.",
                    "###",
                    "..." },
            // S
            new[] { ".##",
                    "##.",
                    "..." },
            // Z
            new[] { "##.",
                    ".##",
                    "..." },
            // J
            new[] { "#..",
                    "###",
                    "..." },
            // L
            new[] { "..#",
                    "###",
                    "..." },
        };

        private static readonly ushort[] _masks = BuildMasks();

        /// <summary>Rotation wraps, including negatives.</summary>
        public static ushort Mask(int shape, int rotation)
        {
            if ((uint)shape >= Count)
                throw new ArgumentOutOfRangeException(nameof(shape), $"Shape {shape} is out of range.");

            var r = rotation & 3;
            return _masks[shape * Rotations + r];
        }

        public static bool IsFilled(ushort mask, int x, int y) =>
            (uint)x < BoxWidth && (uint)y < BoxWidth && (mask & (1 << (y * BoxWidth + x))) != 0;

        /// <summary>Bounds of the filled cells inside the 4x4 box.</summary>
        public static void Bounds(ushort mask, out int xMin, out int yMin, out int xMax, out int yMax)
        {
            xMin = BoxWidth;
            yMin = BoxWidth;
            xMax = -1;
            yMax = -1;

            for (var y = 0; y < BoxWidth; y++)
            for (var x = 0; x < BoxWidth; x++)
            {
                if (!IsFilled(mask, x, y)) continue;

                if (x < xMin) xMin = x;
                if (y < yMin) yMin = y;
                if (x > xMax) xMax = x;
                if (y > yMax) yMax = y;
            }
        }

        private static ushort[] BuildMasks()
        {
            var masks = new ushort[Count * Rotations];

            for (var shape = 0; shape < Count; shape++)
            {
                var box = _boxSizes[shape];
                var current = FromArt(_art[shape], box);

                for (var rotation = 0; rotation < Rotations; rotation++)
                {
                    masks[shape * Rotations + rotation] = current;
                    current = RotateClockwise(current, box);
                }
            }

            return masks;
        }

        private static ushort FromArt(string[] art, int box)
        {
            // Explicitly ushort, not var: var would infer int from the literal, and the
            // accumulated mask would no longer match the type this returns.
            ushort mask = 0;

            for (var artRow = 0; artRow < box; artRow++)
            {
                // First art row is the top of the box, but y counts up from the bottom
                var y = box - 1 - artRow;

                for (var x = 0; x < box; x++)
                {
                    if (art[artRow][x] == '#')
                        mask |= (ushort)(1 << (y * BoxWidth + x));
                }
            }

            return mask;
        }

        // With y pointing up, clockwise sends (x, y) to (y, box - 1 - x).
        private static ushort RotateClockwise(ushort mask, int box)
        {
            // Explicitly ushort, for the same reason as FromArt's accumulator.
            ushort rotated = 0;

            for (var y = 0; y < box; y++)
            for (var x = 0; x < box; x++)
            {
                if ((mask & (1 << (y * BoxWidth + x))) == 0) continue;

                var newX = y;
                var newY = box - 1 - x;
                rotated |= (ushort)(1 << (newY * BoxWidth + newX));
            }

            return rotated;
        }
    }
}
