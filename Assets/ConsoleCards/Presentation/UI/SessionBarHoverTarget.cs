using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>
    /// Reports pointer enter and exit on a session bar button so the bar can show that button's tip (UI-1).
    /// </summary>
    public sealed class SessionBarHoverTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Action<bool> hoverChanged;

        public void SetListener(Action<bool> listener)
        {
            hoverChanged = listener;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hoverChanged?.Invoke(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hoverChanged?.Invoke(false);
        }

        private void OnDisable()
        {
            hoverChanged?.Invoke(false);
        }
    }
}
