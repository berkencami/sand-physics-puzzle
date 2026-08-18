using SandPhysicsPuzzle.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace SandPhysicsPuzzle.Game
{
    // Straight into the texture's raw pixel memory, one worker per row so there is no per-pixel
    // division.
    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
    public struct SandRenderJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<byte> Cells;

        /// <summary>Index 0 is the background, 1..15 are grain colors.</summary>
        [ReadOnly] public NativeArray<Color32> Palette;

        [NativeDisableParallelForRestriction]
        public NativeArray<Color32> Pixels;

        public int Width;
        public int JitterStrength;
        public Color32 FlashColor;

        public void Execute(int y)
        {
            var rowBase = y * Width;

            for (var x = 0; x < Width; x++)
            {
                var cell = Cells[rowBase + x];

                // Empty is the background, a flagged grain is the flash colour, and anything else
                // is its palette colour plus the position jitter that gives sand its texture.
                // ColorMask is 4 bits wide, so the palette lookup is always in range.
                Pixels[rowBase + x] = cell == SandGrid.Empty
                    ? Palette[0]
                    : SandGrid.IsClearing(cell)
                        ? FlashColor
                        : Jitter(Palette[SandGrid.ColorOf(cell)], x, y);
            }
        }

        private Color32 Jitter(Color32 color, int x, int y)
        {
            if (JitterStrength == 0) return color;

            var hash = Hash((uint)x, (uint)y);
            // Scale the -32..31 range by JitterStrength
            var offset = ((int)(hash & 63) - 32) * JitterStrength >> 5;

            color.r = (byte)Clamp255(color.r + offset);
            color.g = (byte)Clamp255(color.g + offset);
            color.b = (byte)Clamp255(color.b + offset);
            return color;
        }

        private static int Clamp255(int value) => value < 0 ? 0 : (value > 255 ? 255 : value);

        // The jitter is never stored, only re-derived, which is what keeps it deterministic.
        private static uint Hash(uint x, uint y)
        {
            var hash = x * 73856093u ^ y * 19349663u;
            hash ^= hash >> 13;
            hash *= 0x85EBCA6Bu;
            hash ^= hash >> 16;
            return hash;
        }
    }
}
