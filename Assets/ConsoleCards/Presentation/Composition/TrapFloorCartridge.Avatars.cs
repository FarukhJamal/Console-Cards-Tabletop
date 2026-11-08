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
using ConsoleCards.GameTemplates.Definitions;
using ConsoleCards.Games.TrapFloor;
using ConsoleCards.Presentation.Feedback;
using ConsoleCards.Presentation.UI;
using UnityEngine;

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

        // ---------- P8A: Avatar draft (owner, 2026-11-08: "like choosing an agent: whoever locks it gets it") ----------

        private readonly HashSet<PlayerId> avatarDraftAsked = new HashSet<PlayerId>();
        private readonly HashSet<PlayerId> avatarDraftLater = new HashSet<PlayerId>();
        private readonly HashSet<PlayerId> avatarDraftSeen = new HashSet<PlayerId>();
        private readonly Dictionary<PlayerId, float> avatarDraftSentAt = new Dictionary<PlayerId, float>();
        private object avatarDraftMatch;
        private PlayerId avatarDraftOpenHere = PlayerId.Empty;

        // Full only, on the computer that holds the table: every seated player without an Avatar is asked to pick one
        // of the Avatars still in the Avatar pile, on their own screen. The first pick of a card gets it (into that
        // player's Console Main slot; the starting Abilities follow as for a hand-placed Avatar). A pick of a card that
        // is gone asks again with what is left. One question at a time on a shared screen.
        private void TickAvatarDraft()
        {
            if (PlayAssist.Level != PlayAssistLevel.Full || host.trapFloorTemplate == null || host.matchState == null
                || host.runtimeUi == null || host.undoTransactionInProgress || host.rebuildingFromUndo) return;
            // A private room's guest holds only its filtered view: its host asks it. Every other computer (offline, or
            // either end of a normal online room, where each computer plays its own seats and its moves go through the
            // host) asks the players sitting at it, each on their own screen at the same time (owner, 2026-11-09).
            if (host.privateViewActive || (host.HasOnline && host.Online.IsPrivateGuest)) return;
            bool privateHost = host.HasOnline && host.Online.IsPrivateHost;
            if (!ReferenceEquals(avatarDraftMatch, host.matchState))
            {
                avatarDraftMatch = host.matchState;
                avatarDraftAsked.Clear();
                avatarDraftLater.Clear();
                avatarDraftSeen.Clear();
                avatarDraftSentAt.Clear();
                avatarDraftOpenHere = PlayerId.Empty;
            }

            if (!host.matchState.Containers.TryGetValue(TrapFloorTemplateFactory.AvatarDeckId, out ContainerState pile) || pile.Count == 0) return;
            int seats = Math.Min(host.trapFloorTemplate.Players.Count, MaxAvatarSeats);
            for (int i = 0; i < seats; i++)
            {
                TrapFloorPlayerSetupDefinition setup = host.trapFloorTemplate.Players[i];
                if (!host.matchState.Seats.TryGetValue(setup.SeatId, out SeatState seat) || seat.OccupantPlayerId.IsEmpty) continue;
                PlayerId player = seat.OccupantPlayerId;
                if (avatarDraftLater.Contains(player) || TryGetSeatAvatar(setup, out _)) continue;
                bool remote = privateHost && host.Online.IsGuestsPlayer(player);
                bool here = privateHost || host.IsPlayerOnThisComputer(player);
                if (!remote && !here)
                {
                    // That player's own computer asks them (a normal online room). A question opened here while their
                    // computer was away is theirs again.
                    if (avatarDraftOpenHere == player)
                    {
                        host.runtimeUi.CloseFocusedCardSelection();
                        avatarDraftOpenHere = PlayerId.Empty;
                    }

                    avatarDraftAsked.Remove(player);
                    if (avatarDraftSeen.Add(player))
                        Debug.Log($"[Draft] Seat {i + 1}: {FormatPlayerName(player)} is asked on their own computer ({(host.HasOnline ? host.Online.DescribeForDraft(player) : "offline")}).");
                    continue;
                }

                if (avatarDraftSeen.Add(player))
                    Debug.Log($"[Draft] Seat {i + 1}: {FormatPlayerName(player)} is asked on {(remote ? "their own computer" : "this screen")} ({(host.HasOnline ? host.Online.DescribeForDraft(player) : "offline")}).");
                if (remote)
                {
                    // On their own screen, once their computer has the table; asked again if the question was lost or
                    // taken back (a new table, a reconnect) and they have not answered "Later".
                    if (avatarDraftOpenHere == player)
                    {
                        // Opened on this screen before their computer had joined: it is theirs, not the host's.
                        host.runtimeUi?.CloseFocusedCardSelection();
                        avatarDraftOpenHere = PlayerId.Empty;
                        avatarDraftAsked.Remove(player);
                    }

                    if (!host.Online.CanSendPrivateSelection(player))
                    {
                        if (!avatarDraftSentAt.ContainsKey(player))
                        {
                            avatarDraftSentAt[player] = Time.unscaledTime;
                            Debug.Log($"[Draft] Avatar choice for {FormatPlayerName(player)}: waiting for their computer to have the table.");
                        }

                        continue;
                    }

                    if (host.Online.HasPendingPrivateSelection(player, FocusedCardSelectionModel.AvatarDraftTitle)) continue;
                    if (avatarDraftSentAt.TryGetValue(player, out float sentAt) && Time.unscaledTime - sentAt < 3f) continue;
                    bool sent = AskAvatarPick(setup, player, pile, true);
                    avatarDraftSentAt[player] = Time.unscaledTime;
                    Debug.Log($"[Draft] Avatar choice for {FormatPlayerName(player)} on their own computer: {(sent ? "sent" : "not sent yet")}.");
                    continue;
                }

                if (avatarDraftAsked.Contains(player)) continue;
                if (!remote && !avatarDraftOpenHere.IsEmpty) continue; // one question at a time on this screen
                if (!AskAvatarPick(setup, player, pile, remote)) continue; // not sent: asked again next frame
                avatarDraftAsked.Add(player);
                if (!remote) avatarDraftOpenHere = player;
            }
        }

        private bool AskAvatarPick(TrapFloorPlayerSetupDefinition setup, PlayerId player, ContainerState pile, bool remote)
        {
            List<FocusedCardOptionModel> options = new List<FocusedCardOptionModel>(pile.Count);
            for (int i = 0; i < pile.Count; i++)
            {
                TabletopObjectId cardId = pile.GetObjectAt(i);
                if (!host.matchState.Cards.TryGetValue(cardId, out CardInstanceState card)) continue;
                string name = host.TryGetAvatarLook(card.BaseState.DefinitionId, out AvatarDefinition look) ? look.DisplayName : "Avatar";
                Texture art = look != null ? host.AvatarArt(look) : null;
                options.Add(new FocusedCardOptionModel(cardId, name, "Available", art, true));
            }

            if (options.Count == 0) return false;
            FocusedCardSelectionModel model = new FocusedCardSelectionModel(
                FocusedCardSelectionModel.AvatarDraftTitle,
                "Pick your Avatar for this game. The first player to lock an Avatar gets it; its starting Abilities are placed beside your Console.",
                $"{FormatPlayerName(player)} · {options.Count} available",
                "Lock in",
                options,
                1,
                selected => selected.Count == 1,
                selected => LockAvatarPick(setup, player, selected[0]),
                () =>
                {
                    host.runtimeUi?.CloseFocusedCardSelection();
                    avatarDraftLater.Add(player);
                    if (avatarDraftOpenHere == player) avatarDraftOpenHere = PlayerId.Empty;
                    host.ShowMessage("No Avatar chosen: drag one from the Avatar pile into your Console's Main slot when you are ready.");
                },
                "Later") { Spread = true };
            if (remote) return host.Online.TrySendPrivateSelection(player, model);
            host.runtimeUi.ShowFocusedCardSelection(model);
            return true;
        }

        private void LockAvatarPick(TrapFloorPlayerSetupDefinition setup, PlayerId player, TabletopObjectId cardId)
        {
            host.runtimeUi?.CloseFocusedCardSelection();
            if (avatarDraftOpenHere == player) avatarDraftOpenHere = PlayerId.Empty;
            ContainerId pileId = TrapFloorTemplateFactory.AvatarDeckId;
            bool stillThere = host.matchState.Containers.TryGetValue(pileId, out ContainerState pile) && pile.Contains(cardId);
            if (!stillThere || TryGetSeatAvatar(setup, out _))
            {
                if (!TryGetSeatAvatar(setup, out _))
                {
                    host.FeedbackLine($"That Avatar was just taken: {FormatPlayerName(player)}, pick another.", FeedbackSeverity.Warning);
                    avatarDraftAsked.Remove(player); // asked again with what is left (next frame)
                    avatarDraftSentAt.Remove(player);
                }

                return;
            }

            TransferCardResult result = host.TableAuthority.Submit<TransferCardCommand, TransferCardResult>(
                TransferCardCommand.ToContainer(host.CreateCommandContext(player), cardId, pileId, setup.MainSlotContainerId));
            if (!result.Succeeded)
            {
                host.ShowMessage("That Avatar could not be placed in the Console. Drag it into the Main slot instead.");
                return;
            }

            host.ViewSync.SyncContainer(pileId);
            host.ViewSync.SyncContainer(setup.MainSlotContainerId);
            host.RefreshAcceptedCardFaces();
            string name = host.matchState.Cards.TryGetValue(cardId, out CardInstanceState card) && host.TryGetAvatarLook(card.BaseState.DefinitionId, out AvatarDefinition look)
                ? look.DisplayName : "an Avatar";
            host.FeedbackLine($"{FormatPlayerName(player)} locked in {name}.", FeedbackSeverity.Info);
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
