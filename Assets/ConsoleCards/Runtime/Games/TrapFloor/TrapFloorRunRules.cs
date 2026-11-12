using System;
using System.Collections.Generic;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Games.TrapFloor
{
    /// <summary>P8A (owner, 2026-11-13): what follows a floor's end.</summary>
    public enum TrapFloorRunStep
    {
        /// <summary>Won, not the last floor: the next floor of the same run (Avatar and unused Abilities kept).</summary>
        NextFloor = 0,
        /// <summary>Won the last floor of a stage that has another: the next stage of the same run (kept the same way).</summary>
        NextStage = 1,
        /// <summary>Won the last floor: GAME WON. No further floor; Play Again starts a fresh run.</summary>
        GameWon = 2,
        /// <summary>Lost (everyone out): GAME OVER. Never the next floor; Play Again starts a fresh run.</summary>
        GameOver = 3,
    }

    /// <summary>
    /// P8A (owner, 2026-11-13: "Floor won vs floor lost"): the run's progression. A won floor goes on (next floor, or
    /// next stage); the last one is GAME WON; a lost floor is GAME OVER whatever floor it was. Only a run that goes on
    /// carries its players' Avatars and unused Abilities; GAME WON and GAME OVER start the next run fresh from Floor 1.
    /// </summary>
    public static class TrapFloorRunProgression
    {
        public static TrapFloorRunStep After(bool won, bool isLastFloor, bool hasNextStage)
        {
            if (!won) return TrapFloorRunStep.GameOver;
            if (!isLastFloor) return TrapFloorRunStep.NextFloor;
            return hasNextStage ? TrapFloorRunStep.NextStage : TrapFloorRunStep.GameWon;
        }

        /// <summary>True when the run goes on (its players keep their Avatar and unused Abilities).</summary>
        public static bool GoesOn(TrapFloorRunStep step) => step == TrapFloorRunStep.NextFloor || step == TrapFloorRunStep.NextStage;
    }

    /// <summary>One Ability card a seat still holds at a floor's end (P8A run inventory).</summary>
    public sealed class TrapFloorCarriedAbility
    {
        public TrapFloorCarriedAbility(int seatIndex, TabletopObjectId cardId, ObjectDefinitionId definitionId, bool bought,
            ContainerId containerId, TabletopPose pose, CardFace face)
        {
            SeatIndex = seatIndex;
            CardId = cardId;
            DefinitionId = definitionId;
            Bought = bought;
            ContainerId = containerId;
            Pose = pose;
            Face = face;
        }

        public int SeatIndex { get; }
        public TabletopObjectId CardId { get; }
        public ObjectDefinitionId DefinitionId { get; }
        /// <summary>An Ability deck card the seat bought (else one of its Avatar's starting Abilities).</summary>
        public bool Bought { get; }
        /// <summary>The seat's hand or Console slot it was in (empty: loose on the table).</summary>
        public ContainerId ContainerId { get; }
        public TabletopPose Pose { get; }
        public CardFace Face { get; }
    }

    /// <summary>What <see cref="TrapFloorRunCarry.Apply"/> did (for the log and the tests).</summary>
    public readonly struct TrapFloorCarryResult
    {
        public TrapFloorCarryResult(int boughtKept, int startingKept, int startingLeftOut)
        {
            BoughtKept = boughtKept;
            StartingKept = startingKept;
            StartingLeftOut = startingLeftOut;
        }

        public int BoughtKept { get; }
        public int StartingKept { get; }
        public int StartingLeftOut { get; }
    }

    /// <summary>
    /// P8A (owner, 2026-11-13: "Persistent Avatar and purchased Abilities"): a run keeps, from one floor to the next, each
    /// seat's unused Abilities: the Ability deck cards it bought, and its Avatar's starting Abilities it still holds. Used
    /// ones (in the Ability discard) and anything in a public pile stay behind. Floor-only things (statuses, turn flags,
    /// pending Traps, Controller cards) are never carried: the next floor's rules state is new.
    /// <para>Capture reads the ending floor. Apply runs once on the computer that deals the next floor (the host), on its
    /// new table before it is shared: a bought card leaves the new Ability deck (the same card id: no copy) to where it
    /// was, still its buyer's; of the starting Abilities the new floor deals beside a seat with its Avatar, only as many
    /// of each as the seat still held stay, the rest go to the STARTING ABILITIES pile (never re-dealt for free).</para>
    /// </summary>
    public static class TrapFloorRunCarry
    {
        private const int StartingFromPile = 44;
        private const int StartingBesideSeat = 46;

        public static List<TrapFloorCarriedAbility> Capture(MatchState match, TrapFloorTemplateDefinition template)
        {
            List<TrapFloorCarriedAbility> carried = new List<TrapFloorCarriedAbility>();
            if (match == null || template == null) return carried;
            for (int seatIndex = 0; seatIndex < template.Players.Count; seatIndex++)
            {
                TrapFloorPlayerSetupDefinition setup = template.Players[seatIndex];
                if (!match.Seats.TryGetValue(setup.SeatId, out SeatState seat) || seat.OccupantPlayerId.IsEmpty) continue;
                PlayerId player = seat.OccupantPlayerId;
                int avatarIndex = SeatAvatarIndex(match, template, setup);
                foreach (KeyValuePair<TabletopObjectId, CardInstanceState> pair in match.Cards)
                {
                    CardInstanceState card = pair.Value;
                    ContainerId where = card.BaseState.ContainerId;
                    if (!StillHeld(template, setup, where)) continue;
                    bool bought = TrapFloorTemplateFactory.IsAbilityDeckCardId(pair.Key) && card.BaseState.OwnerPlayerId == player;
                    bool starting = !bought && IsSeatsStartingAbility(pair.Key, seatIndex, avatarIndex);
                    if (!bought && !starting) continue;
                    carried.Add(new TrapFloorCarriedAbility(seatIndex, pair.Key, card.BaseState.DefinitionId, bought,
                        IsSeatsOwnPlace(setup, where) ? where : ContainerId.Empty, card.BaseState.Pose, card.Face));
                }
            }

            carried.Sort((a, b) => a.CardId.Value.CompareTo(b.CardId.Value)); // the same order on every computer
            return carried;
        }

        public static TrapFloorCarryResult Apply(MatchState match, TrapFloorTemplateDefinition template, IReadOnlyList<TrapFloorCarriedAbility> carried)
        {
            if (match == null || template == null || carried == null) return default;
            ContainerTransferService transfers = new ContainerTransferService();
            int bought = 0;
            int startingKept = 0;
            int startingLeftOut = 0;
            Dictionary<int, List<ObjectDefinitionId>> startingHeld = new Dictionary<int, List<ObjectDefinitionId>>();
            foreach (TrapFloorCarriedAbility item in carried)
            {
                if (item.SeatIndex < 0 || item.SeatIndex >= template.Players.Count) continue;
                if (!item.Bought)
                {
                    if (!startingHeld.TryGetValue(item.SeatIndex, out List<ObjectDefinitionId> list))
                        startingHeld[item.SeatIndex] = list = new List<ObjectDefinitionId>();
                    list.Add(item.DefinitionId);
                    continue;
                }

                TrapFloorPlayerSetupDefinition setup = template.Players[item.SeatIndex];
                if (!match.Seats.TryGetValue(setup.SeatId, out SeatState seat) || seat.OccupantPlayerId.IsEmpty) continue;
                if (!match.Cards.TryGetValue(item.CardId, out CardInstanceState card)) continue;
                ContainerId from = card.BaseState.ContainerId;
                if (!from.IsEmpty && match.Containers.TryGetValue(from, out ContainerState source))
                {
                    if (!transfers.RemoveFromContainer(card.BaseState, source).Succeeded) continue;
                }

                if (!item.ContainerId.IsEmpty && match.Containers.TryGetValue(item.ContainerId, out ContainerState place)
                    && transfers.PlaceIntoContainer(card.BaseState, place).Succeeded)
                {
                    // back in the hand or Console slot it was in
                }
                else
                {
                    card.BaseState.SetPose(item.Pose); // loose where it lay (the Console's place is the same each floor)
                }

                card.BaseState.SetOwner(seat.OccupantPlayerId); // still the buyer's
                card.SetFace(item.Face == CardFace.FaceDown ? CardFace.FaceDown : CardFace.FaceUp);
                bought++;
            }

            // The new floor dealt each seat with an Avatar its whole starting set beside it: keep what the seat still held.
            for (int seatIndex = 0; seatIndex < template.Players.Count; seatIndex++)
            {
                startingHeld.TryGetValue(seatIndex, out List<ObjectDefinitionId> held);
                for (int i = 0; i < 100; i++)
                {
                    TabletopObjectId dealt = TrapFloorTemplateFactory.SeatStartingAbilityCardId(seatIndex, i);
                    if (!match.Cards.TryGetValue(dealt, out CardInstanceState card)) break;
                    if (!card.BaseState.ContainerId.IsEmpty) continue;
                    int index = held != null ? held.IndexOf(card.BaseState.DefinitionId) : -1;
                    if (index >= 0)
                    {
                        held.RemoveAt(index);
                        startingKept++;
                        continue;
                    }

                    if (match.Containers.TryGetValue(TrapFloorTemplateFactory.StartingAbilitiesId, out ContainerState pile)
                        && transfers.PlaceIntoContainer(card.BaseState, pile).Succeeded)
                    {
                        startingLeftOut++; // used on an earlier floor: not dealt again for free
                    }
                }
            }

            return new TrapFloorCarryResult(bought, startingKept, startingLeftOut);
        }

        // Still the seat's: loose, or in its own hand or Console slots (never the Ability deck, the discard or a public pile).
        private static bool StillHeld(TrapFloorTemplateDefinition template, TrapFloorPlayerSetupDefinition setup, ContainerId where)
        {
            if (where.IsEmpty) return true;
            if (where == template.AbilityDeckId || where == template.AbilityDiscardId || where == TrapFloorTemplateFactory.StartingAbilitiesId)
                return false;
            return IsSeatsOwnPlace(setup, where);
        }

        private static bool IsSeatsOwnPlace(TrapFloorPlayerSetupDefinition setup, ContainerId where)
        {
            if (where.IsEmpty) return false;
            if (where == setup.HandContainerId || where == setup.ActionAbilityAreaContainerId) return true;
            for (int i = 0; i < setup.SideSlotContainerIds.Count; i++)
                if (setup.SideSlotContainerIds[i] == where) return true;
            return false;
        }

        // A starting Ability of this seat: dealt beside it with its Avatar (46), or taken from the pile for the Avatar in
        // its Main slot (44). A loose one of another seat's Avatar is that seat's.
        private static bool IsSeatsStartingAbility(TabletopObjectId cardId, int seatIndex, int avatarIndex)
        {
            if (TrapFloorTemplateFactory.TryGetFactoryIndex(cardId, StartingBesideSeat, out int besideIndex))
                return (besideIndex - 1) / 100 == seatIndex;
            if (avatarIndex >= 0 && TrapFloorTemplateFactory.TryGetFactoryIndex(cardId, StartingFromPile, out int pileIndex))
                return (pileIndex - 1) / 20 == avatarIndex;
            return false;
        }

        private static int SeatAvatarIndex(MatchState match, TrapFloorTemplateDefinition template, TrapFloorPlayerSetupDefinition setup)
        {
            if (setup.MainSlotContainerId.IsEmpty || !match.Containers.TryGetValue(setup.MainSlotContainerId, out ContainerState slot)) return -1;
            for (int i = 0; i < slot.Count; i++)
            {
                if (!match.Cards.TryGetValue(slot.GetObjectAt(i), out CardInstanceState card)) continue;
                int index = TrapFloorTemplateFactory.AvatarIndexOf(template.GameDefinition, card.BaseState.DefinitionId, out _);
                if (index >= 0) return index;
            }

            return -1;
        }
    }

    /// <summary>
    /// P8A (owner, 2026-11-13: "Blind Trap: d4 direction"). A direction is legal for a Blind turn when the pawn can take
    /// at least one step that way: the next tile is on the floor and not a hole (the same orthogonal step every move
    /// uses). A d4 that maps to a direction with no legal step is rolled again while another direction has one; when no
    /// direction has one, the roll stands and the player is stuck this turn (the existing stuck / no-escape rules).
    /// </summary>
    public static class TrapFloorBlindRules
    {
        public enum Decision
        {
            Accept = 0,
            RollAgain = 1,
            NoLegalDirection = 2,
        }

        public static readonly TrapFloorBlindMovementDirection[] AllDirections =
        {
            TrapFloorBlindMovementDirection.Left,
            TrapFloorBlindMovementDirection.Down,
            TrapFloorBlindMovementDirection.Right,
            TrapFloorBlindMovementDirection.Up,
        };

        // P8A: the tile one step that way; false off the floor's low edges (a coordinate there cannot exist).
        public static bool TryStep(TrapFloorCoordinate from, TrapFloorBlindMovementDirection direction, out TrapFloorCoordinate to)
        {
            int x = from.X;
            int y = from.Y;
            switch (direction)
            {
                case TrapFloorBlindMovementDirection.Left: x--; break;
                case TrapFloorBlindMovementDirection.Right: x++; break;
                case TrapFloorBlindMovementDirection.Up: y++; break;
                default: y--; break;
            }

            to = default;
            if (x < 1 || y < 1) return false;
            to = new TrapFloorCoordinate(x, y);
            return true;
        }

        public static bool HasLegalStep(TrapFloorCoordinate pawn, TrapFloorBlindMovementDirection direction, Func<TrapFloorCoordinate, bool> isOpenTile) =>
            isOpenTile != null && TryStep(pawn, direction, out TrapFloorCoordinate to) && isOpenTile(to);

        public static Decision Decide(TrapFloorCoordinate pawn, TrapFloorBlindMovementDirection mapped, Func<TrapFloorCoordinate, bool> isOpenTile)
        {
            if (HasLegalStep(pawn, mapped, isOpenTile)) return Decision.Accept;
            for (int i = 0; i < AllDirections.Length; i++)
                if (HasLegalStep(pawn, AllDirections[i], isOpenTile)) return Decision.RollAgain;
            return Decision.NoLegalDirection;
        }

        /// <summary>Board words for a direction ("left, toward column 1").</summary>
        public static string Describe(TrapFloorBlindMovementDirection direction)
        {
            switch (direction)
            {
                case TrapFloorBlindMovementDirection.Left: return "LEFT (toward column 1)";
                case TrapFloorBlindMovementDirection.Right: return "RIGHT (toward the last column)";
                case TrapFloorBlindMovementDirection.Up: return "UP (toward the top row)";
                default: return "DOWN (toward row 1)";
            }
        }

        /// <summary>
        /// The printed Blind rule "During your next Round, do not draw Controller Cards": true when <paramref name="drawer"/>
        /// is Blind this round and <paramref name="source"/> is a Controller deck (the game's shared one). Full enforces it
        /// at the draw itself, on every table; Reminders only warns.
        /// </summary>
        public static bool RefusesControllerDraw(
            TrapFloorTemplateDefinition template,
            TrapFloorTurnState turn,
            TrapFloorAbilityResolutionState ability,
            ContainerId source,
            PlayerId drawer)
        {
            if (template == null || turn == null || ability == null || source.IsEmpty || drawer.IsEmpty) return false;
            bool controllerDeck = false;
            for (int i = 0; i < template.Players.Count && !controllerDeck; i++)
                controllerDeck = template.Players[i].ControllerDeckId == source;
            return controllerDeck && ability.TryGetBlindStatus(drawer, turn.CurrentRound, out _);
        }
    }
}
