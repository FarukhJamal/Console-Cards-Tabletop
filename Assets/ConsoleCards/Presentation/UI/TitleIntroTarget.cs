using UnityEngine;
using UnityEngine.EventSystems;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>A place on the title intro that answers the mouse: the console hotspot and the ENTER prompt.</summary>
    public sealed class TitleIntroTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [SerializeField] private TitleIntroView view;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (view != null) view.PointerEnter();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (view != null) view.PointerExit();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (view != null && eventData.button == PointerEventData.InputButton.Left) view.Activate();
        }
    }
}
