using SandPhysicsPuzzle.Game;
using SandPhysicsPuzzle.App.Session;
using UnityEngine;
using UnityEngine.UI;

namespace SandPhysicsPuzzle.App.UI
{
    // Connects the live session to the board in its own hierarchy. It does not own the session, so
    // leaving this view never destroys it.
    public sealed class GameplayViewController : ViewController<GameSession>
    {
        [SerializeField] private SandBoardView _Board;
        [SerializeField] private PieceTrayView _Tray;
        [SerializeField] private Text _ScoreLabel;
        [SerializeField] private Button _RestartButton;

        private int _shownScore = -1;

        protected override void Initialize()
        {
            if (_RestartButton) _RestartButton.onClick.AddListener(Restart);
            if (!_Board) _Board = GetComponentInChildren<SandBoardView>(true);
            if (!_Tray) _Tray = GetComponentInChildren<PieceTrayView>(true);
        }

        protected override void OnShow()
        {
            _shownScore = -1;

            if (!_Board || ViewData?.Simulation == null) return;

            _Board.Bind(ViewData.Simulation, ViewData.Loop, ViewData.Config);

            // After the board: the tray draws its ghost through the board, so it must not be live
            // before there is a board to draw into.
            if (_Tray) _Tray.Bind(ViewData.Loop, ViewData.Config);
        }

        protected override void OnHide()
        {
            if (_Tray) _Tray.Unbind();
            if (_Board) _Board.Unbind();
        }

        // Polled rather than evented: the score moves at most once per clear, and a label that
        // only rebuilds its string when the number changed costs nothing.
        private void Update()
        {
            if (!IsVisible || !_ScoreLabel || ViewData == null) return;

            var score = ViewData.Score;
            if (score == _shownScore) return;

            _shownScore = score;
            _ScoreLabel.text = score.ToString();
        }

        private void Restart() => GameManager.EndRun();
    }
}
