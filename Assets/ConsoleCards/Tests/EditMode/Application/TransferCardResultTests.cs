using System;
using ConsoleCards.Application.Results;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.Application
{
    public sealed class TransferCardResultTests
    {
        [Test]
        public void Accepted_ReturnsAcceptedResult()
        {
            TransferCardResult result = TransferCardResult.Accepted(7);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Status, Is.EqualTo(CommandResultStatus.Accepted));
            Assert.That(result.Revision, Is.EqualTo(7));
            Assert.That(result.Error, Is.EqualTo(TransferCardError.None));
        }

        [TestCase(CommandResultStatus.Invalid, TransferCardError.MatchMissing)]
        [TestCase(CommandResultStatus.Conflict, TransferCardError.RevisionConflict)]
        [TestCase(CommandResultStatus.Rejected, TransferCardError.ObjectMissing)]
        public void Failure_MapsStatusAndError(CommandResultStatus status, TransferCardError error)
        {
            TransferCardResult result = TransferCardResult.Failure(status, error);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Status, Is.EqualTo(status));
            Assert.That(result.Error, Is.EqualTo(error));
            Assert.That(result.Revision, Is.EqualTo(-1));
        }

        [Test]
        public void Failure_WhenStatusIsAccepted_Rejects()
        {
            Assert.Throws<ArgumentException>(() => TransferCardResult.Failure(
                CommandResultStatus.Accepted,
                TransferCardError.ObjectMissing));
        }

        [Test]
        public void Failure_WhenErrorIsNone_Rejects()
        {
            Assert.Throws<ArgumentException>(() => TransferCardResult.Failure(
                CommandResultStatus.Rejected,
                TransferCardError.None));
        }

        [Test]
        public void Equality_WhenValuesMatch_ReturnsTrueAndHashMatches()
        {
            TransferCardResult left = TransferCardResult.Failure(
                CommandResultStatus.Rejected,
                TransferCardError.ObjectMissing);
            TransferCardResult right = TransferCardResult.Failure(
                CommandResultStatus.Rejected,
                TransferCardError.ObjectMissing);

            Assert.That(left, Is.EqualTo(right));
            Assert.That(left == right, Is.True);
            Assert.That(left.GetHashCode(), Is.EqualTo(right.GetHashCode()));
        }

        [Test]
        public void Inequality_WhenValuesDiffer_ReturnsTrue()
        {
            TransferCardResult left = TransferCardResult.Failure(
                CommandResultStatus.Rejected,
                TransferCardError.ObjectMissing);
            TransferCardResult right = TransferCardResult.Failure(
                CommandResultStatus.Rejected,
                TransferCardError.ObjectNotCard);

            Assert.That(left, Is.Not.EqualTo(right));
            Assert.That(left != right, Is.True);
        }

        [Test]
        public void ToString_ContainsDiagnosticContent()
        {
            TransferCardResult result = TransferCardResult.Failure(
                CommandResultStatus.Rejected,
                TransferCardError.DestinationCapacityExceeded);

            Assert.That(result.ToString(), Does.Contain(nameof(CommandResult)));
            Assert.That(result.ToString(), Does.Contain(nameof(TransferCardError.DestinationCapacityExceeded)));
        }
    }
}
