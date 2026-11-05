using System;
using System.Collections.Generic;
using ConsoleCards.Application.Authority;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Games.TrapFloor;
using NUnit.Framework;

namespace ConsoleCards.Tests
{
    /// <summary>
    /// Regression tests for the 2026-11-05 multiplayer stabilization pass (B01 / B08). Pure C#: they need no scene.
    /// Run them in Window › General › Test Runner › EditMode.
    /// </summary>
    public sealed class StabilizationRegressionTests
    {
        private static ReplicatedTableAuthority HostWhereNobodyPlaysHere()
        {
            ReplicatedTableAuthority authority = new ReplicatedTableAuthority(
                new LocalTableAuthority(), () => null, null, null)
            {
                Mode = TableReplicationMode.Host,
                IsLocalPlayer = _ => false, // every seat is played on another computer
            };
            return authority;
        }

        private static CommandContext ContextFor(PlayerId player) =>
            new CommandContext(CommandId.New(), MatchId.New(), player, null);

        [Test]
        public void ShopPurchaseRunOnAnotherComputer_IsNotReportedAsSuccess()
        {
            // B01/B08: a command for a seat played elsewhere is not run here; it used to read as Accepted (status 0).
            TrapFloorBuyFromShopResult result = HostWhereNobodyPlaysHere()
                .Submit<TrapFloorBuyFromShopCommand, TrapFloorBuyFromShopResult>(
                    new TrapFloorBuyFromShopCommand(
                        ContextFor(PlayerId.New()), "ability-test", TabletopObjectId.New(), Array.Empty<TabletopObjectId>()));
            Assert.IsFalse(result.Succeeded);
        }

        [Test]
        public void TakePrizeRunOnAnotherComputer_IsNotReportedAsSuccess()
        {
            TrapFloorTakePrizeResult result = HostWhereNobodyPlaysHere()
                .Submit<TrapFloorTakePrizeCommand, TrapFloorTakePrizeResult>(
                    new TrapFloorTakePrizeCommand(
                        ContextFor(PlayerId.New()),
                        TabletopObjectId.New(),
                        TabletopObjectId.New(),
                        Array.Empty<TabletopObjectId>(),
                        new TableCoordinate(0d, 0d)));
            Assert.IsFalse(result.Succeeded);
        }

        [Test]
        public void ThirteenthAcquiredCard_DoesNotLandOnAnyEarlierCard()
        {
            // F (post-stabilization): the 12-spot row used to put card 13 back on spot 0.
            TabletopPose basePose = new TabletopPose(new TableCoordinate(3d, -2d), 90f, 0, 0);
            List<TableCoordinate> lying = new List<TableCoordinate>();
            for (int i = 0; i < 30; i++)
            {
                TabletopPose next = TrapFloorTakePrizeUseCase.NextFreeOverflowPose(basePose, lying);
                for (int j = 0; j < lying.Count; j++)
                {
                    double dx = lying[j].X - next.Position.X;
                    double dy = lying[j].Y - next.Position.Y;
                    Assert.Greater(Math.Sqrt((dx * dx) + (dy * dy)), 0.2d, $"card {i + 1} overlaps card {j + 1}");
                }

                lying.Add(next.Position);
            }
        }

        [Test]
        public void AcquiredCard_FillsTheGapLeftByAPickedUpCard()
        {
            TabletopPose basePose = new TabletopPose(new TableCoordinate(0d, 0d), 0f, 0, 0);
            List<TableCoordinate> lying = new List<TableCoordinate>();
            for (int i = 0; i < 5; i++) lying.Add(TrapFloorTakePrizeUseCase.NextFreeOverflowPose(basePose, lying).Position);
            TableCoordinate gap = lying[2];
            lying.RemoveAt(2); // the player picked up the third card
            TabletopPose next = TrapFloorTakePrizeUseCase.NextFreeOverflowPose(basePose, lying);
            Assert.AreEqual(gap.X, next.Position.X, 1e-9);
            Assert.AreEqual(gap.Y, next.Position.Y, 1e-9);
        }

        [Test]
        public void DefaultResults_AreNeverSuccesses()
        {
            Assert.IsFalse(default(TrapFloorBuyFromShopResult).Succeeded);
            Assert.IsFalse(default(TrapFloorTakePrizeResult).Succeeded);
            Assert.IsFalse(default(DrawCardsResult).Succeeded);
        }
    }
}
