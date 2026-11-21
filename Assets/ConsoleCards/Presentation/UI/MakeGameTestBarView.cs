using System;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>
    /// Doc 63 M1 (owner, 2026-11-21): the small bar on the table while a game being made is tested (TEST ON TABLE).
    /// BACK TO GAME SETUP opens the Make a game panel again, as it was. Built by Create Missing Table HUD (HUD-B).
    /// </summary>
    public sealed class MakeGameTestBarView : ReusableUiView
    {
        [SerializeField] private Button backButton;

        private Action back;

        public void ValidateReferences()
        {
            if (backButton == null)
            {
                throw new InvalidOperationException("MakeGameTestBarView requires its BACK TO GAME SETUP button.");
            }
        }

        public void Bind(Action backToSetup)
        {
            RequireAcquired();
            ValidateReferences();
            back = backToSetup;
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(HandleBack);
        }

        public override void Unbind()
        {
            if (backButton != null) backButton.onClick.RemoveAllListeners();
            back = null;
        }

        private void HandleBack() => back?.Invoke();
    }
}
