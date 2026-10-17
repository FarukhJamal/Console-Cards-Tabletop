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
using ConsoleCards.Games.TrapFloor;
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
    /// <summary>Card inspection. (R1a: moved here unchanged from TabletopComposition.cs.)</summary>
    public sealed partial class TabletopComposition
    {
        private void OpenCardInspect(TabletopObjectId targetCardId)
        {
            activeFloorRevealActivity = null;
            if (IsInAnotherPlayersHand(targetCardId))
            {
                ShowMessage("That card is in another player's hand.");
                CloseContextMenu();
                return;
            }

            if (!TryBuildCardInspectModel(targetCardId, out CardInspectModel model))
            {
                ShowMessage("Inspect unavailable: Card Presentation is no longer available.");
                CloseContextMenu();
                return;
            }

            CloseContextMenu();
            inspectedCardId = targetCardId;
            inspectedCardRenderedRevision = matchState.Revision;
            runtimeUi.ShowCardInspect(model, CloseCardInspect);
        }

        private void CloseCardInspect()
        {
            activeFloorRevealActivity = null;
            inspectedCardId = TabletopObjectId.Empty;
            inspectedCardRenderedRevision = -1;
            runtimeUi?.CloseCardInspect();
        }

        private void RefreshCardInspectPopup()
        {
            if (inspectedCardId.IsEmpty || !IsInitialized)
            {
                return;
            }

            if (!TryBuildCardInspectModel(inspectedCardId, out CardInspectModel model))
            {
                CloseCardInspect();
                return;
            }

            if (inspectedCardRenderedRevision != matchState.Revision)
            {
                runtimeUi.RefreshCardInspect(model);
                inspectedCardRenderedRevision = matchState.Revision;
            }
        }

        private bool TryBuildCardInspectModel(
            TabletopObjectId targetCardId,
            out CardInspectModel model)
        {
            if (matchState == null
                || !matchState.Cards.TryGetValue(targetCardId, out CardInstanceState card)
                || !TryGetCardVisualReferences(targetCardId, out CardVisualReferences visualReferences))
            {
                model = null;
                return false;
            }

            if (trapFloorTemplate != null
                && trapFloorTemplate.TryGetFloorCardState(
                    matchState,
                    targetCardId,
                    out TrapFloorFloorCardState floorCard))
            {
                if (trapFloorCollapseState != null
                    && trapFloorCollapseState.IsCollapsed(targetCardId))
                {
                    Color holeColor = new Color(0.025f, 0.03f, 0.04f);
                    model = new CardInspectModel(
                        $"Collapsed Floor {floorCard.Coordinate} | {targetCardId}",
                        card.Face,
                        new CardInspectSideModel(
                            "HOLE",
                            $"Floor {floorCard.Coordinate} is permanently collapsed and unusable.",
                            null,
                            holeColor,
                            Color.white),
                        new CardInspectSideModel(
                            "HOLE",
                            $"Floor {floorCard.Coordinate} is permanently collapsed and unusable.",
                            null,
                            holeColor,
                            Color.white),
                        false);
                    return true;
                }

                TrapFloorActivityEntry revealActivity = activeFloorRevealActivity != null
                    && activeFloorRevealActivity.FloorCardId == targetCardId
                        ? activeFloorRevealActivity
                        : null;
                string revealContext = revealActivity == null
                    ? string.Empty
                    : $"Revealed by: {FormatPlayerName(revealActivity.ActorPlayerId)}\n"
                        + $"Floor coordinate: {floorCard.Coordinate}\n";
                string objectiveContext = string.Empty;
                string frontTitle = floorCard.Content.DisplayName;
                string inputCostContext = TryGetAuthoredCardDefinition(
                        card.BaseState.DefinitionId,
                        out CardDefinition floorCardDefinition)
                    ? FormatInputCost(floorCardDefinition)
                    : string.Empty;
                PopupActionOption? primaryAction = null;
                if (floorCard.Content.Category == TrapFloorFloorContentCategory.Trap)
                {
                    bool safelyRevealed = WasTrapSafelyRevealed(targetCardId);
                    bool neutralized = IsTrapFloorTrapNeutralized(targetCardId);
                    objectiveContext = safelyRevealed
                        ? "CAREFUL SEARCH — Trap did not trigger during this reveal.\nThe Floor remains a Trap.\n"
                        : neutralized
                        ? "TRAP NEUTRALIZED\nThis Floor is safe from assisted Trap consequences.\n"
                        : TrapFloorTrapEffectDescription(floorCard.Content) + "\n";
                    if (safelyRevealed)
                    {
                        frontTitle = $"{floorCard.Content.DisplayName} — REVEALED SAFELY";
                    }
                    else if (neutralized)
                    {
                        frontTitle = $"{floorCard.Content.DisplayName} — SAFE";
                    }
                    else if (IsTrapFloorTrapResolved(targetCardId))
                    {
                        frontTitle = $"{floorCard.Content.DisplayName} — RESOLVED";
                        objectiveContext = "TRAP RESOLVED\nIts assisted consequence has already been applied or prevented.\n";
                    }
                    else if (floorCard.Content.HasSupportedAssistedTrapEffect)
                    {
                        primaryAction = new PopupActionOption(
                            "Resolve Trap",
                            CanResolveTrapFloorTrap(targetCardId),
                            () => ResolveTrapFloorTrap(targetCardId));
                    }
                }
                else if (floorCard.Content.Category == TrapFloorFloorContentCategory.Key
                    && trapFloorObjectiveState != null)
                {
                    if (trapFloorObjectiveState.TryGetClaim(
                            targetCardId,
                            out TrapFloorCollectedKeyState claim))
                    {
                        frontTitle = "KEY — CLAIMED";
                        objectiveContext = $"Key status: CLAIMED\n"
                            + $"Claimed by: {FormatPlayerName(claim.ClaimedByPlayerId)}\n";
                    }
                    else
                    {
                        frontTitle = "KEY — UNCLAIMED";
                        objectiveContext = "Key status: UNCLAIMED\n"
                            + "Claim this Key before attempting to escape.\n";
                        primaryAction = new PopupActionOption(
                            "Claim Key",
                            !trapFloorObjectiveState.IsWon,
                            () => ClaimTrapFloorKey(targetCardId));
                    }
                }

                string frontBody = $"{revealContext}{objectiveContext}"
                    + $"Category: {floorCard.Content.Category}\n"
                    + $"Content: {floorCard.Content.DisplayName}\n\n"
                    + floorCard.Content.DisplayText
                    + inputCostContext;
                model = new CardInspectModel(
                    $"Floor {floorCard.Coordinate} | {targetCardId}",
                    card.Face,
                    new CardInspectSideModel(
                        frontTitle,
                        frontBody,
                        null,
                        TrapFloorContentColor(floorCard.Content.Category),
                        new Color(0.04f, 0.06f, 0.08f)),
                    new CardInspectSideModel(
                        "MYSTERY",
                        $"Floor {floorCard.Coordinate}\nContent stays hidden until a pawn lands on it.",
                        null,
                        new Color(0.10f, 0.19f, 0.42f),
                        Color.white),
                    false,
                    primaryAction);
                return true;
            }

            Color frontSurface = IsButtonCard(card)
                ? new Color(0.58f, 0.88f, 0.82f)
                : new Color(0.95f, 0.88f, 0.42f);
            bool contentVisible = ShouldShowCardContent(card);
            string obscuredContent = "Content is currently obscured by its Container.";
            bool hasAuthoredDefinition = TryGetAuthoredCardDefinition(
                card.BaseState.DefinitionId,
                out CardDefinition authoredDefinition);
            string authoredFrontTitle = hasAuthoredDefinition
                ? authoredDefinition.DisplayName
                : "FRONT";
            string authoredFrontBody = hasAuthoredDefinition
                ? authoredDefinition.Description + FormatInputCost(authoredDefinition)
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

        private bool TryGetCardVisualReferences(
            TabletopObjectId targetCardId,
            out CardVisualReferences resolvedReferences)
        {
            for (int i = 0; i < cardVisualReferences.Count; i++)
            {
                CardVisualReferences candidate = cardVisualReferences[i];
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
