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

        public void ValidateReferences()
        {
            if (textLabel == null)
            {
                throw new InvalidOperationException("RulesCardLineView requires its text label. Run Console Cards > UI > Build Gameplay UI Prefabs.");
            }
        }

        public void Bind(RulesCardLine line)
        {
            RequireAcquired();
            ValidateReferences();
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
