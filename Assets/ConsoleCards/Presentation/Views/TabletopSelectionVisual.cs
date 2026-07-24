using System;
using UnityEngine;

namespace ConsoleCards.Presentation.Views
{
    /// <summary>
    /// Projects local selection presentation onto one explicit child highlight root.
    /// </summary>
    public sealed class TabletopSelectionVisual : MonoBehaviour
    {
        [SerializeField] private TabletopObjectView objectView;
        [SerializeField] private GameObject highlightRoot;

        public bool IsConfigured => !ReferenceEquals(objectView, null) && !ReferenceEquals(highlightRoot, null);

        public TabletopObjectView ObjectView => objectView;

        public GameObject HighlightRoot => highlightRoot;

        public bool IsSelected => IsConfigured && highlightRoot != null && highlightRoot.activeSelf;

        public void Configure(
            TabletopObjectView objectView,
            GameObject highlightRoot)
        {
            ValidateConfiguration(objectView, highlightRoot);

            this.objectView = objectView;
            this.highlightRoot = highlightRoot;
            this.highlightRoot.SetActive(false);
        }

        public void SetSelected(bool selected)
        {
            EnsureConfigured();

            if (selected)
            {
                EnsureSelectable();
            }

            if (highlightRoot != null)
            {
                highlightRoot.SetActive(selected);
            }
        }

        public void Clear()
        {
            if (highlightRoot != null)
            {
                highlightRoot.SetActive(false);
            }

            objectView = null;
            highlightRoot = null;
        }

        private void ValidateConfiguration(
            TabletopObjectView candidateObjectView,
            GameObject candidateHighlightRoot)
        {
            if (candidateObjectView == null)
            {
                throw new ArgumentNullException(nameof(candidateObjectView));
            }

            if (candidateHighlightRoot == null)
            {
                throw new ArgumentNullException(nameof(candidateHighlightRoot));
            }

            if (!ReferenceEquals(candidateObjectView.gameObject, gameObject))
            {
                throw new ArgumentException(
                    "Selection visual objectView must be attached to this component's GameObject.",
                    nameof(candidateObjectView));
            }

            if (ReferenceEquals(candidateHighlightRoot, gameObject))
            {
                throw new ArgumentException(
                    "Selection highlight root cannot be the TabletopSelectionVisual root GameObject.",
                    nameof(candidateHighlightRoot));
            }

            if (!candidateHighlightRoot.transform.IsChildOf(candidateObjectView.transform))
            {
                throw new ArgumentException(
                    "Selection highlight root must be a descendant of the TabletopObjectView Transform.",
                    nameof(candidateHighlightRoot));
            }

            if (ContainsTabletopObjectView(candidateHighlightRoot.transform))
            {
                throw new ArgumentException(
                    "Selection highlight root hierarchy cannot contain another TabletopObjectView.",
                    nameof(candidateHighlightRoot));
            }
        }

        private void EnsureConfigured()
        {
            if (!IsConfigured)
            {
                throw new InvalidOperationException("TabletopSelectionVisual is not configured.");
            }
        }

        private void EnsureSelectable()
        {
            if (objectView == null
                || highlightRoot == null
                || !objectView.IsBound
                || !objectView.isActiveAndEnabled)
            {
                throw new InvalidOperationException("TabletopSelectionVisual cannot select an unavailable View.");
            }
        }

        private static bool ContainsTabletopObjectView(Transform root)
        {
            if (root.GetComponent<TabletopObjectView>() != null)
            {
                return true;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                if (ContainsTabletopObjectView(root.GetChild(i)))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
