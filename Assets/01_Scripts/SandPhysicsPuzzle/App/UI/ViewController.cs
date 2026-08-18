using UnityEngine;
using UnityEngine.UI;

namespace SandPhysicsPuzzle.App.UI
{
    // Each screen carries its own Canvas so views can be sorted by stack position, and its own
    // GraphicRaycaster because of it: a Graphic binds to the nearest enabled Canvas above it, and
    // a raycaster only ever hits Graphics on its own Canvas. Without one, enabling a view would
    // put its buttons out of the root raycaster's reach and silently kill every click.
    [RequireComponent(typeof(Canvas), typeof(CanvasGroup), typeof(GraphicRaycaster))]
    public abstract class ViewController : MonoBehaviour
    {
        [SerializeField] private Canvas _Canvas;
        [SerializeField] private CanvasGroup _CanvasGroup;

        public bool IsVisible { get; private set; }

        protected abstract void Initialize();

        protected virtual void OnShow() { }

        protected virtual void OnHide() { }

        internal virtual void Internal_SetData(object data) { }

        internal void Internal_Initialize()
        {
            if (!_Canvas) _Canvas = GetComponent<Canvas>();
            if (!_CanvasGroup) _CanvasGroup = GetComponent<CanvasGroup>();

            _Canvas.overrideSorting = true;

            // RoundUI's shader reads the corner radii, falloff and border out of TEXCOORD1..3, and
            // a Canvas drops every channel it is not told to send. Without this the extra vertex
            // data never reaches the GPU and rounded panels render as a few stray edge slivers --
            // with no error anywhere, because nothing actually failed.
            _Canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1
                                                | AdditionalCanvasShaderChannels.TexCoord2
                                                | AdditionalCanvasShaderChannels.TexCoord3;

            _Canvas.enabled = false;
            IsVisible = false;

            Initialize();
        }

        internal void Internal_Show()
        {
            IsVisible = true;
            _Canvas.enabled = true;
            OnShow();
        }

        internal void Internal_Hide()
        {
            IsVisible = false;
            _Canvas.enabled = false;
            OnHide();
        }

        internal void SetSortingOrder(int order) => _Canvas.sortingOrder = order;

        internal void SetInteractable(bool value) => _CanvasGroup.blocksRaycasts = value;
    }

    public abstract class ViewController<T> : ViewController
    {
        protected T ViewData { get; private set; }

        internal override void Internal_SetData(object data)
        {
            if (data is T typed) ViewData = typed;
            else Debug.LogError($"{GetType().Name} expects {typeof(T).Name}, got {data?.GetType().Name ?? "null"}.");
        }
    }
}
