using ConsoleCards.Core.Coordinates;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.Core
{
    public sealed class CoordinateTests
    {
        [Test]
        public void TableCoordinate_Constructor_StoresXAndY()
        {
            TableCoordinate coordinate = new TableCoordinate(12.5, -4.25);

            Assert.That(coordinate.X, Is.EqualTo(12.5));
            Assert.That(coordinate.Y, Is.EqualTo(-4.25));
        }

        [Test]
        public void TableCoordinate_WhenValuesMatch_ComparesEqual()
        {
            TableCoordinate first = new TableCoordinate(2.0, 3.5);
            TableCoordinate second = new TableCoordinate(2.0, 3.5);

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first == second, Is.True);
            Assert.That(first.Equals((object)second), Is.True);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        }

        [Test]
        public void TableCoordinate_Zero_ContainsZeroValues()
        {
            TableCoordinate coordinate = TableCoordinate.Zero;

            Assert.That(coordinate.X, Is.EqualTo(0));
            Assert.That(coordinate.Y, Is.EqualTo(0));
        }

        [Test]
        public void TabletopPose_Constructor_StoresAllValues()
        {
            TableCoordinate position = new TableCoordinate(5.5, 8.25);
            TabletopPose pose = new TabletopPose(position, 45.5f, 2, 7);

            Assert.That(pose.Position, Is.EqualTo(position));
            Assert.That(pose.RotationDegrees, Is.EqualTo(45.5f));
            Assert.That(pose.Layer, Is.EqualTo(2));
            Assert.That(pose.LocalOrder, Is.EqualTo(7));
        }

        [Test]
        public void TabletopPose_Default_ContainsApprovedDefaultValues()
        {
            TabletopPose pose = TabletopPose.Default;

            Assert.That(pose.Position, Is.EqualTo(TableCoordinate.Zero));
            Assert.That(pose.RotationDegrees, Is.EqualTo(0));
            Assert.That(pose.Layer, Is.EqualTo(0));
            Assert.That(pose.LocalOrder, Is.EqualTo(0));
        }

        [Test]
        public void TabletopPose_WhenValuesMatch_ComparesEqual()
        {
            TableCoordinate position = new TableCoordinate(1.0, 2.0);
            TabletopPose first = new TabletopPose(position, 90.0f, 3, 4);
            TabletopPose second = new TabletopPose(position, 90.0f, 3, 4);

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first == second, Is.True);
            Assert.That(first.Equals((object)second), Is.True);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        }

        [Test]
        public void TabletopPose_WhenPositionDiffers_ComparesNotEqual()
        {
            TabletopPose first = new TabletopPose(new TableCoordinate(1.0, 2.0), 90.0f, 3, 4);
            TabletopPose second = new TabletopPose(new TableCoordinate(9.0, 2.0), 90.0f, 3, 4);

            Assert.That(first, Is.Not.EqualTo(second));
            Assert.That(first != second, Is.True);
        }

        [Test]
        public void TabletopPose_WhenRotationDiffers_ComparesNotEqual()
        {
            TableCoordinate position = new TableCoordinate(1.0, 2.0);
            TabletopPose first = new TabletopPose(position, 90.0f, 3, 4);
            TabletopPose second = new TabletopPose(position, 180.0f, 3, 4);

            Assert.That(first, Is.Not.EqualTo(second));
            Assert.That(first != second, Is.True);
        }

        [Test]
        public void TabletopPose_WhenLayerDiffers_ComparesNotEqual()
        {
            TableCoordinate position = new TableCoordinate(1.0, 2.0);
            TabletopPose first = new TabletopPose(position, 90.0f, 3, 4);
            TabletopPose second = new TabletopPose(position, 90.0f, 9, 4);

            Assert.That(first, Is.Not.EqualTo(second));
            Assert.That(first != second, Is.True);
        }

        [Test]
        public void TabletopPose_WhenLocalOrderDiffers_ComparesNotEqual()
        {
            TableCoordinate position = new TableCoordinate(1.0, 2.0);
            TabletopPose first = new TabletopPose(position, 90.0f, 3, 4);
            TabletopPose second = new TabletopPose(position, 90.0f, 3, 9);

            Assert.That(first, Is.Not.EqualTo(second));
            Assert.That(first != second, Is.True);
        }
    }
}
