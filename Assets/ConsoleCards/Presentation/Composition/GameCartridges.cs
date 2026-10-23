using System;
using System.Collections.Generic;
using ConsoleCards.Application.Authority;
using ConsoleCards.Application.Commands;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Cards;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Events;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Definitions;
using ConsoleCards.GameTemplates;
using ConsoleCards.GameTemplates.ControllerInputs;
using ConsoleCards.GameTemplates.Definitions;
using ConsoleCards.Presentation.UI;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// The cartridge contract (CART-1, doc 30 §A4): what a game hands the table. The table code talks to "the current
    /// cartridge" through this, never to a game by name. A <b>code cartridge</b> (Trap Floor) brings its own helpers:
    /// table setups (game templates), its rules state and commands, its seats and turns, its board tiles, a per-frame
    /// step, its status card, its Shop, its hint helpers, its own card menus and its Console card reactions. A
    /// <b>data cartridge</b> (<see cref="DataCartridge"/>: an Empty Table, a game a player made, a DLC game without
    /// code) needs no code: every hook does nothing and the status line names it.
    /// CART-1a moved the table's hook points behind the contract; CART-1b moved the rest of the game's state and code
    /// (the table's own files no longer name a game); CART-1c added where the seats may go (<see cref="SeatPlacement"/>).
    /// </summary>
    internal interface IGameCartridge
    {
        // ---------- who it is ----------

        /// <summary>The game's stable ID ("empty-table" for an Empty Table).</summary>
        string StableId { get; }

        /// <summary>The game's name as players see it.</summary>
        string DisplayName { get; }

        /// <summary>The game's definition (cards, modes, rules); null for an Empty Table.</summary>
        GameDefinition Game { get; }

        /// <summary>True for a cartridge with code (helpers); false for data only.</summary>
        bool HasCode { get; }

        // ---------- table setups (templates) ----------

        /// <summary>Adds the cartridge's table setups (one per mode) to the catalog the New table menu starts.</summary>
        void RegisterTemplates(List<GameTemplateRegistration> registrations);

        /// <summary>The game's own setup for one of its templates (false: not one of its templates).</summary>
        bool TryGetTemplateSetup(GameTemplateId templateId, out object setup);

        /// <summary>Lists its playable templates for the game selection menu, with a mode name for each.</summary>
        void ListTemplates(List<GameTemplateId> templates, List<string> modeNames);

        /// <summary>Forgets its registered templates (the table is going away, or registering failed).</summary>
        void ForgetTemplates();

        /// <summary>The table's context for one of its setups, seen from the seat of <paramref name="requestingPlayerId"/>.</summary>
        TabletopComposition.TemplateContext CreateTemplateContext(TabletopSession session, object setup, PlayerId requestingPlayerId);

        /// <summary>
        /// Checks that the game can set itself up from this data in this mode (throws with the reason when it cannot):
        /// a house rule or a deck of yours is tried off the table before it is used.
        /// </summary>
        void CheckSetUp(GameDefinitionData data, string modeStableId);

        /// <summary>Sets the game up once, off the table, with the deck of yours chosen for it. Null when it works, else what went wrong.</summary>
        string TrialChosenDeck(GameTemplateId templateId);

        /// <summary>PLACE-1: the fewest players New table… can deal the game for (0: the menu does not ask).</summary>
        int MinimumDealPlayers { get; }

        /// <summary>PLACE-1: the most players New table… can deal the game for (0: the menu does not ask).</summary>
        int MaximumDealPlayers { get; }

        /// <summary>PLACE-1: how many players the game's next deal is for (0: the menu does not ask).</summary>
        int DealPlayerCount { get; }

        /// <summary>PLACE-1: chooses how many players the game's next deal is for (kept within the range).</summary>
        void ChooseDealPlayerCount(int count);

        // ---------- building and clearing the table ----------

        /// <summary>The table is being built from one of its templates (first step of the build).</summary>
        void UseTemplate(TabletopComposition.TemplateContext context);

        /// <summary>Checks the scene references the game itself needs (throws when one is missing).</summary>
        void ValidateConfiguration();

        /// <summary>Makes the game's rules state (or takes it back from an Undo step) and its use cases.</summary>
        void BuildRules();

        /// <summary>Makes the game's services that need the table's dice and random source.</summary>
        void BuildServices();

        /// <summary>The piece views are bound (the game may hold a view back, for example a tile waiting to turn).</summary>
        void OnObjectViewsBound();

        /// <summary>Adds the game's commands to the table's command door, and the views that follow them. Called once.</summary>
        void AttachToAuthority(LocalTableAuthority authority);

        /// <summary>The table was built and is ready (presentation the game shows from the start).</summary>
        void OnTableReady();

        /// <summary>The table is going away (a new table, Undo's rebuild, leaving Play mode): first step.</summary>
        void OnTableShutdown();

        /// <summary>The table is going away: the game's markers and pending dice, before the views are released.</summary>
        void ClearTableMarkers();

        /// <summary>The table is gone: the game forgets this table's rules state and services.</summary>
        void ClearTableState();

        /// <summary>The game's own part of an Undo step (plain data), or null.</summary>
        ICartridgeUndoState CaptureUndoState();

        // ---------- seats and turns ----------

        /// <summary>The game's seats, in seat order (an Empty Table has none). Read often; never allocates.</summary>
        IReadOnlyList<CartridgeSeat> Seats { get; }

        /// <summary>The player whose turn it is (empty: the game has no turns, or none is running).</summary>
        PlayerId TurnPlayerId { get; }

        /// <summary>True while a player's turn is open (not a game step between turns).</summary>
        bool IsPlayerTurnOpen { get; }

        /// <summary>The round in play (0: the game has no rounds).</summary>
        int CurrentRound { get; }

        /// <summary>The stable ID of the mode in play (empty: no mode).</summary>
        string ActiveModeStableId { get; }

        /// <summary>Where the game's seats may go on the table (CART-1c): kept where its template put them, or along the edge.</summary>
        SeatPlacement SeatPlacement { get; }

        /// <summary>The seat whose turn the game helps with (throws when there is no player turn).</summary>
        CartridgeSeat GetAssistedSeat();

        /// <summary>True for a deck the game deals from itself (the table's Deal and Shuffle leave it alone).</summary>
        bool IsAssistedControllerDeck(ContainerId containerId);

        /// <summary>The game's own draw for the turn player's Controller hand (when the hand has no cap to fill).</summary>
        ControllerInputHandDrawResult DrawControllerHandForTurn(CommandContext context);

        /// <summary>The game's Controller settings (hand limit), or null.</summary>
        ControllerConfigurationData ControllerConfiguration { get; }

        // ---------- board tiles and card looks ----------

        /// <summary>True for a game's own board piece (a floor tile): it never moves, stacks or snaps.</summary>
        bool IsFixedBoardPiece(TabletopObjectId objectId);

        /// <summary>True for any of the game's board tiles, also one lifted off the board: it keeps the tile's look and size.</summary>
        bool IsBoardTile(TabletopObjectId objectId);

        /// <summary>The grid the game's board is drawn with (false: the game has no board grid).</summary>
        bool TryGetBoardGrid(out GridDefinitionData grid);

        /// <summary>
        /// Rows and columns taken off the board grid (MAT-2: Trap Floor's smaller floors). The board is drawn this
        /// many spaces bigger, with the grid's tiles from its first row and column; the extra spaces stay empty.
        /// </summary>
        int BoardRemovedSpaces { get; }

        /// <summary>
        /// T-1: the size (table units) of the playmat the board's spaces lie on, centred on the board; zero: the mat is
        /// just its spaces with their frame.
        /// </summary>
        Vector2 BoardMatSize { get; }

        /// <summary>
        /// T-2: half the table's width and depth (table units, around the table's centre) the game needs with its most
        /// players; false when the game does not say (the table stays as it is).
        /// </summary>
        bool TryGetTableNeed(out Vector2 halfSize);

        /// <summary>A board piece's view was made (the game may mark it, for example for move targets).</summary>
        void OnBoardPieceViewCreated(TabletopObjectId objectId, CardVisualReferences references);

        /// <summary>Draws one of its board pieces: face, back, labels and art.</summary>
        void StyleBoardPiece(CardInstanceState card, ref CardLook look);

        /// <summary>The game's own right-click menu for a card (a card waiting to be resolved, a board tile).</summary>
        bool TryGetCardMenuMode(TabletopObjectId cardId, out TabletopComposition.ContextMenuMode mode);

        /// <summary>Shows one of the game's own menus (true) or leaves the mode to the table (false).</summary>
        bool TryShowContextMenu(TabletopComposition.ContextMenuMode mode);

        /// <summary>What the inspect panel shows for one of its pieces (false: the table's own text).</summary>
        bool TryBuildInspectModel(TabletopObjectId cardId, CardInstanceState card, out CardInspectModel model);

        /// <summary>The inspect panel opened or closed (the game forgets what it was opened for).</summary>
        void ForgetInspectContext();

        /// <summary>The cost line of a card's inspect text ("" for no cost): the rules in play first, else the card's own.</summary>
        string FormatInputCost(CardDefinition definition);

        // ---------- each frame, actions and feedback ----------

        /// <summary>Each frame, before the hint helpers (for example a landing tile turning over).</summary>
        void TickEarly();

        /// <summary>Each frame, after the hint helpers (for example dice of the game settling).</summary>
        void TickLate();

        /// <summary>Each frame: the game's banners, messages and board markers.</summary>
        void TickFeedback();

        /// <summary>An action was accepted (the game follows its own moves, for example a pawn landing).</summary>
        void OnActionAccepted(AuthoritativeActionAcceptance acceptance);

        /// <summary>The Undo text for one of the game's own actions, or null for the table's.</summary>
        string DescribeAction(AuthoritativeActionAcceptance acceptance, string actor);

        /// <summary>The status line under the session bar.</summary>
        string StatusText();

        /// <summary>A number that changes when the status card should redraw (no garbage).</summary>
        int StatusUiSignature();

        /// <summary>Builds and shows the game's status card, or hides it.</summary>
        void RenderStatusUi();

        /// <summary>Asks for the status card to redraw at the end of the frame.</summary>
        void RefreshStatusUi();

        /// <summary>Shows or hides the game's Shop entry.</summary>
        void RefreshShopEntry();

        /// <summary>Adds the game's hint helpers (doc 23, H3).</summary>
        void AddHintHelpers();

        /// <summary>A card was played into a Console slot (the game may react, for example an ability).</summary>
        void OnConsoleCardInteraction(IConsoleCardInteraction interaction, ConsoleCardBehavior behavior);
    }

    /// <summary>Where a game's seats may go on the table (CART-1c, doc 31 PL-CARTRIDGE).</summary>
    internal enum SeatPlacement
    {
        /// <summary>The seats stay where the game's template puts them (the classic table's places).</summary>
        Fixed = 0,

        /// <summary>
        /// Each seat sits at the table's edge, out along the line from the table's centre through its place on the
        /// classic table (so the seats keep their order and their facing); on the classic table they are at home.
        /// </summary>
        AlongEdge = 1,
    }

    /// <summary>
    /// One seat of a game (CART-1b): its place in the table's player layout and the containers that belong to it
    /// (empty when the game has none). The table builds each seat's Console, hand, Controller deck and action area
    /// from these. CART-1c: <see cref="HomeConsolePose"/> is where the template puts the seat's Console on the
    /// classic table.
    /// </summary>
    internal readonly struct CartridgeSeat
    {
        public CartridgeSeat(
            int layoutSeatIndex,
            SeatId seatId,
            ContainerId handContainerId,
            ContainerId controllerDeckId,
            ContainerId actionAreaContainerId,
            TabletopObjectId pawnId,
            TabletopPose homeConsolePose)
        {
            LayoutSeatIndex = layoutSeatIndex;
            SeatId = seatId;
            HandContainerId = handContainerId;
            ControllerDeckId = controllerDeckId;
            ActionAreaContainerId = actionAreaContainerId;
            PawnId = pawnId;
            HomeConsolePose = homeConsolePose;
        }

        public int LayoutSeatIndex { get; }
        public SeatId SeatId { get; }
        public ContainerId HandContainerId { get; }
        public ContainerId ControllerDeckId { get; }
        public ContainerId ActionAreaContainerId { get; }
        public TabletopObjectId PawnId { get; }
        public TabletopPose HomeConsolePose { get; }
    }

    /// <summary>How a card is drawn (CART-1b): a game changes this for its own board pieces.</summary>
    internal struct CardLook
    {
        public Color FrontColor;
        public Color BackColor;
        public string FrontLabel;
        public string BackLabel;
        public bool BackLabelIsPlaceholder;
        public Texture2D FrontArtwork;
        public Texture2D BackArtwork;
    }

    /// <summary>
    /// A game's own part of an Undo step (CART-1b): plain data, no Unity objects. The table keeps it with the step
    /// and hands back a fresh live copy (<see cref="Restore"/>) when the table is rebuilt; only the game reads it.
    /// </summary>
    internal interface ICartridgeUndoState
    {
        object Restore();
    }

    /// <summary>Card text every game shares (CART-1b).</summary>
    internal static class CartridgeText
    {
        /// <summary>A card's authored cost in card notation ("" for no cost).</summary>
        internal static string FormatAuthoredInputCost(InputCostDefinition inputCost)
        {
            if (inputCost == null || inputCost.Requirements.Count == 0)
            {
                return string.Empty;
            }

            List<InputRequirementData> requirements = new List<InputRequirementData>(inputCost.Requirements.Count);
            for (int i = 0; i < inputCost.Requirements.Count; i++)
            {
                InputRequirement requirement = inputCost.Requirements[i];
                List<ControllerInput> inputs = new List<ControllerInput>(1 + requirement.AlsoAccepts.Count)
                {
                    requirement.Input,
                };
                inputs.AddRange(requirement.AlsoAccepts);
                requirements.Add(new InputRequirementData(inputs, requirement.Count));
            }

            return InputCostNotation.Format(new InputCostData(requirements));
        }

        /// <summary>"\n\nCost: …" for an inspect text, or "".</summary>
        internal static string CostLine(string cost) => string.IsNullOrEmpty(cost) ? string.Empty : $"\n\nCost: {cost}";
    }

    /// <summary>
    /// A cartridge with no code (CART-1): an Empty Table, a game a player made (doc 29) or a DLC game that is data only.
    /// The table needs nothing from it; the status line shows its name and how many things are on the table.
    /// </summary>
    internal sealed class DataCartridge : IGameCartridge
    {
        public const string EmptyTableId = "empty-table";
        private static readonly CartridgeSeat[] NoSeats = new CartridgeSeat[0];

        private readonly TabletopComposition host;
        private readonly GameDefinition game;

        internal DataCartridge(TabletopComposition host, GameDefinition game)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            this.game = game;
        }

        public string StableId => game != null ? game.StableId ?? string.Empty : EmptyTableId;

        // An Empty Table started from a game you made shows that game's name.
        public string DisplayName
        {
            get
            {
                if (game != null) return game.DisplayName;
                string playerGame = host.PlayerGamesPanel.activePlayerGame != null ? host.PlayerGamesPanel.activePlayerGame.name : null;
                return string.IsNullOrWhiteSpace(playerGame) ? "Empty Table" : playerGame;
            }
        }

        public GameDefinition Game => game;
        public bool HasCode => false;

        public void RegisterTemplates(List<GameTemplateRegistration> registrations)
        {
        }

        public bool TryGetTemplateSetup(GameTemplateId templateId, out object setup)
        {
            setup = null;
            return false;
        }

        public void ListTemplates(List<GameTemplateId> templates, List<string> modeNames)
        {
        }

        public void ForgetTemplates()
        {
        }

        public TabletopComposition.TemplateContext CreateTemplateContext(TabletopSession session, object setup, PlayerId requestingPlayerId) =>
            throw new InvalidOperationException($"{DisplayName} has no table setups.");

        public void CheckSetUp(GameDefinitionData data, string modeStableId)
        {
        }

        public string TrialChosenDeck(GameTemplateId templateId) => null;

        public void UseTemplate(TabletopComposition.TemplateContext context) =>
            throw new InvalidOperationException($"{DisplayName} has no table setups.");

        public void ValidateConfiguration()
        {
        }

        public void BuildRules()
        {
        }

        public void BuildServices()
        {
        }

        public void OnObjectViewsBound()
        {
        }

        public void AttachToAuthority(LocalTableAuthority authority)
        {
        }

        public void OnTableReady()
        {
        }

        public void OnTableShutdown()
        {
        }

        public void ClearTableMarkers()
        {
        }

        public void ClearTableState()
        {
        }

        public ICartridgeUndoState CaptureUndoState() => null;

        public IReadOnlyList<CartridgeSeat> Seats => NoSeats;

        public int MinimumDealPlayers => 0;

        public int MaximumDealPlayers => 0;

        public int DealPlayerCount => 0;

        public void ChooseDealPlayerCount(int count)
        {
        }
        public PlayerId TurnPlayerId => PlayerId.Empty;
        public bool IsPlayerTurnOpen => false;
        public int CurrentRound => 0;
        public string ActiveModeStableId => string.Empty;
        public SeatPlacement SeatPlacement => SeatPlacement.Fixed;

        public CartridgeSeat GetAssistedSeat() =>
            throw new InvalidOperationException("Controller assistance requires a game with player turns.");

        public bool IsAssistedControllerDeck(ContainerId containerId) => false;

        public ControllerInputHandDrawResult DrawControllerHandForTurn(CommandContext context) =>
            ControllerInputHandDrawResult.NoChange(host.matchState != null ? host.matchState.Revision : 0L);

        public ControllerConfigurationData ControllerConfiguration => null;

        public bool IsFixedBoardPiece(TabletopObjectId objectId) => false;

        public bool IsBoardTile(TabletopObjectId objectId) => false;

        public bool TryGetBoardGrid(out GridDefinitionData grid)
        {
            grid = null;
            return false;
        }

        public int BoardRemovedSpaces => 0;

        public Vector2 BoardMatSize => Vector2.zero;

        public bool TryGetTableNeed(out Vector2 halfSize)
        {
            halfSize = Vector2.zero;
            return false;
        }

        public void OnBoardPieceViewCreated(TabletopObjectId objectId, CardVisualReferences references)
        {
        }

        public void StyleBoardPiece(CardInstanceState card, ref CardLook look)
        {
        }

        public bool TryGetCardMenuMode(TabletopObjectId cardId, out TabletopComposition.ContextMenuMode mode)
        {
            mode = TabletopComposition.ContextMenuMode.None;
            return false;
        }

        public bool TryShowContextMenu(TabletopComposition.ContextMenuMode mode) => false;

        public bool TryBuildInspectModel(TabletopObjectId cardId, CardInstanceState card, out CardInspectModel model)
        {
            model = null;
            return false;
        }

        public void ForgetInspectContext()
        {
        }

        public string FormatInputCost(CardDefinition definition) =>
            definition == null ? string.Empty : CartridgeText.CostLine(CartridgeText.FormatAuthoredInputCost(definition.InputCost));

        public void TickEarly()
        {
        }

        public void TickLate()
        {
        }

        public void TickFeedback() => host.boardFeedback?.HideAll();

        public void OnActionAccepted(AuthoritativeActionAcceptance acceptance)
        {
        }

        public string DescribeAction(AuthoritativeActionAcceptance acceptance, string actor) => null;

        public string StatusText()
        {
            if (Time.unscaledTime <= host.operationMessageUntil) return host.operationMessage;
            return $"{DisplayName} | Objects {host.matchState?.ObjectCount ?? 0}";
        }

        public int StatusUiSignature() => 0;

        public void RenderStatusUi()
        {
            if (host.runtimeUi != null && host.IsInitialized) host.runtimeUi.HideTrapFloorStatus();
        }

        public void RefreshStatusUi() => host.MarkTableUiDirty();

        public void RefreshShopEntry() => host.runtimeUi?.ShowShopEntry(null);

        public void AddHintHelpers()
        {
        }

        public void OnConsoleCardInteraction(IConsoleCardInteraction interaction, ConsoleCardBehavior behavior)
        {
        }
    }

    /// <summary>
    /// The cartridges the table knows (CART-1): the code cartridges (each registered once, by its game's stable ID)
    /// and a data cartridge for any other game box, made on first use. A game box points at its game; the game picks
    /// its cartridge.
    /// </summary>
    internal sealed class GameCartridgeRegistry
    {
        private readonly TabletopComposition host;
        private readonly List<IGameCartridge> codeCartridges = new List<IGameCartridge>();
        private readonly Dictionary<string, IGameCartridge> byGame = new Dictionary<string, IGameCartridge>(StringComparer.OrdinalIgnoreCase);
        private DataCartridge emptyTable;

        internal GameCartridgeRegistry(TabletopComposition host)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));
        }

        /// <summary>The cartridges with code, in the order they were registered.</summary>
        internal IReadOnlyList<IGameCartridge> CodeCartridges => codeCartridges;

        /// <summary>The Empty Table's cartridge (also a game a player made).</summary>
        internal DataCartridge EmptyTable => emptyTable ?? (emptyTable = new DataCartridge(host, null));

        /// <summary>
        /// The first code cartridge's game: the game an Empty Table borrows cards, rules and pictures from (null when
        /// the build has no game with code).
        /// </summary>
        internal GameDefinition DefaultGame
        {
            get
            {
                for (int i = 0; i < codeCartridges.Count; i++)
                {
                    if (codeCartridges[i].Game != null) return codeCartridges[i].Game;
                }

                return null;
            }
        }

        internal void Register(IGameCartridge cartridge)
        {
            if (cartridge == null) throw new ArgumentNullException(nameof(cartridge));
            if (codeCartridges.Contains(cartridge)) return;
            codeCartridges.Add(cartridge);
            if (!string.IsNullOrEmpty(cartridge.StableId)) byGame[cartridge.StableId] = cartridge;
        }

        /// <summary>The cartridge of a game: its code cartridge, else a data cartridge for it.</summary>
        internal IGameCartridge For(GameDefinition game)
        {
            if (game == null) return EmptyTable;
            IGameCartridge code = CodeCartridgeFor(game);
            if (code != null) return code;
            string id = game.StableId ?? string.Empty;
            if (byGame.TryGetValue(id, out IGameCartridge cartridge)) return cartridge;
            DataCartridge data = new DataCartridge(host, game);
            if (id.Length > 0) byGame[id] = data;
            return data;
        }

        /// <summary>The code cartridge of a game, or null (no game, or a game with data only).</summary>
        internal IGameCartridge CodeCartridgeFor(GameDefinition game)
        {
            if (game == null) return null;
            for (int i = 0; i < codeCartridges.Count; i++)
            {
                if (ReferenceEquals(codeCartridges[i].Game, game)) return codeCartridges[i];
            }

            return null;
        }

        /// <summary>The code cartridge that registered a template, with its own setup for it.</summary>
        internal bool TryGetTemplateSetup(GameTemplateId templateId, out IGameCartridge owner, out object setup)
        {
            for (int i = 0; i < codeCartridges.Count; i++)
            {
                if (codeCartridges[i].TryGetTemplateSetup(templateId, out setup))
                {
                    owner = codeCartridges[i];
                    return true;
                }
            }

            owner = null;
            setup = null;
            return false;
        }

        /// <summary>Every code cartridge forgets its registered templates.</summary>
        internal void ForgetTemplates()
        {
            for (int i = 0; i < codeCartridges.Count; i++)
            {
                codeCartridges[i].ForgetTemplates();
            }
        }
    }
}
