using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    public sealed class TabletopPopupView : ReusableUiView, IPointerClickHandler
    {
        private const float PointerOffset = 8f;

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

        // Board 9 (owner, 2026-10-30): a piece's menu shows its actions in groups (PLAY · ARRANGE · TABLE), and Draw how
        // many has 1 / 3 / 5 / 7 and Enter. Optional: a popup made before board 9 has neither.
        [SerializeField] private GameObject groupTemplate;
        [SerializeField] private Button[] drawQuickButtons = Array.Empty<Button>();
        [SerializeField] private int[] drawQuickCounts = { 1, 3, 5, 7 };

        private static readonly string[] GroupNames = { "PLAY", "ARRANGE", "TABLE" };
        private readonly List<Text> groupHeaders = new List<Text>();
        private readonly List<GameObject> groupObjects = new List<GameObject>();
        private Action drawDecrement;
        private Action drawIncrement;
        private Action drawConfirm;
        private int drawShown;
        private int drawAvailable;

        private readonly List<PopupActionRowView> actionRows =
            new List<PopupActionRowView>();
        private Action dismiss;
        private Action<Vector2> secondaryDismiss;
        private Vector2 anchorScreenPosition;
        private IRuntimeUiService uiManager;
        private int preparedFrame = -1;
        private Canvas canvas;

        public void Initialize(IRuntimeUiService manager)
        {
            uiManager = manager ?? throw new ArgumentNullException(nameof(manager));
        }

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
                || uiManager == null)
            {
                throw new InvalidOperationException(
                    "TabletopPopupView requires its authored panels, controls, content roots, and runtime UI manager.");
            }
        }

        public void ShowContextMenu(
            Vector2 screenPosition,
            string title,
            string body,
            IReadOnlyList<PopupActionOption> actions,
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
            base.Show();
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
            drawDecrement = decrement;
            drawIncrement = increment;
            drawConfirm = confirm;
            for (int i = 0; i < drawQuickButtons.Length; i++)
            {
                if (drawQuickButtons[i] == null) continue;
                int count = i < drawQuickCounts.Length ? drawQuickCounts[i] : i + 1;
                drawQuickButtons[i].onClick.RemoveAllListeners();
                drawQuickButtons[i].onClick.AddListener(() => DrawQuick(count));
            }
            SetDrawCount(selectedCount, availableCount);
            base.Show();
            PositionPanel(drawCountPanel);
        }

        public void SetDrawCount(int selectedCount, int availableCount)
        {
            bool canDraw = availableCount > 0;
            int visibleCount = canDraw ? Mathf.Clamp(selectedCount, 1, availableCount) : 0;
            drawCountLabel.text = visibleCount.ToString();
            drawShown = visibleCount;
            drawAvailable = availableCount;
            drawAvailableLabel.text = !canDraw ? "Deck is empty."
                : groupTemplate != null ? $"Into your hand · Enter draws · {availableCount} left"
                : $"Available: {availableCount}";
            for (int i = 0; i < drawQuickButtons.Length; i++)
            {
                int count = i < drawQuickCounts.Length ? drawQuickCounts[i] : i + 1;
                if (drawQuickButtons[i] != null) drawQuickButtons[i].interactable = canDraw && count <= availableCount;
            }
            drawDecrementButton.interactable = canDraw && visibleCount > 1;
            drawIncrementButton.interactable = canDraw && visibleCount < availableCount;
            drawConfirmButton.interactable = canDraw;
        }

        public void ShowMergeDestinations(
            Vector2 screenPosition,
            IReadOnlyList<PopupActionOption> destinations,
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
            mergeScrollRect.verticalNormalizedPosition = 1f;
            base.Show();
            PositionPanel(mergePanel);
        }

        public override void Hide()
        {
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

            base.Hide();
            ClearSelectedUiObject();
        }

        public override void Unbind()
        {
            ClearRows();
            RemoveButtonListeners();
            dismiss = null;
            secondaryDismiss = null;
            drawDecrement = null;
            drawIncrement = null;
            drawConfirm = null;
            if (contextTitleLabel != null) contextTitleLabel.text = string.Empty;
            if (contextBodyLabel != null) contextBodyLabel.text = string.Empty;
            if (drawCountLabel != null) drawCountLabel.text = string.Empty;
            if (drawAvailableLabel != null) drawAvailableLabel.text = string.Empty;
            anchorScreenPosition = Vector2.zero;
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

        // 1 / 3 / 5 / 7: steps the count there with the deck's own − and +.
        private void DrawQuick(int count)
        {
            int target = Mathf.Clamp(count, 1, Mathf.Max(1, drawAvailable));
            int guard = 64;
            while (drawShown < target && drawIncrement != null && guard-- > 0)
            {
                int before = drawShown;
                drawIncrement();
                if (drawShown == before) break;
            }

            while (drawShown > target && drawDecrement != null && guard-- > 0)
            {
                int before = drawShown;
                drawDecrement();
                if (drawShown == before) break;
            }
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                dismiss?.Invoke();
                return;
            }

            // Board 9: Enter draws.
            if (Keyboard.current != null && drawCountPanel != null && drawCountPanel.gameObject.activeInHierarchy
                && Time.frameCount != preparedFrame && drawConfirm != null && drawConfirmButton.interactable
                && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame))
            {
                drawConfirm();
                return;
            }

            // SMOOTH-1: a press anywhere outside the open panel closes it and still reaches the table (or the other
            // button) underneath, so the press after a menu never needs a second try. Not in the frame the popup opened
            // (the right-click that opened it).
            Mouse mouse = Mouse.current;
            if (dismiss == null || mouse == null || Time.frameCount == preparedFrame) return;
            if (!mouse.leftButton.wasPressedThisFrame
                && !mouse.rightButton.wasPressedThisFrame
                && !mouse.middleButton.wasPressedThisFrame)
            {
                return;
            }

            if (!IsOverOpenPanel(mouse.position.ReadValue())) dismiss.Invoke();
        }

        private bool IsOverOpenPanel(Vector2 screenPosition)
        {
            if (canvas == null) canvas = GetComponentInParent<Canvas>();
            UnityEngine.Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            return IsOver(contextPanel, screenPosition, eventCamera)
                || IsOver(drawCountPanel, screenPosition, eventCamera)
                || IsOver(mergePanel, screenPosition, eventCamera);
        }

        private static bool IsOver(RectTransform panel, Vector2 screenPosition, UnityEngine.Camera eventCamera)
        {
            return panel != null
                && panel.gameObject.activeInHierarchy
                && RectTransformUtility.RectangleContainsScreenPoint(panel, screenPosition, eventCamera);
        }

        // SMOOTH-1: the full-screen click catcher no longer eats the press (the press closes the popup in Update).
        private void LetPressesThrough()
        {
            Graphic catcher = GetComponent<Graphic>();
            if (catcher != null && catcher.raycastTarget) catcher.raycastTarget = false;
        }

        private void Prepare(
            Vector2 screenPosition,
            Action dismissPopup,
            Action<Vector2> secondaryDismissPopup)
        {
            RequireAcquired();
            if (dismissPopup == null)
            {
                throw new ArgumentNullException(nameof(dismissPopup));
            }

            ValidateReferences();
            Unbind();
            dismiss = dismissPopup;
            secondaryDismiss = secondaryDismissPopup;
            anchorScreenPosition = screenPosition;
            preparedFrame = Time.frameCount;
            LetPressesThrough();
            contextPanel.gameObject.SetActive(false);
            drawCountPanel.gameObject.SetActive(false);
            mergePanel.gameObject.SetActive(false);
        }

        private void RebuildRows(
            RectTransform contentRoot,
            IReadOnlyList<PopupActionOption> actions)
        {
            HideGroupHeaders();
            if (groupTemplate != null && contentRoot == contextActionsRoot && InGroups(actions))
            {
                int headers = 0;
                for (int group = 0; group < GroupNames.Length; group++)
                {
                    bool headed = false;
                    for (int i = 0; i < actions.Count; i++)
                    {
                        if (PopupActionRowView.GroupOf(actions[i].Label) != group) continue;
                        if (!headed)
                        {
                            Text header = GroupHeader(headers++, contentRoot);
                            header.text = GroupNames[group];
                            groupObjects[headers - 1].transform.SetAsLastSibling();
                            headed = true;
                        }

                        AddRow(contentRoot, actions[i]);
                    }
                }

                return;
            }

            for (int i = 0; i < actions.Count; i++) AddRow(contentRoot, actions[i]);
        }

        private void AddRow(RectTransform contentRoot, PopupActionOption action)
        {
            PopupActionRowView row = uiManager.AcquirePooled<PopupActionRowView>(UiPrefabIds.PopupActionRow, contentRoot);
            row.Bind(action);
            row.Show();
            row.transform.SetAsLastSibling();
            actionRows.Add(row);
        }

        // Groups only when every row is a piece action and there are at least two kinds.
        private static bool InGroups(IReadOnlyList<PopupActionOption> actions)
        {
            int kinds = 0;
            for (int i = 0; i < actions.Count; i++)
            {
                int group = PopupActionRowView.GroupOf(actions[i].Label);
                if (group < 0) return false;
                kinds |= 1 << group;
            }

            return kinds != 0 && (kinds & (kinds - 1)) != 0;
        }

        private Text GroupHeader(int index, RectTransform contentRoot)
        {
            while (groupObjects.Count <= index)
            {
                GameObject made = Instantiate(groupTemplate, contentRoot, false);
                made.name = "Group";
                groupObjects.Add(made);
                groupHeaders.Add(made.GetComponentInChildren<Text>(true));
            }

            GameObject header = groupObjects[index];
            if (header.transform.parent != contentRoot) header.transform.SetParent(contentRoot, false);
            header.SetActive(true);
            return groupHeaders[index];
        }

        private void HideGroupHeaders()
        {
            for (int i = 0; i < groupObjects.Count; i++)
            {
                if (groupObjects[i] != null) groupObjects[i].SetActive(false);
            }
        }

        private void PositionPanel(RectTransform panel)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    popupBounds,
                    anchorScreenPosition,
                    null,
                    out Vector2 localPoint))
            {
                localPoint = Vector2.zero;
            }

            Rect bounds = popupBounds.rect;
            // Board 9: a panel sized by its parts may not have its size yet; its preferred size keeps it on screen.
            float width = Mathf.Max(panel.rect.width, LayoutUtility.GetPreferredWidth(panel));
            float height = Mathf.Max(panel.rect.height, LayoutUtility.GetPreferredHeight(panel));
            float x = localPoint.x - bounds.xMin + PointerOffset;
            float y = localPoint.y - bounds.yMin - PointerOffset;
            float clampedX = Mathf.Clamp(x, 0f, Mathf.Max(0f, bounds.width - width));
            float clampedY = Mathf.Clamp(y, height, bounds.height);
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
            HideGroupHeaders();
            for (int i = actionRows.Count - 1; i >= 0; i--)
            {
                PopupActionRowView row = actionRows[i];
                if (row == null)
                {
                    continue;
                }

                uiManager.Release(row);
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

    }
}
