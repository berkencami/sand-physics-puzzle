using System;
using SandPhysicsPuzzle.Core;

namespace SandPhysicsPuzzle.Rules
{
    // A tetromino positioned over the board, before it becomes sand. It is not part of the grid
    // while the player aims it, so posing and previewing cost the simulation nothing.
    //
    // Position is in grain space, not block space, so a piece can be aimed anywhere instead of
    // snapping to a lattice. X and Y are the bottom-left of the 4x4 box, which is not the
    // bottom-left of the visible shape -- GetGrainBounds gives that.
    public struct PieceState
    {
        public int Shape;
        public int Rotation;
        public int X;
        public int Y;
        public byte ColorId;

        /// <summary>Width and height of one tetromino block, in grains.</summary>
        public int BlockSize;

        public ushort Mask => Tetromino.Mask(Shape, Rotation);

        public PieceState(int shape, int rotation, int x, int y, byte colorId, int blockSize)
        {
            // A zero block size makes every grain loop unreachable, which reads as "collides with
            // nothing" and stamps nothing: a piece legal everywhere that silently does not exist.
            if (blockSize < 1)
                throw new ArgumentOutOfRangeException(nameof(blockSize), blockSize,
                    "A piece needs a block size of at least one grain.");

            Shape = shape;
            Rotation = rotation;
            X = x;
            Y = y;
            ColorId = colorId;
            BlockSize = blockSize;
        }

        // The ceiling counts as a collision because Stamp writes through SetCell, which silently
        // drops out-of-bounds grains. Treating cells above the grid as free once meant a piece
        // released near the top showed a full ghost and then lost half of itself on landing.
        public bool Collides(in SandGrid grid)
        {
            var mask = Tetromino.Mask(Shape, Rotation);

            for (var by = 0; by < Tetromino.BoxWidth; by++)
            for (var bx = 0; bx < Tetromino.BoxWidth; bx++)
            {
                if (!Tetromino.IsFilled(mask, bx, by)) continue;

                var originX = X + bx * BlockSize;
                var originY = Y + by * BlockSize;

                for (var gy = originY; gy < originY + BlockSize; gy++)
                {
                    if (gy < 0) return true;
                    if (gy >= grid.Height) return true;

                    for (var gx = originX; gx < originX + BlockSize; gx++)
                    {
                        if (gx < 0 || gx >= grid.Width) return true;
                        if (grid.Get(gx, gy) != SandGrid.Empty) return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Turns the piece into sand, through SetCell so the chunk map and grain count
        /// stay correct.</summary>
        public void Stamp(SandSimulation simulation)
        {
            var mask = Mask;

            for (var by = 0; by < Tetromino.BoxWidth; by++)
            for (var bx = 0; bx < Tetromino.BoxWidth; bx++)
            {
                if (!Tetromino.IsFilled(mask, bx, by)) continue;

                var originX = X + bx * BlockSize;
                var originY = Y + by * BlockSize;

                for (var gy = originY; gy < originY + BlockSize; gy++)
                for (var gx = originX; gx < originX + BlockSize; gx++)
                    simulation.SetCell(gx, gy, ColorId);
            }
        }

        public void GetGrainBounds(out int xMin, out int yMin, out int xMax, out int yMax)
        {
            var mask = Mask;
            xMin = int.MaxValue; yMin = int.MaxValue;
            xMax = int.MinValue; yMax = int.MinValue;

            for (var by = 0; by < Tetromino.BoxWidth; by++)
            for (var bx = 0; bx < Tetromino.BoxWidth; bx++)
            {
                if (!Tetromino.IsFilled(mask, bx, by)) continue;

                var x0 = X + bx * BlockSize;
                var y0 = Y + by * BlockSize;

                if (x0 < xMin) xMin = x0;
                if (y0 < yMin) yMin = y0;
                if (x0 + BlockSize - 1 > xMax) xMax = x0 + BlockSize - 1;
                if (y0 + BlockSize - 1 > yMax) yMax = y0 + BlockSize - 1;
            }
        }
    }
}
