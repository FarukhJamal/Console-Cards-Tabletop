using UnityEngine;
using UnityEngine.EventSystems;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>
    /// HUD-B: shows a small tip (a child object authored on the prefab, such as "Undo · Ctrl Z") while the pointer is
    /// over this button, and hides it otherwise. Nothing is built at runtime.
    /// <para>While shown, the tip is lifted to the top of its view (owner, 2026-10-30: the Toolbox tip went under the
    /// player chip drawn after it) and goes back under its button when hidden; it stays in the view's own layer.</para>
    /// </summary>
    public sealed class HudHoverTip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private GameObject tip;

        private Transform home;
        private int homeIndex;
        private bool lifted;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (tip == null) return;
            Lift();
            tip.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            HideTip();
        }

        private void OnDisable()
        {
            HideTip();
        }

        private void HideTip()
        {
            if (tip == null) return;
            tip.SetActive(false);
            Return();
        }

        private void Lift()
        {
            if (lifted) return;
            ReusableUiView view = GetComponentInParent<ReusableUiView>();
            Transform top = view != null ? view.transform : null;
            if (top == null || tip.transform.parent == top) return;
            home = tip.transform.parent;
            homeIndex = tip.transform.GetSiblingIndex();
            tip.transform.SetParent(top, true);
            tip.transform.SetAsLastSibling();
            lifted = true;
        }

        private void Return()
        {
            if (!lifted) return;
            lifted = false;
            if (home == null) return; // the button went away; the tip goes with the view
            tip.transform.SetParent(home, true);
            tip.transform.SetSiblingIndex(homeIndex);
        }
    }
}
