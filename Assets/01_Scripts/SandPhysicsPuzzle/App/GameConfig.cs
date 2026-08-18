using System;
using SandPhysicsPuzzle.Rules;
using SandPhysicsPuzzle.App.UI;
using UnityEngine;

namespace SandPhysicsPuzzle.App
{
    /// <summary>The one root config, loaded from <c>Resources/GameConfig</c> at boot.</summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Sand Physics Puzzle/Game Config")]
    public sealed class GameConfig : ScriptableObject
    {
        [SerializeField] private ViewController[] _ViewPrefabs = Array.Empty<ViewController>();
        [SerializeField] private Vector2 _ReferenceResolution = new(1080f, 1920f);
        [SerializeField] private LevelConfig _Level;
        [SerializeField] private int _TargetFrameRate = 60;

        public ViewController[] ViewPrefabs => _ViewPrefabs;
        public Vector2 ReferenceResolution => _ReferenceResolution;
        public int TargetFrameRate => _TargetFrameRate;

        /// <summary>The board setup. Play is endless, so there is nothing to progress through.</summary>
        public LevelConfig Level => _Level;
    }
}
