using System;
using ConsoleCards.Application.Results;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.Application
{
    public sealed class ShuffleDeckResultTests
    {
        [Test]
        public void Accepted_ReportsSuccessAcceptedStatusNoneErrorAndRevision()
        {
            ShuffleDeckResult result = ShuffleDeckResult.Accepted(8);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Status, Is.EqualTo(CommandResultStatus.Accepted));
            Assert.That(result.Error, Is.EqualTo(ShuffleDeckError.None));
            Assert.That(result.Revision, Is.EqualTo(8));
            Assert.That(result.CommandResult, Is.EqualTo(CommandResult.Accepted(8)));
        }

        [Test]
        public void Accepted_WhenRevisionIsNegative_ThrowsArgumentOutOfRangeException()
        {
            Assert.That(
                () => ShuffleDeckResult.Accepted(-1),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [TestCase(CommandResultStatus.Invalid, ShuffleDeckError.MatchMissing)]
        [TestCase(CommandResultStatus.Rejected, ShuffleDeckError.ContainerMissing)]
        [TestCase(CommandResultStatus.Conflict, ShuffleDeckError.RevisionConflict)]
        public void Failure_ReportsSuppliedStatusErrorAndRevisionMinusOne(
            CommandResultStatus status,
            ShuffleDeckError error)
        {
            ShuffleDeckResult result = ShuffleDeckResult.Failure(status, error);

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
                () => ShuffleDeckResult.Failure(CommandResultStatus.Accepted, ShuffleDeckError.ContainerMissing),
                Throws.ArgumentException);
        }

        [Test]
        public void Failure_WhenErrorIsNone_ThrowsArgumentException()
        {
            Assert.That(
                () => ShuffleDeckResult.Failure(CommandResultStatus.Rejected, ShuffleDeckError.None),
                Throws.ArgumentException);
        }

        [Test]
        public void EqualityHashCodeAndOperators_WhenValuesMatch_AreConsistent()
        {
            ShuffleDeckResult first = ShuffleDeckResult.Failure(CommandResultStatus.Conflict, ShuffleDeckError.RevisionConflict);
            ShuffleDeckResult second = ShuffleDeckResult.Failure(CommandResultStatus.Conflict, ShuffleDeckError.RevisionConflict);

            Assert.That(first.Equals(second), Is.True);
            Assert.That(first.Equals((object)second), Is.True);
            Assert.That(first == second, Is.True);
            Assert.That(first != second, Is.False);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        }

        [Test]
        public void EqualityHashCodeAndOperators_WhenValuesDiffer_AreConsistent()
        {
            ShuffleDeckResult result = ShuffleDeckResult.Failure(CommandResultStatus.Conflict, ShuffleDeckError.RevisionConflict);
            ShuffleDeckResult differentStatus = ShuffleDeckResult.Failure(CommandResultStatus.Rejected, ShuffleDeckError.RevisionConflict);
            ShuffleDeckResult differentError = ShuffleDeckResult.Failure(CommandResultStatus.Conflict, ShuffleDeckError.RevisionOverflow);

            Assert.That(result != differentStatus, Is.True);
            Assert.That(result != differentError, Is.True);
        }

        [Test]
        public void ToString_ContainsUsefulStatusErrorAndRevisionInformation()
        {
            ShuffleDeckResult result = ShuffleDeckResult.Failure(CommandResultStatus.Rejected, ShuffleDeckError.ContainerNotDeck);

            string text = result.ToString();

            Assert.That(text, Does.Contain(CommandResultStatus.Rejected.ToString()));
            Assert.That(text, Does.Contain(ShuffleDeckError.ContainerNotDeck.ToString()));
            Assert.That(text, Does.Contain("-1"));
        }
    }
}
