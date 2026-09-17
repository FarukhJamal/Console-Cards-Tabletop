using System;
using ConsoleCards.Core.Domain;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    public sealed class PrototypeCardInspectView : ReusableUiView
    {
        [SerializeField] private Button dismissOverlayButton;
        [SerializeField] private GameObject panel;
        [SerializeField] private Text identityLabel;
        [SerializeField] private Text faceStateLabel;
        [SerializeField] private Image cardSurface;
        [SerializeField] private Image artworkImage;
        [SerializeField] private Text sideTitleLabel;
        [SerializeField] private Text sideBodyLabel;
        [SerializeField] private RectTransform iconsRoot;
        [SerializeField] private Button viewOtherSideButton;
        [SerializeField] private Text viewOtherSideButtonLabel;
        [SerializeField] private Button closeButton;

        private PrototypeCardInspectModel model;
        private CardFace displayedFace;
        private Action dismiss;

        public void ValidateReferences()
        {
            if (dismissOverlayButton == null
                || panel == null
                || identityLabel == null
                || faceStateLabel == null
                || cardSurface == null
                || artworkImage == null
                || sideTitleLabel == null
                || sideBodyLabel == null
                || iconsRoot == null
                || viewOtherSideButton == null
                || viewOtherSideButtonLabel == null
                || closeButton == null)
            {
                throw new InvalidOperationException(
                    "PrototypeCardInspectView requires its authored overlay, card surface, content fields, and controls.");
            }
        }

        public void Bind(PrototypeCardInspectModel inspectModel, Action dismissPopup)
        {
            RequireAcquired();
            if (inspectModel == null)
            {
                throw new ArgumentNullException(nameof(inspectModel));
            }

            ValidateReferences();
            Unbind();
            model = inspectModel;
            displayedFace = inspectModel.AuthoritativeFace;
            dismiss = dismissPopup ?? throw new ArgumentNullException(nameof(dismissPopup));
            BindButton(dismissOverlayButton, dismissPopup);
            BindButton(closeButton, dismissPopup);
            panel.SetActive(true);
            ApplyModel();
            ClearSelectedUiObject();
        }

        public void Refresh(PrototypeCardInspectModel inspectModel)
        {
            if (inspectModel == null)
            {
                throw new ArgumentNullException(nameof(inspectModel));
            }

            if (model == null || model.CardIdentity != inspectModel.CardIdentity)
            {
                throw new InvalidOperationException(
                    "An open Card inspection popup cannot be rebound to a different authoritative Card identity.");
            }

            model = inspectModel;
            if (!model.CanViewOtherSide || displayedFace != model.AuthoritativeFace)
            {
                displayedFace = model.AuthoritativeFace;
            }

            ApplyModel();
        }

        public override void Hide()
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }

            base.Hide();
            ClearSelectedUiObject();
        }

        public override void Unbind()
        {
            RemoveListeners(dismissOverlayButton);
            RemoveListeners(viewOtherSideButton);
            RemoveListeners(closeButton);
            model = null;
            dismiss = null;
            if (identityLabel != null) identityLabel.text = string.Empty;
            if (faceStateLabel != null) faceStateLabel.text = string.Empty;
            if (sideTitleLabel != null) sideTitleLabel.text = string.Empty;
            if (sideBodyLabel != null) sideBodyLabel.text = string.Empty;
            if (artworkImage != null) artworkImage.sprite = null;
            if (viewOtherSideButtonLabel != null) viewOtherSideButtonLabel.text = string.Empty;
        }

        private void ToggleInspectionSide()
        {
            if (model == null || !model.CanViewOtherSide)
            {
                return;
            }

            displayedFace = displayedFace == CardFace.FaceUp
                ? CardFace.FaceDown
                : CardFace.FaceUp;
            ApplyModel();
        }

        private void ApplyModel()
        {
            PrototypeCardInspectSideModel side = displayedFace == CardFace.FaceUp
                ? model.Front
                : model.Back;
            bool isInspectionOnlySide = displayedFace != model.AuthoritativeFace;
            identityLabel.text = $"Card ID: {model.CardIdentity}";
            faceStateLabel.text = isInspectionOnlySide
                ? $"Authoritative face: {FormatFace(model.AuthoritativeFace)} | Inspection view: {FormatFace(displayedFace)} (Card unchanged)"
                : $"Authoritative face: {FormatFace(model.AuthoritativeFace)}";
            cardSurface.color = side.SurfaceColor;
            sideTitleLabel.text = side.Title;
            sideTitleLabel.color = side.TextColor;
            sideBodyLabel.text = side.Body;
            sideBodyLabel.color = side.TextColor;
            artworkImage.sprite = side.Artwork;
            artworkImage.gameObject.SetActive(side.Artwork != null);
            iconsRoot.gameObject.SetActive(false);
            ConfigureAuxiliaryAction();
        }

        private void ConfigureAuxiliaryAction()
        {
            RemoveListeners(viewOtherSideButton);
            bool hasPrimaryAction = model.PrimaryAction.HasValue;
            bool showAuxiliaryButton = hasPrimaryAction || model.CanViewOtherSide;
            viewOtherSideButton.gameObject.SetActive(showAuxiliaryButton);
            viewOtherSideButton.interactable = !hasPrimaryAction || model.PrimaryAction.Value.Enabled;
            if (hasPrimaryAction)
            {
                PrototypePopupActionOption action = model.PrimaryAction.Value;
                viewOtherSideButtonLabel.text = action.Label;
                BindButton(viewOtherSideButton, action.Selected);
            }
            else if (model.CanViewOtherSide)
            {
                viewOtherSideButtonLabel.text = displayedFace == CardFace.FaceUp
                    ? "View Back (Inspection Only)"
                    : "View Front (Inspection Only)";
                BindButton(viewOtherSideButton, ToggleInspectionSide);
            }
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                dismiss?.Invoke();
            }
        }

        private static string FormatFace(CardFace face)
        {
            return face == CardFace.FaceUp ? "Face Up" : "Face Down";
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

    public sealed class PrototypeCardInspectModel
    {
        public PrototypeCardInspectModel(
            string cardIdentity,
            CardFace authoritativeFace,
            PrototypeCardInspectSideModel front,
            PrototypeCardInspectSideModel back,
            bool canViewOtherSide)
            : this(
                cardIdentity,
                authoritativeFace,
                front,
                back,
                canViewOtherSide,
                null)
        {
        }

        public PrototypeCardInspectModel(
            string cardIdentity,
            CardFace authoritativeFace,
            PrototypeCardInspectSideModel front,
            PrototypeCardInspectSideModel back,
            bool canViewOtherSide,
            PrototypePopupActionOption? primaryAction)
        {
            if (string.IsNullOrWhiteSpace(cardIdentity))
            {
                throw new ArgumentException("Card inspection identity cannot be empty.", nameof(cardIdentity));
            }

            if (canViewOtherSide && primaryAction.HasValue)
            {
                throw new ArgumentException(
                    "Card inspection cannot bind side-toggle and primary actions to the same control.",
                    nameof(primaryAction));
            }

            CardIdentity = cardIdentity;
            AuthoritativeFace = authoritativeFace;
            Front = front ?? throw new ArgumentNullException(nameof(front));
            Back = back ?? throw new ArgumentNullException(nameof(back));
            CanViewOtherSide = canViewOtherSide;
            PrimaryAction = primaryAction;
        }

        public string CardIdentity { get; }

        public CardFace AuthoritativeFace { get; }

        public PrototypeCardInspectSideModel Front { get; }

        public PrototypeCardInspectSideModel Back { get; }

        public bool CanViewOtherSide { get; }

        public PrototypePopupActionOption? PrimaryAction { get; }
    }

    public sealed class PrototypeCardInspectSideModel
    {
        public PrototypeCardInspectSideModel(
            string title,
            string body,
            Sprite artwork,
            Color surfaceColor,
            Color textColor)
        {
            Title = title ?? string.Empty;
            Body = body ?? string.Empty;
            Artwork = artwork;
            SurfaceColor = surfaceColor;
            TextColor = textColor;
        }

        public string Title { get; }

        public string Body { get; }

        public Sprite Artwork { get; }

        public Color SurfaceColor { get; }

        public Color TextColor { get; }
    }
}
