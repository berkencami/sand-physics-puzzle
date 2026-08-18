using System;
using SandPhysicsPuzzle.Core;

namespace SandPhysicsPuzzle.Rules
{
    public enum PreviewResult
    {
        /// <summary>Nothing to draw: the slot is empty or the board is not taking input.</summary>
        NoPiece,

        /// <summary>A real pose that would bury sand. Draw it as rejected; a drop here fails.</summary>
        Blocked,

        /// <summary>Where the piece would land if dropped now.</summary>
        Ok,
    }

    public enum TurnPhase
    {
        AwaitingPlacement,
        Settling,
        Flashing,
        GameOver,
    }

    // The whole turn cycle, engine free: the tray deals, the player drops, the sand settles,
    // spans flash and clear, what is left falls and may clear again, and the danger line ends it.
    //
    // Owns the tray, the colour picker, the detector and the score, but not the simulation --
    // the caller creates and disposes that. Time is counted in Tick calls, not seconds.
    public sealed class TurnLoop : IDisposable
    {
        private readonly SandSimulation _simulation;
        private readonly ClearDetector _detector;
        private readonly ColorPicker _colors;
        private readonly PieceTray _tray;
        private readonly ScoreSystem _score;

        private readonly int _blockSize;
        private readonly int _dangerLineY;
        private readonly int _flashTicks;

        // Sized once so the stuck check allocates nothing.
        private readonly int[] _columnHeights;

        private int _flashTicksLeft;

        /// <summary>Grains cleared by the wave that just committed, for the caller's effects.</summary>
        public event Action<int> Cleared;

        public event Action GameOver;
        public event Action TrayRefilled;

        public TurnPhase Phase { get; private set; }
        public int DangerLineY => _dangerLineY;

        // Read through here rather than handed out: a caller holding the PieceTray could consume a
        // slot with no placement, or reset the score mid-run, and nothing would notice.
        public int FilledSlotCount => _tray.FilledCount;
        public TraySlot GetSlot(int index) => _tray.GetSlot(index);

        public int Score => _score.Score;
        public int BestCombo => _score.BestCombo;
        public int GrainsCleared => _score.GrainsCleared;

        public bool AcceptsInput => Phase == TurnPhase.AwaitingPlacement;

        public TurnLoop(SandSimulation simulation, in TurnLoopSettings settings)
        {
            _simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));

            // IsBreached returns false for a row outside the grid, so an out-of-range line does
            // not fail loudly: it produces a run that simply can never end.
            if (settings.DangerLineY < 1 || settings.DangerLineY >= simulation.Height)
                throw new ArgumentOutOfRangeException(nameof(settings), settings.DangerLineY,
                    $"The danger line must be inside a {simulation.Height}-row grid.");

            _blockSize = settings.BlockSize;
            _dangerLineY = settings.DangerLineY;
            _flashTicks = Math.Max(1, settings.FlashTicks);

            _columnHeights = new int[simulation.Width];
            _detector = new ClearDetector(simulation.Width, simulation.Height);
            _colors = new ColorPicker(settings.Seed, settings.ColorCount,
                settings.StickChance, settings.BoardBias);
            _tray = new PieceTray(settings.Seed, _colors);
            _score = new ScoreSystem();

            _tray.Refill(_simulation.Grid);
            Phase = TurnPhase.AwaitingPlacement;

