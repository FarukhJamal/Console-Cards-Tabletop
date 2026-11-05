using System;
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
        public void DefaultResults_AreNeverSuccesses()
        {
            Assert.IsFalse(default(TrapFloorBuyFromShopResult).Succeeded);
            Assert.IsFalse(default(TrapFloorTakePrizeResult).Succeeded);
            Assert.IsFalse(default(DrawCardsResult).Succeeded);
        }
    }
}
