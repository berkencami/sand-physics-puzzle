using SandPhysicsPuzzle.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace SandPhysicsPuzzle.Rules
{
    // Flags every same-colour region connecting both side walls. Connectivity is 8-way: grains
    // touching only at a corner still read as one line of sand to the player.
    //
    // The fill is the expensive part, so it opens with a cheap rejection -- intersect the colours
    // present in column 0 with those in the last column. A colour missing from either wall can
    // never span, and that skips the fill outright on most landings.
    [BurstCompile(CompileSynchronously = true)]
    public struct ClearDetectJob : IJob
    {
        public NativeArray<byte> Cells;

        // Compared against Stamp, so the buffer never needs clearing between runs.
        public NativeArray<int> VisitStamp;

        public NativeList<int> Stack;
        public NativeList<int> Component;

        public NativeList<int> Flagged;

        public int Width;
        public int Height;
        public int Stamp;

        public void Execute()
        {
            Flagged.Clear();

            var lastColumn = Width - 1;
            var leftMask = 0;
            var rightMask = 0;

            for (var y = 0; y < Height; y++)
            {
                var left = Cells[y * Width];
                if (left != SandGrid.Empty && !SandGrid.IsClearing(left))
                    leftMask |= 1 << SandGrid.ColorOf(left);

                var right = Cells[y * Width + lastColumn];
                if (right != SandGrid.Empty && !SandGrid.IsClearing(right))
                    rightMask |= 1 << SandGrid.ColorOf(right);
            }

            var candidates = leftMask & rightMask;
            if (candidates == 0) return;

            for (var y = 0; y < Height; y++)
            {
                var start = y * Width;
                var cell = Cells[start];

                if (cell == SandGrid.Empty || SandGrid.IsClearing(cell)) continue;

                var color = SandGrid.ColorOf(cell);
                if ((candidates & (1 << color)) == 0) continue;
                if (VisitStamp[start] == Stamp) continue;

                if (FloodFill(start, color, lastColumn))
                {
                    for (var i = 0; i < Component.Length; i++)
                    {
                        var index = Component[i];
                        Cells[index] = (byte)(Cells[index] | SandGrid.ClearingBit);
                        Flagged.Add(index);
                    }
                }
            }
        }

        // True when the region reaches the last column. Every visited cell is stamped whether or
        // not the region wins, so a losing one is never explored twice.
        private bool FloodFill(int start, byte color, int lastColumn)
        {
            Stack.Clear();
            Component.Clear();

            Stack.Add(start);
            VisitStamp[start] = Stamp;

            var reachedRight = false;

            while (Stack.Length > 0)
            {
                var index = Stack[Stack.Length - 1];
                Stack.RemoveAt(Stack.Length - 1);
                Component.Add(index);

                var x = index % Width;
                var y = index / Width;

                if (x == lastColumn) reachedRight = true;

                for (var dy = -1; dy <= 1; dy++)
                {
                    var ny = y + dy;
                    if (ny < 0 || ny >= Height) continue;

                    for (var dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;

                        var nx = x + dx;
                        if (nx < 0 || nx >= Width) continue;

                        var neighbour = ny * Width + nx;
                        if (VisitStamp[neighbour] == Stamp) continue;

                        var cell = Cells[neighbour];
                        if (cell == SandGrid.Empty || SandGrid.IsClearing(cell)) continue;
                        if (SandGrid.ColorOf(cell) != color) continue;

                        VisitStamp[neighbour] = Stamp;
                        Stack.Add(neighbour);
                    }
                }
            }

            return reachedRight;
        }
    }
}
