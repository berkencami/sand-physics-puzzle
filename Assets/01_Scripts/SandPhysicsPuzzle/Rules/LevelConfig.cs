using SandPhysicsPuzzle.Core;
using UnityEngine;

namespace SandPhysicsPuzzle.Rules
{
    // One asset on purpose. BlockSize and DangerLineY are arithmetic on the grid, and splitting
    // them off let a danger line sit above the ceiling or a block size the width could not divide.
    [CreateAssetMenu(fileName = "LevelConfig", menuName = "Sand Physics Puzzle/Level Config")]
    public sealed class LevelConfig : ScriptableObject
    {
        [Header("Grid")]
        [Tooltip("Grid width in sand grains.")]
        [Range(32, 512)] public int GridWidth = 120;

        [Tooltip("Grid height in sand grains.")]
        [Range(32, 1024)] public int GridHeight = 168;

        [Tooltip("Grains per tetromino block. GridWidth / BlockSize is the board's width in blocks.")]
        [Range(4, 32)] public int BlockSize = 12;

        [Tooltip("Grid row that ends the run once settled sand rests on it. A piece is 4 blocks, " +
                 "so 576 grains at BlockSize 12, and sand holds its angle of repose rather than " +
                 "spreading flat -- one piece piles into a cone roughly 24 rows tall. Leave headroom.")]
        public int DangerLineY = 132;

        [Header("Simulation")]
        [Tooltip("Fixed simulation rate (Hz). Independent of the render frame rate.")]
        [Range(30, 120)] public int SimHz = 60;

        [Tooltip("Sand sub-steps per simulation tick. Controls how fast sand falls.")]
        [Range(1, 4)] public int SubStepsPerTick = 2;

        [Tooltip("Maximum ticks caught up in a single frame (spiral-of-death guard).")]
        [Range(1, 8)] public int MaxTicksPerFrame = 3;

        [Tooltip("When disabled every step scans the whole grid (A/B performance measurement).")]
        public bool UseChunkCulling = true;

        [Header("Appearance")]
        public Color BackgroundColor = new Color(0.05f, 0.05f, 0.07f, 1f);

        [Tooltip("Grain colors. Palette ids start at 1; index 0 is the background.")]
        public Color[] SandColors =
        {
            new Color(0.91f, 0.30f, 0.33f),
            new Color(0.98f, 0.75f, 0.23f),
            new Color(0.35f, 0.78f, 0.45f),
            new Color(0.29f, 0.60f, 0.94f),
            new Color(0.70f, 0.44f, 0.89f),
            new Color(0.95f, 0.35f, 0.72f),
        };

        [Tooltip("Per-grain brightness deviation; this is what gives the sand its texture.")]
        [Range(0, 64)] public int GrainJitter = 22;

        [Tooltip("Color used for grains flagged with SandGrid.ClearingBit.")]
        public Color ClearFlashColor = Color.white;

        [Tooltip("Ticks a completed span stays frozen and flashing before it is deleted.")]
        [Range(1, 60)] public int FlashTicks = 9;

        [Header("Colour draw")]
        [Tooltip("Chance the next piece repeats the previous colour. Raise it if wall-to-wall " +
                 "spans feel unreachable, lower it if the board turns monochrome. This and " +
                 "BoardBias are the difficulty, far more than grid size is.")]
        [Range(0f, 1f)] public float StickChance = 0.22f;

        [Tooltip("How strongly the deal follows the colours already on the board.")]
        [Range(0f, 1f)] public float BoardBias = 0.32f;

        /// <summary>Number of usable colors (ids 1..ColorCount).</summary>
        public int ColorCount => SandColors != null ? Mathf.Min(SandColors.Length, SandGrid.MaxColorId) : 0;

        public float SimDeltaTime => 1f / Mathf.Max(1, SimHz);

        // The only place the wrap rule lives. The renderer builds its palette from this and the
        // tray colours its pieces with it, so a piece and the sand it becomes cannot disagree.
        public Color SandColor(int colorId)
        {
            var count = ColorCount;

            // Magenta rather than a plausible colour: an id with no palette behind it is a
            // configuration mistake and should look like one.
            if (count <= 0 || colorId < 1) return Color.magenta;

            return SandColors[(colorId - 1) % count];
        }

        // Floored at one because ColorPicker rejects zero and this runs on the boot path: an
        // empty palette should surface as a warning on the asset, not an exception at startup.
        public TurnLoopSettings CreateTurnLoopSettings(uint seed) => new TurnLoopSettings
        {
            Seed = seed,
            ColorCount = Mathf.Max(1, ColorCount),
            BlockSize = BlockSize,
            DangerLineY = DangerLineY,
            FlashTicks = FlashTicks,
            StickChance = StickChance,
            BoardBias = BoardBias,
        };

#if UNITY_EDITOR
        // At edit time, rather than as a board that quietly never ends.
        private void OnValidate()
        {
            if (DangerLineY >= GridHeight)
            {
                Debug.LogWarning(
                    $"{name}: DangerLineY {DangerLineY} is outside a {GridHeight}-row grid, " +
                    "so the run can never end. Clamping.", this);
                DangerLineY = GridHeight - 1;
            }

            if (DangerLineY < 1) DangerLineY = 1;

            if (ColorCount == 0)
                Debug.LogWarning($"{name}: SandColors is empty, so every piece would be the same " +
                                 "colour and nothing could ever span the board.", this);

            if (GridWidth % BlockSize != 0)
                Debug.LogWarning(
                    $"{name}: GridWidth {GridWidth} is not a multiple of BlockSize {BlockSize}, " +
                    $"so the rightmost {GridWidth % BlockSize} grains are less than a block wide.",
                    this);

            // The check above is about a stub last column; this one is about the board being
            // unplayable outright, which is what TurnLoop refuses to be built on.
            var pieceSpan = Tetromino.BoxWidth * BlockSize;

            if (GridWidth < pieceSpan || GridHeight < pieceSpan)
                Debug.LogWarning(
                    $"{name}: BlockSize {BlockSize} makes a piece {pieceSpan} grains across, which " +
                    $"does not fit a {GridWidth}x{GridHeight} grid. No run can start on this " +
                    "config. Lower BlockSize or raise the grid.", this);
        }
#endif
    }
}
