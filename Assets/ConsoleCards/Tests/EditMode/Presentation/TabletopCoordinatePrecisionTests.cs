using System;
using System.Globalization;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Presentation.Coordinates;
using NUnit.Framework;
using UnityEngine;

namespace ConsoleCards.Tests.EditMode.Presentation
{
    public sealed class TabletopCoordinatePrecisionTests
    {
        private const double ApprovedCoordinateLimit = 100_000.0;
        private const double GapSeparation = 0.10;
        private const double CardWidth = 1.0;
        private const double CardHeight = 1.4;
        private const double MaximumApprovedGapError = 0.01;
        private const double EquivalentPrecisionTolerance = 0.000001;

        private static readonly TabletopCoordinateConverter Converter =
            new TabletopCoordinateConverter(1f, 0f, 0f, 0f);

        [TestCase(-ApprovedCoordinateLimit, -ApprovedCoordinateLimit)]
        [TestCase(-ApprovedCoordinateLimit, ApprovedCoordinateLimit)]
        [TestCase(ApprovedCoordinateLimit, -ApprovedCoordinateLimit)]
        [TestCase(ApprovedCoordinateLimit, ApprovedCoordinateLimit)]
        public void ApprovedRangeCorners_ConvertToFiniteVector3Positions(double logicalX, double logicalY)
        {
            Vector3 position = Converter.ToWorldPosition(new TableCoordinate(logicalX, logicalY));

            Assert.That(IsFinite(position.x), Is.True);
            Assert.That(IsFinite(position.y), Is.True);
            Assert.That(IsFinite(position.z), Is.True);
        }

        [TestCase(0.0)]
        [TestCase(10_000.0)]
        [TestCase(-10_000.0)]
        [TestCase(ApprovedCoordinateLimit)]
        [TestCase(-ApprovedCoordinateLimit)]
        public void GapSeparation_OnWorldXWithinApprovedRange_RemainsDistinguishableWithinTolerance(double logicalBase)
        {
            PrecisionMeasurement measurement = Measure(MeasurementAxis.LogicalXToWorldX, logicalBase, GapSeparation);

            AssertApprovedRangeSeparation(measurement);
        }

        [TestCase(ApprovedCoordinateLimit)]
        [TestCase(-ApprovedCoordinateLimit)]
        public void GapSeparation_OnWorldZWithinApprovedRange_RemainsDistinguishableWithinTolerance(double logicalBase)
        {
            PrecisionMeasurement measurement = Measure(MeasurementAxis.LogicalYToWorldZ, logicalBase, GapSeparation);

            AssertApprovedRangeSeparation(measurement);
        }

        [TestCase(ApprovedCoordinateLimit)]
        [TestCase(-ApprovedCoordinateLimit)]
        public void CardWidth_OnWorldXAtApprovedRangeLimit_RemainsDistinguishableWithinTolerance(double logicalBase)
        {
            PrecisionMeasurement measurement = Measure(MeasurementAxis.LogicalXToWorldX, logicalBase, CardWidth);

            AssertApprovedRangeSeparation(measurement);
        }

        [TestCase(ApprovedCoordinateLimit)]
        [TestCase(-ApprovedCoordinateLimit)]
        public void CardHeight_OnWorldZAtApprovedRangeLimit_RemainsDistinguishableWithinTolerance(double logicalBase)
        {
            PrecisionMeasurement measurement = Measure(MeasurementAxis.LogicalYToWorldZ, logicalBase, CardHeight);

            AssertApprovedRangeSeparation(measurement);
        }

        [TestCase(10_000.0, GapSeparation)]
        [TestCase(ApprovedCoordinateLimit, GapSeparation)]
        [TestCase(-ApprovedCoordinateLimit, CardHeight)]
        public void EquivalentLogicalXAndYMeasurements_HaveEquivalentWorldXZPrecision(
            double logicalBase,
            double requestedSeparation)
        {
            PrecisionMeasurement xMeasurement = Measure(
                MeasurementAxis.LogicalXToWorldX,
                logicalBase,
                requestedSeparation);
            PrecisionMeasurement yMeasurement = Measure(
                MeasurementAxis.LogicalYToWorldZ,
                logicalBase,
                requestedSeparation);

            Assert.That(
                xMeasurement.ActualSeparation,
                Is.EqualTo(yMeasurement.ActualSeparation).Within(EquivalentPrecisionTolerance));
            Assert.That(
                xMeasurement.AbsoluteError,
                Is.EqualTo(yMeasurement.AbsoluteError).Within(EquivalentPrecisionTolerance));
        }

