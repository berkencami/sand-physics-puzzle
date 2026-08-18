using UnityEngine;

namespace SandPhysicsPuzzle.App
{
    // The one object that says "this scene is the game", and the only place real time enters it.
    // Everything below this counts ticks. The harness scene simply does not contain it.
    [DisallowMultipleComponent]
    public sealed class GameBootstrap : MonoBehaviour
    {
        private void Awake() => GameManager.Boot();

        private void Update() => GameManager.Advance(Time.deltaTime);

        // The only teardown hook needed: Unity raises it on quit and on leaving play mode.
        private void OnDestroy() => GameManager.Shutdown();
    }
}
