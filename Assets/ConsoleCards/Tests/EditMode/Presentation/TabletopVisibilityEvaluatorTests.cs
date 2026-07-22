using System;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Presentation.Camera;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.Presentation
{
    public sealed class TabletopVisibilityEvaluatorTests
    {
        private const double Tolerance = 0.0001;

        [Test]
        public void ViewBoundsConstructor_StoresValues()
        {
            TabletopCameraViewBounds bounds = new TabletopCameraViewBounds(-2.0, 3.0, -4.0, 5.0);

            Assert.That(bounds.MinimumX, Is.EqualTo(-2.0));
            Assert.That(bounds.MaximumX, Is.EqualTo(3.0));
            Assert.That(bounds.MinimumY, Is.EqualTo(-4.0));
            Assert.That(bounds.MaximumY, Is.EqualTo(5.0));
        }

        [Test]
        public void ViewBounds_WidthHeightAndCenter_AreCorrect()
        {
            TabletopCameraViewBounds bounds = new TabletopCameraViewBounds(-2.0, 4.0, -5.0, 9.0);

            Assert.That(bounds.Width, Is.EqualTo(6.0).Within(Tolerance));
            Assert.That(bounds.Height, Is.EqualTo(14.0).Within(Tolerance));
            AssertCoordinate(bounds.Center, 1.0, 2.0);
        }

        [TestCase(double.NaN, 1.0, -1.0, 1.0)]
        [TestCase(double.PositiveInfinity, 1.0, -1.0, 1.0)]
        [TestCase(double.NegativeInfinity, 1.0, -1.0, 1.0)]
        [TestCase(-1.0, double.NaN, -1.0, 1.0)]
        [TestCase(-1.0, double.PositiveInfinity, -1.0, 1.0)]
        [TestCase(-1.0, double.NegativeInfinity, -1.0, 1.0)]
        [TestCase(-1.0, 1.0, double.NaN, 1.0)]
        [TestCase(-1.0, 1.0, double.PositiveInfinity, 1.0)]
        [TestCase(-1.0, 1.0, double.NegativeInfinity, 1.0)]
        [TestCase(-1.0, 1.0, -1.0, double.NaN)]
        [TestCase(-1.0, 1.0, -1.0, double.PositiveInfinity)]
        [TestCase(-1.0, 1.0, -1.0, double.NegativeInfinity)]
        public void ViewBoundsConstructor_WhenValueIsNonFinite_ThrowsArgumentOutOfRangeException(
            double minimumX,
            double maximumX,
            double minimumY,
            double maximumY)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new TabletopCameraViewBounds(minimumX, maximumX, minimumY, maximumY));
        }

        [Test]
        public void ViewBoundsConstructor_WhenXRangeIsReversed_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new TabletopCameraViewBounds(2.0, 1.0, -1.0, 1.0));
        }

        [Test]
        public void ViewBoundsConstructor_WhenYRangeIsReversed_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new TabletopCameraViewBounds(-1.0, 1.0, 2.0, 1.0));
        }

        [Test]
        public void Contains_WhenCoordinateIsCenter_ReturnsTrue()
        {
            TabletopCameraViewBounds bounds = CreateDefaultBounds();

            Assert.That(bounds.Contains(new TableCoordinate(0.0, 0.0)), Is.True);
        }

        [TestCase(-5.0, 0.0)]
        [TestCase(5.0, 0.0)]
        [TestCase(0.0, -3.0)]
        [TestCase(0.0, 3.0)]
        public void Contains_WhenCoordinateIsOnBoundary_ReturnsTrue(double x, double y)
        {
            TabletopCameraViewBounds bounds = CreateDefaultBounds();

            Assert.That(bounds.Contains(new TableCoordinate(x, y)), Is.True);
        }

        [TestCase(-5.1, 0.0)]
        [TestCase(5.1, 0.0)]
        [TestCase(0.0, -3.1)]
        [TestCase(0.0, 3.1)]
        public void Contains_WhenPointIsBeyondBoundary_ReturnsFalse(double x, double y)
        {
            TabletopCameraViewBounds bounds = CreateDefaultBounds();

            Assert.That(bounds.Contains(new TableCoordinate(x, y)), Is.False);
        }

        [TestCase(double.NaN, 0.0)]
        [TestCase(double.PositiveInfinity, 0.0)]
        [TestCase(double.NegativeInfinity, 0.0)]
        [TestCase(0.0, double.NaN)]
        [TestCase(0.0, double.PositiveInfinity)]
        [TestCase(0.0, double.NegativeInfinity)]
        public void Contains_WhenCoordinateIsNonFinite_ThrowsArgumentOutOfRangeException(double x, double y)
        {
            TabletopCameraViewBounds bounds = CreateDefaultBounds();

            Assert.Throws<ArgumentOutOfRangeException>(() => bounds.Contains(new TableCoordinate(x, y)));
        }

        [Test]
        public void Intersects_WhenObjectBoundsAreFullyContained_ReturnsTrue()
        {
            TabletopCameraViewBounds bounds = CreateDefaultBounds();

            Assert.That(bounds.Intersects(new TableCoordinate(0.0, 0.0), 1.0, 1.0), Is.True);
        }

        [TestCase(-5.5, 0.0, 1.0, 1.0)]
        [TestCase(5.5, 0.0, 1.0, 1.0)]
        [TestCase(0.0, -3.5, 1.0, 1.0)]
        [TestCase(0.0, 3.5, 1.0, 1.0)]
        public void Intersects_WhenObjectBoundsPartiallyOverlap_ReturnsTrue(
            double x,
            double y,
            double halfWidth,
            double halfHeight)
        {
            TabletopCameraViewBounds bounds = CreateDefaultBounds();

            Assert.That(bounds.Intersects(new TableCoordinate(x, y), halfWidth, halfHeight), Is.True);
        }

        [TestCase(-6.0, 0.0, 1.0, 1.0)]
        [TestCase(6.0, 0.0, 1.0, 1.0)]
        [TestCase(0.0, -4.0, 1.0, 1.0)]
        [TestCase(0.0, 4.0, 1.0, 1.0)]
        public void Intersects_WhenObjectBoundsTouchEdge_ReturnsTrue(
            double x,
            double y,
            double halfWidth,
            double halfHeight)
        {
            TabletopCameraViewBounds bounds = CreateDefaultBounds();

            Assert.That(bounds.Intersects(new TableCoordinate(x, y), halfWidth, halfHeight), Is.True);
        }

        [TestCase(-6.1, 0.0, 1.0, 1.0)]
        [TestCase(6.1, 0.0, 1.0, 1.0)]
        [TestCase(0.0, -4.1, 1.0, 1.0)]
        [TestCase(0.0, 4.1, 1.0, 1.0)]
        public void Intersects_WhenObjectBoundsAreSeparated_ReturnsFalse(
            double x,
            double y,
            double halfWidth,
            double halfHeight)
        {
            TabletopCameraViewBounds bounds = CreateDefaultBounds();

            Assert.That(bounds.Intersects(new TableCoordinate(x, y), halfWidth, halfHeight), Is.False);
        }

        [Test]
        public void Intersects_WhenObjectBoundHasZeroSize_ReturnsTrue()
        {
            TabletopCameraViewBounds bounds = CreateDefaultBounds();

            Assert.That(bounds.Intersects(new TableCoordinate(1.0, 1.0), 0.0, 0.0), Is.True);
        }

        [Test]
        public void Intersects_WhenHalfWidthIsNegative_ThrowsArgumentOutOfRangeException()
        {
            TabletopCameraViewBounds bounds = CreateDefaultBounds();

            Assert.Throws<ArgumentOutOfRangeException>(
                () => bounds.Intersects(TableCoordinate.Zero, -0.1, 1.0));
        }

        [Test]
        public void Intersects_WhenHalfHeightIsNegative_ThrowsArgumentOutOfRangeException()
        {
            TabletopCameraViewBounds bounds = CreateDefaultBounds();

            Assert.Throws<ArgumentOutOfRangeException>(
                () => bounds.Intersects(TableCoordinate.Zero, 1.0, -0.1));
        }

        [TestCase(double.NaN, 0.0, 1.0, 1.0)]
        [TestCase(double.PositiveInfinity, 0.0, 1.0, 1.0)]
        [TestCase(double.NegativeInfinity, 0.0, 1.0, 1.0)]
        [TestCase(0.0, double.NaN, 1.0, 1.0)]
        [TestCase(0.0, double.PositiveInfinity, 1.0, 1.0)]
        [TestCase(0.0, double.NegativeInfinity, 1.0, 1.0)]
        [TestCase(0.0, 0.0, double.NaN, 1.0)]
        [TestCase(0.0, 0.0, double.PositiveInfinity, 1.0)]
        [TestCase(0.0, 0.0, double.NegativeInfinity, 1.0)]
        [TestCase(0.0, 0.0, 1.0, double.NaN)]
        [TestCase(0.0, 0.0, 1.0, double.PositiveInfinity)]
        [TestCase(0.0, 0.0, 1.0, double.NegativeInfinity)]
        public void Intersects_WhenCenterOrExtentsAreNonFinite_ThrowsArgumentOutOfRangeException(
            double x,
            double y,
            double halfWidth,
            double halfHeight)
        {
            TabletopCameraViewBounds bounds = CreateDefaultBounds();

            Assert.Throws<ArgumentOutOfRangeException>(
                () => bounds.Intersects(new TableCoordinate(x, y), halfWidth, halfHeight));
        }

        [Test]
        public void Intersects_WhenCalculatedObjectBoundaryOverflows_ThrowsOverflowException()
        {
            TabletopCameraViewBounds bounds = CreateDefaultBounds();

            Assert.Throws<OverflowException>(
                () => bounds.Intersects(new TableCoordinate(double.MaxValue, 0.0), double.MaxValue, 1.0));
        }

        [Test]
        public void ViewBounds_EqualityAndToString_BehaveCorrectly()
        {
            TabletopCameraViewBounds first = new TabletopCameraViewBounds(-1.0, 2.0, -3.0, 4.0);
            TabletopCameraViewBounds second = new TabletopCameraViewBounds(-1.0, 2.0, -3.0, 4.0);
            TabletopCameraViewBounds different = new TabletopCameraViewBounds(-1.0, 3.0, -3.0, 4.0);

            Assert.That(first.Equals(second), Is.True);
            Assert.That(first.Equals((object)second), Is.True);
            Assert.That(first == second, Is.True);
            Assert.That(first != second, Is.False);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
            Assert.That(first.Equals(different), Is.False);
            Assert.That(first == different, Is.False);
            Assert.That(first != different, Is.True);
            Assert.That(first.ToString(), Does.Contain("-1"));
            Assert.That(first.ToString(), Does.Contain("4"));
        }

        [Test]
        public void EvaluatorConstructor_StoresConfiguration()
        {
            TabletopVisibilityEvaluator evaluator = new TabletopVisibilityEvaluator(2.5f, 1.25);

            Assert.That(evaluator.WorldUnitsPerTableUnit, Is.EqualTo(2.5f));
            Assert.That(evaluator.CullingMarginTableUnits, Is.EqualTo(1.25).Within(Tolerance));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void EvaluatorConstructor_WhenWorldScaleIsInvalid_ThrowsArgumentOutOfRangeException(float worldUnitsPerTableUnit)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new TabletopVisibilityEvaluator(worldUnitsPerTableUnit, 0.0));
        }

        [TestCase(-0.1)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void EvaluatorConstructor_WhenMarginIsInvalid_ThrowsArgumentOutOfRangeException(double cullingMarginTableUnits)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new TabletopVisibilityEvaluator(1f, cullingMarginTableUnits));
        }

        [Test]
        public void CalculateViewBounds_WhenCameraStateIsNull_ThrowsArgumentNullException()
        {
            TabletopVisibilityEvaluator evaluator = new TabletopVisibilityEvaluator(1f, 0.0);

            Assert.Throws<ArgumentNullException>(() => evaluator.CalculateViewBounds(null, 1f));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void CalculateViewBounds_WhenAspectIsInvalid_ThrowsArgumentOutOfRangeException(float cameraAspect)
        {
            TabletopVisibilityEvaluator evaluator = new TabletopVisibilityEvaluator(1f, 0.0);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => evaluator.CalculateViewBounds(CreateState(TableCoordinate.Zero, 5f), cameraAspect));
        }

        [Test]
        public void CalculateViewBounds_WhenAspectIsOne_CreatesSquareBounds()
        {
            TabletopVisibilityEvaluator evaluator = new TabletopVisibilityEvaluator(1f, 0.0);

            TabletopCameraViewBounds bounds = evaluator.CalculateViewBounds(CreateState(TableCoordinate.Zero, 5f), 1f);

            AssertBounds(bounds, -5.0, 5.0, -5.0, 5.0);
        }

        [Test]
        public void CalculateViewBounds_WhenAspectIsSixteenByNine_CreatesWiderHorizontalBounds()
        {
            TabletopVisibilityEvaluator evaluator = new TabletopVisibilityEvaluator(1f, 0.0);

            TabletopCameraViewBounds bounds = evaluator.CalculateViewBounds(CreateState(TableCoordinate.Zero, 9f), 16f / 9f);

            AssertBounds(bounds, -16.0, 16.0, -9.0, 9.0);
        }

        [Test]
        public void CalculateViewBounds_OrthographicSizeDefinesVerticalHalfExtent()
        {
            TabletopVisibilityEvaluator evaluator = new TabletopVisibilityEvaluator(1f, 0.0);

            TabletopCameraViewBounds bounds = evaluator.CalculateViewBounds(CreateState(TableCoordinate.Zero, 7f), 1f);

            AssertBounds(bounds, -7.0, 7.0, -7.0, 7.0);
        }

        [Test]
        public void CalculateViewBounds_WorldScaleConvertsWorldExtentToLogicalExtent()
        {
            TabletopVisibilityEvaluator evaluator = new TabletopVisibilityEvaluator(2f, 0.0);

            TabletopCameraViewBounds bounds = evaluator.CalculateViewBounds(CreateState(TableCoordinate.Zero, 6f), 1f);

            AssertBounds(bounds, -3.0, 3.0, -3.0, 3.0);
        }

        [Test]
        public void CalculateViewBounds_CullingMarginExpandsEveryEdge()
        {
            TabletopVisibilityEvaluator evaluator = new TabletopVisibilityEvaluator(1f, 2.0);

            TabletopCameraViewBounds bounds = evaluator.CalculateViewBounds(CreateState(TableCoordinate.Zero, 5f), 2f);

            AssertBounds(bounds, -12.0, 12.0, -7.0, 7.0);
        }

        [Test]
        public void CalculateViewBounds_BoundsAreCenteredOnCameraFocus()
        {
            TabletopVisibilityEvaluator evaluator = new TabletopVisibilityEvaluator(1f, 0.0);

            TabletopCameraViewBounds bounds = evaluator.CalculateViewBounds(CreateState(new TableCoordinate(10.0, -20.0), 5f), 1f);

            AssertBounds(bounds, 5.0, 15.0, -25.0, -15.0);
        }

        [TestCase(10.0, 20.0, 5.0, 15.0, 15.0, 25.0)]
        [TestCase(-10.0, -20.0, -15.0, -5.0, -25.0, -15.0)]
        public void CalculateViewBounds_PositiveAndNegativeCameraFocusesAreHandled(
            double focusX,
            double focusY,
            double expectedMinimumX,
            double expectedMaximumX,
            double expectedMinimumY,
            double expectedMaximumY)
        {
            TabletopVisibilityEvaluator evaluator = new TabletopVisibilityEvaluator(1f, 0.0);

            TabletopCameraViewBounds bounds = evaluator.CalculateViewBounds(
                CreateState(new TableCoordinate(focusX, focusY), 5f),
                1f);

            AssertBounds(bounds, expectedMinimumX, expectedMaximumX, expectedMinimumY, expectedMaximumY);
        }

        [TestCase(0.0, 0.0, true)]
        [TestCase(6.0, 0.0, false)]
        [TestCase(0.0, -6.0, false)]
        public void IsPointVisible_ReturnsExpectedResult(double x, double y, bool expected)
        {
            TabletopVisibilityEvaluator evaluator = new TabletopVisibilityEvaluator(1f, 0.0);

            bool isVisible = evaluator.IsPointVisible(new TableCoordinate(x, y), CreateState(TableCoordinate.Zero, 5f), 1f);

            Assert.That(isVisible, Is.EqualTo(expected));
        }

        [TestCase(0.0, 0.0, 1.0, 1.0, true)]
        [TestCase(5.5, 0.0, 1.0, 1.0, true)]
        [TestCase(6.0, 0.0, 1.0, 1.0, true)]
        [TestCase(6.1, 0.0, 1.0, 1.0, false)]
        public void IsBoundsVisible_ReturnsExpectedResult(
            double x,
            double y,
            double halfWidth,
            double halfHeight,
            bool expected)
        {
            TabletopVisibilityEvaluator evaluator = new TabletopVisibilityEvaluator(1f, 0.0);

            bool isVisible = evaluator.IsBoundsVisible(
                new TableCoordinate(x, y),
                halfWidth,
                halfHeight,
                CreateState(TableCoordinate.Zero, 5f),
                1f);

            Assert.That(isVisible, Is.EqualTo(expected));
        }

        [Test]
        public void EvaluatorQueries_DoNotMutateCameraState()
        {
            TabletopVisibilityEvaluator evaluator = new TabletopVisibilityEvaluator(1f, 1.0);
            TableCoordinate focus = new TableCoordinate(2.0, 3.0);
            TabletopCameraState state = CreateState(focus, 5f);

            evaluator.CalculateViewBounds(state, 1f);
            evaluator.IsPointVisible(TableCoordinate.Zero, state, 1f);
            evaluator.IsBoundsVisible(TableCoordinate.Zero, 1.0, 1.0, state, 1f);

            Assert.That(state.FocusCoordinate, Is.EqualTo(focus));
            Assert.That(state.OrthographicSize, Is.EqualTo(5f));
        }

        [Test]
        public void CalculateViewBounds_WithLargeApprovedRangeCameraCoordinates_ProducesFiniteBounds()
        {
            TabletopVisibilityEvaluator evaluator = new TabletopVisibilityEvaluator(1f, 0.0);

            TabletopCameraViewBounds bounds = evaluator.CalculateViewBounds(
                CreateState(new TableCoordinate(100_000.0, -100_000.0), 20f),
                16f / 9f);

            Assert.That(IsFinite(bounds.MinimumX), Is.True);
            Assert.That(IsFinite(bounds.MaximumX), Is.True);
            Assert.That(IsFinite(bounds.MinimumY), Is.True);
            Assert.That(IsFinite(bounds.MaximumY), Is.True);
        }

        [Test]
        public void CalculateViewBounds_WhenCalculatedBoundsOverflow_ThrowsWithoutMutatingCameraState()
        {
            TabletopVisibilityEvaluator evaluator = new TabletopVisibilityEvaluator(1f, double.MaxValue);
            TableCoordinate focus = new TableCoordinate(double.MaxValue, 0.0);
            TabletopCameraState state = CreateState(focus, 5f);

            Assert.Throws<OverflowException>(() => evaluator.CalculateViewBounds(state, 1f));
            Assert.That(state.FocusCoordinate, Is.EqualTo(focus));
            Assert.That(state.OrthographicSize, Is.EqualTo(5f));
        }

        private static TabletopCameraViewBounds CreateDefaultBounds()
        {
            return new TabletopCameraViewBounds(-5.0, 5.0, -3.0, 3.0);
        }

        private static TabletopCameraState CreateState(TableCoordinate focusCoordinate, float orthographicSize)
        {
            return new TabletopCameraState(focusCoordinate, orthographicSize, 1f, 100f);
        }

        private static void AssertBounds(
            TabletopCameraViewBounds actual,
            double expectedMinimumX,
            double expectedMaximumX,
            double expectedMinimumY,
            double expectedMaximumY)
        {
            Assert.That(actual.MinimumX, Is.EqualTo(expectedMinimumX).Within(Tolerance));
            Assert.That(actual.MaximumX, Is.EqualTo(expectedMaximumX).Within(Tolerance));
            Assert.That(actual.MinimumY, Is.EqualTo(expectedMinimumY).Within(Tolerance));
            Assert.That(actual.MaximumY, Is.EqualTo(expectedMaximumY).Within(Tolerance));
        }

        private static void AssertCoordinate(TableCoordinate actual, double expectedX, double expectedY)
        {
            Assert.That(actual.X, Is.EqualTo(expectedX).Within(Tolerance));
            Assert.That(actual.Y, Is.EqualTo(expectedY).Within(Tolerance));
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
