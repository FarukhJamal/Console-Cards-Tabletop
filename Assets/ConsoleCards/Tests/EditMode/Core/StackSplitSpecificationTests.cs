using System;
using ConsoleCards.Core.Domain.Containers;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.Core
{
    public sealed class StackSplitSpecificationTests
    {
        [Test]
        public void Constructor_WhenValid_StoresFirstMovedIndex()
        {
            StackSplitSpecification specification = new StackSplitSpecification(2);

            Assert.That(specification.FirstMovedIndex, Is.EqualTo(2));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Constructor_WhenFirstMovedIndexIsBelowOne_ThrowsArgumentOutOfRangeException(int firstMovedIndex)
        {
            Assert.That(
                () => new StackSplitSpecification(firstMovedIndex),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [TestCase(1, 2, 1)]
        [TestCase(1, 4, 3)]
        [TestCase(2, 4, 2)]
        [TestCase(3, 4, 1)]
        public void GetMovedCount_WhenValid_ReturnsSourceCountMinusFirstMovedIndex(
            int firstMovedIndex,
            int sourceCount,
            int expectedMovedCount)
        {
            StackSplitSpecification specification = new StackSplitSpecification(firstMovedIndex);

            int movedCount = specification.GetMovedCount(sourceCount);

            Assert.That(movedCount, Is.EqualTo(expectedMovedCount));
        }

        [TestCase(0)]
        [TestCase(1)]
        public void GetMovedCount_WhenSourceCountIsBelowTwo_ThrowsArgumentOutOfRangeException(int sourceCount)
        {
            StackSplitSpecification specification = new StackSplitSpecification(1);

            Assert.That(
                () => specification.GetMovedCount(sourceCount),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [TestCase(2, 2)]
        [TestCase(3, 2)]
        public void GetMovedCount_WhenFirstMovedIndexIsNotLessThanSourceCount_ThrowsArgumentOutOfRangeException(
            int firstMovedIndex,
            int sourceCount)
        {
            StackSplitSpecification specification = new StackSplitSpecification(firstMovedIndex);

            Assert.That(
                () => specification.GetMovedCount(sourceCount),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Equality_WhenValuesMatch_ReturnsTrueAndHashCodesMatch()
        {
            StackSplitSpecification first = new StackSplitSpecification(2);
            StackSplitSpecification second = new StackSplitSpecification(2);

            Assert.That(first.Equals(second), Is.True);
            Assert.That(first == second, Is.True);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        }

        [Test]
        public void Inequality_WhenValuesDiffer_ReturnsTrue()
        {
            StackSplitSpecification first = new StackSplitSpecification(2);
            StackSplitSpecification second = new StackSplitSpecification(3);

            Assert.That(first.Equals(second), Is.False);
            Assert.That(first != second, Is.True);
        }

        [Test]
        public void ToString_ContainsDiagnosticContent()
        {
            StackSplitSpecification specification = new StackSplitSpecification(2);

            string value = specification.ToString();

            Assert.That(value, Does.Contain("FirstMovedIndex"));
            Assert.That(value, Does.Contain("2"));
        }
    }
}
