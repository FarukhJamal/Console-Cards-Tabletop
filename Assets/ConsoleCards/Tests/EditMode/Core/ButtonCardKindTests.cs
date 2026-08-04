using System;
using System.Linq;
using ConsoleCards.Core.Domain.Cards;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.Core
{
    public sealed class ButtonCardKindTests
    {
        [Test]
        public void Enum_WhenInspected_HasExactlyApprovedSemanticValues()
        {
            string[] names = Enum.GetNames(typeof(ButtonCardKind));

            Assert.That(
                names,
                Is.EquivalentTo(new[]
                {
                    nameof(ButtonCardKind.Up),
                    nameof(ButtonCardKind.Down),
                    nameof(ButtonCardKind.Left),
                    nameof(ButtonCardKind.Right),
                    nameof(ButtonCardKind.A),
                    nameof(ButtonCardKind.B),
                    nameof(ButtonCardKind.X),
                    nameof(ButtonCardKind.Y)
                }));
            Assert.That(names, Has.Length.EqualTo(8));
        }

        [Test]
        public void Enum_WhenValuesAreRead_UsesStableExplicitNumericAssignments()
        {
            Assert.That((int)ButtonCardKind.Up, Is.EqualTo(1));
            Assert.That((int)ButtonCardKind.Down, Is.EqualTo(2));
            Assert.That((int)ButtonCardKind.Left, Is.EqualTo(3));
            Assert.That((int)ButtonCardKind.Right, Is.EqualTo(4));
            Assert.That((int)ButtonCardKind.A, Is.EqualTo(5));
            Assert.That((int)ButtonCardKind.B, Is.EqualTo(6));
            Assert.That((int)ButtonCardKind.X, Is.EqualTo(7));
            Assert.That((int)ButtonCardKind.Y, Is.EqualTo(8));
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(9)]
        public void Enum_WhenValueIsOutsideApprovedRange_IsUndefined(int value)
        {
            Assert.That(Enum.IsDefined(typeof(ButtonCardKind), (ButtonCardKind)value), Is.False);
        }

        [Test]
        public void StaticBoundary_CoreAssemblyHasNoUnityDependency()
        {
            string[] referencedAssemblyNames = typeof(ButtonCardKind)
                .Assembly
                .GetReferencedAssemblies()
                .Select(assemblyName => assemblyName.Name)
                .ToArray();

            Assert.That(referencedAssemblyNames, Does.Not.Contain("UnityEngine"));
            Assert.That(referencedAssemblyNames, Does.Not.Contain("Unity.InputSystem"));
        }
    }
}
