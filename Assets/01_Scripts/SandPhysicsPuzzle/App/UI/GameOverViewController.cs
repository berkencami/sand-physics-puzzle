using SandPhysicsPuzzle.App.Session;
using UnityEngine;
using UnityEngine.UI;

namespace SandPhysicsPuzzle.App.UI
{
    // Stacked on top of the gameplay view rather than replacing it: the pile that ended the run is
    // the result being reported. The numbers are read once, not polled -- nothing can change them.
    public sealed class GameOverViewController : ViewController<GameSession>
    {
        [SerializeField] private Text _ScoreLabel;
        [SerializeField] private Text _ComboLabel;
        [SerializeField] private Text _GrainsLabel;
        [SerializeField] private Button _PlayAgainButton;

        protected override void Initialize()
        {
            if (_PlayAgainButton) _PlayAgainButton.onClick.AddListener(PlayAgain);
        }

        protected override void OnShow()
        {
            var loop = ViewData?.Loop;

            if (_ScoreLabel) _ScoreLabel.text = (loop?.Score ?? 0).ToString();
            if (_ComboLabel) _ComboLabel.text = $"Best combo   x{loop?.BestCombo ?? 0}";
            if (_GrainsLabel) _GrainsLabel.text = $"{loop?.GrainsCleared ?? 0} grains cleared";
        }

        // From a button callback, well clear of the tick that ended the run, which is what makes
        // the session's native buffers safe to free here.
        private void PlayAgain() => GameManager.EndRun();
    }
}
