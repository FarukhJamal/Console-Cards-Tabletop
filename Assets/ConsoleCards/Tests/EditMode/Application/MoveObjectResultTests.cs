using System;
using ConsoleCards.Application.Results;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.Application
{
    public sealed class MoveObjectResultTests
    {
        [Test]
        public void Accepted_ReportsSuccessAcceptedStatusNoneErrorAndRevision()
        {
            MoveObjectResult result = MoveObjectResult.Accepted(7);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Status, Is.EqualTo(CommandResultStatus.Accepted));
            Assert.That(result.Error, Is.EqualTo(MoveObjectError.None));
            Assert.That(result.Revision, Is.EqualTo(7));
            Assert.That(result.CommandResult, Is.EqualTo(CommandResult.Accepted(7)));
        }

        [Test]
        public void Accepted_WhenRevisionIsNegative_ThrowsArgumentOutOfRangeException()
        {
            Assert.That(
                () => MoveObjectResult.Accepted(-1),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [TestCase(CommandResultStatus.Invalid, MoveObjectError.MatchRequired)]
        [TestCase(CommandResultStatus.Rejected, MoveObjectError.ObjectNotFound)]
        [TestCase(CommandResultStatus.Conflict, MoveObjectError.RevisionConflict)]
        public void Failure_ReportsSuppliedStatusErrorAndRevisionMinusOne(
            CommandResultStatus status,
            MoveObjectError error)
        {
            MoveObjectResult result = MoveObjectResult.Failure(status, error);

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
                () => MoveObjectResult.Failure(CommandResultStatus.Accepted, MoveObjectError.ObjectNotFound),
                Throws.ArgumentException);
        }

        [Test]
        public void Failure_WhenErrorIsNone_ThrowsArgumentException()
        {
            Assert.That(
                () => MoveObjectResult.Failure(CommandResultStatus.Rejected, MoveObjectError.None),
                Throws.ArgumentException);
        }

        [Test]
        public void EqualityHashCodeAndOperators_WhenValuesMatch_AreConsistent()
        {
            MoveObjectResult first = MoveObjectResult.Failure(CommandResultStatus.Conflict, MoveObjectError.RevisionConflict);
            MoveObjectResult second = MoveObjectResult.Failure(CommandResultStatus.Conflict, MoveObjectError.RevisionConflict);

            Assert.That(first.Equals(second), Is.True);
            Assert.That(first.Equals((object)second), Is.True);
            Assert.That(first == second, Is.True);
            Assert.That(first != second, Is.False);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        }

        [Test]
        public void EqualityHashCodeAndOperators_WhenValuesDiffer_AreConsistent()
        {
            MoveObjectResult result = MoveObjectResult.Failure(CommandResultStatus.Conflict, MoveObjectError.RevisionConflict);
            MoveObjectResult differentStatus = MoveObjectResult.Failure(CommandResultStatus.Rejected, MoveObjectError.RevisionConflict);
            MoveObjectResult differentError = MoveObjectResult.Failure(CommandResultStatus.Conflict, MoveObjectError.RevisionOverflow);

            Assert.That(result != differentStatus, Is.True);
            Assert.That(result != differentError, Is.True);
        }

        [Test]
        public void ToString_ContainsUsefulStatusErrorAndRevisionInformation()
        {
            MoveObjectResult result = MoveObjectResult.Failure(CommandResultStatus.Rejected, MoveObjectError.ObjectUserLocked);

            string text = result.ToString();

            Assert.That(text, Does.Contain(CommandResultStatus.Rejected.ToString()));
            Assert.That(text, Does.Contain(MoveObjectError.ObjectUserLocked.ToString()));
            Assert.That(text, Does.Contain("-1"));
        }
    }
}
