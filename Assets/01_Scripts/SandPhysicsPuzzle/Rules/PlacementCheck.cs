using SandPhysicsPuzzle.Core;

namespace SandPhysicsPuzzle.Rules
{
    // CenterAt, then Clamp, then IsLegal, in that order -- centring after clamping would undo the
    // clamp. TurnLoop.Preview is the only place that sequences them, and both the ghost and the
    // drop go through it, which is what keeps the two from ever disagreeing.
    //
    // Clamp only moves, IsLegal only rejects. Neither nudges a pose the player was already shown.
    public static class PlacementCheck
    {
        public static bool IsLegal(in SandGrid grid, in PieceState piece) => !piece.Collides(grid);

        // Row the sand stops at per column, 0 for an empty one. Stopping at the first gap is exact
        // because a settled column has no holes -- same induction DangerLine relies on -- and it
        // costs the height of the pile rather than of the board. Meaningless mid-fall.
        public static void MeasureColumns(in SandGrid grid, int[] columnHeights)
        {
            for (var x = 0; x < grid.Width; x++)
            {
                var y = 0;
                while (y < grid.Height && grid.Get(x, y) != SandGrid.Empty) y++;
                columnHeights[x] = y;
            }
        }

        // The column heights only propose a candidate row; IsLegal gives the answer. So if the
        // no-gaps assumption ever stopped holding, the worst case is a placement this fails to
        // find -- never a run ended while a legal move was still on the board.
        public static bool HasLegalPlacement(in SandGrid grid, in TraySlot slot, int blockSize,
            int[] columnHeights)
        {
            if (!slot.IsFilled) return false;

            var mask = Tetromino.Mask(slot.Shape, slot.Rotation);
            Tetromino.Bounds(mask, out var bxMin, out var byMin, out var bxMax, out var byMax);

            var minX = -bxMin * blockSize;
            var maxX = grid.Width - (bxMax + 1) * blockSize;
            var ceiling = grid.Height - (byMax + 1) * blockSize;

            for (var x = minX; x <= maxX; x++)
            {
                // Lowest origin clearing both the floor and every column the piece covers.
                var y = -byMin * blockSize;

                for (var by = 0; by < Tetromino.BoxWidth; by++)
                for (var bx = 0; bx < Tetromino.BoxWidth; bx++)
                {
                    if (!Tetromino.IsFilled(mask, bx, by)) continue;

                    var columnStart = x + bx * blockSize;
                    for (var c = columnStart; c < columnStart + blockSize; c++)
                    {
                        var rest = columnHeights[c] - by * blockSize;
                        if (rest > y) y = rest;
                    }
                }

                if (y > ceiling) continue;

                var piece = slot.ToPiece(x, y, blockSize);
                if (IsLegal(grid, piece)) return true;
            }

            return false;
        }

        // The ceiling is clamped rather than left open: pouring from as high as possible is a real
        // tactic and clamping keeps it, while removing the pose that hangs off the top, which
        // Stamp cannot write and would silently truncate. The floor is clamped last so a piece
        // taller than the grid gets rejected by IsLegal instead of quietly cut in half.
        public static PieceState Clamp(in SandGrid grid, PieceState piece)
        {
            piece.GetGrainBounds(out var xMin, out var yMin, out var xMax, out var yMax);

            var leftOffset = xMin - piece.X;
            var rightOffset = xMax - piece.X;
            var bottomOffset = yMin - piece.Y;
            var topOffset = yMax - piece.Y;

            var minX = -leftOffset;
            var maxX = grid.Width - 1 - rightOffset;
            if (piece.X < minX) piece.X = minX;
            if (piece.X > maxX) piece.X = maxX;

            var maxY = grid.Height - 1 - topOffset;
            if (piece.Y > maxY) piece.Y = maxY;

            var minY = -bottomOffset;
            if (piece.Y < minY) piece.Y = minY;

            return piece;
        }

        // Anchors on the visible shape, not the 4x4 box: two rotations can sit in different
        // corners of that box, so anchoring on the origin would make the piece jump under a finger.
        public static PieceState CenterAt(PieceState piece, int centerX, int centerY)
        {
            piece.GetGrainBounds(out var xMin, out var yMin, out var xMax, out var yMax);

            var currentX = (xMin + xMax) / 2;
            var currentY = (yMin + yMax) / 2;

            piece.X += centerX - currentX;
            piece.Y += centerY - currentY;
            return piece;
        }
    }
}
