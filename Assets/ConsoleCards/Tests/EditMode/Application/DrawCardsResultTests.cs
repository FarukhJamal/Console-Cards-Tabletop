using System;
using ConsoleCards.Application.Results;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.Application
{
    public sealed class DrawCardsResultTests
    {
        [Test]
        public void Accepted_ReportsSuccessAcceptedStatusNoneErrorAndRevision()
        {
            DrawCardsResult result = DrawCardsResult.Accepted(12);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Status, Is.EqualTo(CommandResultStatus.Accepted));
            Assert.That(result.Error, Is.EqualTo(DrawCardsError.None));
            Assert.That(result.Revision, Is.EqualTo(12));
            Assert.That(result.CommandResult, Is.EqualTo(CommandResult.Accepted(12)));
        }

        [Test]
        public void Accepted_WhenRevisionIsNegative_ThrowsArgumentOutOfRangeException()
        {
            Assert.That(
                () => DrawCardsResult.Accepted(-1),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [TestCase(CommandResultStatus.Invalid, DrawCardsError.MatchMissing)]
        [TestCase(CommandResultStatus.Rejected, DrawCardsError.InsufficientCards)]
        [TestCase(CommandResultStatus.Conflict, DrawCardsError.RevisionConflict)]
        public void Failure_ReportsSuppliedStatusErrorAndRevisionMinusOne(
            CommandResultStatus status,
            DrawCardsError error)
        {
            DrawCardsResult result = DrawCardsResult.Failure(status, error);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Status, Is.EqualTo(status));
            Assert.That(result.Error, Is.EqualTo(error));
            Assert.That(result.Revision, Is.EqualTo(-1));
            Assert.That(result.CommandResult, Is.EqualTo(CommandResult.Failure(status)));
        }

        [Test]
        public void Failure_WhenStatusIsAccepted_ThrowsArgumentException()
        {
            Assert.That(
                () => DrawCardsResult.Failure(CommandResultStatus.Accepted, DrawCardsError.InsufficientCards),
                Throws.ArgumentException);
        }

        [Test]
        public void Failure_WhenErrorIsNone_ThrowsArgumentException()
        {
            Assert.That(
                () => DrawCardsResult.Failure(CommandResultStatus.Rejected, DrawCardsError.None),
                Throws.ArgumentException);
        }

        [Test]
        public void EqualityHashCodeAndOperators_WhenValuesMatch_AreConsistent()
        {
            DrawCardsResult first = DrawCardsResult.Failure(CommandResultStatus.Conflict, DrawCardsError.RevisionConflict);
            DrawCardsResult second = DrawCardsResult.Failure(CommandResultStatus.Conflict, DrawCardsError.RevisionConflict);

            Assert.That(first.Equals(second), Is.True);
            Assert.That(first.Equals((object)second), Is.True);
            Assert.That(first == second, Is.True);
            Assert.That(first != second, Is.False);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        }

        [Test]
        public void EqualityHashCodeAndOperators_WhenValuesDiffer_AreConsistent()
        {
            DrawCardsResult result = DrawCardsResult.Failure(CommandResultStatus.Conflict, DrawCardsError.RevisionConflict);
            DrawCardsResult differentStatus = DrawCardsResult.Failure(CommandResultStatus.Rejected, DrawCardsError.RevisionConflict);
            DrawCardsResult differentError = DrawCardsResult.Failure(CommandResultStatus.Conflict, DrawCardsError.RevisionOverflow);

            Assert.That(result != differentStatus, Is.True);
            Assert.That(result != differentError, Is.True);
        }

        [Test]
        public void ToString_ContainsUsefulStatusErrorAndRevisionInformation()
        {
            DrawCardsResult result = DrawCardsResult.Failure(CommandResultStatus.Rejected, DrawCardsError.DestinationCapacityExceeded);

            string text = result.ToString();

            Assert.That(text, Does.Contain(CommandResultStatus.Rejected.ToString()));
            Assert.That(text, Does.Contain(DrawCardsError.DestinationCapacityExceeded.ToString()));
            Assert.That(text, Does.Contain("-1"));
        }
    }
}