        [TestCase(GapSeparation)]
        [TestCase(CardWidth)]
        [TestCase(CardHeight)]
        public void PositiveAndNegativeMeasurements_AreSymmetricWithinFloatTolerance(double requestedSeparation)
        {
            PrecisionMeasurement positiveMeasurement = Measure(
                MeasurementAxis.LogicalXToWorldX,
                ApprovedCoordinateLimit,
                requestedSeparation);
            PrecisionMeasurement negativeMeasurement = Measure(
                MeasurementAxis.LogicalXToWorldX,
                -ApprovedCoordinateLimit,
                requestedSeparation);

            Assert.That(
                positiveMeasurement.ActualSeparation,
                Is.EqualTo(negativeMeasurement.ActualSeparation).Within(EquivalentPrecisionTolerance));
            Assert.That(
                positiveMeasurement.AbsoluteError,
                Is.EqualTo(negativeMeasurement.AbsoluteError).Within(EquivalentPrecisionTolerance));
        }

        [Test]
        public void KnownLimit_AtOneMillionGapSeparationExceedsApprovedTolerance()
        {
            PrecisionMeasurement measurement = Measure(MeasurementAxis.LogicalXToWorldX, 1_000_000.0, GapSeparation);

            Assert.That(measurement.ActualSeparation, Is.Not.EqualTo(0f));
            Assert.That(measurement.AbsoluteError, Is.GreaterThan(MaximumApprovedGapError));
        }

        [Test]
        public void KnownLimit_AtTwoMillionNinetySevenThousandOneHundredFiftyTwo_CharacterizesGapCollapse()
        {
            PrecisionMeasurement measurement = Measure(MeasurementAxis.LogicalXToWorldX, 2_097_152.0, GapSeparation);
            bool collapsed = measurement.ActualSeparation == 0f;

            TestContext.WriteLine("Collapsed: {0}", collapsed);
            Assert.That(IsFinite(measurement.ConvertedBase), Is.True);
            Assert.That(IsFinite(measurement.ConvertedOffset), Is.True);
            Assert.That(measurement.AbsoluteError, Is.GreaterThan(MaximumApprovedGapError));
        }

        private static PrecisionMeasurement Measure(
            MeasurementAxis axis,
            double logicalBase,
            double requestedSeparation)
        {
            TableCoordinate baseCoordinate = axis == MeasurementAxis.LogicalXToWorldX
                ? new TableCoordinate(logicalBase, 0.0)
                : new TableCoordinate(0.0, logicalBase);
            TableCoordinate offsetCoordinate = axis == MeasurementAxis.LogicalXToWorldX
                ? new TableCoordinate(logicalBase + requestedSeparation, 0.0)
                : new TableCoordinate(0.0, logicalBase + requestedSeparation);

            Vector3 basePosition = Converter.ToWorldPosition(baseCoordinate);
            Vector3 offsetPosition = Converter.ToWorldPosition(offsetCoordinate);
            float convertedBase = axis == MeasurementAxis.LogicalXToWorldX ? basePosition.x : basePosition.z;
            float convertedOffset = axis == MeasurementAxis.LogicalXToWorldX ? offsetPosition.x : offsetPosition.z;
            float actualSeparation = convertedOffset - convertedBase;
            double absoluteError = Math.Abs((double)actualSeparation - requestedSeparation);

            PrecisionMeasurement measurement = new PrecisionMeasurement(
                axis,
                logicalBase,
                requestedSeparation,
                convertedBase,
                convertedOffset,
                actualSeparation,
                absoluteError);

            WriteMeasurement(measurement);
            return measurement;
        }

        private static void AssertApprovedRangeSeparation(PrecisionMeasurement measurement)
        {
            Assert.That(measurement.ActualSeparation, Is.Not.EqualTo(0f));
            Assert.That(measurement.AbsoluteError, Is.LessThanOrEqualTo(MaximumApprovedGapError));
        }

        private static void WriteMeasurement(PrecisionMeasurement measurement)
        {
            TestContext.WriteLine(
                "{0}: logical base {1}, requested separation {2}, converted base {3}, converted offset {4}, actual represented separation {5}, absolute error {6}",
                measurement.Axis,
                Format(measurement.LogicalBase),
                Format(measurement.RequestedSeparation),
                Format(measurement.ConvertedBase),
                Format(measurement.ConvertedOffset),
                Format(measurement.ActualSeparation),
                Format(measurement.AbsoluteError));
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static string Format(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string Format(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private enum MeasurementAxis
        {
            LogicalXToWorldX,
            LogicalYToWorldZ
        }

        private readonly struct PrecisionMeasurement
        {
            public PrecisionMeasurement(
                MeasurementAxis axis,
                double logicalBase,
                double requestedSeparation,
                float convertedBase,
                float convertedOffset,
                float actualSeparation,
                double absoluteError)
            {
                Axis = axis;
                LogicalBase = logicalBase;
                RequestedSeparation = requestedSeparation;
                ConvertedBase = convertedBase;
                ConvertedOffset = convertedOffset;
                ActualSeparation = actualSeparation;
                AbsoluteError = absoluteError;
            }

            public MeasurementAxis Axis { get; }

            public double LogicalBase { get; }

            public double RequestedSeparation { get; }

            public float ConvertedBase { get; }

            public float ConvertedOffset { get; }

            public float ActualSeparation { get; }

            public double AbsoluteError { get; }
        }
    }
}
