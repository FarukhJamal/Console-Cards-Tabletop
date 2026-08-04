using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Cards;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Identifiers;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.Core
{
    public sealed class ButtonCardDefinitionTests
    {
        [TestCase(ButtonCardKind.Up)]
        [TestCase(ButtonCardKind.Down)]
        [TestCase(ButtonCardKind.Left)]
        [TestCase(ButtonCardKind.Right)]
        [TestCase(ButtonCardKind.A)]
        [TestCase(ButtonCardKind.B)]
        [TestCase(ButtonCardKind.X)]
        [TestCase(ButtonCardKind.Y)]
        public void Constructor_WhenKindIsApproved_CreatesDefinition(ButtonCardKind kind)
        {
            ObjectDefinitionId definitionId = ObjectDefinitionId.New();

            ButtonCardDefinition definition = new ButtonCardDefinition(definitionId, kind);

            Assert.That(definition.DefinitionId, Is.EqualTo(definitionId));
            Assert.That(definition.Kind, Is.EqualTo(kind));
        }

        [Test]
        public void Constructor_ExposesDefinitionIdAndKindExactly()
        {
            ObjectDefinitionId definitionId = ObjectDefinitionId.New();

            ButtonCardDefinition definition = new ButtonCardDefinition(definitionId, ButtonCardKind.A);

            Assert.That(definition.DefinitionId, Is.EqualTo(definitionId));
            Assert.That(definition.Kind, Is.EqualTo(ButtonCardKind.A));
        }

        [Test]
        public void Constructor_WhenDefinitionIdIsEmpty_ThrowsArgumentException()
        {
            Assert.That(
                () => new ButtonCardDefinition(ObjectDefinitionId.Empty, ButtonCardKind.A),
                Throws.ArgumentException);
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(9)]
        public void Constructor_WhenKindIsUndefined_ThrowsArgumentOutOfRangeException(int kind)
        {
            Assert.That(
                () => new ButtonCardDefinition(ObjectDefinitionId.New(), (ButtonCardKind)kind),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Contract_IsReadonlyStructWithImmutablePublicSurface()
        {
            Type type = typeof(ButtonCardDefinition);

            Assert.That(type.IsValueType, Is.True);
            Assert.That(type.IsDefined(typeof(IsReadOnlyAttribute), inherit: false), Is.True);
            Assert.That(type.GetProperties().Where(property => property.SetMethod != null), Is.Empty);
            Assert.That(type.GetFields(BindingFlags.Instance | BindingFlags.Public), Is.Empty);
        }

        [Test]
        public void Equality_WhenValuesMatch_ComparesEqual()
        {
            ObjectDefinitionId definitionId = ObjectDefinitionId.New();
            ButtonCardDefinition first = new ButtonCardDefinition(definitionId, ButtonCardKind.A);
            ButtonCardDefinition second = new ButtonCardDefinition(definitionId, ButtonCardKind.A);

            Assert.That(first.Equals(second), Is.True);
            Assert.That(first.Equals((object)second), Is.True);
            Assert.That(first == second, Is.True);
        }

        [Test]
        public void Equality_WhenDefinitionIdDiffers_ComparesUnequal()
        {
            ButtonCardDefinition first = new ButtonCardDefinition(ObjectDefinitionId.New(), ButtonCardKind.A);
            ButtonCardDefinition second = new ButtonCardDefinition(ObjectDefinitionId.New(), ButtonCardKind.A);

            Assert.That(first.Equals(second), Is.False);
            Assert.That(first != second, Is.True);
        }

        [Test]
        public void Equality_WhenKindDiffers_ComparesUnequal()
        {
            ObjectDefinitionId definitionId = ObjectDefinitionId.New();
            ButtonCardDefinition first = new ButtonCardDefinition(definitionId, ButtonCardKind.A);
            ButtonCardDefinition second = new ButtonCardDefinition(definitionId, ButtonCardKind.B);

            Assert.That(first.Equals(second), Is.False);
            Assert.That(first != second, Is.True);
        }

        [Test]
        public void Operators_ReturnExpectedValues()
        {
            ObjectDefinitionId definitionId = ObjectDefinitionId.New();
            ButtonCardDefinition first = new ButtonCardDefinition(definitionId, ButtonCardKind.Up);
            ButtonCardDefinition matching = new ButtonCardDefinition(definitionId, ButtonCardKind.Up);
            ButtonCardDefinition different = new ButtonCardDefinition(definitionId, ButtonCardKind.Down);

            Assert.That(first == matching, Is.True);
            Assert.That(first != matching, Is.False);
            Assert.That(first == different, Is.False);
            Assert.That(first != different, Is.True);
        }

        [Test]
        public void GetHashCode_WhenValuesMatch_IsConsistent()
        {
            ObjectDefinitionId definitionId = ObjectDefinitionId.New();
            ButtonCardDefinition first = new ButtonCardDefinition(definitionId, ButtonCardKind.X);
            ButtonCardDefinition second = new ButtonCardDefinition(definitionId, ButtonCardKind.X);

            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        }

        [Test]
        public void DictionaryAndSet_WhenUsedAsKey_UseValueEquality()
        {
            ObjectDefinitionId definitionId = ObjectDefinitionId.New();
            ButtonCardDefinition first = new ButtonCardDefinition(definitionId, ButtonCardKind.Y);
            ButtonCardDefinition second = new ButtonCardDefinition(definitionId, ButtonCardKind.Y);
            HashSet<ButtonCardDefinition> set = new HashSet<ButtonCardDefinition> { first };
            Dictionary<ButtonCardDefinition, string> dictionary = new Dictionary<ButtonCardDefinition, string>
            {
                [first] = "button-y"
            };

            Assert.That(set.Contains(second), Is.True);
            Assert.That(dictionary[second], Is.EqualTo("button-y"));
        }

        [Test]
        public void ToString_ContainsDefinitionIdAndKind()
        {
            ObjectDefinitionId definitionId = ObjectDefinitionId.New();
            ButtonCardDefinition definition = new ButtonCardDefinition(definitionId, ButtonCardKind.Left);

            string value = definition.ToString();

            Assert.That(value, Does.Contain("DefinitionId"));
            Assert.That(value, Does.Contain(definitionId.ToString()));
            Assert.That(value, Does.Contain("Kind"));
            Assert.That(value, Does.Contain(nameof(ButtonCardKind.Left)));
        }

        [Test]
        public void CardInstanceState_WhenDefinitionIdMatchesButtonDefinition_RemainsNormalCardState()
        {
            ObjectDefinitionId definitionId = ObjectDefinitionId.New();
            ButtonCardDefinition definition = new ButtonCardDefinition(definitionId, ButtonCardKind.B);
            ContainerId containerId = ContainerId.New();
            PlayerId ownerId = PlayerId.New();
            TabletopPose pose = new TabletopPose(new TableCoordinate(2.0, 3.0), -450.0f, 4, 5);
            TabletopObjectState baseState = new TabletopObjectState(
                TabletopObjectId.New(),
                definition.DefinitionId,
                TabletopObjectKind.Card,
                pose,
                containerId,
                ownerId,
                ObjectVisibility.OwnerOnly,
                true);
            CardInstanceState card = new CardInstanceState(baseState, CardFace.FaceDown);

            card.SetFace(CardFace.FaceUp);

            Assert.That(card.BaseState.DefinitionId, Is.EqualTo(definition.DefinitionId));
            Assert.That(card.BaseState.Kind, Is.EqualTo(TabletopObjectKind.Card));
            Assert.That(card.Face, Is.EqualTo(CardFace.FaceUp));
            Assert.That(card.BaseState.Pose, Is.EqualTo(pose));
            Assert.That(card.BaseState.ContainerId, Is.EqualTo(containerId));
            Assert.That(card.BaseState.OwnerPlayerId, Is.EqualTo(ownerId));
            Assert.That(card.BaseState.Visibility, Is.EqualTo(ObjectVisibility.OwnerOnly));
            Assert.That(card.BaseState.IsUserLocked, Is.True);
        }

        [Test]
        public void StaticBoundary_CoreContainsNoButtonCardInstanceState()
        {
            Type[] coreTypes = typeof(ButtonCardDefinition).Assembly.GetTypes();

            Assert.That(
                coreTypes.Any(type => type.Name == "ButtonCardInstanceState"),
                Is.False);
        }

        [Test]
        public void StaticBoundary_ButtonCardDefinitionContainsNoRuntimeVisualInputOrRuleReferences()
        {
            Type[] storedTypes = typeof(ButtonCardDefinition)
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Select(field => field.FieldType)
                .Concat(typeof(ButtonCardDefinition).GetProperties().Select(property => property.PropertyType))
                .ToArray();

            string[] forbiddenNameFragments =
            {
                "UnityEngine",
                "InputAction",
                "Sprite",
                "Material",
                "Color",
                "Renderer",
                "GameObject",
                "TabletopObjectState",
                "CardInstanceState",
                "Command",
                "UseCase",
                "Container",
                "Visibility",
                "Owner"
            };

            foreach (Type storedType in storedTypes)
            {
                foreach (string forbiddenNameFragment in forbiddenNameFragments)
                {
                    Assert.That(
                        storedType.FullName,
                        Does.Not.Contain(forbiddenNameFragment),
                        $"{storedType.FullName} should not be part of the Button Card definition contract.");
                }
            }

            FieldInfo[] mutableStaticFields = typeof(ButtonCardDefinition)
                .GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(field => !field.IsLiteral && !field.IsInitOnly)
                .ToArray();

            Assert.That(mutableStaticFields, Is.Empty);
        }

        [Test]
        public void StaticBoundary_ApplicationContainsNoButtonCardActivationCode()
        {
            Assembly applicationAssembly = Assembly.Load("ConsoleCards.Application");

            string[] matchingTypeNames = applicationAssembly
                .GetTypes()
                .Select(type => type.FullName)
                .Where(name => name.Contains("ButtonCard") || name.Contains("ActivateButton"))
                .ToArray();

            Assert.That(matchingTypeNames, Is.Empty);
        }
    }
}
