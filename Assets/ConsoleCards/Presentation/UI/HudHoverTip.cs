using UnityEngine;
using UnityEngine.EventSystems;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>
    /// HUD-B: shows a small tip (a child object authored on the prefab, such as "Undo · Ctrl Z") while the pointer is
    /// over this button, and hides it otherwise. Nothing is built at runtime.
    /// </summary>
    public sealed class HudHoverTip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private GameObject tip;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (tip != null) tip.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (tip != null) tip.SetActive(false);
        }

        private void OnDisable()
        {
            if (tip != null) tip.SetActive(false);
        }
    }
}
