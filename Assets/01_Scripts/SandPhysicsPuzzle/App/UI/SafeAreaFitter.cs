using UnityEngine;

namespace SandPhysicsPuzzle.App.UI
{
    // Shrinks itself to Screen.safeArea so everything under it clears the notch and gesture bar.
    // Anything the player reads or touches belongs under one of these; anything that is only
    // colour stays outside and keeps the full bleed.
    //
    // The anchors are fractions of the parent, so this is only correct while that parent covers
    // the whole screen -- which the view root does by construction.
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rectTransform;

        // The applied state, not the current one: it is what tells us a re-fit is needed.
        private Rect _appliedArea;
        private int _appliedWidth;
        private int _appliedHeight;

        private void Awake() => _rectTransform = (RectTransform)transform;

        private void OnEnable() => Apply();

        // Unity raises no event for a safe area change, and it moves on rotation, on a fold, and
        // during the first frames of a cold start. Two comparisons a frame beats being wrong.
        private void Update()
        {
            if (Screen.safeArea == _appliedArea &&
                Screen.width == _appliedWidth && Screen.height == _appliedHeight) return;

            Apply();
        }

        private void Apply()
        {
            var width = Screen.width;
            var height = Screen.height;

            // Zero either way and every anchor below is a division by zero. Happens on the frame
            // an Android app is restored from the background.
            if (width <= 0 || height <= 0) return;

            var area = Screen.safeArea;

            _appliedArea = area;
            _appliedWidth = width;
            _appliedHeight = height;

            _rectTransform.anchorMin = new Vector2(area.xMin / width, area.yMin / height);
            _rectTransform.anchorMax = new Vector2(area.xMax / width, area.yMax / height);
            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;
        }
    }
}
