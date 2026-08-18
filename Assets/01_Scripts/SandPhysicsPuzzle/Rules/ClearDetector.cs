using System;
using SandPhysicsPuzzle.Core;
using Unity.Collections;
using Unity.Jobs;

namespace SandPhysicsPuzzle.Rules
{
    // Two phases so the caller owns the timing: MarkClears flags spanning regions and freezes
    // them, the caller waits out the flash, CommitClears deletes them. Keeping the wait outside
    // means no animation timing lives in the mechanic, and cascades fall out for free -- the pile
    // collapses onto the gap and the next MarkClears sees whatever that formed.
    //
    // Every buffer is allocated once and reused, so a clear costs no allocation.
    public sealed class ClearDetector : IDisposable
    {
        private NativeArray<int> _visitStamp;
        private NativeList<int> _stack;
        private NativeList<int> _component;
        private NativeList<int> _flagged;

        private readonly int _width;
        private readonly int _height;
        private int _stamp;

        public ClearDetector(int width, int height)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentException($"Invalid grid size {width}x{height}");

            _width = width;
            _height = height;

            _visitStamp = new NativeArray<int>(width * height, Allocator.Persistent,
                NativeArrayOptions.ClearMemory);
            _stack = new NativeList<int>(256, Allocator.Persistent);
            _component = new NativeList<int>(256, Allocator.Persistent);
            _flagged = new NativeList<int>(256, Allocator.Persistent);
        }

        // A no-op while flags are pending. The job rebuilds its list from scratch and skips grains
        // that are already flagged, so a second call would lose track of them for good: frozen
        // with ClearingBit set forever, solid, invisible to everything, and painted as a permanent
        // white band. Commit or cancel before marking again.
        public int MarkClears(SandSimulation simulation)
        {
            if (_flagged.IsCreated && _flagged.Length > 0) return 0;

            if (simulation.Width != _width || simulation.Height != _height)
                throw new ArgumentException(
                    $"Detector is {_width}x{_height} but the simulation is " +
                    $"{simulation.Width}x{simulation.Height}.");

            // 0 is what a freshly cleared VisitStamp holds, so stamps start at 1.
            _stamp++;

            var job = new ClearDetectJob
            {
                Cells = simulation.Grid.Cells,
                VisitStamp = _visitStamp,
                Stack = _stack,
                Component = _component,
                Flagged = _flagged,
                Width = _width,
                Height = _height,
                Stamp = _stamp,
            };

            job.Run();

            // Flagging moves nothing, so the chunk map is deliberately left alone until the commit.
            if (_flagged.Length > 0) simulation.BumpVersion();

            return _flagged.Length;
        }

        public int CommitClears(SandSimulation simulation)
        {
            var count = _flagged.Length;
            if (count == 0) return 0;

            for (var i = 0; i < count; i++)
            {
                var index = _flagged[i];
                simulation.SetCell(index % _width, index / _width, SandGrid.Empty);
            }

            _flagged.Clear();
            return count;
        }

        /// <summary>Drops pending flags without deleting the grains. Used when restarting.</summary>
        public void CancelPending(SandSimulation simulation)
        {
            for (var i = 0; i < _flagged.Length; i++)
            {
                var index = _flagged[i];
                var x = index % _width;
                var y = index / _width;

                // Through SetCell, not into the cells directly, so the grain count and chunk map
                // stay true. Unflagging adds and removes nothing today, which is exactly why
                // writing behind its back would go unnoticed until one day it did.
                simulation.SetCell(x, y, SandGrid.ColorOf(simulation.Grid.Get(x, y)));
            }

            _flagged.Clear();
        }

        public void Dispose()
        {
            if (_visitStamp.IsCreated) _visitStamp.Dispose();
            if (_stack.IsCreated) _stack.Dispose();
            if (_component.IsCreated) _component.Dispose();
            if (_flagged.IsCreated) _flagged.Dispose();
        }
    }
}
