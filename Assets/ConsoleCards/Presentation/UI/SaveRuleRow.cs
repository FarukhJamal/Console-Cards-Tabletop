using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>
    /// MAKE-B: one rule in Make a game's rules list: its words (typed over to change them), × to remove it, and the
    /// ⋮⋮ handle to drag it up or down. Rows are laid out by the panel, one every <c>pitch</c> units.
    /// </summary>
    public sealed class SaveRuleRow : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private InputField textField;
        [SerializeField] private Button removeButton;
        [SerializeField] private RectTransform handle;

        private int index;
        private float pitch = 69f;
        private Action<int, string> edited;
        private Action<int> removed;
        private Action<int, int> moved;
        private bool dragging;
        private float grabOffset;

        public void Bind(int rowIndex, string text, float rowPitch, Action<int, string> onEdited, Action<int> onRemoved,
            Action<int, int> onMoved)
        {
            index = rowIndex;
            pitch = Mathf.Max(1f, rowPitch);
            edited = onEdited;
            removed = onRemoved;
            moved = onMoved;
            RectTransform rect = (RectTransform)transform;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -index * pitch);
            if (textField != null)
            {
                textField.onEndEdit.RemoveAllListeners();
                textField.SetTextWithoutNotify(text);
                textField.onEndEdit.AddListener(value => edited?.Invoke(index, value));
            }

            if (removeButton != null)
            {
                removeButton.onClick.RemoveAllListeners();
                removeButton.onClick.AddListener(() => removed?.Invoke(index));
            }
        }

        public void Unbind()
        {
            edited = null;
            removed = null;
            moved = null;
            if (textField != null) textField.onEndEdit.RemoveAllListeners();
            if (removeButton != null) removeButton.onClick.RemoveAllListeners();
        }

        // Only a drag that starts on the handle moves the row (a drag on the words selects them).
        public void OnBeginDrag(PointerEventData eventData)
        {
            dragging = handle != null && RectTransformUtility.RectangleContainsScreenPoint(handle, eventData.pressPosition,
                eventData.pressEventCamera);
            if (!dragging) return;
            transform.SetAsLastSibling();
            grabOffset = LocalY(eventData) - ((RectTransform)transform).anchoredPosition.y;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragging) return;
            RectTransform rect = (RectTransform)transform;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, Mathf.Min(0f, LocalY(eventData) - grabOffset));
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!dragging) return;
            dragging = false;
            int to = Mathf.RoundToInt(-((RectTransform)transform).anchoredPosition.y / pitch);
            moved?.Invoke(index, to);
        }

        private float LocalY(PointerEventData eventData)
        {
            RectTransform parent = (RectTransform)transform.parent;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, eventData.position, eventData.pressEventCamera,
                out Vector2 local);
            return local.y;
        }
    }
}
