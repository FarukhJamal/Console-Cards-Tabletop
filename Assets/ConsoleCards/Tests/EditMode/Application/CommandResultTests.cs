using System;
using ConsoleCards.Application.Results;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.Application
{
    public sealed class CommandResultTests
    {
        [Test]
        public void Accepted_StoresRevisionAndReportsSuccess()
        {
            CommandResult result = CommandResult.Accepted(8);

            Assert.That(result.Status, Is.EqualTo(CommandResultStatus.Accepted));
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Revision, Is.EqualTo(8));
        }

        [Test]
        public void Accepted_WhenRevisionIsNegative_ThrowsArgumentOutOfRangeException()
        {
            Assert.That(
                () => CommandResult.Accepted(-1),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Failure_SupportsEachNonAcceptedStatus()
        {
            CommandResultStatus[] statuses =
            {
                CommandResultStatus.Rejected,
                CommandResultStatus.Conflict,
                CommandResultStatus.Invalid,
                CommandResultStatus.Unauthorized,
                CommandResultStatus.Stale
            };

            foreach (CommandResultStatus status in statuses)
            {
                CommandResult result = CommandResult.Failure(status);

                Assert.That(result.Status, Is.EqualTo(status));
                Assert.That(result.Succeeded, Is.False);
            }
        }

        [Test]
        public void Failure_WhenStatusIsAccepted_ThrowsArgumentException()
        {
            Assert.That(
                () => CommandResult.Failure(CommandResultStatus.Accepted),
                Throws.ArgumentException);
        }

        [Test]
        public void Failure_UsesRevisionMinusOne()
        {
            CommandResult result = CommandResult.Failure(CommandResultStatus.Conflict);

            Assert.That(result.Revision, Is.EqualTo(-1));
        }

        [Test]
        public void Equality_WhenValuesMatch_ComparesEqual()
        {
            CommandResult first = CommandResult.Accepted(2);
            CommandResult second = CommandResult.Accepted(2);

            Assert.That(first.Equals(second), Is.True);
            Assert.That(first.Equals((object)second), Is.True);
            Assert.That(first == second, Is.True);
            Assert.That(first != second, Is.False);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        }

        [Test]
        public void Equality_WhenStatusOrRevisionDiffers_ComparesUnequal()
        {
            CommandResult accepted = CommandResult.Accepted(2);
            CommandResult differentRevision = CommandResult.Accepted(3);
            CommandResult differentStatus = CommandResult.Failure(CommandResultStatus.Rejected);

            Assert.That(accepted != differentRevision, Is.True);
            Assert.That(accepted != differentStatus, Is.True);
        }

        [Test]
        public void ToString_IdentifiesStatusAndRevision()
        {
            CommandResult result = CommandResult.Accepted(4);

            string text = result.ToString();

            Assert.That(text, Does.Contain(CommandResultStatus.Accepted.ToString()));
            Assert.That(text, Does.Contain("4"));
        }
    }
}
