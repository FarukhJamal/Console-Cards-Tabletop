using System;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>
    /// HUD-B: one rule on a rulebook page: its number, its words, a HOUSE mark when the table changed it, and a small
    /// CHANGE button that proposes a house rule (doc 23, R3). The row is a template on the rulebook prefab, cloned
    /// per line and kept.
    /// </summary>
    public sealed class RulebookLineView : MonoBehaviour
    {
        [SerializeField] private Text numberLabel;
        [SerializeField] private Text textLabel;
        [SerializeField] private GameObject houseMark;
        [SerializeField] private Button changeButton;
        [Tooltip("A heading line uses the number slot for nothing and the words in the heading style.")]
        [SerializeField] private int headingSize = 22;
        [SerializeField] private int ruleSize = 17;

        private string entryStableId = string.Empty;
        private Action<string> proposeChange;

        public bool IsComplete => numberLabel != null && textLabel != null && houseMark != null && changeButton != null;

        public void Bind(RulesCardLine line, Action<string> propose)
        {
            entryStableId = line.EntryStableId;
            proposeChange = propose;
            bool heading = line.Kind == RulesCardLineKind.Heading;
            numberLabel.gameObject.SetActive(!heading);
            numberLabel.text = line.Number;
            textLabel.text = heading ? line.Text.ToUpperInvariant() : line.Text;
            textLabel.fontSize = heading ? headingSize : ruleSize;
            textLabel.fontStyle = heading ? FontStyle.Bold : FontStyle.Normal;
            houseMark.SetActive(!heading && line.Changed);
            bool canChange = !heading && propose != null && !string.IsNullOrEmpty(entryStableId);
            changeButton.gameObject.SetActive(canChange);
            changeButton.onClick.RemoveAllListeners();
            if (canChange) changeButton.onClick.AddListener(Propose);
        }

        private void Propose()
        {
            if (proposeChange != null && !string.IsNullOrEmpty(entryStableId)) proposeChange(entryStableId);
        }
    }
}
