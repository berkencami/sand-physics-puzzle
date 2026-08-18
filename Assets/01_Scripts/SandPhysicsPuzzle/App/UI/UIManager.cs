using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SandPhysicsPuzzle.App.UI
{
    // Views are addressed by type, instantiated on first show and cached after that.
    public static class UIManager
    {
        private static readonly Dictionary<Type, ViewController> _instances = new();
        private static readonly List<ViewController> _stack = new();

        private static ViewController[] _prefabs = Array.Empty<ViewController>();
        private static Canvas _rootCanvas;

        public static void Initialize(ViewController[] prefabs, Vector2 referenceResolution)
        {
            _prefabs = prefabs ?? Array.Empty<ViewController>();

            CreateRootCanvas(referenceResolution);
            EnsureEventSystem();
        }

        public static T Show<T>(object data = null) where T : ViewController
        {
            var view = GetOrCreate<T>();
            if (!view || _stack.Contains(view)) return view;

            if (data != null) view.Internal_SetData(data);

            _stack.Add(view);
            view.Internal_Show();
            Refresh();
            return view;
        }

        public static void HideAll()
        {
            for (var i = _stack.Count - 1; i >= 0; i--) _stack[i].Internal_Hide();

            _stack.Clear();
        }

        /// <summary>Sorting order follows stack position; only the top view takes input.</summary>
        private static void Refresh()
        {
            for (var i = 0; i < _stack.Count; i++)
            {
                _stack[i].SetSortingOrder(i);
                _stack[i].SetInteractable(i == _stack.Count - 1);
            }
        }

        private static T GetOrCreate<T>() where T : ViewController
        {
            var type = typeof(T);

            if (_instances.TryGetValue(type, out var cached) && cached) return (T)cached;

            foreach (var prefab in _prefabs)
            {
                if (prefab is not T match) continue;

                var instance = Object.Instantiate(match, _rootCanvas.transform, false);
                instance.name = type.Name;
                _instances[type] = instance;
                instance.Internal_Initialize();
                return instance;
            }

            Debug.LogError($"No prefab for {type.Name} — add it to GameConfig.ViewPrefabs.");
            return null;
        }

        private static void CreateRootCanvas(Vector2 referenceResolution)
        {
            var go = new GameObject("UIRoot", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Object.DontDestroyOnLoad(go);

            _rootCanvas = go.GetComponent<Canvas>();
            _rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;

            // Match width, not height. The design is portrait, and matching height on a tall phone
            // scales by screenHeight/1920 -- on a 1080x2340 screen that leaves the canvas only ~886
            // reference units wide, so roughly 18% of a 1080-wide layout falls off the sides.
            scaler.matchWidthOrHeight = 0f;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>()) return;

            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
            Object.DontDestroyOnLoad(go);
        }

        /// <summary>Editor only: clears statics when domain reload is disabled.</summary>
        internal static void ResetStatics()
        {
            _instances.Clear();
            _stack.Clear();
            _prefabs = Array.Empty<ViewController>();
            _rootCanvas = null;
        }
    }
}
