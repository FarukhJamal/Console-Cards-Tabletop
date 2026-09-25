using System;
using System.Collections.Generic;
using ConsoleCards.Core.Identifiers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    public sealed class PrototypeFocusedCardOptionModel
    {
        public PrototypeFocusedCardOptionModel(
            TabletopObjectId cardId,
            string title,
            string subtitle,
            Texture artwork,
            bool eligible,
            bool selected = false)
        {
            if (cardId.IsEmpty) throw new ArgumentException("A focused Card option requires an authoritative Card ID.", nameof(cardId));
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("A focused Card option requires a title.", nameof(title));
            CardId = cardId;
            Title = title;
            Subtitle = subtitle ?? string.Empty;
            Artwork = artwork;
            Eligible = eligible;
            Selected = selected;
        }

        public TabletopObjectId CardId { get; }
        public string Title { get; }
        public string Subtitle { get; }
        public Texture Artwork { get; }
        public bool Eligible { get; }
        public bool Selected { get; }
    }

    public sealed class PrototypeFocusedCardSelectionModel
    {
        public PrototypeFocusedCardSelectionModel(
            string title,
            string instruction,
            string status,
            string confirmLabel,
            IReadOnlyList<PrototypeFocusedCardOptionModel> options,
            int maximumSelectionCount,
            Func<IReadOnlyList<TabletopObjectId>, bool> canConfirm,
            Action<IReadOnlyList<TabletopObjectId>> confirm,
            Action dismiss)
        {
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("A focused Card selection requires a title.", nameof(title));
            if (string.IsNullOrWhiteSpace(confirmLabel)) throw new ArgumentException("A focused Card selection requires a confirm label.", nameof(confirmLabel));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (maximumSelectionCount <= 0) throw new ArgumentOutOfRangeException(nameof(maximumSelectionCount));
            Title = title;
            Instruction = instruction ?? string.Empty;
            Status = status ?? string.Empty;
            ConfirmLabel = confirmLabel;
            Options = options;
            MaximumSelectionCount = maximumSelectionCount;
            CanConfirm = canConfirm ?? throw new ArgumentNullException(nameof(canConfirm));
            Confirm = confirm ?? throw new ArgumentNullException(nameof(confirm));
            Dismiss = dismiss ?? throw new ArgumentNullException(nameof(dismiss));
        }

        public string Title { get; }
        public string Instruction { get; }
        public string Status { get; }
        public string ConfirmLabel { get; }
        public IReadOnlyList<PrototypeFocusedCardOptionModel> Options { get; }
        public int MaximumSelectionCount { get; }
        public Func<IReadOnlyList<TabletopObjectId>, bool> CanConfirm { get; }
        public Action<IReadOnlyList<TabletopObjectId>> Confirm { get; }
        public Action Dismiss { get; }
    }

    public sealed class PrototypeFocusedCardRevealModel
    {
        public PrototypeFocusedCardRevealModel(
            TabletopObjectId cardId,
            Texture backArtwork,
            Texture frontArtwork,
            string headerTitle,
            string resultState,
            string cardTitle,
            string cardSubtitle,
            string body,
            string completionLabel,
            Action completed)
        {
            if (cardId.IsEmpty) throw new ArgumentException("A focused reveal requires an authoritative Card ID.", nameof(cardId));
            CardId = cardId;
            BackArtwork = backArtwork;
            FrontArtwork = frontArtwork;
            HeaderTitle = string.IsNullOrWhiteSpace(headerTitle) ? "RESULT" : headerTitle;
            ResultState = resultState ?? string.Empty;
            CardTitle = cardTitle ?? string.Empty;
            CardSubtitle = cardSubtitle ?? string.Empty;
            Body = body ?? string.Empty;
            CompletionLabel = string.IsNullOrWhiteSpace(completionLabel) ? "Return" : completionLabel;
            Completed = completed ?? throw new ArgumentNullException(nameof(completed));
        }

        public TabletopObjectId CardId { get; }
        public Texture BackArtwork { get; }
        public Texture FrontArtwork { get; }
        public string HeaderTitle { get; }
        public string ResultState { get; }
        public string CardTitle { get; }
        public string CardSubtitle { get; }
        public string Body { get; }
        public string CompletionLabel { get; }
        public Action Completed { get; }
    }

    /// <summary>
    /// Generic focused card-choice surface. Game Presentation supplies exact Card identities,
    /// eligibility, confirmation rules, and meaning; this View owns only representations and input.
    /// </summary>
    public sealed class PrototypeFocusedCardSelectionView : ReusableUiView
    {
        [SerializeField] private Button dismissOverlayButton;
        [SerializeField] private GameObject panel;
        [SerializeField] private RectTransform optionsRoot;
        [SerializeField] private ScrollRect optionsScrollRect;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text instructionLabel;
        [SerializeField] private Text statusLabel;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Text confirmButtonLabel;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Text cancelButtonLabel;
        [SerializeField] private GameObject[] unusedAuthoredRoots = Array.Empty<GameObject>();

        private readonly List<PrototypeFocusedCardOptionView> optionViews =
            new List<PrototypeFocusedCardOptionView>();
        private readonly List<TabletopObjectId> selectedCardIds = new List<TabletopObjectId>();
        private PrototypeFocusedCardSelectionModel model;
        private PrototypeFocusedCardRevealModel revealModel;
        private IRuntimeUiService uiService;

        public void Initialize(IRuntimeUiService service)
        {
            uiService = service ?? throw new ArgumentNullException(nameof(service));
        }

        public void ValidateReferences()
        {
            if (dismissOverlayButton == null
                || panel == null
                || optionsRoot == null
                || optionsScrollRect == null
                || titleLabel == null
                || instructionLabel == null
                || statusLabel == null
                || confirmButton == null
                || confirmButtonLabel == null
                || cancelButton == null
                || cancelButtonLabel == null
                || uiService == null)
            {
                throw new InvalidOperationException(
                    "PrototypeFocusedCardSelectionView requires its authored overlay, Card area, labels, controls, and runtime UI service.");
            }
        }

        public void Bind(PrototypeFocusedCardSelectionModel selectionModel)
        {
            RequireAcquired();
            if (selectionModel == null) throw new ArgumentNullException(nameof(selectionModel));
            ValidateReferences();
            Unbind();
            model = selectionModel;
            titleLabel.text = model.Title;
            instructionLabel.text = model.Instruction;
            statusLabel.text = model.Status;
            confirmButtonLabel.text = model.ConfirmLabel;
            cancelButtonLabel.text = "Cancel";
            BindButton(dismissOverlayButton, model.Dismiss);
            BindButton(cancelButton, model.Dismiss);
            BindButton(confirmButton, Confirm);
            panel.SetActive(true);
            SetUnusedRootsActive(false);
            ConfigureFocusedLayout();
            BuildOptions();
            RefreshSelectionState();
            ClearSelectedUiObject();
        }

        public void ShowReveal(PrototypeFocusedCardRevealModel cardReveal)
        {
            RequireAcquired();
            ValidateReferences();
            Unbind();
            revealModel = cardReveal ?? throw new ArgumentNullException(nameof(cardReveal));
            panel.SetActive(true);
            SetUnusedRootsActive(false);
            ConfigureFocusedLayout();
            titleLabel.text = revealModel.HeaderTitle;
            instructionLabel.text = revealModel.ResultState;
            statusLabel.text = revealModel.Body;
            cancelButton.gameObject.SetActive(false);
            dismissOverlayButton.interactable = false;
            confirmButtonLabel.text = revealModel.CompletionLabel;
            confirmButton.interactable = true;
            BindButton(confirmButton, CompleteReveal);

            PrototypeFocusedCardOptionView card = uiService.AcquirePooled<PrototypeFocusedCardOptionView>(
                PrototypeUiPrefabIds.FocusedCardOption,
                optionsRoot);
            card.Bind(new PrototypeFocusedCardOptionModel(
                revealModel.CardId,
                "MYSTERY",
                string.Empty,
                revealModel.BackArtwork,
                false), IgnoreChoice);
            optionsRoot.sizeDelta = new Vector2(620f, 260f);
            RectTransform cardTransform = card.GetComponent<RectTransform>();
            cardTransform.anchorMin = new Vector2(0f, 0.5f);
            cardTransform.anchorMax = new Vector2(0f, 0.5f);
            cardTransform.pivot = new Vector2(0.5f, 0.5f);
            card.SetFanPose(new Vector2(310f, 0f), 0f, false);
            card.Show();
            optionViews.Add(card);
            optionsScrollRect.horizontalNormalizedPosition = 0.5f;
            card.ShowReveal(
                revealModel.BackArtwork,
                revealModel.FrontArtwork,
                revealModel.CardTitle,
                revealModel.CardSubtitle,
                null);
            ClearSelectedUiObject();
        }

        public override void Hide()
        {
            if (panel != null) panel.SetActive(false);
            base.Hide();
            ClearSelectedUiObject();
        }

        public override void Unbind()
        {
            ReleaseOptions();
            selectedCardIds.Clear();
            model = null;
            revealModel = null;
            RemoveListeners(dismissOverlayButton);
            RemoveListeners(confirmButton);
            RemoveListeners(cancelButton);
            if (dismissOverlayButton != null) dismissOverlayButton.interactable = true;
            if (cancelButton != null) cancelButton.gameObject.SetActive(true);
            if (confirmButton != null) confirmButton.interactable = false;
            if (panel != null) panel.SetActive(false);
            if (titleLabel != null) titleLabel.text = string.Empty;
            if (instructionLabel != null) instructionLabel.text = string.Empty;
            if (statusLabel != null) statusLabel.text = string.Empty;
            if (confirmButtonLabel != null) confirmButtonLabel.text = string.Empty;
            if (cancelButtonLabel != null) cancelButtonLabel.text = string.Empty;
            SetUnusedRootsActive(true);
        }

        private void BuildOptions()
        {
            for (int i = 0; i < model.Options.Count; i++)
            {
                PrototypeFocusedCardOptionModel option = model.Options[i];
                PrototypeFocusedCardOptionView view =
                    uiService.AcquirePooled<PrototypeFocusedCardOptionView>(
                        PrototypeUiPrefabIds.FocusedCardOption,
                        optionsRoot);
                view.Bind(option, ToggleSelection);
                if (option.Selected) selectedCardIds.Add(option.CardId);
                view.Show();
                optionViews.Add(view);
            }
            ArrangeFan(animateEntry: true);
        }

        private void ToggleSelection(TabletopObjectId cardId)
        {
            int index = selectedCardIds.IndexOf(cardId);
            if (index >= 0)
            {
                selectedCardIds.RemoveAt(index);
            }
            else
            {
                if (model.MaximumSelectionCount == 1) selectedCardIds.Clear();
                if (selectedCardIds.Count >= model.MaximumSelectionCount) return;
                selectedCardIds.Add(cardId);
            }
            RefreshSelectionState();
        }

        private void RefreshSelectionState()
        {
            for (int i = 0; i < optionViews.Count; i++)
                optionViews[i].SetSelected(selectedCardIds.Contains(optionViews[i].CardId));
            confirmButton.interactable = model != null && model.CanConfirm(selectedCardIds.AsReadOnly());
        }

        private void Confirm()
        {
            if (model == null || !model.CanConfirm(selectedCardIds.AsReadOnly())) return;
            model.Confirm(new List<TabletopObjectId>(selectedCardIds).AsReadOnly());
        }

        private void CompleteReveal()
        {
            Action completed = revealModel?.Completed;
            completed?.Invoke();
        }

        private static void IgnoreChoice(TabletopObjectId _)
        {
        }

        private void ConfigureFocusedLayout()
        {
            optionsScrollRect.horizontal = true;
            optionsScrollRect.vertical = false;
            RectTransform viewport = optionsScrollRect.GetComponent<RectTransform>();
            viewport.anchorMin = new Vector2(0.04f, 0.12f);
            viewport.anchorMax = new Vector2(0.96f, 0.70f);
            viewport.anchoredPosition = Vector2.zero;
            viewport.sizeDelta = Vector2.zero;
            optionsRoot.anchorMin = new Vector2(0f, 0.5f);
            optionsRoot.anchorMax = new Vector2(0f, 0.5f);
            optionsRoot.pivot = new Vector2(0f, 0.5f);
            VerticalLayoutGroup vertical = optionsRoot.GetComponent<VerticalLayoutGroup>();
            if (vertical != null) vertical.enabled = false;
            ContentSizeFitter fitter = optionsRoot.GetComponent<ContentSizeFitter>();
            if (fitter != null) fitter.enabled = false;
        }

        private void ArrangeFan(bool animateEntry)
        {
            const float spacing = 126f;
            float width = Mathf.Max(620f, ((optionViews.Count - 1) * spacing) + 170f);
            optionsRoot.sizeDelta = new Vector2(width, 260f);
            float center = (optionViews.Count - 1) * 0.5f;
            for (int i = 0; i < optionViews.Count; i++)
            {
                float offset = i - center;
                RectTransform card = optionViews[i].GetComponent<RectTransform>();
                card.anchorMin = new Vector2(0f, 0.5f);
                card.anchorMax = new Vector2(0f, 0.5f);
                card.pivot = new Vector2(0.5f, 0.5f);
                optionViews[i].SetFanPose(
                    new Vector2((width * 0.5f) + (offset * spacing), -Mathf.Abs(offset) * 2.5f),
                    -offset * 2.4f,
                    animateEntry);
            }
            optionsScrollRect.horizontalNormalizedPosition = 0.5f;
        }

        private void ReleaseOptions()
        {
            for (int i = optionViews.Count - 1; i >= 0; i--)
            {
                if (optionViews[i] != null && uiService != null) uiService.Release(optionViews[i]);
            }
            optionViews.Clear();
        }

        private void Update()
        {
            if (Keyboard.current != null
                && Keyboard.current.escapeKey.wasPressedThisFrame
                && model != null)
            {
                model.Dismiss();
            }
        }

        private void SetUnusedRootsActive(bool active)
        {
            if (unusedAuthoredRoots == null) return;
            for (int i = 0; i < unusedAuthoredRoots.Length; i++)
                if (unusedAuthoredRoots[i] != null) unusedAuthoredRoots[i].SetActive(active);
        }

        private static void BindButton(Button button, Action callback)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(callback.Invoke);
        }

        private static void RemoveListeners(Button button)
        {
            if (button != null) button.onClick.RemoveAllListeners();
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
