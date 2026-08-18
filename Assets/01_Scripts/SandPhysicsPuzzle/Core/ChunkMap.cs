using System;
using Unity.Collections;

namespace SandPhysicsPuzzle.Core
{
    // 32x32 chunks tracking what has to be scanned next step. Active is read during the step,
    // ActiveNext accumulates marks while it runs, and BeginStep promotes one into the other.
    public struct ChunkMap : IDisposable
    {
        public const int ChunkShift = 5;
        public const int ChunkSize = 1 << ChunkShift;

        public readonly int GridWidth;
        public readonly int GridHeight;
        public readonly int ChunksX;
        public readonly int ChunksY;

        public NativeArray<byte> Active;
        public NativeArray<byte> ActiveNext;

        public const int MaxGridWidth = 64 * ChunkSize;

        // SandStepJob packs dirty chunk columns into one ulong per chunk row, so 64 chunk columns
        // is a hard ceiling. Call before allocating anything else.
        public static void ValidateSize(int gridWidth)
        {
            if (gridWidth > MaxGridWidth)
                throw new ArgumentException($"Grid width too large: {gridWidth} (max {MaxGridWidth})");
        }

        public ChunkMap(int gridWidth, int gridHeight, Allocator allocator)
        {
            ValidateSize(gridWidth);

            GridWidth = gridWidth;
            GridHeight = gridHeight;
            ChunksX = (gridWidth + ChunkSize - 1) >> ChunkShift;
            ChunksY = (gridHeight + ChunkSize - 1) >> ChunkShift;

            Active = new NativeArray<byte>(ChunksX * ChunksY, allocator, NativeArrayOptions.ClearMemory);
            ActiveNext = new NativeArray<byte>(ChunksX * ChunksY, allocator, NativeArrayOptions.ClearMemory);
        }

        public int ChunkCount => ChunksX * ChunksY;

        public void ActivateAll()
        {
            for (var i = 0; i < Active.Length; i++)
            {
                Active[i] = 1;
                ActiveNext[i] = 1;
            }
        }

        // For edits made outside the step job: a piece landing, a clear, the debug brush. Wakes
        // the cell's 3x3 neighbourhood so a grain can fall diagonally into a sleeping chunk.
        public void MarkCell(int x, int y)
        {
            var x0 = x - 1; if (x0 < 0) x0 = 0;
            var y0 = y - 1; if (y0 < 0) y0 = 0;
            var x1 = x + 1; if (x1 >= GridWidth) x1 = GridWidth - 1;
            var y1 = y + 1; if (y1 >= GridHeight) y1 = GridHeight - 1;

            MarkChunkRange(x0 >> ChunkShift, x1 >> ChunkShift, y0 >> ChunkShift, y1 >> ChunkShift);
        }

        public void MarkRect(int xMin, int yMin, int xMax, int yMax)
        {
            if (xMin > xMax || yMin > yMax) return;

            var x0 = xMin - 1; if (x0 < 0) x0 = 0;
            var y0 = yMin - 1; if (y0 < 0) y0 = 0;
            var x1 = xMax + 1; if (x1 >= GridWidth) x1 = GridWidth - 1;
            var y1 = yMax + 1; if (y1 >= GridHeight) y1 = GridHeight - 1;

            MarkChunkRange(x0 >> ChunkShift, x1 >> ChunkShift, y0 >> ChunkShift, y1 >> ChunkShift);
        }

        private void MarkChunkRange(int cx0, int cx1, int cy0, int cy1)
        {
            for (var cy = cy0; cy <= cy1; cy++)
            {
                var rowBase = cy * ChunksX;
                for (var cx = cx0; cx <= cx1; cx++)
                    ActiveNext[rowBase + cx] = 1;
            }
        }

        public void BeginStep()
        {
            for (var i = 0; i < Active.Length; i++)
            {
                Active[i] = ActiveNext[i];
                ActiveNext[i] = 0;
            }
        }

        // Nothing marked means nothing moved, which is how the simulation knows it is at rest.
        public bool AnyActiveNext()
        {
            for (var i = 0; i < ActiveNext.Length; i++)
                if (ActiveNext[i] != 0) return true;
            return false;
        }

        public int ActiveCount()
        {
            var count = 0;
            for (var i = 0; i < Active.Length; i++)
                if (Active[i] != 0) count++;
            return count;
        }

        public void Dispose()
        {
            if (Active.IsCreated) Active.Dispose();
            if (ActiveNext.IsCreated) ActiveNext.Dispose();
        }
    }
}
