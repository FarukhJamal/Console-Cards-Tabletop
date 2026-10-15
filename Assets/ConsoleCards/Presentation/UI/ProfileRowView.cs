using System;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>One profile in the profile panel: its name, when it was last played, and Play as (doc 28, P2).</summary>
    public sealed class ProfileRowView : ReusableUiView
    {
        [SerializeField] private Text nameLabel;
        [SerializeField] private Text detailLabel;
        [SerializeField] private Button playAsButton;
        [SerializeField] private GameObject activeMark;

        private string profileId;
        private Action<string> playAs;

        public void Bind(ProfileRowModel row, Action<string> playAsAction)
        {
            RequireAcquired();
            if (nameLabel == null || detailLabel == null || playAsButton == null || activeMark == null)
            {
                throw new InvalidOperationException("ProfileRowView requires its authored references.");
            }

            profileId = row.Id;
            playAs = playAsAction;
            nameLabel.text = row.Name;
            detailLabel.text = row.Detail;
            playAsButton.gameObject.SetActive(!row.IsActive);
            activeMark.SetActive(row.IsActive);
            playAsButton.onClick.RemoveAllListeners();
            playAsButton.onClick.AddListener(HandlePlayAs);
        }

        public override void Unbind()
        {
            if (playAsButton != null) playAsButton.onClick.RemoveAllListeners();
            playAs = null;
        }

        private void HandlePlayAs() => playAs?.Invoke(profileId);
    }
}
