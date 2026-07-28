using System;
using ConsoleCards.Application.Results;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.Application
{
    public sealed class ReorderContainerResultTests
    {
        [Test]
        public void Accepted_ReturnsAcceptedResult()
        {
            ReorderContainerResult result = ReorderContainerResult.Accepted(12);

            Assert.That(result.CommandResult, Is.EqualTo(CommandResult.Accepted(12)));
            Assert.That(result.Status, Is.EqualTo(CommandResultStatus.Accepted));
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Revision, Is.EqualTo(12));
            Assert.That(result.Error, Is.EqualTo(ReorderContainerError.None));
        }

        [TestCase(CommandResultStatus.Invalid, ReorderContainerError.MatchMissing)]
        [TestCase(CommandResultStatus.Conflict, ReorderContainerError.RevisionConflict)]
        [TestCase(CommandResultStatus.Rejected, ReorderContainerError.ObjectIndexMismatch)]
        public void Failure_ReturnsFailureResult(CommandResultStatus status, ReorderContainerError error)
        {
            ReorderContainerResult result = ReorderContainerResult.Failure(status, error);

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
                () => ReorderContainerResult.Failure(
                    CommandResultStatus.Accepted,
                    ReorderContainerError.ContainerMissing),
                Throws.ArgumentException);
        }

        [Test]
        public void Failure_WhenErrorIsNone_ThrowsArgumentException()
        {
            Assert.That(
                () => ReorderContainerResult.Failure(
                    CommandResultStatus.Rejected,
                    ReorderContainerError.None),
                Throws.ArgumentException);
        }

        [Test]
        public void Equality_UsesCommandResultAndError()
        {
            ReorderContainerResult left = ReorderContainerResult.Failure(
                CommandResultStatus.Rejected,
                ReorderContainerError.ContainerMissing);
            ReorderContainerResult right = ReorderContainerResult.Failure(
                CommandResultStatus.Rejected,
                ReorderContainerError.ContainerMissing);
            ReorderContainerResult different = ReorderContainerResult.Failure(
                CommandResultStatus.Rejected,
                ReorderContainerError.ObjectMissing);

            Assert.That(left, Is.EqualTo(right));
            Assert.That(left == right, Is.True);
            Assert.That(left.Equals((object)right), Is.True);
            Assert.That(left, Is.Not.EqualTo(different));
            Assert.That(left != different, Is.True);
        }

        [Test]
        public void GetHashCode_WhenValuesMatch_IsConsistent()
        {
            ReorderContainerResult left = ReorderContainerResult.Accepted(3);
            ReorderContainerResult right = ReorderContainerResult.Accepted(3);

            Assert.That(left.GetHashCode(), Is.EqualTo(right.GetHashCode()));
        }

        [Test]
        public void ToString_ContainsDiagnosticContent()
        {
            ReorderContainerResult result = ReorderContainerResult.Failure(
                CommandResultStatus.Rejected,
                ReorderContainerError.ObjectMembershipMissing);

            Assert.That(result.ToString(), Does.Contain(nameof(CommandResult)));
            Assert.That(result.ToString(), Does.Contain(nameof(ReorderContainerError.ObjectMembershipMissing)));
        }
    }
}
