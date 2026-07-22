using System;
using ConsoleCards.Core.Results;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.Core
{
    public sealed class ContainerTransferResultTests
    {
        [Test]
        public void Success_StoresDestinationIndexAndErrorNone()
        {
            ContainerTransferResult result = ContainerTransferResult.Success(3);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Error, Is.EqualTo(ContainerTransferError.None));
            Assert.That(result.DestinationIndex, Is.EqualTo(3));
        }

        [Test]
        public void Failure_StoresErrorAndIndexMinusOne()
        {
            ContainerTransferResult result = ContainerTransferResult.Failure(ContainerTransferError.DestinationFull);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(ContainerTransferError.DestinationFull));
            Assert.That(result.DestinationIndex, Is.EqualTo(-1));
        }

        [Test]
        public void Failure_WhenErrorIsNone_ThrowsArgumentException()
        {
            Assert.That(
                () => ContainerTransferResult.Failure(ContainerTransferError.None),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void Equality_BehavesCorrectly()
        {
            ContainerTransferResult first = ContainerTransferResult.Success(1);
            ContainerTransferResult equal = ContainerTransferResult.Success(1);
            ContainerTransferResult different = ContainerTransferResult.Failure(ContainerTransferError.InvalidDestinationIndex);

            Assert.That(first.Equals(equal), Is.True);
            Assert.That(first == equal, Is.True);
            Assert.That(first != different, Is.True);
            Assert.That(first.GetHashCode(), Is.EqualTo(equal.GetHashCode()));
        }
    }
}
