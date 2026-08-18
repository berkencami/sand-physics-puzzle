using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace SandPhysicsPuzzle.Core
{
    // Owns the grid and its chunk map, and drives one step. No MonoBehaviour anywhere in it.
    public sealed class SandSimulation : IDisposable
    {
        // Fields, not properties: both are structs holding a NativeArray, and a property would
        // hand out a copy whose Dispose frees the buffer while the original still claims it.
        // Read freely; write through this class so GrainCount and the chunk map stay true.
        public SandGrid Grid;
        public ChunkMap Chunks;

        public int StepCount { get; private set; }

        /// <summary>Bumped when the grid may have changed, so the renderer can skip a redraw.</summary>
        public int Version { get; private set; }

        /// <summary>Non-empty cells, tracked incrementally. A step conserves mass, so only the
        /// writing entry points adjust it.</summary>
        public int GrainCount { get; private set; }

        public bool UseChunkCulling = true;

        public int Width => Grid.Width;
        public int Height => Grid.Height;

        public SandSimulation(int width, int height)
        {
            // Before anything is allocated, so a rejected size cannot leak the grid.
            ChunkMap.ValidateSize(width);

            Grid = new SandGrid(width, height, Allocator.Persistent);
            Chunks = new ChunkMap(width, height, Allocator.Persistent);
            Chunks.ActivateAll();
        }

        public void Step()
        {
            Chunks.BeginStep();

            var job = new SandStepJob
            {
                Cells = Grid.Cells,
                ChunkActive = Chunks.Active,
                ChunkActiveNext = Chunks.ActiveNext,
                Width = Grid.Width,
                Height = Grid.Height,
                ChunksX = Chunks.ChunksX,
                ChunksY = Chunks.ChunksY,
                Step = StepCount,
                UseCulling = (byte)(UseChunkCulling ? 1 : 0),
            };

            // Order dependent, so Burst-compiled on this thread rather than scheduled.
            job.Run();

            StepCount++;

            // A grain that moves always dirties its chunk, so an empty ActiveNext means nothing did.
            if (Chunks.AnyActiveNext()) Version++;
        }

        /// <summary>For edits that repaint without moving anything, such as flagging a clear.</summary>
        public void BumpVersion() => Version++;

        public void Reset()
        {
            Grid.ClearAll();
            Chunks.ActivateAll();
            StepCount = 0;
            GrainCount = 0;
            Version++;
        }

        public void SetCell(int x, int y, byte value)
        {
            if (!Grid.InBounds(x, y)) return;

            var previous = Grid.Get(x, y);
            if (previous == value) return;

            if (previous == SandGrid.Empty) GrainCount++;
            else if (value == SandGrid.Empty) GrainCount--;

            Grid.Set(x, y, value);
            Chunks.MarkCell(x, y);
            Version++;
        }

        /// <summary>Debug brush: scatter sand across a circular area.</summary>
        public void PaintCircle(int centerX, int centerY, int radius, byte colorId,
                                ref Random random, float density = 0.7f)
        {
            var xMin = math.max(0, centerX - radius);
            var xMax = math.min(Grid.Width - 1, centerX + radius);
            var yMin = math.max(0, centerY - radius);
            var yMax = math.min(Grid.Height - 1, centerY + radius);
            var radiusSquared = radius * radius;

            for (var y = yMin; y <= yMax; y++)
            {
                var dy = y - centerY;
                for (var x = xMin; x <= xMax; x++)
                {
                    var dx = x - centerX;
                    if (dx * dx + dy * dy > radiusSquared) continue;
                    if (random.NextFloat() > density) continue;

                    var previous = Grid.Get(x, y);
                    if (previous == colorId) continue;

                    // colorId 0 makes the brush an eraser, so both directions count.
                    if (previous == SandGrid.Empty) GrainCount++;
                    else if (colorId == SandGrid.Empty) GrainCount--;

                    Grid.Set(x, y, colorId);
                }
            }

            Chunks.MarkRect(xMin, yMin, xMax, yMax);
            Version++;
        }

        public void Dispose()
        {
            Grid.Dispose();
            Chunks.Dispose();
        }
    }
}
