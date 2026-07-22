using System;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Presentation.Camera;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.Presentation
{
    public sealed class TabletopCameraStateTests
    {
        private const double Tolerance = 0.0001;

        [Test]
        public void Constructor_StoresValidValues()
        {
            TableCoordinate focus = new TableCoordinate(2.5, -4.25);

            TabletopCameraState state = new TabletopCameraState(focus, 6f, 2f, 10f);

            Assert.That(state.FocusCoordinate, Is.EqualTo(focus));
            Assert.That(state.OrthographicSize, Is.EqualTo(6f));
            Assert.That(state.MinimumOrthographicSize, Is.EqualTo(2f));
            Assert.That(state.MaximumOrthographicSize, Is.EqualTo(10f));
        }

        [Test]
        public void Constructor_WhenInitialSizeIsBelowMinimum_ClampsToMinimum()
        {
            TabletopCameraState state = new TabletopCameraState(TableCoordinate.Zero, 1f, 2f, 10f);

            Assert.That(state.OrthographicSize, Is.EqualTo(2f));
        }

        [Test]
        public void Constructor_WhenInitialSizeIsAboveMaximum_ClampsToMaximum()
        {
            TabletopCameraState state = new TabletopCameraState(TableCoordinate.Zero, 12f, 2f, 10f);

            Assert.That(state.OrthographicSize, Is.EqualTo(10f));
        }

        [TestCase(0f, 10f)]
        [TestCase(-1f, 10f)]
        [TestCase(float.NaN, 10f)]
        [TestCase(float.PositiveInfinity, 10f)]
        [TestCase(2f, 1f)]
        [TestCase(2f, float.NaN)]
        [TestCase(2f, float.NegativeInfinity)]
        public void Constructor_WhenMinimumOrMaximumSizeIsInvalid_ThrowsArgumentOutOfRangeException(
            float minimumOrthographicSize,
            float maximumOrthographicSize)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new TabletopCameraState(
                    TableCoordinate.Zero,
                    5f,
                    minimumOrthographicSize,
                    maximumOrthographicSize));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void Constructor_WhenOrthographicSizeIsNonFinite_ThrowsArgumentOutOfRangeException(float orthographicSize)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new TabletopCameraState(TableCoordinate.Zero, orthographicSize, 2f, 10f));
        }

        [TestCase(double.NaN, 0.0)]
        [TestCase(double.PositiveInfinity, 0.0)]
        [TestCase(0.0, double.NaN)]
        [TestCase(0.0, double.NegativeInfinity)]
        public void Constructor_WhenFocusCoordinateIsNonFinite_ThrowsArgumentOutOfRangeException(double x, double y)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new TabletopCameraState(new TableCoordinate(x, y), 5f, 2f, 10f));
        }

        [Test]
        public void Pan_UpdatesFocus()
        {
            TabletopCameraState state = new TabletopCameraState(new TableCoordinate(2.0, -3.0), 5f, 2f, 10f);

            state.Pan(1.5, -2.25);

            AssertCoordinate(state.FocusCoordinate, 3.5, -5.25);
        }

        [TestCase(double.NaN, 0.0)]
        [TestCase(double.PositiveInfinity, 0.0)]
        [TestCase(double.NegativeInfinity, 0.0)]
        [TestCase(0.0, double.NaN)]
        [TestCase(0.0, double.PositiveInfinity)]
        [TestCase(0.0, double.NegativeInfinity)]
        public void Pan_WhenDeltaIsNonFinite_ThrowsArgumentOutOfRangeException(double deltaX, double deltaY)
        {
            TabletopCameraState state = new TabletopCameraState(TableCoordinate.Zero, 5f, 2f, 10f);

            Assert.Throws<ArgumentOutOfRangeException>(() => state.Pan(deltaX, deltaY));
        }

        [Test]
        public void Pan_WhenResultOverflows_ThrowsOverflowExceptionWithoutCorruptingPreviousFocus()
        {
            TableCoordinate originalFocus = new TableCoordinate(double.MaxValue, 1.0);
            TabletopCameraState state = new TabletopCameraState(originalFocus, 5f, 2f, 10f);

            Assert.Throws<OverflowException>(() => state.Pan(double.MaxValue, 0.0));

            Assert.That(state.FocusCoordinate, Is.EqualTo(originalFocus));
        }

        [Test]
        public void SetFocus_UpdatesFocus()
        {
            TabletopCameraState state = new TabletopCameraState(TableCoordinate.Zero, 5f, 2f, 10f);
            TableCoordinate focus = new TableCoordinate(-6.0, 7.5);

            state.SetFocus(focus);

            Assert.That(state.FocusCoordinate, Is.EqualTo(focus));
        }

        [Test]
        public void Zoom_UpdatesAndClamps()
        {
            TabletopCameraState state = new TabletopCameraState(TableCoordinate.Zero, 5f, 2f, 10f);

            state.Zoom(3f);
            Assert.That(state.OrthographicSize, Is.EqualTo(8f));

            state.Zoom(100f);
            Assert.That(state.OrthographicSize, Is.EqualTo(10f));

            state.Zoom(-100f);
            Assert.That(state.OrthographicSize, Is.EqualTo(2f));
        }

        [Test]
        public void SetOrthographicSize_Clamps()
        {
            TabletopCameraState state = new TabletopCameraState(TableCoordinate.Zero, 5f, 2f, 10f);

            state.SetOrthographicSize(12f);
            Assert.That(state.OrthographicSize, Is.EqualTo(10f));

            state.SetOrthographicSize(1f);
            Assert.That(state.OrthographicSize, Is.EqualTo(2f));
        }

        [Test]
        public void FocusWithSize_UpdatesFocusAndOrthographicSize()
        {
            TabletopCameraState state = new TabletopCameraState(TableCoordinate.Zero, 5f, 2f, 10f);
            TableCoordinate focus = new TableCoordinate(9.0, -2.0);

            state.SetFocus(focus, 12f);

            Assert.That(state.FocusCoordinate, Is.EqualTo(focus));
            Assert.That(state.OrthographicSize, Is.EqualTo(10f));
        }

        private static void AssertCoordinate(TableCoordinate actual, double expectedX, double expectedY)
        {
            Assert.That(actual.X, Is.EqualTo(expectedX).Within(Tolerance));
            Assert.That(actual.Y, Is.EqualTo(expectedY).Within(Tolerance));
        }
    }
}
