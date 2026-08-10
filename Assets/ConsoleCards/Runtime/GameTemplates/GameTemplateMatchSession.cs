using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.PlayerLayouts;

namespace ConsoleCards.GameTemplates
{
    /// <summary>
    /// Owns the live Match produced by one Template bootstrap and its immutable initial baseline.
    /// </summary>
    public sealed class GameTemplateMatchSession
    {
        private readonly ReadOnlyCollection<GameTemplateCameraBookmarkDefinition> cameraBookmarks;

        internal GameTemplateMatchSession(
            GameTemplate template,
            PlayerLayoutDefinition playerLayout,
            MatchState currentMatch,
            GameTemplateInitialSnapshot initialBaseline)
        {
            Template = template ?? throw new ArgumentNullException(nameof(template));
            PlayerLayout = playerLayout ?? throw new ArgumentNullException(nameof(playerLayout));
            CurrentMatch = currentMatch ?? throw new ArgumentNullException(nameof(currentMatch));
            InitialBaseline = initialBaseline ?? throw new ArgumentNullException(nameof(initialBaseline));
            cameraBookmarks = new ReadOnlyCollection<GameTemplateCameraBookmarkDefinition>(
                new List<GameTemplateCameraBookmarkDefinition>(template.CameraBookmarks));
        }

        public GameTemplate Template { get; }

        public PlayerLayoutDefinition PlayerLayout { get; }

        public MatchState CurrentMatch { get; private set; }

        public GameTemplateInitialSnapshot InitialBaseline { get; }

        public IReadOnlyList<GameTemplateCameraBookmarkDefinition> CameraBookmarks => cameraBookmarks;

        /// <summary>
        /// Restores from the captured authoritative baseline. The current Match is replaced only after
        /// the complete replacement has been constructed successfully.
        /// </summary>
        public MatchState Reset()
        {
            MatchState replacement = InitialBaseline.Restore();
            CurrentMatch = replacement;
            return replacement;
        }
    }
}
