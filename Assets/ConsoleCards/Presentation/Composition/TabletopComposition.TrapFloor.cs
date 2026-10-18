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
    /// <summary>The table side of a game: the pawn hops each frame and passing Console card events on. The Trap Floor game itself is TrapFloorCartridge (R1e).</summary>
    public sealed partial class TabletopComposition
    {

        private void LateUpdate()
        {
            cardFlipTurns?.Tick(Time.unscaledTime);
            if (pawnHops.Count == 0) return;
            float now = Time.unscaledTime;
            for (int i = pawnHops.Count - 1; i >= 0; i--)
            {
                PawnHop hop = pawnHops[i];
                if (hop.Visual == null)
                {
                    pawnHops.RemoveAt(i);
                    continue;
                }

                float t = Mathf.Clamp01((now - hop.Started) / Mathf.Max(0.05f, pawnHopSeconds));
                if (t >= 1f || hop.Visual.parent == null)
                {
                    hop.Visual.localPosition = hop.RestLocal;
                    pawnHops.RemoveAt(i);
                    continue;
                }

                Vector3 restWorld = hop.Visual.parent.TransformPoint(hop.RestLocal);
                float eased = t * t * (3f - (2f * t));
                hop.Visual.position = Vector3.Lerp(hop.StartWorld, restWorld, eased)
                    + (Vector3.up * (pawnHopHeight * 4f * t * (1f - t)));
            }
        }

        private void HandleConsoleCardInteractionAccepted(IConsoleCardInteraction interaction)
        {
            if (interaction == null)
            {
                return;
            }

            ConsoleCardBehavior behavior = ConsoleCardBehavior.None;
            string cardName = "Card";
            if (TryGetAuthoredCardDefinition(interaction.CardDefinitionId, out CardDefinition definition))
            {
                behavior = definition.ConsoleBehavior;
                cardName = definition.DisplayName;
            }

            bool inserted = interaction is ConsoleCardInserted;
            string verb = inserted ? "inserted" : "removed";
            string direction = inserted ? "into" : "from";
            ShowMessage(
                $"{TrapFloorGame.FormatPlayerName(interaction.ActorPlayerId)} {verb} {cardName} {direction} Console");

            try
            {
                ConsoleCardInteractionAccepted?.Invoke(interaction, behavior);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }
    }
}
