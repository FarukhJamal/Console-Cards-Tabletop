using System;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>
    /// A pooled line of the rules card (doc 23, R1): a section heading, or a rule with its number and a mark when
    /// it differs from the rules it is based on. Heading prefabs leave the number and mark empty.
    /// </summary>
    public sealed class RulesCardLineView : ReusableUiView
    {
        [SerializeField] private Text numberLabel;
        [SerializeField] private Text textLabel;
        [SerializeField] private GameObject changedMark;
        [Tooltip("Optional (doc 23, R3): proposes a change to this rule. Added by Create Missing Gameplay UI Prefabs.")]
        [SerializeField] private Button changeButton;
        [Tooltip("Optional (V5): the whole row proposes the change (it lights on hover); the Change button then hides.")]
        [SerializeField] private Button rowButton;

        public void ValidateReferences()
        {
            if (textLabel == null)
            {
                throw new InvalidOperationException("RulesCardLineView requires its text label. Run Console Cards > UI > Build Gameplay UI Prefabs.");
            }
        }

        public void Bind(RulesCardLine line) => Bind(line, null);

        /// <summary>Shows the line; with <paramref name="proposeChange"/> its Change button proposes a change to it.</summary>
        public void Bind(RulesCardLine line, Action<string> proposeChange)
        {
            RequireAcquired();
            ValidateReferences();
            if (rowButton != null)
            {
                rowButton.onClick.RemoveAllListeners();
                bool rowCanChange = proposeChange != null && !string.IsNullOrEmpty(line.EntryStableId);
                rowButton.interactable = rowCanChange;
                if (rowCanChange)
                {
                    string rowEntryId = line.EntryStableId;
                    rowButton.onClick.AddListener(() => proposeChange(rowEntryId));
                }
            }

            if (changeButton != null)
            {
                changeButton.onClick.RemoveAllListeners();
                bool canChange = proposeChange != null && !string.IsNullOrEmpty(line.EntryStableId) && rowButton == null;
                changeButton.gameObject.SetActive(canChange);
                if (canChange)
                {
                    string entryId = line.EntryStableId;
                    changeButton.onClick.AddListener(() => proposeChange(entryId));
                }
            }

            textLabel.text = line.Text;
            if (numberLabel != null)
            {
                numberLabel.text = line.Number;
            }

            if (changedMark != null)
            {
                changedMark.SetActive(line.Changed);
            }
        }

        public override void Unbind()
        {
            if (rowButton != null) rowButton.onClick.RemoveAllListeners();

            if (changeButton != null)
            {
                changeButton.onClick.RemoveAllListeners();
                changeButton.gameObject.SetActive(false);
            }

            if (textLabel != null)
            {
                textLabel.text = string.Empty;
            }

            if (numberLabel != null)
            {
                numberLabel.text = string.Empty;
            }

            if (changedMark != null)
            {
                changedMark.SetActive(false);
            }
        }
    }
}
