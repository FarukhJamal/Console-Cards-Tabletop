using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    public sealed class PrototypeTabletopPopupView : MonoBehaviour, IPointerClickHandler
    {
        private const float PanelWidth = 260f;
        private const float PanelPadding = 12f;
        private const float TitleHeight = 28f;
        private const float ActionHeight = 44f;
        private const float ActionSpacing = 6f;
        private const float ElementSpacing = 8f;
        private const float PointerOffset = 8f;
        private const float MergePanelMaximumHeight = 320f;

        [SerializeField] private RectTransform popupBounds;
        [SerializeField] private RectTransform contextPanel;
        [SerializeField] private Text contextTitleLabel;
        [SerializeField] private Text contextBodyLabel;
        [SerializeField] private RectTransform contextActionsRoot;
        [SerializeField] private RectTransform drawCountPanel;
        [SerializeField] private Text drawCountLabel;
        [SerializeField] private Text drawAvailableLabel;
        [SerializeField] private Button drawDecrementButton;
        [SerializeField] private Button drawIncrementButton;
        [SerializeField] private Button drawConfirmButton;
        [SerializeField] private Button drawCancelButton;
        [SerializeField] private RectTransform mergePanel;
        [SerializeField] private RectTransform mergeViewport;
        [SerializeField] private RectTransform mergeActionsRoot;
        [SerializeField] private ScrollRect mergeScrollRect;
        [SerializeField] private Button mergeBackButton;
        [SerializeField] private PrototypePopupActionRowView actionRowPrefab;

        private readonly List<PrototypePopupActionRowView> actionRows =
            new List<PrototypePopupActionRowView>();
        private Action dismiss;
        private Action<Vector2> secondaryDismiss;
        private Vector2 anchorScreenPosition;

        public void ValidateReferences()
        {
            if (popupBounds == null
                || contextPanel == null
                || contextTitleLabel == null
                || contextBodyLabel == null
                || contextActionsRoot == null
                || drawCountPanel == null
                || drawCountLabel == null
                || drawAvailableLabel == null
                || drawDecrementButton == null
                || drawIncrementButton == null
                || drawConfirmButton == null
                || drawCancelButton == null
                || mergePanel == null
                || mergeViewport == null
                || mergeActionsRoot == null
                || mergeScrollRect == null
                || mergeBackButton == null
                || actionRowPrefab == null
                || actionRowPrefab.gameObject.scene.IsValid())
            {
                throw new InvalidOperationException(
                    "PrototypeTabletopPopupView requires its authored panels, controls, content roots, and action-row prefab asset.");
            }

            actionRowPrefab.ValidateReferences();
        }

        public void ShowContextMenu(
            Vector2 screenPosition,
            string title,
            string body,
            IReadOnlyList<PrototypePopupActionOption> actions,
            Action dismissPopup,
            Action<Vector2> secondaryDismissPopup)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("A context menu requires a title.", nameof(title));
            }

            if (actions == null)
            {
                throw new ArgumentNullException(nameof(actions));
            }

            Prepare(screenPosition, dismissPopup, secondaryDismissPopup);
            contextPanel.gameObject.SetActive(true);
            contextTitleLabel.text = title;
            bool hasBody = !string.IsNullOrWhiteSpace(body);
            contextBodyLabel.text = hasBody ? body : string.Empty;
            contextBodyLabel.gameObject.SetActive(hasBody);
            RebuildRows(contextActionsRoot, actions);
            LayoutContextPanel(hasBody, actions.Count);
            PositionPanel(contextPanel);
        }

        public void ShowDrawCount(
            Vector2 screenPosition,
            int selectedCount,
            int availableCount,
            Action decrement,
            Action increment,
            Action confirm,
            Action cancel,
            Action dismissPopup,
            Action<Vector2> secondaryDismissPopup)
        {
            Prepare(screenPosition, dismissPopup, secondaryDismissPopup);
            drawCountPanel.gameObject.SetActive(true);
            BindButton(drawDecrementButton, decrement);
            BindButton(drawIncrementButton, increment);
            BindButton(drawConfirmButton, confirm);
            BindButton(drawCancelButton, cancel);
            SetDrawCount(selectedCount, availableCount);
            PositionPanel(drawCountPanel);
        }

        public void SetDrawCount(int selectedCount, int availableCount)
        {
            bool canDraw = availableCount > 0;
            int visibleCount = canDraw ? Mathf.Clamp(selectedCount, 1, availableCount) : 0;
            drawCountLabel.text = visibleCount.ToString();
            drawAvailableLabel.text = canDraw
                ? $"Available: {availableCount}"
                : "Deck is empty.";
            drawDecrementButton.interactable = canDraw && visibleCount > 1;
            drawIncrementButton.interactable = canDraw && visibleCount < availableCount;
            drawConfirmButton.interactable = canDraw;
        }

        public void ShowMergeDestinations(
            Vector2 screenPosition,
            IReadOnlyList<PrototypePopupActionOption> destinations,
            Action back,
            Action dismissPopup,
            Action<Vector2> secondaryDismissPopup)
        {
            if (destinations == null)
            {
                throw new ArgumentNullException(nameof(destinations));
            }

            Prepare(screenPosition, dismissPopup, secondaryDismissPopup);
            mergePanel.gameObject.SetActive(true);
            RebuildRows(mergeActionsRoot, destinations);
            BindButton(mergeBackButton, back);
            LayoutMergePanel(destinations.Count);
            mergeScrollRect.verticalNormalizedPosition = 1f;
            PositionPanel(mergePanel);
        }

        public void Close()
        {
            ClearRows();
            RemoveButtonListeners();
            dismiss = null;
            secondaryDismiss = null;
            if (contextPanel != null)
            {
                contextPanel.gameObject.SetActive(false);
            }

            if (drawCountPanel != null)
            {
                drawCountPanel.gameObject.SetActive(false);
            }

            if (mergePanel != null)
            {
                mergePanel.gameObject.SetActive(false);
            }

            gameObject.SetActive(false);
            ClearSelectedUiObject();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null)
            {
                return;
            }

            if (eventData.button == PointerEventData.InputButton.Right)
            {
                Action<Vector2> callback = secondaryDismiss;
                callback?.Invoke(eventData.position);
                return;
            }

            if (eventData.button == PointerEventData.InputButton.Left)
            {
                dismiss?.Invoke();
            }
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                dismiss?.Invoke();
            }
        }

        private void Prepare(
            Vector2 screenPosition,
            Action dismissPopup,
            Action<Vector2> secondaryDismissPopup)
        {
            if (dismissPopup == null)
            {
                throw new ArgumentNullException(nameof(dismissPopup));
            }

            ValidateReferences();
            ClearRows();
            RemoveButtonListeners();
            dismiss = dismissPopup;
            secondaryDismiss = secondaryDismissPopup;
            anchorScreenPosition = screenPosition;
            contextPanel.gameObject.SetActive(false);
            drawCountPanel.gameObject.SetActive(false);
            mergePanel.gameObject.SetActive(false);
            gameObject.SetActive(true);
        }

        private void RebuildRows(
            RectTransform contentRoot,
            IReadOnlyList<PrototypePopupActionOption> actions)
        {
            for (int i = 0; i < actions.Count; i++)
            {
                PrototypePopupActionRowView row = Instantiate(actionRowPrefab, contentRoot, false);
                row.name = actionRowPrefab.name;
                row.Bind(actions[i]);
                actionRows.Add(row);
            }

            float height = actions.Count == 0
                ? 0f
                : (actions.Count * ActionHeight) + ((actions.Count - 1) * ActionSpacing);
            contentRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }

        private void LayoutContextPanel(bool hasBody, int actionCount)
        {
            float bodyHeight = 0f;
            if (hasBody)
            {
                contextBodyLabel.rectTransform.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Horizontal,
                    PanelWidth - (PanelPadding * 2f));
                bodyHeight = Mathf.Clamp(contextBodyLabel.preferredHeight, 20f, 96f);
                contextBodyLabel.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bodyHeight);
            }

            float actionsHeight = actionCount == 0
                ? 0f
                : (actionCount * ActionHeight) + ((actionCount - 1) * ActionSpacing);
            float bodyBlock = hasBody ? bodyHeight + ElementSpacing : 0f;
            float panelHeight = (PanelPadding * 2f) + TitleHeight + ElementSpacing + bodyBlock + actionsHeight;
            contextPanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, PanelWidth);
            contextPanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, panelHeight);

            contextTitleLabel.rectTransform.anchoredPosition = new Vector2(PanelPadding, -PanelPadding);
            contextBodyLabel.rectTransform.anchoredPosition =
                new Vector2(PanelPadding, -(PanelPadding + TitleHeight + ElementSpacing));
            contextActionsRoot.anchoredPosition = new Vector2(
                PanelPadding,
                -(PanelPadding + TitleHeight + ElementSpacing + bodyBlock));
        }

        private void LayoutMergePanel(int destinationCount)
        {
            float rowContentHeight = destinationCount == 0
                ? ActionHeight
                : (destinationCount * ActionHeight) + ((destinationCount - 1) * ActionSpacing);
            float panelHeight = Mathf.Min(MergePanelMaximumHeight, 92f + rowContentHeight);
            mergePanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, PanelWidth);
            mergePanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, panelHeight);
            mergeViewport.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, panelHeight - 92f);
            mergeActionsRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rowContentHeight);
        }

        private void PositionPanel(RectTransform panel)
        {
            Canvas.ForceUpdateCanvases();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    popupBounds,
                    anchorScreenPosition,
                    null,
                    out Vector2 localPoint))
            {
                localPoint = Vector2.zero;
            }

            Rect bounds = popupBounds.rect;
            float x = localPoint.x - bounds.xMin + PointerOffset;
            float y = localPoint.y - bounds.yMin - PointerOffset;
            float clampedX = Mathf.Clamp(x, 0f, Mathf.Max(0f, bounds.width - panel.rect.width));
            float clampedY = Mathf.Clamp(y, panel.rect.height, bounds.height);
            panel.anchoredPosition = new Vector2(clampedX, clampedY);
        }

        private static void BindButton(Button button, Action callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(callback.Invoke);
        }

        private void ClearRows()
        {
            for (int i = actionRows.Count - 1; i >= 0; i--)
            {
                PrototypePopupActionRowView row = actionRows[i];
                if (row == null)
                {
                    continue;
                }

                row.Unbind();
                if (UnityEngine.Application.isPlaying)
                {
                    Destroy(row.gameObject);
                }
                else
                {
                    DestroyImmediate(row.gameObject);
                }
            }

            actionRows.Clear();
        }

        private void RemoveButtonListeners()
        {
            RemoveListeners(drawDecrementButton);
            RemoveListeners(drawIncrementButton);
            RemoveListeners(drawConfirmButton);
            RemoveListeners(drawCancelButton);
            RemoveListeners(mergeBackButton);
        }

        private static void RemoveListeners(Button button)
        {
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
            }
        }

        private void ClearSelectedUiObject()
        {
            EventSystem current = EventSystem.current;
            if (current != null
                && current.currentSelectedGameObject != null
                && current.currentSelectedGameObject.transform.IsChildOf(transform))
            {
                current.SetSelectedGameObject(null);
            }
        }

        private void OnDestroy()
        {
            ClearRows();
            RemoveButtonListeners();
        }
    }
}
