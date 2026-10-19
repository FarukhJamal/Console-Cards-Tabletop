using System;
using System.Collections.Generic;
using ConsoleCards.Core.Events;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Definitions;
using ConsoleCards.GameTemplates;
using ConsoleCards.GameTemplates.Definitions;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// The cartridge contract (CART-1, doc 30 §A4): what a game hands the table. The table code talks to "the current
    /// cartridge" through this, never to a game by name. A <b>code cartridge</b> (Trap Floor) brings its own helpers:
    /// table setups (game templates), a per-frame step, its status card, its Shop, its hint helpers, its own card
    /// menus and its Console card reactions. A <b>data cartridge</b> (<see cref="DataCartridge"/>: an Empty Table, a game
    /// a player made, a DLC game without code) needs no code: every hook does nothing and the status line names it.
    /// CART-1a moves the table's hook points behind the contract; CART-1b moves the rest of the game's state.
    /// </summary>
    internal interface IGameCartridge
    {
        /// <summary>The game's stable ID ("empty-table" for an Empty Table).</summary>
        string StableId { get; }

        /// <summary>The game's name as players see it.</summary>
        string DisplayName { get; }

        /// <summary>The game's definition (cards, modes, rules); null for an Empty Table.</summary>
        GameDefinition Game { get; }

        /// <summary>True for a cartridge with code (helpers); false for data only.</summary>
        bool HasCode { get; }

        /// <summary>Adds the cartridge's table setups (one per mode) to the catalog the New table menu starts.</summary>
        void RegisterTemplates(List<GameTemplateRegistration> registrations);

        /// <summary>The table was built and is ready (presentation the game shows from the start).</summary>
        void OnTableReady();

        /// <summary>The table is going away (a new table, Undo's rebuild, leaving Play mode).</summary>
        void OnTableShutdown();

        /// <summary>Each frame, before the hint helpers (for example a landing tile turning over).</summary>
        void TickEarly();

        /// <summary>Each frame, after the hint helpers (for example dice of the game settling).</summary>
        void TickLate();

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

        /// <summary>Shows one of the game's own menus (true) or leaves the mode to the table (false).</summary>
        bool TryShowContextMenu(TabletopComposition.ContextMenuMode mode);

        /// <summary>True for a deck the game deals from itself (the table's Deal and Shuffle leave it alone).</summary>
        bool IsAssistedControllerDeck(ContainerId containerId);
    }

    /// <summary>
    /// A cartridge with no code (CART-1): an Empty Table, a game a player made (doc 29) or a DLC game that is data only.
    /// The table needs nothing from it; the status line shows its name and how many things are on the table.
    /// </summary>
    internal sealed class DataCartridge : IGameCartridge
    {
        public const string EmptyTableId = "empty-table";

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

        public void OnTableReady()
        {
        }

        public void OnTableShutdown()
        {
        }

        public void TickEarly()
        {
        }

        public void TickLate()
        {
        }

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

        public bool TryShowContextMenu(TabletopComposition.ContextMenuMode mode) => false;

        public bool IsAssistedControllerDeck(ContainerId containerId) => false;
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
            string id = game.StableId ?? string.Empty;
            for (int i = 0; i < codeCartridges.Count; i++)
            {
                if (ReferenceEquals(codeCartridges[i].Game, game)) return codeCartridges[i];
            }

            if (byGame.TryGetValue(id, out IGameCartridge cartridge)) return cartridge;
            DataCartridge data = new DataCartridge(host, game);
            if (id.Length > 0) byGame[id] = data;
            return data;
        }
    }
}
