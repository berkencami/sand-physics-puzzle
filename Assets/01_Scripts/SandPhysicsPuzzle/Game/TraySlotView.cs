using SandPhysicsPuzzle.Rules;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SandPhysicsPuzzle.Game
{
    // Draws the piece and forwards the drag; decides nothing. The handlers sit on the cell rather
    // than the piece image because the image is only as big as the silhouette -- a flat I would
    // otherwise leave a touch target one block tall.
    public sealed class TraySlotView : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private RawImage _Image;
        [SerializeField] private AspectRatioFitter _Fitter;

        private PieceTrayView _tray;
        private int _index;
        private Texture2D _texture;
        private Color _color = Color.white;

        /// <summary>The silhouette currently shown, reused by the drag ghost so it is not rebuilt per frame.</summary>
        public Texture2D Texture => _texture;

        internal void Attach(PieceTrayView tray, int index)
        {
            _tray = tray;
            _index = index;
        }

        internal void Show(in TraySlot slot, Color color)
        {
            Release();

            if (!_Image) return;

            if (!slot.IsFilled)
            {
                _Image.enabled = false;
                return;
            }

            _texture = PieceTexture.Create(slot.Shape, slot.Rotation, out var aspect);
            if (_Fitter) _Fitter.aspectRatio = aspect;

            _color = color;
            _Image.texture = _texture;
            _Image.color = color;
            _Image.enabled = true;
        }

        /// <summary>Fades the cell while its piece is the one being dragged over the board.</summary>
        internal void SetDimmed(bool value)
        {
            if (!_Image) return;

            _Image.color = value ? new Color(_color.r, _color.g, _color.b, 0.2f) : _color;
        }

        public void OnBeginDrag(PointerEventData eventData) => _tray?.BeginDrag(_index, eventData);

        public void OnDrag(PointerEventData eventData) => _tray?.Drag(_index, eventData);

        public void OnEndDrag(PointerEventData eventData) => _tray?.EndDrag(_index, eventData);

        private void OnDestroy() => Release();

        private void Release()
        {
            if (_Image) _Image.texture = null;
            if (!_texture) return;

            Destroy(_texture);
            _texture = null;
        }
    }
}
