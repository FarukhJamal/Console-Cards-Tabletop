using System;
using ConsoleCards.Application.Results;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.Application
{
    public sealed class RotateObjectResultTests
    {
        [Test]
        public void Accepted_ReportsSuccessAcceptedStatusNoneErrorAndRevision()
        {
            RotateObjectResult result = RotateObjectResult.Accepted(7);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Status, Is.EqualTo(CommandResultStatus.Accepted));
            Assert.That(result.Error, Is.EqualTo(RotateObjectError.None));
            Assert.That(result.Revision, Is.EqualTo(7));
            Assert.That(result.CommandResult, Is.EqualTo(CommandResult.Accepted(7)));
        }

        [Test]
        public void Accepted_WhenRevisionIsNegative_ThrowsArgumentOutOfRangeException()
        {
            Assert.That(
                () => RotateObjectResult.Accepted(-1),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [TestCase(CommandResultStatus.Invalid, RotateObjectError.MatchRequired)]
        [TestCase(CommandResultStatus.Rejected, RotateObjectError.ObjectNotFound)]
        [TestCase(CommandResultStatus.Conflict, RotateObjectError.RevisionConflict)]
        public void Failure_StoresSuppliedStatusErrorAndRevisionMinusOne(
            CommandResultStatus status,
            RotateObjectError error)
        {
            RotateObjectResult result = RotateObjectResult.Failure(status, error);

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
                () => RotateObjectResult.Failure(CommandResultStatus.Accepted, RotateObjectError.ObjectNotFound),
                Throws.ArgumentException);
        }

        [Test]
        public void Failure_WhenErrorIsNone_ThrowsArgumentException()
        {
            Assert.That(
                () => RotateObjectResult.Failure(CommandResultStatus.Rejected, RotateObjectError.None),
                Throws.ArgumentException);
        }

        [Test]
        public void EqualityHashCodeAndOperators_WhenValuesMatch_AreConsistent()
        {
            RotateObjectResult first = RotateObjectResult.Failure(CommandResultStatus.Conflict, RotateObjectError.RevisionConflict);
            RotateObjectResult second = RotateObjectResult.Failure(CommandResultStatus.Conflict, RotateObjectError.RevisionConflict);

            Assert.That(first.Equals(second), Is.True);
            Assert.That(first.Equals((object)second), Is.True);
            Assert.That(first == second, Is.True);
            Assert.That(first != second, Is.False);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        }

        [Test]
        public void EqualityHashCodeAndOperators_WhenValuesDiffer_AreConsistent()
        {
            RotateObjectResult result = RotateObjectResult.Failure(CommandResultStatus.Conflict, RotateObjectError.RevisionConflict);
            RotateObjectResult differentStatus = RotateObjectResult.Failure(CommandResultStatus.Rejected, RotateObjectError.RevisionConflict);
            RotateObjectResult differentError = RotateObjectResult.Failure(CommandResultStatus.Conflict, RotateObjectError.RevisionOverflow);

            Assert.That(result != differentStatus, Is.True);
            Assert.That(result != differentError, Is.True);
        }

        [Test]
        public void ToString_ContainsUsefulStatusErrorAndRevisionInformation()
        {
            RotateObjectResult result = RotateObjectResult.Failure(CommandResultStatus.Rejected, RotateObjectError.ObjectUserLocked);

            string text = result.ToString();

            Assert.That(text, Does.Contain(CommandResultStatus.Rejected.ToString()));
            Assert.That(text, Does.Contain(RotateObjectError.ObjectUserLocked.ToString()));
            Assert.That(text, Does.Contain("-1"));
        }
    }
}
