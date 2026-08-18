using SandPhysicsPuzzle.Core;

namespace SandPhysicsPuzzle.Rules
{
    public static class DangerLine
    {
        // Only the marked row is scanned, which is sound because a settled grain always has
        // support directly beneath it: the step rule tries straight down first and takes a
        // diagonal only when the cell below is filled. By induction a settled grain above the
        // line implies one on it, so nothing can step over unnoticed.
        //
        // "Settled" is load bearing -- falling sand passes straight through -- so the caller has
        // to wait for the board to come to rest and for every cascade to finish first.
        public static bool IsBreached(in SandGrid grid, int lineY)
        {
            if (lineY < 0 || lineY >= grid.Height) return false;

            var start = lineY * grid.Width;
            for (var x = 0; x < grid.Width; x++)
                if (grid.Cells[start + x] != SandGrid.Empty) return true;

            return false;
        }
    }
}
