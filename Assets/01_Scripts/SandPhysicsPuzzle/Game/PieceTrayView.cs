using SandPhysicsPuzzle.Rules;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SandPhysicsPuzzle.Game
{
    // The three dealt pieces and the drag that turns one into sand. Owns no game state; Bind hands
    // it the live loop. Every pose goes through Preview and every drop through TryPlace, which
    // share a clamp, so the ghost is literally the pose that gets stamped.
    public sealed class PieceTrayView : MonoBehaviour
    {
        [SerializeField] private SandBoardView _Board;
        [SerializeField] private TraySlotView[] _Slots;

        private TurnLoop _loop;
        private LevelConfig _config;

        /// <summary>Slot being dragged, or -1. Guards against a second finger starting a second drag.</summary>
        private int _dragging = -1;

        // The slot index alone is not enough: uGUI tracks a drag per pointer, so two fingers on the
        // same slot both raise events for it, and the second one would place the piece.
        private int _dragPointerId;

        public void Bind(TurnLoop loop, LevelConfig config)
        {
            Unbind();

            if (loop == null || config == null || !_Board || _Slots == null)
            {
                Debug.LogError($"{nameof(PieceTrayView)}.{nameof(Bind)} needs a loop, a config, " +
                               "a board and its slots.", this);
                return;
            }

            _loop = loop;
            _config = config;
            _loop.TrayRefilled += Refresh;

            for (var i = 0; i < _Slots.Length; i++)
                if (_Slots[i]) _Slots[i].Attach(this, i);

            Refresh();
        }

        public void Unbind()
        {
            CancelDrag();

            if (_loop != null) _loop.TrayRefilled -= Refresh;
            _loop = null;
            _config = null;

            Refresh();
        }

        internal void BeginDrag(int index, PointerEventData eventData)
        {
            if (_dragging >= 0) return;
            if (_loop == null || !_loop.AcceptsInput) return;
            if ((uint)index >= PieceTray.SlotCount || !_loop.GetSlot(index).IsFilled) return;

            _dragging = index;
            _dragPointerId = eventData.pointerId;
            if (_Slots[index]) _Slots[index].SetDimmed(true);

            Drag(index, eventData);
        }

        internal void Drag(int index, PointerEventData eventData)
        {
            if (!OwnsDrag(index, eventData) || _loop == null) return;

            // No ghost outside the drop range, so the empty board is the cue that releasing here
            // returns the piece instead of placing it.
            if (!_Board.IsWithinDropRange(eventData.position, eventData.pressEventCamera) ||
                !_Board.TryScreenToGrain(eventData.position, eventData.pressEventCamera, out var grain))
            {
                _Board.HideGhost();
                return;
            }

            // The pose is clamped into the board, so a finger dragged past an edge still shows the
            // piece resting against that wall rather than the ghost vanishing.
            var result = _loop.Preview(index, grain.x, grain.y, out var piece);
            if (result == PreviewResult.NoPiece)
            {
                _Board.HideGhost();
                return;
            }

            _Board.ShowGhost(_Slots[index] ? _Slots[index].Texture : null, piece,
                SlotColor(_loop.GetSlot(index).ColorId), result == PreviewResult.Ok);
        }

        internal void EndDrag(int index, PointerEventData eventData)
        {
            if (!OwnsDrag(index, eventData)) return;

            CancelDrag();

            if (_loop == null) return;
            if (!_Board.IsWithinDropRange(eventData.position, eventData.pressEventCamera)) return;
            if (!_Board.TryScreenToGrain(eventData.position, eventData.pressEventCamera, out var grain)) return;

            // A refused drop leaves the tray untouched, so there is nothing to undo -- the piece
            // simply stays in its slot.
            if (_loop.TryPlace(index, grain.x, grain.y)) Refresh();
        }

        private bool OwnsDrag(int index, PointerEventData eventData) =>
            _dragging == index && _dragPointerId == eventData.pointerId;

        private void CancelDrag()
        {
            if (_Board) _Board.HideGhost();

            if (_dragging >= 0 && _Slots != null && _Slots[_dragging])
                _Slots[_dragging].SetDimmed(false);

            _dragging = -1;
        }

        /// <summary>Redraws every cell from the loop. Unbound, this empties them.</summary>
        private void Refresh()
        {
            if (_Slots == null) return;

            for (var i = 0; i < _Slots.Length; i++)
            {
                if (!_Slots[i]) continue;

                var slot = _loop != null && i < PieceTray.SlotCount ? _loop.GetSlot(i) : default;
                _Slots[i].Show(slot, SlotColor(slot.ColorId));
            }
        }

        private Color SlotColor(byte colorId) => _config ? _config.SandColor(colorId) : Color.white;

        private void OnDisable() => CancelDrag();

        // Without this the loop keeps a TrayRefilled delegate pointing into a destroyed component.
        private void OnDestroy() => Unbind();
    }
}
