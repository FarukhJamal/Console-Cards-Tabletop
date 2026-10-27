using System;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>
    /// The hint strip (doc 23 §10): one hint at a time under the session bar, with "Got it" (never show it again)
    /// and "Later" (hide it for now). An authored Final UI prefab; nothing is built at runtime.
    /// </summary>
    public sealed class HintStripView : ReusableUiView
    {
        [SerializeField] private Text kindLabel;
        [SerializeField] private Text textLabel;
        [SerializeField] private Button gotItButton;
        [SerializeField] private Button laterButton;

        private const int MaxHintCharacters = 90;

        public void ValidateReferences()
        {
            if (kindLabel == null || textLabel == null || gotItButton == null || laterButton == null)
            {
                throw new InvalidOperationException(
                    "HintStripView is missing an authored reference. Run Console Cards > UI > Create Missing Gameplay UI Prefabs (Final UI).");
            }
        }

        public void Bind(string kind, string text, Action gotIt, Action later)
        {
            RequireAcquired();
            ValidateReferences();
            RemoveListeners();
            kindLabel.text = string.IsNullOrEmpty(kind) ? "TIP" : kind;
            FitText.Shrink(textLabel); // MP-P (owner, 2026-10-27: "the hints are too big"): short, and always inside the strip
            textLabel.text = FitText.Concise(text, MaxHintCharacters);
            if (gotIt != null) gotItButton.onClick.AddListener(() => gotIt());
            if (later != null) laterButton.onClick.AddListener(() => later());
        }

        public override void Unbind()
        {
            RemoveListeners();
            if (textLabel != null) textLabel.text = string.Empty;
        }

        private void RemoveListeners()
        {
            if (gotItButton != null) gotItButton.onClick.RemoveAllListeners();
            if (laterButton != null) laterButton.onClick.RemoveAllListeners();
        }
    }
}
