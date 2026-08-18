using System;
using SandPhysicsPuzzle.Core;
using SandPhysicsPuzzle.Rules;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace SandPhysicsPuzzle.Game
{
    // Grid to Texture2D and nothing else -- no opinion on how it reaches the screen, which is why
    // a SpriteRenderer and a RawImage can share it. Pixels come from GetRawTextureData as a zero
    // copy NativeArray the Burst job writes into: no Color32[], no SetPixels, no allocation.
    public sealed class SandTexture : IDisposable
    {
        private Texture2D _texture;
        private NativeArray<Color32> _palette;

        private readonly int _width;
        private readonly int _height;
        private readonly int _jitterStrength;
        private readonly Color32 _flashColor;

        // Version already on the GPU; -1 means nothing yet.
        private int _renderedVersion = -1;

        public Texture2D Texture => _texture;

        public SandTexture(LevelConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            _width = config.GridWidth;
            _height = config.GridHeight;
            _jitterStrength = config.GrainJitter;
            _flashColor = config.ClearFlashColor;

            // linear:false -> sRGB texture. The project uses Linear color space, so the
            // GPU converts on sample and colors look exactly like in the Inspector.
            _texture = new Texture2D(_width, _height, TextureFormat.RGBA32, mipChain: false, linear: false)
            {
                name = "SandPhysicsPuzzle_Sand",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0,
            };

            BuildPalette(config);
        }

        private void BuildPalette(LevelConfig config)
        {
            // One slot per grain id the cell format can hold, plus the background at 0.
            _palette = new NativeArray<Color32>(SandGrid.MaxColorId + 1, Allocator.Persistent);
            _palette[0] = config.BackgroundColor;

            // Through the config lookup rather than a second copy of the wrap rule: the tray uses
            // the same call, so a piece and the sand it becomes match by construction.
            for (var i = 1; i <= SandGrid.MaxColorId; i++)
                _palette[i] = config.SandColor(i);
        }

        /// <summary>Draws the grid and uploads it to the GPU, unless nothing changed.</summary>
        public void Render(SandSimulation simulation)
        {
            if (_texture == null || simulation == null) return;

            // The jitter is a pure function of position, so an unchanged grid produces the same
            // texture byte for byte. Skipping saves a job pass and a full upload every settled frame.
            if (simulation.Version == _renderedVersion) return;
            _renderedVersion = simulation.Version;

            // Only valid for the scope of this call -- never cache it, re-fetch every frame.
            var pixels = _texture.GetRawTextureData<Color32>();

            var job = new SandRenderJob
            {
                Cells = simulation.Grid.Cells,
                Palette = _palette,
                Pixels = pixels,
                Width = _width,
                JitterStrength = _jitterStrength,
                FlashColor = _flashColor,
            };

            job.Schedule(_height, 8).Complete();

            _texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
        }

        public void Dispose()
        {
            if (_palette.IsCreated) _palette.Dispose();

            if (_texture != null)
            {
                UnityEngine.Object.Destroy(_texture);
                _texture = null;
            }

            _renderedVersion = -1;
        }
    }
}
