using System;
using System.Collections.Generic;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Dice;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates;
using ConsoleCards.Games.TrapFloor;
using ConsoleCards.Presentation.Content;
using ConsoleCards.Presentation.UI;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// LAYOUT-1 (owner, 2026-10-21): the players' own table layout. "Arrange table…" in the Table menu shows the real
    /// table from above; players move the Consoles and piles (right-click → Move), drag the floor dice and place the
    /// board (right-click a floor tile → Move board). "Use Table" asks every player to agree; the layout is then saved
    /// for this game and player count on this computer and the table is dealt fresh with it. Every later deal (new
    /// game, restart, Reset, each smaller floor) uses it until the players agree to the default layout again.
    /// Event-time only; nothing here runs per frame except a cheap flag check.
    /// </summary>
    internal sealed partial class TrapFloorCartridge
    {
        private const string SeatRolePrefix = "seat:";
        private const string ControllerDeckRole = "pile:controller";
        private const string AbilityDeckRole = "pile:ability";
        private const string AbilityDiscardRole = "pile:ability-discard";
        private const string DieXRole = "die:x";
        private const string DieYRole = "die:y";
        private const string BoardRole = "board";

        private TableLayoutStore layoutStore;
        private TrapFloorTableLayout layoutPreview;
        private Action openArrangeTableAction;
        private bool arranging;
        private bool arrangePreviewDealt;
        private bool arrangeRedealPending;
        private TableCoordinate? arrangeBoardCenter;

        private readonly List<PlayerId> layoutVoters = new List<PlayerId>(4);
        private readonly List<bool> layoutAgreed = new List<bool>(4);
        private bool layoutProposalToDefault;
        private RuleProposalActions layoutProposalActions;

        internal bool IsArrangingTable => arranging;

        internal Action OpenArrangeTableAction => openArrangeTableAction ??= OpenArrangeTable;

        internal bool CanArrangeTable =>
            host.trapFloorTemplate != null
            && host.activeSession != null
            && host.activeSession.Selection.Kind == TabletopSessionKind.GameTemplate;

        // The layout key: Trap Floor always seats its four players.
        private static int LayoutPlayerCount => TrapFloorTemplateFactory.StandardPlayerCount;

        private string LayoutGameId => Game != null ? Game.StableId : "trap-floor";

        private TableLayoutStore LayoutStore =>
            layoutStore ??= new TableLayoutStore(ContentPackStore.ForProfile(host.EnsureProfile()));

        // ---------- the layout a deal uses ----------

        // The layout being arranged (a preview deal), else the saved one, else none (the default places).
        internal TrapFloorTableLayout LayoutForDeal()
        {
            if (layoutPreview != null) return layoutPreview;
            try
            {
                TableLayoutRecord record = LayoutStore.TryLoad(LayoutGameId, LayoutPlayerCount);
                return record != null ? ToLayout(record) : null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Layout] The saved table layout could not be read ({exception.Message}).");
                return null;
            }
        }

        private static TrapFloorTableLayout ToLayout(TableLayoutRecord record)
        {
            TabletopPose?[] seats = new TabletopPose?[LayoutPlayerCount];
            for (int i = 0; i < seats.Length; i++) seats[i] = Place(record, SeatRolePrefix + i);
            TabletopPose? board = Place(record, BoardRole);
            return new TrapFloorTableLayout(
                seats,
                Place(record, ControllerDeckRole),
                Place(record, AbilityDeckRole),
                Place(record, AbilityDiscardRole),
                Place(record, DieXRole),
                Place(record, DieYRole),
                board.HasValue ? board.Value.Position : (TableCoordinate?)null);
        }

        private static TabletopPose? Place(TableLayoutRecord record, string role) =>
            record.TryGet(role, out TableLayoutPlaceRecord place)
                ? new TabletopPose(new TableCoordinate(place.x, place.y), place.rotation, 0, 0)
                : (TabletopPose?)null;

        // Where every piece is on the table now (the board where it was placed while arranging).
        private TableLayoutRecord CaptureLayout()
        {
            TrapFloorTemplateDefinition template = host.trapFloorTemplate;
            TableLayoutRecord record = new TableLayoutRecord
            {
                gameStableId = LayoutGameId,
                playerCount = LayoutPlayerCount,
                savedUtc = DateTime.UtcNow.ToString("o"),
            };
            for (int i = 0; i < template.Players.Count; i++)
            {
                TrapFloorPlayerSetupDefinition setup = template.Players[i];
                if (host.matchState.Seats.TryGetValue(setup.SeatId, out SeatState seat))
                {
                    TabletopPose pose = seat.ConsolePose;
                    record.Set(SeatRolePrefix + setup.LayoutSeatIndex, pose.Position.X, pose.Position.Y, pose.RotationDegrees);
                }
            }

            CapturePile(record, ControllerDeckRole, template.Players.Count > 0 ? template.Players[0].ControllerDeckId : ContainerId.Empty);
            CapturePile(record, AbilityDeckRole, template.AbilityDeckId);
            CapturePile(record, AbilityDiscardRole, template.AbilityDiscardId);
            CaptureDie(record, DieXRole, template.FloorfallXAxisDieId);
            CaptureDie(record, DieYRole, template.FloorfallYAxisDieId);
            TableCoordinate board = arrangeBoardCenter ?? new TableCoordinate(template.Grid.OriginX, template.Grid.OriginY);
            record.Set(BoardRole, board.X, board.Y, 0f);
            return record;
        }

        private void CapturePile(TableLayoutRecord record, string role, ContainerId containerId)
        {
            if (!containerId.IsEmpty && host.matchState.ContainerPlacements.TryGetValue(containerId, out ContainerPlacementState placement))
            {
                record.Set(role, placement.Pose.Position.X, placement.Pose.Position.Y, placement.Pose.RotationDegrees);
            }
        }

        private void CaptureDie(TableLayoutRecord record, string role, TabletopObjectId dieId)
        {
            if (!host.matchState.Dice.TryGetValue(dieId, out DieState die)) return;
            TableCoordinate position = die.BaseState.Pose.Position;
            PhysicalObjectState physical = die.BaseState.PhysicalState;
            if (physical != null && host.coordinateConverter != null)
            {
                position = host.coordinateConverter.ToTableCoordinate(
                    new Vector3(physical.Position.X, physical.Position.Y, physical.Position.Z));
            }

            record.Set(role, position.X, position.Y, 0f);
        }

        // ---------- Arrange table ----------

        internal void OpenArrangeTable()
        {
            if (!CanArrangeTable || host.runtimeUi == null) return;
            if (host.runtimeUi.IsTablePanelOpen && !arranging)
            {
                host.ShowMessage("Finish Table tools first.");
                return;
            }

            host.ContextMenus.CloseContextMenu();
            arranging = true;
            arrangePreviewDealt = false;
            arrangeBoardCenter = null;
            host.FrameWholeTable();
            ShowArrangePanel();
            host.ShowMessage("Arrange the table: right-click a Console or a pile and choose Move, drag the dice, right-click "
                + "the board for Move board. Use Table asks everyone, then deals the table fresh with this layout.");
        }

        private void ShowArrangePanel()
        {
            TablePanelModel model = new TablePanelModel
            {
                SelectedShape = -1,
                ShowShapes = false,
                ShowHistory = false,
                ResetText = "Default Layout",
                CancelText = "Cancel",
                DoneText = "Use Table",
                Status = "ARRANGE TABLE\nRight-click a Console or a pile → Move. Drag the dice. Right-click the board → "
                    + "Move board.\nUse Table: everyone agrees, then the table is dealt fresh with this layout.",
            };
            for (int i = 0; i < model.FieldText.Length; i++) model.FieldText[i] = string.Empty;
            host.runtimeUi.ShowTablePanel(model, new TablePanelActions
            {
                Reset = () => ProposeLayout(true),
                Cancel = CancelArrangeTable,
                Done = () => ProposeLayout(false),
            });
        }

        internal void BeginArrangeBoardMove()
        {
            host.ContextMenus.CloseContextMenu();
            host.BeginTemplateBoardPlacement(pose =>
            {
                arrangeBoardCenter = pose.Position;
                arrangeRedealPending = true; // dealt on the next frame, once the placement has finished
                return true;
            });
        }

        // Called once a frame: the board was placed, so the table is dealt again to show it there.
        internal void DealArrangedBoardIfPlaced()
        {
            if (!arrangeRedealPending) return;
            arrangeRedealPending = false;
            if (!arranging || host.trapFloorTemplate == null) return;
            TrapFloorTableLayout preview = ToLayout(CaptureLayout());
            arrangeBoardCenter = null; // the board is now dealt there
            arrangePreviewDealt = true;
            if (DealWithLayout(preview))
            {
                host.FrameWholeTable();
                ShowArrangePanel();
                host.ShowMessage("The board is in its new place. Use Table keeps this layout; Cancel goes back.");
            }
        }

        private void CancelArrangeTable()
        {
            bool dealt = arrangePreviewDealt;
            EndArrangeTable();
            if (dealt)
            {
                DealWithLayout(null); // back to the saved (or default) layout
                host.ShowMessage("Arranging cancelled: the table is dealt with the layout in use.");
            }
            else
            {
                host.ShowMessage("Arranging cancelled. Pieces you moved stay where they are (Undo takes them back).");
            }
        }

        private void EndArrangeTable()
        {
            arranging = false;
            arrangePreviewDealt = false;
            arrangeRedealPending = false;
            arrangeBoardCenter = null;
            host.runtimeUi?.CloseTablePanel();
            host.ShowPlayView();
        }

        // Deals the same game and floor again, with this layout for the deal (null: the saved or default one).
        private bool DealWithLayout(TrapFloorTableLayout layout)
        {
            if (host.activeSession == null || host.trapFloorTemplate == null) return false;
            int floorIndex = host.trapFloorTemplate.FloorIndex;
            layoutPreview = layout;
            host.trapFloorNextFloorIndex = floorIndex;
            try
            {
                return host.SessionService.TryReplaceTable(host.activeSession.Selection);
            }
            finally
            {
                host.trapFloorNextFloorIndex = 0;
                layoutPreview = null;
            }
        }

        // ---------- everyone agrees (the rule-change panel) ----------

        private void ProposeLayout(bool toDefault)
        {
            if (host.activeSession == null || host.runtimeUi == null) return;
            layoutProposalToDefault = toDefault;
            layoutVoters.Clear();
            layoutAgreed.Clear();
            IReadOnlyList<PlayerId> players = host.activeSession.Request.ActivePlayerIds;
            for (int i = 0; i < players.Count; i++)
            {
                layoutVoters.Add(players[i]);
                layoutAgreed.Add(false);
            }

            layoutProposalActions ??= new RuleProposalActions
            {
                Agree = AgreeLayout,
                Reject = RejectLayout,
            };
            ShowLayoutProposal();
        }

        private void ShowLayoutProposal()
        {
            RuleProposalVoter[] voters = new RuleProposalVoter[layoutVoters.Count];
            for (int i = 0; i < voters.Length; i++) voters[i] = new RuleProposalVoter(host.PlayerLabel(layoutVoters[i]), layoutAgreed[i]);
            bool saved = LayoutStore.TryLoad(LayoutGameId, LayoutPlayerCount) != null;
            RuleProposalModel model = new RuleProposalModel
            {
                Title = layoutProposalToDefault ? "USE THE DEFAULT LAYOUT?" : "USE THIS TABLE?",
                CurrentText = saved ? "In use: your saved table layout" : "In use: the game's default layout",
                PreviewText = layoutProposalToDefault
                    ? "Back to the game's default layout for every deal."
                    : "This layout for every deal of this game (new game, restart, Reset, each floor).",
                EditKind = RuleProposalEditKind.None,
                Agreeing = true,
                Voters = voters,
                Status = "Every player agrees; then the table is dealt fresh (the game starts over).",
                RejectText = "No, keep arranging",
            };
            host.runtimeUi.ShowRuleProposal(model, layoutProposalActions);
        }

        private void AgreeLayout(int voter)
        {
            if (voter < 0 || voter >= layoutAgreed.Count) return;
            layoutAgreed[voter] = true;
            for (int i = 0; i < layoutAgreed.Count; i++)
            {
                if (!layoutAgreed[i])
                {
                    ShowLayoutProposal();
                    return;
                }
            }

            CommitLayout();
        }

        private void RejectLayout()
        {
            host.runtimeUi?.CloseRuleProposal();
            host.ShowMessage("Not everyone agreed. Keep arranging, or Cancel.");
        }

        private void CommitLayout()
        {
            host.runtimeUi?.CloseRuleProposal();
            string error;
            bool stored = layoutProposalToDefault
                ? LayoutStore.TryDelete(LayoutGameId, LayoutPlayerCount, out error)
                : LayoutStore.TrySave(LayoutGameId, CaptureLayout(), out error);
            TrapFloorTableLayout unsaved = !stored && !layoutProposalToDefault ? ToLayout(CaptureLayout()) : null;
            EndArrangeTable();
            if (!stored) Debug.LogWarning($"[Layout] The table layout could not be saved: {error}");
            DealWithLayout(unsaved); // the saved layout (or default), or this one for this deal if saving failed
            host.ShowMessage(layoutProposalToDefault
                ? "Everyone agreed: the default table layout is back."
                : stored
                    ? "Everyone agreed: this table layout is saved and used for every deal of this game."
                    : "Everyone agreed, but the layout could not be saved on this computer; it is used for this deal only.");
        }
    }
}
