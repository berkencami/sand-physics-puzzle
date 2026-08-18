using SandPhysicsPuzzle.App.Session;
using SandPhysicsPuzzle.App.UI;
using UnityEngine;

namespace SandPhysicsPuzzle.App
{
    // Loads the config, raises the UI and owns the live run. Play is endless and there is one
    // board, so booting means playing. Nothing here starts itself -- GameBootstrap decides when.
    public static class GameManager
    {
        public static GameConfig Config { get; private set; }

        /// <summary>The live session. Never null for long: a finished run starts the next one.</summary>
        public static GameSession Session { get; private set; }

        /// <summary>Loads the config, raises the UI and starts the first run.</summary>
        public static void Boot()
        {
            Config = Resources.Load<GameConfig>("GameConfig");

            if (!Config)
            {
                Debug.LogError("No GameConfig found at Resources/GameConfig.");
                return;
            }

            Application.targetFrameRate = Config.TargetFrameRate;

            UIManager.Initialize(Config.ViewPrefabs, Config.ReferenceResolution);
            StartRun();
        }

        /// <summary>Steps the live run. Called once a frame by <see cref="GameBootstrap"/>.</summary>
        public static void Advance(float deltaTime) => Session?.Advance(deltaTime);

        // Without this the run's Allocator.Persistent buffers are only ever freed by a game over,
        // so quitting or leaving play mode would leak a whole grid and chunk map.
        public static void Shutdown() => DisposeSession();

        private static void DisposeSession()
        {
            if (Session == null) return;

            Session.Finished -= FinishRun;
            Session.Destroy();
            Session = null;
        }

        public static void StartRun()
        {
            if (Session != null) return;

            var level = Config ? Config.Level : null;
            if (!level)
            {
                Debug.LogError("GameConfig has no level assigned; there is no board to play on.");
                return;
            }

            UIManager.HideAll();

            Session = new GameSession(level);
            Session.Finished += FinishRun;

            UIManager.Show<GameplayViewController>(Session);
        }

        // The session is deliberately left alive: the summary stacks on top of the board, so the
        // pile that ended the run stays behind it. Tearing down is EndRun's job, from a button
        // callback rather than from inside the tick that ended the run.
        private static void FinishRun()
        {
            if (Session == null) return;

            UIManager.Show<GameOverViewController>(Session);
        }

        /// <summary>Ends the run and immediately starts the next one. Called on game over or restart.</summary>
        public static void EndRun()
        {
            if (Session == null) return;

            // Hide first, then dispose. This is reachable from inside a tick, so the board has to
            // be unbound before its native memory goes away.
            UIManager.HideAll();

            DisposeSession();
            StartRun();
        }

#if UNITY_EDITOR
        // With domain reload off the previous run's native buffers survive into this one, so this
        // has to dispose rather than just null the reference.
        [UnityEditor.InitializeOnEnterPlayMode]
        private static void ResetStatics()
        {
            UIManager.ResetStatics();

            DisposeSession();
            Config = null;
        }
#endif
    }
}
