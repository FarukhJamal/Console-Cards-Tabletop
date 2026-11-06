using System;
using System.Collections.Generic;
using ConsoleCards.Application.Authority;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Random;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates.Definitions;
using ConsoleCards.Games.TrapFloor;
using ConsoleCards.Presentation.Composition;
using ConsoleCards.Presentation.UI;
using NUnit.Framework;

namespace ConsoleCards.Tests
{
    /// <summary>
    /// 2026-11-06: ASSIST-ROOM (the room's Assist level), DEV-FIXTURE (reproducible deals) and AUTH-PRIZE (the host's
    /// own check of a prize claim). Deterministic; no network.
    /// </summary>
    public sealed class FullModeAndFixtureTests
    {
        private PlayAssistLevel savedLocal;

        [SetUp]
        public void Remember()
        {
            PlayAssist.ClearRoomLevel();
            savedLocal = PlayAssist.LocalLevel;
        }

        [TearDown]
        public void Restore()
        {
            PlayAssist.ClearRoomLevel();
            PlayAssist.TryChoose(savedLocal); // this computer's own setting as it was
        }

        [Test]
        public void JoinedComputer_PlaysTheHostsLevel_AndCannotChangeIt()
        {
            PlayAssist.TryChoose(PlayAssistLevel.Free);
            PlayAssist.UseRoomLevel(PlayAssistLevel.Full, true); // the host's roster
            Assert.AreEqual(PlayAssistLevel.Full, PlayAssist.Level);
            Assert.IsTrue(PlayAssist.IsSetByRoom);
            Assert.IsFalse(PlayAssist.IsQuiet, "Full rules run on the joined computer too");

            Assert.IsFalse(PlayAssist.TryChoose(PlayAssistLevel.Free), "a joined computer cannot change the room's level");
            PlayAssist.Level = PlayAssistLevel.Reminders; // the old setter path is refused the same way
            Assert.AreEqual(PlayAssistLevel.Full, PlayAssist.Level);
            Assert.AreEqual(PlayAssistLevel.Free, PlayAssist.LocalLevel, "its own setting is untouched");
        }

        [Test]
        public void Host_ChangesTheRoomsLevel()
        {
            PlayAssist.UseRoomLevel(PlayAssistLevel.Free, false); // hosting
            Assert.IsFalse(PlayAssist.IsSetByRoom);
            Assert.IsTrue(PlayAssist.TryChoose(PlayAssistLevel.Full));
            Assert.AreEqual(PlayAssistLevel.Full, PlayAssist.Level, "the roster sends this to everyone");
        }

        [Test]
        public void LeavingTheRoom_GivesBackThisComputersOwnLevel()
        {
            PlayAssist.TryChoose(PlayAssistLevel.Reminders);
            PlayAssist.UseRoomLevel(PlayAssistLevel.Full, true);
            PlayAssist.ClearRoomLevel();
            Assert.AreEqual(PlayAssistLevel.Reminders, PlayAssist.Level);
            Assert.IsTrue(PlayAssist.TryChoose(PlayAssistLevel.Free));
        }

        [Test]
        public void ThreeLevels_StayDistinct()
        {
            PlayAssist.UseRoomLevel(PlayAssistLevel.Free, true);
            Assert.IsTrue(PlayAssist.IsFree && PlayAssist.IsQuiet);
            PlayAssist.UseRoomLevel(PlayAssistLevel.Reminders, true);
            Assert.IsTrue(!PlayAssist.IsFree && PlayAssist.IsQuiet);
            PlayAssist.UseRoomLevel(PlayAssistLevel.Full, true);
            Assert.IsTrue(!PlayAssist.IsFree && !PlayAssist.IsQuiet);
        }

        [Test]
        public void FixedSeed_DealsTheSameFloor_AndDescribesIt()
        {
            GameDefinitionData game = TrapFloorTestTable.LoadGame();
            for (int m = 0; m < game.Modes.Count; m++)
            {
                string mode = game.Modes[m].StableId;
                TrapFloorFloorRecipe recipe = new TrapFloorFloorRecipe(3, 3, true, 3);
                TrapFloorTemplateDefinition first = TrapFloorTemplateFactory.CreateStandardFourPlayer(
                    new SystemRandomValueSource(1101), game, mode, null, 0, null, 3, null, null, recipe);
                TrapFloorTemplateDefinition second = TrapFloorTemplateFactory.CreateStandardFourPlayer(
                    new SystemRandomValueSource(1101), game, mode, null, 0, null, 3, null, null, recipe);
                string a = TrapFloorDevFixtures.DescribeTemplate(first);
                Assert.AreEqual(a, TrapFloorDevFixtures.DescribeTemplate(second), $"{mode}: same seed, same floor");
                List<TrapFloorCoordinate> keys = TrapFloorDevFixtures.CellsNamed(first, "Key");
                Assert.AreEqual(3, keys.Count, $"{mode}: the fixture floor shows its 3 Keys\n{a}");
            }
        }

        [Test]
        public void HostCheck_RefusesWhatTheGameRefuses_AndDoesNotRememberIt()
        {
            HostCommandPolicy policy = new HostCommandPolicy();
            PlayerId sender = new PlayerId(Guid.NewGuid());
            FlipCardCommand command = new FlipCardCommand(
                new CommandContext(CommandId.New(), new MatchId(Guid.NewGuid()), sender, null),
                new TabletopObjectId(Guid.NewGuid()),
                CardFace.FaceUp);
            bool allow = false;
            Func<object, PlayerId, bool> game = (_, __) => allow;
            Assert.AreEqual(HostCommandVerdict.RefusedByGame, policy.Check(null, sender, command, null, game));
            allow = true; // e.g. the pawn reached the tile on the host's table
            Assert.AreEqual(HostCommandVerdict.Allowed, policy.Check(null, sender, command, null, game),
                "a refused command is not counted as run");
            Assert.AreEqual(HostCommandVerdict.Duplicate, policy.Check(null, sender, command, null, game));
        }
    }
}
