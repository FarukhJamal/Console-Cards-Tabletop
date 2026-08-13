using System;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    public sealed class PrototypeInteractionGuide : MonoBehaviour
    {
        [SerializeField] private GameObject guidePanel;
        [SerializeField] private Button toggleButton;
        [SerializeField] private Text toggleButtonLabel;
        [SerializeField] private Button closeButton;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text bodyLabel;
        [SerializeField] private string title = "Console Cards Prototype";
        [SerializeField, TextArea(8, 20)] private string guideText =
            "Click Card, Pawn, or Token: select\n"
            + "Click empty table: clear selection\n"
            + "Drag tabletop Card, Pawn, or Token: move\n"
            + "Drag contained Card: transfer or play to table\n"
            + "Drag tabletop Card onto Container: place in Container\n"
            + "Right-click Deck, Card, Stack, or Die: actions\n"
            + "Drag Hand Card left/right: reorder Hand\n"
            + "Esc: cancel / rollback\n"
            + "Mouse wheel + selection: rotate 15 degrees\n"
            + "Mouse wheel + no selection: camera zoom\n"
            + "Scroll during drag: no zoom or rotation\n"
            + "F + selected Card: flip face\n"
            + "F + Pawn or Token: rejected, no visible change\n"
            + "WASD / Arrow keys: camera pan\n"
            + "Middle mouse drag: camera pan";

        public void ValidateReferences()
        {
            if (guidePanel == null
                || toggleButton == null
                || toggleButtonLabel == null
                || closeButton == null
                || titleLabel == null
                || bodyLabel == null)
            {
                throw new InvalidOperationException(
                    "PrototypeInteractionGuide requires its authored panel, buttons, and text references.");
            }
        }

        public void Bind()
        {
            ValidateReferences();
            Unbind();
            titleLabel.text = title;
            bodyLabel.text = guideText;
            toggleButton.onClick.AddListener(ToggleGuide);
            closeButton.onClick.AddListener(HideGuide);
            gameObject.SetActive(true);
            SetGuideVisible(true);
        }

        public void Unbind()
        {
            if (toggleButton != null)
            {
                toggleButton.onClick.RemoveAllListeners();
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
            }
        }

        public void Hide()
        {
            Unbind();
            gameObject.SetActive(false);
        }

        private void ToggleGuide()
        {
            SetGuideVisible(!guidePanel.activeSelf);
        }

        private void HideGuide()
        {
            SetGuideVisible(false);
        }

        private void SetGuideVisible(bool visible)
        {
            guidePanel.SetActive(visible);
            toggleButtonLabel.text = visible ? "Hide Help" : "Show Help";
        }

        private void OnDestroy()
        {
            Unbind();
        }
    }
}
