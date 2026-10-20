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
using static ConsoleCards.Presentation.Composition.TabletopComposition;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// The card inspection panel: showing a card large with its text and art.
    /// (R1d: moved out of TabletopComposition; the table reaches it as <c>CardInspectPanel</c> and it
    /// reaches the table through <c>host</c>.)
    /// </summary>
    internal sealed class CardInspectPresenter
    {
        private readonly TabletopComposition host;

        internal CardInspectPresenter(TabletopComposition host)
        {
            this.host = host;
        }

        // ---------- from TabletopComposition.CardInspect.cs ----------

        internal void OpenCardInspect(TabletopObjectId targetCardId)
        {
            host.ActiveCartridge.ForgetInspectContext(); // CART-1b: e.g. which reveal the inspect was opened for
            if (host.ContextMenus.IsInAnotherPlayersHand(targetCardId))
            {
                host.ShowMessage("That card is in another player's hand.");
                host.ContextMenus.CloseContextMenu();
                return;
            }

            if (!TryBuildCardInspectModel(targetCardId, out CardInspectModel model))
            {
                host.ShowMessage("Inspect unavailable: Card Presentation is no longer available.");
                host.ContextMenus.CloseContextMenu();
                return;
            }

            host.ContextMenus.CloseContextMenu();
            host.inspectedCardId = targetCardId;
            host.inspectedCardRenderedRevision = host.matchState.Revision;
            host.runtimeUi.ShowCardInspect(model, CloseCardInspect);
        }

        internal void CloseCardInspect()
        {
            host.ActiveCartridge.ForgetInspectContext(); // CART-1b: e.g. which reveal the inspect was opened for
            host.inspectedCardId = TabletopObjectId.Empty;
            host.inspectedCardRenderedRevision = -1;
            host.runtimeUi?.CloseCardInspect();
        }

        internal void RefreshCardInspectPopup()
        {
            if (host.inspectedCardId.IsEmpty || !host.IsInitialized)
            {
                return;
            }

            // R2c: the card's picture and texts only change with the table; build the model only then (it used to
            // be built every frame and thrown away).
            if (host.matchState != null && host.inspectedCardRenderedRevision == host.matchState.Revision)
            {
                return;
            }

            if (!TryBuildCardInspectModel(host.inspectedCardId, out CardInspectModel model))
            {
                CloseCardInspect();
                return;
            }

            if (host.inspectedCardRenderedRevision != host.matchState.Revision)
            {
                host.runtimeUi.RefreshCardInspect(model);
                host.inspectedCardRenderedRevision = host.matchState.Revision;
            }
        }

        internal bool TryBuildCardInspectModel(
            TabletopObjectId targetCardId,
            out CardInspectModel model)
        {
            if (host.matchState == null
                || !host.matchState.Cards.TryGetValue(targetCardId, out CardInstanceState card)
                || !TryGetCardVisualReferences(targetCardId, out CardVisualReferences visualReferences))
            {
                model = null;
                return false;
            }

            // CART-1b: a game's own pieces (for example a board tile) say what they show.
            if (host.ActiveCartridge.TryBuildInspectModel(targetCardId, card, out model))
            {
                return true;
            }

            Color frontSurface = host.IsButtonCard(card)
                ? new Color(0.58f, 0.88f, 0.82f)
                : new Color(0.95f, 0.88f, 0.42f);
            bool contentVisible = host.ShouldShowCardContent(card);
            string obscuredContent = "Content is currently obscured by its Container.";
            bool hasAuthoredDefinition = host.TryGetAuthoredCardDefinition(
                card.BaseState.DefinitionId,
                out CardDefinition authoredDefinition);
            string authoredFrontTitle = hasAuthoredDefinition
                ? authoredDefinition.DisplayName
                : "FRONT";
            string authoredFrontBody = hasAuthoredDefinition
                ? authoredDefinition.Description + host.ActiveCartridge.FormatInputCost(authoredDefinition)
                : visualReferences.FrontLabel.text;
            model = new CardInspectModel(
                targetCardId.ToString(),
                card.Face,
                new CardInspectSideModel(
                    authoredFrontTitle,
                    contentVisible ? authoredFrontBody : obscuredContent,
                    null,
                    frontSurface,
                    new Color(0.06f, 0.08f, 0.10f)),
                new CardInspectSideModel(
                    "BACK",
                    contentVisible ? visualReferences.BackLabel.text : obscuredContent,
                    null,
                    new Color(0.10f, 0.19f, 0.42f),
                    Color.white),
                false);
            return true;
        }

        internal bool TryGetCardVisualReferences(
            TabletopObjectId targetCardId,
            out CardVisualReferences resolvedReferences)
        {
            for (int i = 0; i < host.Registry.cardVisualReferences.Count; i++)
            {
                CardVisualReferences candidate = host.Registry.cardVisualReferences[i];
                if (candidate != null
                    && candidate.CardView != null
                    && candidate.CardView.IsBound
                    && candidate.CardView.ObjectId == targetCardId)
                {
                    resolvedReferences = candidate;
                    return true;
                }
            }

            resolvedReferences = null;
            return false;
        }
    }
}
