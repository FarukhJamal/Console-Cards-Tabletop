using System;
using UnityEngine;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>
    /// Doc 63 M1 (owner, 2026-11-21): while a game being made is tested on the table, a small bar with BACK TO GAME
    /// SETUP. It sits in the HUD layer, so the table, its HUD and its tools stay usable around it.
    /// </summary>
    public sealed partial class TableUiController
    {
        private MakeGameTestBarView makeGameTestBarView;
        private bool makeGameTestBarWarned;

        /// <summary>Shows the BACK TO GAME SETUP bar. False when its prefab is missing (warned once).</summary>
        public bool ShowMakeGameTestBar(Action backToSetup)
        {
            try
            {
                EnsureService();
                if (makeGameTestBarView == null || !makeGameTestBarView.IsAcquired)
                {
                    makeGameTestBarView = uiService.AcquireCached<MakeGameTestBarView>(UiPrefabIds.MakeGameTestBar);
                }

                makeGameTestBarView.Bind(backToSetup);
                makeGameTestBarView.Show();
                return true;
            }
            catch (InvalidOperationException exception)
            {
                if (!makeGameTestBarWarned)
                {
                    makeGameTestBarWarned = true;
                    Debug.LogWarning(
                        "The BACK TO GAME SETUP bar is missing: run Console Cards > UI > Create Missing Table HUD (HUD-B). "
                        + exception.Message);
                }

                return false;
            }
        }

        public void HideMakeGameTestBar() => ReleaseView(makeGameTestBarView);
    }
}
