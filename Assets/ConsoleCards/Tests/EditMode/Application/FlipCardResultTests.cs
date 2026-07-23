using System;
using ConsoleCards.Application.Results;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.Application
{
    public sealed class FlipCardResultTests
    {
        [Test]
        public void Accepted_ReportsSuccessAcceptedStatusNoneErrorAndRevision()
        {
            FlipCardResult result = FlipCardResult.Accepted(7);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Status, Is.EqualTo(CommandResultStatus.Accepted));
            Assert.That(result.Error, Is.EqualTo(FlipCardError.None));
            Assert.That(result.Revision, Is.EqualTo(7));
            Assert.That(result.CommandResult, Is.EqualTo(CommandResult.Accepted(7)));
        }

        [Test]
        public void Accepted_WhenRevisionIsNegative_ThrowsArgumentOutOfRangeException()
        {
            Assert.That(
                () => FlipCardResult.Accepted(-1),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [TestCase(CommandResultStatus.Invalid, FlipCardError.MatchRequired)]
        [TestCase(CommandResultStatus.Rejected, FlipCardError.ObjectNotFound)]
        [TestCase(CommandResultStatus.Conflict, FlipCardError.RevisionConflict)]
        public void Failure_StoresSuppliedStatusErrorAndRevisionMinusOne(
            CommandResultStatus status,
            FlipCardError error)
        {
            FlipCardResult result = FlipCardResult.Failure(status, error);

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
                () => FlipCardResult.Failure(CommandResultStatus.Accepted, FlipCardError.ObjectNotFound),
                Throws.ArgumentException);
        }

        [Test]
        public void Failure_WhenErrorIsNone_ThrowsArgumentException()
        {
            Assert.That(
                () => FlipCardResult.Failure(CommandResultStatus.Rejected, FlipCardError.None),
                Throws.ArgumentException);
        }

        [Test]
        public void EqualityHashCodeAndOperators_WhenValuesMatch_AreConsistent()
        {
            FlipCardResult first = FlipCardResult.Failure(CommandResultStatus.Conflict, FlipCardError.RevisionConflict);
            FlipCardResult second = FlipCardResult.Failure(CommandResultStatus.Conflict, FlipCardError.RevisionConflict);

            Assert.That(first.Equals(second), Is.True);
            Assert.That(first.Equals((object)second), Is.True);
            Assert.That(first == second, Is.True);
            Assert.That(first != second, Is.False);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        }

        [Test]
        public void EqualityHashCodeAndOperators_WhenValuesDiffer_AreConsistent()
        {
            FlipCardResult result = FlipCardResult.Failure(CommandResultStatus.Conflict, FlipCardError.RevisionConflict);
            FlipCardResult differentStatus = FlipCardResult.Failure(CommandResultStatus.Rejected, FlipCardError.RevisionConflict);
            FlipCardResult differentError = FlipCardResult.Failure(CommandResultStatus.Conflict, FlipCardError.RevisionOverflow);

            Assert.That(result != differentStatus, Is.True);
            Assert.That(result != differentError, Is.True);
        }

        [Test]
        public void ToString_ContainsUsefulStatusErrorAndRevisionInformation()
        {
            FlipCardResult result = FlipCardResult.Failure(CommandResultStatus.Rejected, FlipCardError.ObjectUserLocked);

            string text = result.ToString();

            Assert.That(text, Does.Contain(CommandResultStatus.Rejected.ToString()));
            Assert.That(text, Does.Contain(FlipCardError.ObjectUserLocked.ToString()));
            Assert.That(text, Does.Contain("-1"));
        }
    }
}
