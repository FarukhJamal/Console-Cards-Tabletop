using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    public readonly struct PrototypeComponentToolboxBindings
    {
        public PrototypeComponentToolboxBindings(
            Action placeCard,
            Action placeDeck,
            Action placeStack,
            Action placePawn,
            Action placeToken,
            Action placeConsole,
            Action<int> placeDie)
        {
            PlaceCard = placeCard ?? throw new ArgumentNullException(nameof(placeCard));
            PlaceDeck = placeDeck ?? throw new ArgumentNullException(nameof(placeDeck));
            PlaceStack = placeStack ?? throw new ArgumentNullException(nameof(placeStack));
            PlacePawn = placePawn ?? throw new ArgumentNullException(nameof(placePawn));
            PlaceToken = placeToken ?? throw new ArgumentNullException(nameof(placeToken));
            PlaceConsole = placeConsole ?? throw new ArgumentNullException(nameof(placeConsole));
            PlaceDie = placeDie ?? throw new ArgumentNullException(nameof(placeDie));
        }

        public Action PlaceCard { get; }

        public Action PlaceDeck { get; }

        public Action PlaceStack { get; }

        public Action PlacePawn { get; }

        public Action PlaceToken { get; }

        public Action PlaceConsole { get; }

        public Action<int> PlaceDie { get; }
    }

    public sealed class PrototypeComponentToolboxView : MonoBehaviour
    {
        [SerializeField] private Button addComponentButton;
        [SerializeField] private GameObject toolboxOverlay;
        [SerializeField] private Button dismissOverlayButton;
        [SerializeField] private GameObject toolboxPanel;
        [SerializeField] private Button cardButton;
        [SerializeField] private Button deckButton;
        [SerializeField] private Button stackButton;
        [SerializeField] private Button pawnButton;
        [SerializeField] private Button tokenButton;
        [SerializeField] private Button consoleButton;
        [SerializeField] private Button dieButton;
        [SerializeField] private GameObject dieChoicePanel;
        [SerializeField] private Button d4Button;
        [SerializeField] private Button d6Button;
        [SerializeField] private Button d8Button;
        [SerializeField] private Button d10Button;
        [SerializeField] private Button d12Button;
        [SerializeField] private Button d20Button;
        [SerializeField] private GameObject placementHintPanel;
        [SerializeField] private Text placementSubjectLabel;
        private Action beforeOpen;

        public void ValidateReferences()
        {
            if (addComponentButton == null
                || toolboxOverlay == null
                || dismissOverlayButton == null
                || toolboxPanel == null
                || cardButton == null
                || deckButton == null
                || stackButton == null
                || pawnButton == null
                || tokenButton == null
                || consoleButton == null
                || dieButton == null
                || dieChoicePanel == null
                || d4Button == null
                || d6Button == null
                || d8Button == null
                || d10Button == null
                || d12Button == null
                || d20Button == null
                || placementHintPanel == null
                || placementSubjectLabel == null)
            {
                throw new InvalidOperationException(
                    "PrototypeComponentToolboxView requires all authored controls, panels, and placement-hint references.");
            }
        }

        public void Bind(PrototypeComponentToolboxBindings bindings, Action beforeOpenToolbox = null)
        {
            if (bindings.PlaceCard == null
                || bindings.PlaceDeck == null
                || bindings.PlaceStack == null
                || bindings.PlacePawn == null
                || bindings.PlaceToken == null
                || bindings.PlaceConsole == null
                || bindings.PlaceDie == null)
            {
                throw new ArgumentException(
                    "PrototypeComponentToolboxView requires callbacks for every authored component and Die choice.",
                    nameof(bindings));
            }

            ValidateReferences();
            Unbind();
            beforeOpen = beforeOpenToolbox;

            addComponentButton.onClick.AddListener(ToggleToolbox);
            dismissOverlayButton.onClick.AddListener(CloseToolbox);
            dieButton.onClick.AddListener(ToggleDieChoices);
            BindPlacement(cardButton, bindings.PlaceCard);
            BindPlacement(deckButton, bindings.PlaceDeck);
            BindPlacement(stackButton, bindings.PlaceStack);
            BindPlacement(pawnButton, bindings.PlacePawn);
            BindPlacement(tokenButton, bindings.PlaceToken);
            BindPlacement(consoleButton, bindings.PlaceConsole);
            BindDiePlacement(d4Button, 4, bindings.PlaceDie);
            BindDiePlacement(d6Button, 6, bindings.PlaceDie);
            BindDiePlacement(d8Button, 8, bindings.PlaceDie);
            BindDiePlacement(d10Button, 10, bindings.PlaceDie);
            BindDiePlacement(d12Button, 12, bindings.PlaceDie);
            BindDiePlacement(d20Button, 20, bindings.PlaceDie);

            addComponentButton.interactable = true;
            CloseToolbox();
            ClearPlacementHint();
        }

        public void ShowPlacementHint(string placementSubject)
        {
            if (string.IsNullOrWhiteSpace(placementSubject))
            {
                throw new ArgumentException("A placement hint requires a component name.", nameof(placementSubject));
            }

            ValidateReferences();
            placementSubjectLabel.text = $"Placing: {placementSubject}";
            placementHintPanel.SetActive(true);
        }

        public void ClearPlacementHint()
        {
            if (placementSubjectLabel != null)
            {
                placementSubjectLabel.text = string.Empty;
            }

            if (placementHintPanel != null)
            {
                placementHintPanel.SetActive(false);
            }
        }

        public void CloseToolbox()
        {
            if (dieChoicePanel != null)
            {
                dieChoicePanel.SetActive(false);
            }

            if (toolboxOverlay != null)
            {
                toolboxOverlay.SetActive(false);
            }

            ClearSelectedUiObject();
        }

        public void Unbind()
        {
            RemoveListeners(addComponentButton);
            RemoveListeners(dismissOverlayButton);
            RemoveListeners(cardButton);
            RemoveListeners(deckButton);
            RemoveListeners(stackButton);
            RemoveListeners(pawnButton);
            RemoveListeners(tokenButton);
            RemoveListeners(consoleButton);
            RemoveListeners(dieButton);
            RemoveListeners(d4Button);
            RemoveListeners(d6Button);
            RemoveListeners(d8Button);
            RemoveListeners(d10Button);
            RemoveListeners(d12Button);
            RemoveListeners(d20Button);

            if (addComponentButton != null)
            {
                addComponentButton.interactable = false;
            }

            beforeOpen = null;
            CloseToolbox();
            ClearPlacementHint();
        }

        private void ToggleToolbox()
        {
            bool show = !toolboxOverlay.activeSelf;
            if (show)
            {
                beforeOpen?.Invoke();
            }

            toolboxOverlay.SetActive(show);
            dieChoicePanel.SetActive(false);
            ClearSelectedUiObject();
        }

        private void ToggleDieChoices()
        {
            dieChoicePanel.SetActive(!dieChoicePanel.activeSelf);
            ClearSelectedUiObject();
        }

        private void BeginPlacement(Action beginPlacement)
        {
            CloseToolbox();
            beginPlacement.Invoke();
        }

        private void BeginDiePlacement(int sideCount, Action<int> beginPlacement)
        {
            CloseToolbox();
            beginPlacement.Invoke(sideCount);
        }

        private void BindPlacement(Button button, Action beginPlacement)
        {
            button.onClick.AddListener(() => BeginPlacement(beginPlacement));
        }

        private void BindDiePlacement(Button button, int sideCount, Action<int> beginPlacement)
        {
            button.onClick.AddListener(() => BeginDiePlacement(sideCount, beginPlacement));
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
            Unbind();
        }
    }
}
