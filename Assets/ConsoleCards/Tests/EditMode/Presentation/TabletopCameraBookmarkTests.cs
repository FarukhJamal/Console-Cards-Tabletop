using System;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Presentation.Camera;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.Presentation
{
    public sealed class TabletopCameraBookmarkTests
    {
        [Test]
        public void Constructor_StoresAllValues()
        {
            TableCoordinate focus = new TableCoordinate(3.5, -2.25);

            TabletopCameraBookmark bookmark = new TabletopCameraBookmark("Overview", focus, 6.5f);

            Assert.That(bookmark.Name, Is.EqualTo("Overview"));
            Assert.That(bookmark.FocusCoordinate, Is.EqualTo(focus));
            Assert.That(bookmark.OrthographicSize, Is.EqualTo(6.5f));
        }

        [Test]
        public void Constructor_WhenNameIsNull_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(
                () => new TabletopCameraBookmark(null, TableCoordinate.Zero, 5f));
        }

        [Test]
        public void Constructor_WhenNameIsEmpty_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(
                () => new TabletopCameraBookmark(string.Empty, TableCoordinate.Zero, 5f));
        }

        [Test]
        public void Constructor_WhenNameIsWhitespace_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(
                () => new TabletopCameraBookmark("   ", TableCoordinate.Zero, 5f));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void Constructor_WhenCoordinateXIsNonFinite_ThrowsArgumentOutOfRangeException(double x)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new TabletopCameraBookmark("Overview", new TableCoordinate(x, 0.0), 5f));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void Constructor_WhenCoordinateYIsNonFinite_ThrowsArgumentOutOfRangeException(double y)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new TabletopCameraBookmark("Overview", new TableCoordinate(0.0, y), 5f));
        }

        [Test]
        public void Constructor_WhenOrthographicSizeIsZero_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new TabletopCameraBookmark("Overview", TableCoordinate.Zero, 0f));
        }

        [Test]
        public void Constructor_WhenOrthographicSizeIsNegative_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new TabletopCameraBookmark("Overview", TableCoordinate.Zero, -1f));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void Constructor_WhenOrthographicSizeIsNonFinite_ThrowsArgumentOutOfRangeException(float orthographicSize)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new TabletopCameraBookmark("Overview", TableCoordinate.Zero, orthographicSize));
        }

        [Test]
        public void Equals_WhenBookmarksHaveSameValues_ComparesEqual()
        {
            TableCoordinate focus = new TableCoordinate(1.0, 2.0);
            TabletopCameraBookmark first = new TabletopCameraBookmark("Overview", focus, 5f);
            TabletopCameraBookmark second = new TabletopCameraBookmark("Overview", focus, 5f);

            Assert.That(first.Equals(second), Is.True);
            Assert.That(first.Equals((object)second), Is.True);
            Assert.That(first == second, Is.True);
            Assert.That(first != second, Is.False);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        }

        [TestCase("Detail", 1.0, 2.0, 5f)]
        [TestCase("Overview", 3.0, 2.0, 5f)]
        [TestCase("Overview", 1.0, 4.0, 5f)]
        [TestCase("Overview", 1.0, 2.0, 6f)]
        public void Equals_WhenNameCoordinateOrZoomDiffers_ComparesUnequal(
            string name,
            double x,
            double y,
            float orthographicSize)
        {
            TabletopCameraBookmark baseline = new TabletopCameraBookmark(
                "Overview",
                new TableCoordinate(1.0, 2.0),
                5f);
            TabletopCameraBookmark other = new TabletopCameraBookmark(
                name,
                new TableCoordinate(x, y),
                orthographicSize);

            Assert.That(baseline.Equals(other), Is.False);
            Assert.That(baseline.Equals((object)other), Is.False);
            Assert.That(baseline == other, Is.False);
            Assert.That(baseline != other, Is.True);
        }

        [Test]
        public void ToString_ContainsUsefulBookmarkInformation()
        {
            TabletopCameraBookmark bookmark = new TabletopCameraBookmark(
                "Overview",
                new TableCoordinate(1.0, -2.0),
                6f);

            string text = bookmark.ToString();

            Assert.That(text, Does.Contain("Overview"));
            Assert.That(text, Does.Contain("1"));
            Assert.That(text, Does.Contain("-2"));
            Assert.That(text, Does.Contain("6"));
        }
    }
}
