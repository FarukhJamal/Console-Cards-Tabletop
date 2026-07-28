using System;
using ConsoleCards.Application.Results;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.Application
{
    public sealed class MergeStacksResultTests
    {
        [Test]
        public void Accepted_ReturnsAcceptedResult()
        {
            MergeStacksResult result = MergeStacksResult.Accepted(12);

            Assert.That(result.CommandResult, Is.EqualTo(CommandResult.Accepted(12)));
            Assert.That(result.Status, Is.EqualTo(CommandResultStatus.Accepted));
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Revision, Is.EqualTo(12));
            Assert.That(result.Error, Is.EqualTo(MergeStacksError.None));
        }

        [TestCase(CommandResultStatus.Invalid, MergeStacksError.MatchMissing)]
        [TestCase(CommandResultStatus.Conflict, MergeStacksError.RevisionConflict)]
        [TestCase(CommandResultStatus.Rejected, MergeStacksError.SourceStackEmpty)]
        public void Failure_ReturnsFailureResult(CommandResultStatus status, MergeStacksError error)
        {
            MergeStacksResult result = MergeStacksResult.Failure(status, error);

            Assert.That(result.CommandResult, Is.EqualTo(CommandResult.Failure(status)));
            Assert.That(result.Status, Is.EqualTo(status));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Revision, Is.EqualTo(-1));
            Assert.That(result.Error, Is.EqualTo(error));
        }

        [Test]
        public void Failure_WhenStatusIsAccepted_ThrowsArgumentException()
        {
            Assert.That(
                () => MergeStacksResult.Failure(
                    CommandResultStatus.Accepted,
                    MergeStacksError.SourceStackMissing),
                Throws.ArgumentException);
        }

        [Test]
        public void Failure_WhenErrorIsNone_ThrowsArgumentException()
        {
            Assert.That(
                () => MergeStacksResult.Failure(CommandResultStatus.Rejected, MergeStacksError.None),
                Throws.ArgumentException);
        }

        [Test]
        public void Equality_UsesCommandResultAndError()
        {
            MergeStacksResult left = MergeStacksResult.Failure(
                CommandResultStatus.Rejected,
                MergeStacksError.DestinationCapacityExceeded);
            MergeStacksResult right = MergeStacksResult.Failure(
                CommandResultStatus.Rejected,
                MergeStacksError.DestinationCapacityExceeded);
            MergeStacksResult different = MergeStacksResult.Failure(
                CommandResultStatus.Rejected,
                MergeStacksError.ObjectMissing);

            Assert.That(left, Is.EqualTo(right));
            Assert.That(left == right, Is.True);
            Assert.That(left.Equals((object)right), Is.True);
            Assert.That(left, Is.Not.EqualTo(different));
            Assert.That(left != different, Is.True);
        }

        [Test]
        public void GetHashCode_WhenValuesMatch_IsConsistent()
        {
            MergeStacksResult left = MergeStacksResult.Accepted(3);
            MergeStacksResult right = MergeStacksResult.Accepted(3);

            Assert.That(left.GetHashCode(), Is.EqualTo(right.GetHashCode()));
        }

        [Test]
        public void ToString_ContainsDiagnosticContent()
        {
            MergeStacksResult result = MergeStacksResult.Failure(
                CommandResultStatus.Rejected,
                MergeStacksError.SourceContainerRemovalFailed);

            Assert.That(result.ToString(), Does.Contain(nameof(CommandResult)));
            Assert.That(result.ToString(), Does.Contain(nameof(MergeStacksError.SourceContainerRemovalFailed)));
        }
    }
}
