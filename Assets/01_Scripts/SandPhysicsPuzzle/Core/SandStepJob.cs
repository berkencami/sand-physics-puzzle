using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace SandPhysicsPuzzle.Core
{
    // One simulation step, in place, over a single buffer. Rows go from y = 1 upward, so the
    // destination row is always already processed: a grain cannot move twice in a step and two
    // grains cannot land in the same cell, which is what conserves mass without a second buffer
    // or an atomic claim pass. That ordering is also why this is an IJob and not parallel.
    //
    // Scan direction and diagonal preference flip every step so piles do not drift to one side.
    //
    // CompileSynchronously: without it the first frames of a session run as managed IL while
    // Burst compiles in the background, which makes any timing measured there meaningless.
    [BurstCompile(CompileSynchronously = true)]
    public struct SandStepJob : IJob
    {
        public NativeArray<byte> Cells;

        [ReadOnly] public NativeArray<byte> ChunkActive;
        public NativeArray<byte> ChunkActiveNext;

        public int Width;
        public int Height;
        public int ChunksX;
        public int ChunksY;

        public int Step;

        /// <summary>0 disables chunk culling, for A/B measurement against a full scan.</summary>
        public byte UseCulling;

        public void Execute()
        {
            var leftToRight = (Step & 1) == 0;
            var diagonal = leftToRight ? 1 : -1;

            // Dirty columns are collected per chunk row so marking happens once per row rather
            // than once per moved grain.
            //
            // Explicitly ulong, not var: this is a 64-column bitmask, and var would infer int
            // from the literal and silently drop every chunk column past the 31st.
            ulong dirtyColumns = 0;
            var currentChunkY = 0;

            for (var y = 1; y < Height; y++)
            {
                var chunkY = y >> ChunkMap.ChunkShift;
                if (chunkY != currentChunkY)
                {
                    FlushDirty(currentChunkY, dirtyColumns);
                    dirtyColumns = 0;
                    currentChunkY = chunkY;
                }

                var rowBase = y * Width;
                var belowBase = rowBase - Width;
                var chunkRowBase = chunkY * ChunksX;

                for (var i = 0; i < ChunksX; i++)
                {
                    var chunkX = leftToRight ? i : ChunksX - 1 - i;

                    if (UseCulling != 0 && ChunkActive[chunkRowBase + chunkX] == 0)
                        continue;

                    var xStart = chunkX << ChunkMap.ChunkShift;
                    var xEnd = xStart + ChunkMap.ChunkSize;
                    if (xEnd > Width) xEnd = Width;

                    var moved = false;

                    for (var k = xStart; k < xEnd; k++)
                    {
                        var x = leftToRight ? k : (xStart + xEnd - 1 - k);

                        var index = rowBase + x;
                        var cell = Cells[index];

                        // Empty, or frozen in the middle of its clear flash.
                        if (cell == SandGrid.Empty || SandGrid.IsClearing(cell))
                            continue;

                        var below = belowBase + x;
                        if (Cells[below] == 0)
                        {
                            Cells[below] = cell;
                            Cells[index] = 0;
                            moved = true;
                            continue;
                        }

                        var preferredX = x + diagonal;
                        if ((uint)preferredX < (uint)Width && Cells[belowBase + preferredX] == 0)
                        {
                            Cells[belowBase + preferredX] = cell;
                            Cells[index] = 0;
                            moved = true;
                            continue;
                        }

                        var otherX = x - diagonal;
                        if ((uint)otherX < (uint)Width && Cells[belowBase + otherX] == 0)
                        {
                            Cells[belowBase + otherX] = cell;
                            Cells[index] = 0;
                            moved = true;
                        }
                    }

                    if (moved)
                        dirtyColumns |= 1UL << chunkX;
                }
            }

            FlushDirty(currentChunkY, dirtyColumns);
        }

        // The 3x3 neighbourhood, not just the column: a diagonal move can push a grain into the
        // next chunk column and a downward move into the chunk row below.
        private void FlushDirty(int chunkY, ulong dirtyColumns)
        {
            if (dirtyColumns == 0) return;

            var minY = chunkY - 1; if (minY < 0) minY = 0;
            var maxY = chunkY + 1; if (maxY >= ChunksY) maxY = ChunksY - 1;

            for (var chunkX = 0; chunkX < ChunksX; chunkX++)
            {
                if ((dirtyColumns & (1UL << chunkX)) == 0) continue;

                var minX = chunkX - 1; if (minX < 0) minX = 0;
                var maxX = chunkX + 1; if (maxX >= ChunksX) maxX = ChunksX - 1;

                for (var y = minY; y <= maxY; y++)
                {
                    var rowBase = y * ChunksX;
                    for (var x = minX; x <= maxX; x++)
                        ChunkActiveNext[rowBase + x] = 1;
                }
            }
        }
    }
}
