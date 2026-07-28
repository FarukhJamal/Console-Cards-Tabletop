using System;
using ConsoleCards.Application.Results;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.Application
{
    public sealed class SplitStackResultTests
    {
        [Test]
        public void Accepted_ReturnsAcceptedResult()
        {
            SplitStackResult result = SplitStackResult.Accepted(12);

            Assert.That(result.CommandResult, Is.EqualTo(CommandResult.Accepted(12)));
            Assert.That(result.Status, Is.EqualTo(CommandResultStatus.Accepted));
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Revision, Is.EqualTo(12));
            Assert.That(result.Error, Is.EqualTo(SplitStackError.None));
        }

        [TestCase(CommandResultStatus.Invalid, SplitStackError.MatchMissing)]
        [TestCase(CommandResultStatus.Conflict, SplitStackError.RevisionConflict)]
        [TestCase(CommandResultStatus.Rejected, SplitStackError.SourceStackTooSmall)]
        public void Failure_ReturnsFailureResult(CommandResultStatus status, SplitStackError error)
        {
            SplitStackResult result = SplitStackResult.Failure(status, error);

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
                () => SplitStackResult.Failure(
                    CommandResultStatus.Accepted,
                    SplitStackError.SourceStackMissing),
                Throws.ArgumentException);
        }

        [Test]
        public void Failure_WhenErrorIsNone_ThrowsArgumentException()
        {
            Assert.That(
                () => SplitStackResult.Failure(CommandResultStatus.Rejected, SplitStackError.None),
                Throws.ArgumentException);
        }

        [Test]
        public void Equality_UsesCommandResultAndError()
        {
            SplitStackResult left = SplitStackResult.Failure(
                CommandResultStatus.Rejected,
                SplitStackError.InvalidSplitIndex);
            SplitStackResult right = SplitStackResult.Failure(
                CommandResultStatus.Rejected,
                SplitStackError.InvalidSplitIndex);
            SplitStackResult different = SplitStackResult.Failure(
                CommandResultStatus.Rejected,
                SplitStackError.ObjectMissing);

            Assert.That(left, Is.EqualTo(right));
            Assert.That(left == right, Is.True);
            Assert.That(left.Equals((object)right), Is.True);
            Assert.That(left, Is.Not.EqualTo(different));
            Assert.That(left != different, Is.True);
        }

        [Test]
        public void GetHashCode_WhenValuesMatch_IsConsistent()
        {
            SplitStackResult left = SplitStackResult.Accepted(3);
            SplitStackResult right = SplitStackResult.Accepted(3);

            Assert.That(left.GetHashCode(), Is.EqualTo(right.GetHashCode()));
        }

        [Test]
        public void ToString_ContainsDiagnosticContent()
        {
            SplitStackResult result = SplitStackResult.Failure(
                CommandResultStatus.Rejected,
                SplitStackError.NewStackCreationFailed);

            Assert.That(result.ToString(), Does.Contain(nameof(CommandResult)));
            Assert.That(result.ToString(), Does.Contain(nameof(SplitStackError.NewStackCreationFailed)));
        }
    }
}
