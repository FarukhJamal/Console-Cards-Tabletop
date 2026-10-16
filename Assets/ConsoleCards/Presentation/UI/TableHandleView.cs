using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>
    /// One size handle of Table mode (doc 30, B3; T3): a small dot drawn over a point of the table's edge that the
    /// player drags to resize the table. It only reports the drag; the Table panel places it and the game resizes.
    /// Part of the authored Table panel prefab (added by Create Missing Gameplay UI Prefabs).
    /// </summary>
    public sealed class TableHandleView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private int index;
        private Action<int> began;
        private Action<int, Vector2> dragged;
        private Action<int> ended;
        private bool dragging;

        public RectTransform Rect => (RectTransform)transform;

        public void Bind(int handleIndex, Action<int> onBegin, Action<int, Vector2> onDrag, Action<int> onEnd)
        {
            index = handleIndex;
            began = onBegin;
            dragged = onDrag;
            ended = onEnd;
        }

        public void Unbind()
        {
            if (dragging) ended?.Invoke(index);
            dragging = false;
            began = null;
            dragged = null;
            ended = null;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            dragging = true;
            began?.Invoke(index);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (dragging) dragged?.Invoke(index, eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!dragging) return;
            dragging = false;
            ended?.Invoke(index);
        }

        private void OnDisable()
        {
            if (!dragging) return;
            dragging = false;
            ended?.Invoke(index);
        }
    }
}
