using System;
using System.Collections.Generic;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Definitions;
using ConsoleCards.Games.TrapFloor;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// D2/D3 (owner, 2026-11-02: "add the avatar cards as a deck, and by default don't put an avatar on any player's
    /// Console: the players choose themselves, and after that the system works"): a player chooses an Avatar by
    /// putting its card into their Console's Main slot. The first Avatar a seat gets on a floor brings its starting
    /// Abilities from the STARTING ABILITIES pile into that player's hand. The Avatar a seat has is read from its Main
    /// slot, and a seat keeps its Avatar on the next floor or stage of the same game.
    /// Checked once a frame for the seats played on this computer (no allocation); nothing happens on idle frames.
    /// </summary>
    internal sealed partial class TrapFloorCartridge
    {
        private const int MaxAvatarSeats = 8;
        private readonly bool[] avatarGiven = new bool[MaxAvatarSeats];
        private object avatarCheckedMatch;

        // TickEarly: a seat that just got its first Avatar on this table gets that Avatar's starting Abilities.
        internal void GiveChosenAvatarsTheirAbilities()
        {
            if (host.trapFloorTemplate == null || host.matchState == null || host.undoTransactionInProgress || host.rebuildingFromUndo) return;
            bool newTable = !ReferenceEquals(avatarCheckedMatch, host.matchState);
            avatarCheckedMatch = host.matchState;
            int seats = Math.Min(host.trapFloorTemplate.Players.Count, MaxAvatarSeats);
            for (int i = 0; i < seats; i++)
            {
                TrapFloorPlayerSetupDefinition setup = host.trapFloorTemplate.Players[i];
                bool hasAvatar = TryGetSeatAvatar(setup, out CardInstanceState avatarCard);
                if (newTable)
                {
                    // A table dealt with an Avatar already in place (a later floor) has its Abilities already.
                    avatarGiven[i] = hasAvatar;
                    continue;
                }

                if (avatarGiven[i] || !hasAvatar) continue;
                if (!host.matchState.Seats.TryGetValue(setup.SeatId, out SeatState seat) || seat.OccupantPlayerId.IsEmpty) continue;
                if (!host.IsPlayerOnThisComputer(seat.OccupantPlayerId)) continue; // that player's computer does it
                avatarGiven[i] = true;
                GiveStartingAbilities(setup, seat.OccupantPlayerId, avatarCard);
            }
        }

        private void GiveStartingAbilities(TrapFloorPlayerSetupDefinition setup, PlayerId player, CardInstanceState avatarCard)
        {
            ObjectDefinitionId avatarDefinition = avatarCard.BaseState.DefinitionId;
            string avatarName = host.TryGetAvatarLook(avatarDefinition, out AvatarDefinition look) ? look.DisplayName : "an Avatar";
            int avatarIndex = TrapFloorTemplateFactory.AvatarIndexOf(host.trapFloorTemplate.GameDefinition, avatarDefinition, out int abilityCount);
            ContainerId pile = TrapFloorTemplateFactory.StartingAbilitiesId;
            int given = 0;
            if (avatarIndex >= 0 && abilityCount > 0 && host.matchState.Containers.ContainsKey(pile))
            {
                host.SessionService.BeginUndoBatch();
                try
                {
                    // P8A (owner, 2026-11-08): the starting Abilities slide out beside the player's Console (the spot a
                    // bought card uses), not into the hand.
                    List<TableCoordinate> loose = new List<TableCoordinate>();
                    foreach (CardInstanceState lying in host.matchState.Cards.Values)
                    {
                        if (lying.BaseState.ContainerId.IsEmpty && !lying.BaseState.IsUserLocked) loose.Add(lying.BaseState.Pose.Position);
                    }

                    for (int a = 0; a < abilityCount; a++)
                    {
                        TabletopObjectId cardId = TrapFloorTemplateFactory.StartingAbilityCardId(avatarIndex, a);
                        if (!host.matchState.Cards.TryGetValue(cardId, out CardInstanceState card) || card.BaseState.ContainerId != pile) continue;
                        TabletopPose beside = TrapFloorTakePrizeUseCase.NextFreeOverflowPose(setup.AbilityOverflowPose, loose);
                        TransferCardResult result = host.TableAuthority.Submit<TransferCardCommand, TransferCardResult>(
                            new TransferCardCommand(host.CreateCommandContext(player), cardId, pile, ContainerId.Empty, beside));
                        if (!result.Succeeded) continue;
                        loose.Add(beside.Position);
                        host.PresentAcceptedObject(cardId);
                        given++;
                    }
                }
                finally
                {
                    host.SessionService.EndUndoBatch();
                }

                host.ViewSync.SyncContainer(pile);
                host.RefreshAcceptedCardFaces();
            }

            string who = FormatPlayerName(player);
            host.GameplayLog(given > 0
                ? $"{who} chose the {avatarName}: {given} starting Abilit{(given == 1 ? "y" : "ies")} were placed beside their Console."
                : $"{who} chose the {avatarName}.");
        }

        /// <summary>The Avatar card in this seat's Main slot (the one it chose), else the one it was dealt.</summary>
        internal bool TryGetSeatAvatar(TrapFloorPlayerSetupDefinition setup, out CardInstanceState avatarCard)
        {
            avatarCard = null;
            if (setup == null || host.matchState == null) return false;
            if (host.matchState.Containers.TryGetValue(setup.MainSlotContainerId, out ContainerState main))
            {
                for (int i = main.Count - 1; i >= 0; i--)
                {
                    if (host.matchState.Cards.TryGetValue(main.GetObjectAt(i), out CardInstanceState card)
                        && host.TryGetAvatarLook(card.BaseState.DefinitionId, out _))
                    {
                        avatarCard = card;
                        return true;
                    }
                }
            }

            return false;
        }

        // DealFloor: each seat's Avatar now, so the next floor deals it straight into that seat's Main slot.
        private ObjectDefinitionId[] CaptureSeatAvatars()
        {
            if (host.trapFloorTemplate == null) return null;
            ObjectDefinitionId[] chosen = new ObjectDefinitionId[host.trapFloorTemplate.Players.Count];
            for (int i = 0; i < chosen.Length; i++)
            {
                if (TryGetSeatAvatar(host.trapFloorTemplate.Players[i], out CardInstanceState card)) chosen[i] = card.BaseState.DefinitionId;
            }

            return chosen;
        }
    }
}
