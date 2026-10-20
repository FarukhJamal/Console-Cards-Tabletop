using System;
using System.Collections.Generic;
using System.Text;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Random;
using ConsoleCards.Application.Results;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Boards;
using ConsoleCards.Core.Domain.Cards;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Consoles;
using ConsoleCards.Core.Domain.Dice;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.PlayAreas;
using ConsoleCards.Core.Domain.PlayerLayouts;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Events;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Definitions;
using ConsoleCards.GameTemplates;
using ConsoleCards.GameTemplates.ControllerInputs;
using ConsoleCards.GameTemplates.Definitions;
using ConsoleCards.Presentation.Art;
using ConsoleCards.Presentation.Camera;
using ConsoleCards.Presentation.Content;
using ConsoleCards.Presentation.Catalog;
using ConsoleCards.Presentation.Coordinates;
using ConsoleCards.Presentation.Feedback;
using ConsoleCards.Presentation.Hints;
using ConsoleCards.Presentation.Input;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.Saving;
using ConsoleCards.Presentation.Settings;
using ConsoleCards.Presentation.UI;
using ConsoleCards.Presentation.UI.Toolbox;
using ConsoleCards.Presentation.Views;
using ConsoleCards.Presentation.Views.Containers;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityCamera = UnityEngine.Camera;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// R2c: the status line and the game's status card and turn strip are redrawn when something changed, not
    /// every frame (they build new texts and button lists, which made garbage every frame). They redraw when:
    /// a table change is accepted (the table authority's announcement, or the match revision moving for changes
    /// made outside the door); a game step asks for it (<see cref="MarkTableUiDirty"/>, which the old hand-called
    /// refreshes now do); the turn, round, waiting and pending states change (a small value check each frame, no
    /// garbage); a message on the status line runs out; or, as a safety net, every half second.
    /// </summary>
    public sealed partial class TabletopComposition
    {
        private const float TableUiSafetyRefreshSeconds = 0.5f;

        private bool tableUiDirty = true;
        private long tableUiRenderedRevision = -1;
        private int tableUiRenderedSignature;
        private bool tableUiRenderedWithMessage;
        private float tableUiNextSafetyRefreshAt;

        /// <summary>
        /// R2d: raised once after every table build (new table, Reset, Undo, Redo, loading, a seat switch): the table
        /// was replaced as a whole, so anything that follows it starts again from the new state instead of from
        /// single changes. Joining a networked game will arrive the same way (a whole table at once, MP-1).
        /// </summary>
        internal event Action TableReplaced;

        private void NotifyTableReplaced()
        {
            // A rebuilt match can carry a revision number already drawn before (Undo, then Redo), so nothing may be
            // skipped as "already drawn".
            tableUiDirty = true;
            tableUiRenderedRevision = -1;
            inspectedCardRenderedRevision = -1;
            TableReplaced?.Invoke();
        }

        /// <summary>Asks for the status line and the game's status to be redrawn at the end of this frame.</summary>
        internal void MarkTableUiDirty()
        {
            tableUiDirty = true;
        }

        // Every accepted change, whoever made it (R2).
        private void HandleTableCommandAccepted(object command)
        {
            tableUiDirty = true;
        }

        // Called once per frame from Update.
        private void RefreshTableUiIfChanged()
        {
            if (!IsInitialized || runtimeUi == null)
            {
                return;
            }

            long revision = matchState != null ? matchState.Revision : -1;
            int signature = ActiveCartridge.StatusUiSignature();
            bool withMessage = Time.unscaledTime <= operationMessageUntil;
            bool safetyDue = Time.unscaledTime >= tableUiNextSafetyRefreshAt;
            if (!tableUiDirty
                && !safetyDue
                && revision == tableUiRenderedRevision
                && signature == tableUiRenderedSignature
                && withMessage == tableUiRenderedWithMessage)
            {
                return;
            }

            tableUiDirty = false;
            tableUiRenderedRevision = revision;
            tableUiRenderedSignature = signature;
            tableUiRenderedWithMessage = withMessage;
            tableUiNextSafetyRefreshAt = Time.unscaledTime + TableUiSafetyRefreshSeconds;
            SessionService.RefreshRuntimeStatusUi();
            ActiveCartridge.RenderStatusUi();
        }
    }
}
