using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>What the Persona panel shows (PERSONA-2).</summary>
    public sealed class PersonaPanelModel
    {
        /// <summary>Each choice's title ("HAIR"), in slot order.</summary>
        public IReadOnlyList<string> Titles = Array.Empty<string>();

        /// <summary>Each choice's current value ("Long"), in slot order.</summary>
        public IReadOnlyList<string> Values = Array.Empty<string>();

        /// <summary>The turning 3D preview of the character.</summary>
        public Texture Preview;

        public string Status = string.Empty;
    }

    /// <summary>The Persona panel's actions, supplied by the game.</summary>
    public sealed class PersonaPanelActions
    {
        /// <summary>The slot and the step (-1 or 1).</summary>
        public Action<int, int> Cycle;
        public Action Randomize;
        /// <summary>Turns the preview: -1 left, 1 right.</summary>
        public Action<int> Turn;
        public Action Close;
    }

    /// <summary>
    /// PERSONA-2 (owner, 2026-10-21: "a proper lite customization"): your character, the one sitting at your side of
    /// the table. A turning 3D preview on the left; on the right one row per choice (body, build, skin, eyes, hair,
    /// hair colour, beard, outfit, top, bottom, shoes, emblem) with &lt; and &gt;; Randomize and Done. Every change is
    /// saved in your profile at once. An authored Final UI prefab made by Create Missing Gameplay UI Prefabs.
    /// </summary>
    public sealed class PersonaPanelView : ReusableUiView
    {
        [SerializeField] private Button dismissOverlayButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private RawImage preview;
        [SerializeField] private Button turnLeftButton;
        [SerializeField] private Button turnRightButton;
        [Tooltip("One per choice row, in slot order.")]
        [SerializeField] private Text[] titleLabels = new Text[0];
        [SerializeField] private Text[] valueLabels = new Text[0];
        [SerializeField] private Button[] previousButtons = new Button[0];
        [SerializeField] private Button[] nextButtons = new Button[0];
        [SerializeField] private Button randomizeButton;
        [SerializeField] private Button doneButton;
        [SerializeField] private Text statusLabel;

        private PersonaPanelActions actions;

        public void ValidateReferences()
        {
            if (dismissOverlayButton == null || closeButton == null || preview == null || doneButton == null
                || statusLabel == null || valueLabels == null || valueLabels.Length == 0)
            {
                throw new InvalidOperationException("PersonaPanelView requires its authored references.");
            }
        }

        public void Bind(PersonaPanelModel model, PersonaPanelActions panelActions)
        {
            RequireAcquired();
            ValidateReferences();
            if (model == null) throw new ArgumentNullException(nameof(model));
            actions = panelActions ?? throw new ArgumentNullException(nameof(panelActions));
            RemoveListeners();

            preview.texture = model.Preview;
            preview.color = model.Preview != null ? Color.white : new Color(1f, 1f, 1f, 0.2f);
            for (int i = 0; i < valueLabels.Length; i++)
            {
                bool used = i < model.Values.Count;
                if (valueLabels[i] != null)
                {
                    valueLabels[i].text = used ? model.Values[i] : string.Empty;
                    Transform row = valueLabels[i].transform.parent;
                    if (row != null) row.gameObject.SetActive(used);
                }

                if (i < titleLabels.Length && titleLabels[i] != null)
                {
                    titleLabels[i].text = i < model.Titles.Count ? model.Titles[i] : string.Empty;
                }
            }

            statusLabel.text = model.Status;
            dismissOverlayButton.onClick.AddListener(HandleClose);
            closeButton.onClick.AddListener(HandleClose);
            doneButton.onClick.AddListener(HandleClose);
            if (randomizeButton != null) randomizeButton.onClick.AddListener(HandleRandomize);
            if (turnLeftButton != null) turnLeftButton.onClick.AddListener(HandleTurnLeft);
            if (turnRightButton != null) turnRightButton.onClick.AddListener(HandleTurnRight);
            for (int i = 0; i < previousButtons.Length; i++)
            {
                int slot = i;
                if (previousButtons[i] != null) previousButtons[i].onClick.AddListener(() => actions?.Cycle?.Invoke(slot, -1));
            }

            for (int i = 0; i < nextButtons.Length; i++)
            {
                int slot = i;
                if (nextButtons[i] != null) nextButtons[i].onClick.AddListener(() => actions?.Cycle?.Invoke(slot, 1));
            }
        }

        public override void Unbind()
        {
            RemoveListeners();
            if (preview != null) preview.texture = null;
            actions = null;
        }

        private void RemoveListeners()
        {
            Clear(dismissOverlayButton);
            Clear(closeButton);
            Clear(doneButton);
            Clear(randomizeButton);
            Clear(turnLeftButton);
            Clear(turnRightButton);
            for (int i = 0; previousButtons != null && i < previousButtons.Length; i++) Clear(previousButtons[i]);
            for (int i = 0; nextButtons != null && i < nextButtons.Length; i++) Clear(nextButtons[i]);
        }

        private static void Clear(Button button)
        {
            if (button != null) button.onClick.RemoveAllListeners();
        }

        private void HandleClose() => actions?.Close?.Invoke();
        private void HandleRandomize() => actions?.Randomize?.Invoke();
        private void HandleTurnLeft() => actions?.Turn?.Invoke(-1);
        private void HandleTurnRight() => actions?.Turn?.Invoke(1);
    }
}
