using System;
using Unity.Collections;

namespace SandPhysicsPuzzle.Core
{
    // Cell layout: 0 is empty, the low 4 bits are the colour id, bit 0x80 marks a grain that is
    // clearing -- frozen in place but still solid. y = 0 is the floor and y grows upward, which
    // matches Texture2D row order, so rendering needs no vertical flip.
    public struct SandGrid : IDisposable
    {
        public const byte Empty = 0;
        public const byte ClearingBit = 0x80;

        // Keeps ColorOf inside the 16 entry palette whatever a caller stores in the cell.
        public const byte ColorMask = 0x0F;

        public const int MaxColorId = 15;

        public readonly int Width;
        public readonly int Height;
        public NativeArray<byte> Cells;

        public SandGrid(int width, int height, Allocator allocator)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentException($"Invalid grid size {width}x{height}");

            Width = width;
            Height = height;
            Cells = new NativeArray<byte>(width * height, allocator, NativeArrayOptions.ClearMemory);
        }

        public bool InBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

        public byte Get(int x, int y) => Cells[y * Width + x];

        public void Set(int x, int y, byte value) => Cells[y * Width + x] = value;

        public static byte ColorOf(byte cell) => (byte)(cell & ColorMask);

        public static bool IsClearing(byte cell) => (cell & ClearingBit) != 0;

        public void ClearAll()
        {
            for (var i = 0; i < Cells.Length; i++) Cells[i] = Empty;
        }

        public void Dispose()
        {
            if (Cells.IsCreated) Cells.Dispose();
        }
    }
}
