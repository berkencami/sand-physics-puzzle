using SandPhysicsPuzzle.Core;
using SandPhysicsPuzzle.Rules;
using UnityEngine;
using UnityEngine.UI;

namespace SandPhysicsPuzzle.Game
{
    // Draws the sand into a RawImage and turns a touch into a grain coordinate. It steps nothing
    // and owns no game state -- the session hands both over through Bind, which is what keeps
    // this usable from an assembly it may not reference back into.
    //
    // Input arrives from PieceTrayView: a drop starts on a tray cell and ends over the board, so
    // the gesture belongs to whoever owns the piece.
    [RequireComponent(typeof(RawImage))]
    public sealed class SandBoardView : MonoBehaviour
    {
        /// <summary>How far outside the board still counts as a drop, as a fraction of its width.</summary>
        private const float DropMarginFraction = 0.08f;

        [SerializeField] private RawImage _Image;

        [Tooltip("Overlay that previews the piece being dragged. Must not be a raycast target.")]
        [SerializeField] private RawImage _Ghost;

        [Tooltip("Marker for the row that ends the run. Positioned from the loop, not by hand.")]
        [SerializeField] private RectTransform _DangerLine;

        [Tooltip("Frame this board sits inside. Its aspect is driven from the grid at bind time, " +
                 "so a grain comes out exactly square.")]
        [SerializeField] private AspectRatioFitter _Frame;

        [Tooltip("Gap between the board and its frame, as a fraction of the frame width.")]
        [SerializeField, Range(0f, 0.05f)] private float _FrameInset = 0.0131f;

        private LevelConfig _config;
        private SandSimulation _simulation;
        private TurnLoop _loop;
        private SandTexture _texture;

        public TurnLoop Loop => _loop;

        private void Awake()
        {
            if (!_Image) _Image = GetComponent<RawImage>();

            HideGhost();
        }

        /// <summary>Starts drawing and driving the given board. Safe to call again with a new one.</summary>
        public void Bind(SandSimulation simulation, TurnLoop loop, LevelConfig config)
        {
            Unbind();

            // Validated before anything is assigned: a half-bound view passes its own guards and
            // then throws every frame, which is worse than staying inert.
            if (simulation == null || loop == null || config == null)
            {
                Debug.LogError($"{nameof(SandBoardView)}.{nameof(Bind)} needs all three arguments.", this);
                return;
            }

            if (!_Image) _Image = GetComponent<RawImage>();
            if (!_Image)
            {
                Debug.LogError($"{nameof(SandBoardView)} has no RawImage to draw into.", this);
                return;
            }

            _simulation = simulation;
            _loop = loop;
            _config = config;

            _texture = new SandTexture(_config);
            _Image.texture = _texture.Texture;
            _Image.color = Color.white;

            FitToGrid();
            PlaceDangerLine();
        }

