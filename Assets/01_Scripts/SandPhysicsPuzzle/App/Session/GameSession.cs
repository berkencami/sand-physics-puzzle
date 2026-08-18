using System;
using SandPhysicsPuzzle.Core;
using SandPhysicsPuzzle.Rules;
using UnityEngine;

namespace SandPhysicsPuzzle.App.Session
{
    // One run: the simulation, the turn loop, and the clock that drives them. Plain C# object.
    //
    // The clock lives here rather than in the view because a run whose sand only settles while
    // something is drawing it stops the moment that view is disabled, rebuilt or absent.
    public sealed class GameSession
    {
        private float _accumulator;

        public LevelConfig Config { get; }

        /// <summary>Ended or torn down. Both, so the single check in Advance covers each.</summary>
        public bool IsFinished { get; private set; }

        /// <summary>The grid. Null if the config could not produce a playable board.</summary>
        public SandSimulation Simulation { get; }

        /// <summary>The turn cycle. Null if the config could not produce a playable board.</summary>
        public TurnLoop Loop { get; }

        public int Score => Loop?.Score ?? 0;

        /// <summary>Raised once, when the run ends. An event rather than a call up into the thing
        /// that owns the run.</summary>
        public event Action Finished;

        public GameSession(LevelConfig config)
        {
            Config = config;

            if (!config)
            {
                Debug.LogError("A run needs a LevelConfig; there is no board to play on.");
                return;
            }

            var simulation = new SandSimulation(config.GridWidth, config.GridHeight)
            {
                UseChunkCulling = config.UseChunkCulling,
            };

            // A fresh seed per run, so two runs do not deal the same pieces.
            var seed = (uint)UnityEngine.Random.Range(int.MinValue, int.MaxValue);

            try
            {
                Loop = new TurnLoop(simulation, config.CreateTurnLoopSettings(seed));
            }
            catch (ArgumentException exception)
            {
                // The config describes a board no piece can be played on. The grid is freed here
                // rather than left behind: with no Loop there is no run for anything to hold, so
                // nothing would ever dispose it.
                simulation.Dispose();

                Debug.LogError($"{config.name} does not describe a playable board, so the run was " +
                               $"not started. {exception.Message}", config);
                return;
            }

            Simulation = simulation;
            Loop.GameOver += Lose;
        }

        // An accumulator rather than FixedUpdate, so the rate is the config's and not the
        // project's physics step, and the step count holds when the frame rate drops.
        public void Advance(float deltaTime)
        {
            if (Loop == null || IsFinished) return;

            var simDeltaTime = Config.SimDeltaTime;
            _accumulator += deltaTime;

            var ticks = 0;
            while (_accumulator >= simDeltaTime && ticks < Config.MaxTicksPerFrame)
            {
                _accumulator -= simDeltaTime;
                ticks++;

                Loop.Tick(Config.SubStepsPerTick);

                // Ticking can end the run, and a handler is free to tear this session down from
                // inside that -- the restart button does exactly that. Both paths set the latch,
                // so neither can leave the rest of this frame stepping a disposed grid.
                if (IsFinished) return;
            }

            // Spiral-of-death guard: drop the surplus after a long hitch but keep the sub-tick
            // remainder, so the simulation phase does not jitter.
            var maxDebt = simDeltaTime * Config.MaxTicksPerFrame;
            if (_accumulator > maxDebt) _accumulator = Mathf.Repeat(_accumulator, simDeltaTime);
        }

        // One-way latch. It reports rather than tears down, because this is reached from inside
        // Tick: freeing the grid here would pull it out from under the call that is running.
        public void Lose()
        {
            if (IsFinished) return;

            IsFinished = true;
            Finished?.Invoke();
        }

        internal void Destroy()
        {
            // Latched before anything is freed, so a caller still holding this cannot step it.
            IsFinished = true;

            if (Loop != null)
            {
                Loop.GameOver -= Lose;
                Loop.Dispose();
            }

            Simulation?.Dispose();
        }
    }
}