            // An empty board is the easiest there will ever be, so a tray that cannot be played on
            // one never will be. It matters because the other stuck check runs from Tick, and Tick
            // does nothing while awaiting a placement: an unplayable board would refuse every drop
            // forever without ever ending. That is a config error, so it throws.
            if (!AnyPieceFits())
            {
                // Nothing will hold this instance, so nothing else can free the detector's buffers.
                _detector.Dispose();

                throw new ArgumentException(
                    $"No tetromino fits a {simulation.Width}x{simulation.Height} grid at block " +
                    $"size {_blockSize}: a piece spans {Tetromino.BoxWidth * _blockSize} grains.",
                    nameof(settings));
            }
        }

        // The ghost and the drop both go through here, which is what stops a preview disagreeing
        // with what happens on release. Blocked means the pose is real and should be drawn as
        // rejected; NoPiece means there is nothing to draw and piece is meaningless.
        public PreviewResult Preview(int slotIndex, int grainX, int grainY, out PieceState piece)
        {
            piece = default;

            if (!AcceptsInput) return PreviewResult.NoPiece;
            if (slotIndex < 0 || slotIndex >= PieceTray.SlotCount) return PreviewResult.NoPiece;

            var slot = _tray.GetSlot(slotIndex);
            if (!slot.IsFilled) return PreviewResult.NoPiece;

            piece = slot.ToPiece(0, 0, _blockSize);
            piece = PlacementCheck.CenterAt(piece, grainX, grainY);
            piece = PlacementCheck.Clamp(_simulation.Grid, piece);

            return PlacementCheck.IsLegal(_simulation.Grid, piece)
                ? PreviewResult.Ok
                : PreviewResult.Blocked;
        }

        /// <summary>A rejected drop leaves the board and the tray untouched, so the caller can
        /// animate the piece back without undoing anything.</summary>
        public bool TryPlace(int slotIndex, int grainX, int grainY)
        {
            if (Preview(slotIndex, grainX, grainY, out var piece) != PreviewResult.Ok) return false;

            piece.Stamp(_simulation);
            _tray.TryConsume(slotIndex);

            Phase = TurnPhase.Settling;
            return true;
        }

        /// <summary>Cheap while awaiting input: a settled board is not stepped at all.</summary>
        public void Tick(int subSteps)
        {
            switch (Phase)
            {
                case TurnPhase.Settling:
                    TickSettling(subSteps);
                    break;

                case TurnPhase.Flashing:
                    TickFlashing();
                    break;
            }
        }

        private void TickSettling(int subSteps)
        {
            // Version only moves when a grain did, so an unchanged version is the settle signal.
            var versionBefore = _simulation.Version;
            for (var i = 0; i < subSteps; i++) _simulation.Step();
            if (_simulation.Version != versionBefore) return;

            if (_detector.MarkClears(_simulation) > 0)
            {
                _flashTicksLeft = _flashTicks;
                Phase = TurnPhase.Flashing;
                return;
            }

            EndChain();
        }

        private void TickFlashing()
        {
            if (--_flashTicksLeft > 0) return;

            var cleared = _detector.CommitClears(_simulation);
            _score.RegisterClear(cleared);
            Cleared?.Invoke(cleared);

            // What was resting on the cleared span now falls, and may complete another.
            Phase = TurnPhase.Settling;
        }

        // The lose check belongs here and nowhere earlier: mid-cascade the pile is still
        // collapsing and would read as breached when it is not.
        private void EndChain()
        {
            _score.EndChain();

            if (DangerLine.IsBreached(_simulation.Grid, _dangerLineY))
            {
                End();
                return;
            }

            if (_tray.IsExhausted)
            {
                _tray.Refill(_simulation.Grid);
                TrayRefilled?.Invoke();
            }

            // After the refill, so the fresh set gets its say before the run is called.
            if (!AnyPieceFits())
            {
                End();
                return;
            }

            Phase = TurnPhase.AwaitingPlacement;
        }

        // The danger line is not the only way to run out of board: a piece is up to four blocks
        // tall while the line leaves room for three, so a pile can sit just under it with nowhere
        // to put an upright I. Without this the run does not end, it stops.
        private bool AnyPieceFits()
        {
            PlacementCheck.MeasureColumns(_simulation.Grid, _columnHeights);

            for (var i = 0; i < PieceTray.SlotCount; i++)
            {
                if (PlacementCheck.HasLegalPlacement(
                        _simulation.Grid, _tray.GetSlot(i), _blockSize, _columnHeights))
                    return true;
            }

            return false;
        }

        private void End()
        {
            Phase = TurnPhase.GameOver;
            GameOver?.Invoke();
        }

        /// <summary>Back to a fresh board, keeping the same colour stream.</summary>
        public void Reset()
        {
            _detector.CancelPending(_simulation);
            _simulation.Reset();

            _colors.Reset();
            _tray.Clear();
            _score.Reset();

            _flashTicksLeft = 0;
            _tray.Refill(_simulation.Grid);
            Phase = TurnPhase.AwaitingPlacement;
        }

        public void Dispose() => _detector.Dispose();
    }
}