        // A gap in pixels would be taken off after the frame was fitted, leaving the sand a
        // different ratio than the frame was given -- and a different one on every screen size.
        // Anchoring it makes it proportional, which leaves exactly one frame ratio whose remainder
        // is GridWidth : GridHeight. The gap stays because the frame is rounded and the sand is not.
        private void FitToGrid()
        {
            if (!_Frame) return;

            var grid = (float)_simulation.Width / _simulation.Height;
            var inset = Mathf.Clamp(_FrameInset, 0f, 0.45f);

            // Solved from (W - 2p) / (H - 2p) = grid, with the gap p taken as inset * W.
            var frameRatio = grid / (1f - 2f * inset * (1f - grid));
            _Frame.aspectRatio = frameRatio;

            // Scaled by the frame ratio so the gap is the same number of pixels on all four sides.
            var verticalInset = inset * frameRatio;

            var rectTransform = (RectTransform)transform;
            rectTransform.anchorMin = new Vector2(inset, verticalInset);
            rectTransform.anchorMax = new Vector2(1f - inset, 1f - verticalInset);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        // Anchors, not a pixel offset: the aspect fitter resizes the board at runtime and
        // anything measured in pixels would drift the moment it did.
        private void PlaceDangerLine()
        {
            if (!_DangerLine) return;

            var y = (float)_loop.DangerLineY / _simulation.Height;

            _DangerLine.anchorMin = new Vector2(0f, y);
            _DangerLine.anchorMax = new Vector2(1f, y);
            _DangerLine.pivot = new Vector2(0.5f, 0.5f);
            _DangerLine.anchoredPosition = Vector2.zero;
            _DangerLine.sizeDelta = new Vector2(0f, _DangerLine.sizeDelta.y);
        }

        public void Unbind()
        {
            _simulation = null;
            _loop = null;
            _config = null;

            HideGhost();

            if (_Image) _Image.texture = null;

            _texture?.Dispose();
            _texture = null;
        }

        private void LateUpdate() => _texture?.Render(_simulation);

        // The pose comes from the caller rather than being recomputed here: it has to be the exact
        // PieceState TryPlace will stamp. A ghost drawn from a parallel calculation can lie.
        public void ShowGhost(Texture2D shape, in PieceState piece, Color color, bool legal)
        {
            if (!_Ghost || _simulation == null) return;

            piece.GetGrainBounds(out var xMin, out var yMin, out var xMax, out var yMax);

            var rect = ((RectTransform)transform).rect;
            var scaleX = rect.width / _simulation.Width;
            var scaleY = rect.height / _simulation.Height;

            // Pinned to the board's bottom-left, which is grain (0, 0), so the grain coordinates
            // translate straight into the overlay's position with no pivot arithmetic.
            var ghostRect = (RectTransform)_Ghost.transform;
            ghostRect.anchorMin = Vector2.zero;
            ghostRect.anchorMax = Vector2.zero;
            ghostRect.pivot = Vector2.zero;
            ghostRect.anchoredPosition = new Vector2(xMin * scaleX, yMin * scaleY);
            ghostRect.sizeDelta = new Vector2((xMax - xMin + 1) * scaleX, (yMax - yMin + 1) * scaleY);

            _Ghost.texture = shape;
            _Ghost.color = legal
                ? new Color(color.r, color.g, color.b, 0.8f)
                : new Color(0.95f, 0.25f, 0.25f, 0.55f);
            _Ghost.enabled = true;
        }

        public void HideGhost()
        {
            if (!_Ghost) return;

            _Ghost.enabled = false;

            // The texture belongs to a tray slot, which destroys it on the next deal.
            _Ghost.texture = null;
        }

        // The margin is deliberate: aiming at a wall means sliding the finger off that edge, and
        // TryScreenToGrain is unclamped so that still reads as a drop against it. What this
        // rejects is a release nowhere near the board, which would otherwise clamp onto the floor.
        public bool IsWithinDropRange(Vector2 screenPosition, Camera eventCamera)
        {
            var rectTransform = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform, screenPosition, eventCamera, out var local))
                return false;

            var rect = rectTransform.rect;
            var margin = rect.width * DropMarginFraction;

            return local.x >= rect.xMin - margin && local.x <= rect.xMax + margin
                && local.y >= rect.yMin - margin && local.y <= rect.yMax + margin;
        }

        // Grid row 0 is the floor and a texture's row 0 is its bottom, so no vertical flip.
        public bool TryScreenToGrain(Vector2 screenPosition, Camera eventCamera, out Vector2Int grain)
        {
            grain = default;
            if (_simulation == null) return false;

            var rectTransform = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform, screenPosition, eventCamera, out var local))
                return false;

            var rect = rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f) return false;

            var u = (local.x - rect.xMin) / rect.width;
            var v = (local.y - rect.yMin) / rect.height;

            // Unclamped on purpose: a finger past the edge still reads as a drop against that wall.
            grain = new Vector2Int(
                Mathf.FloorToInt(u * _simulation.Width),
                Mathf.FloorToInt(v * _simulation.Height));
            return true;
        }

        private void OnDestroy() => Unbind();
    }
}
