using SandPhysicsPuzzle.Rules;
using UnityEngine;

namespace SandPhysicsPuzzle.Game
{
    // One pixel per block, cropped to the shape. White rather than coloured so one texture serves
    // any colour -- the RawImage tints it. The crop matters: only the I fills its 4x4 box, so
    // drawing the whole box would leave every other shape floating off-centre in its slot.
    public static class PieceTexture
    {
        /// <summary>Builds the silhouette. The caller owns the texture and must destroy it.</summary>
        public static Texture2D Create(int shape, int rotation, out float aspect)
        {
            var mask = Tetromino.Mask(shape, rotation);
            Tetromino.Bounds(mask, out var xMin, out var yMin, out var xMax, out var yMax);

            var width = xMax - xMin + 1;
            var height = yMax - yMin + 1;
            aspect = (float)width / height;

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false, linear: false)
            {
                name = "SandPhysicsPuzzle_Piece",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0,
            };

            var pixels = new Color32[width * height];
            var filled = new Color32(255, 255, 255, 255);
            var empty = new Color32(255, 255, 255, 0);

            // Mask y grows upward and so does a texture's row order, so this needs no flip.
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                pixels[y * width + x] = Tetromino.IsFilled(mask, xMin + x, yMin + y) ? filled : empty;

            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            return texture;
        }
    }
}
