using ConsoleCards.Presentation.Views;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ConsoleCards.Tests.EditMode.Presentation
{
    public sealed class PhysicalDieMappingTests
    {
        [TestCase(4)] [TestCase(6)] [TestCase(8)] [TestCase(10)] [TestCase(12)] [TestCase(20)]
        public void EachAuthoredFace_ResolvesItsExplicitValue(int sides)
        {
            PhysicalDieDefinition definition = AssetDatabase.LoadAssetAtPath<PhysicalDieDefinition>(
                $"Assets/ConsoleCards/Content/Prefabs/Prototype/PhysicalD{sides}.asset");
            Assert.That(definition, Is.Not.Null);
            SerializedObject serialized = new SerializedObject(definition);
            SerializedProperty faces = serialized.FindProperty("faces");
            bool opposite = serialized.FindProperty("readOppositeSupportingFace").boolValue;
            Assert.That(faces.arraySize, Is.EqualTo(sides));
            for (int i = 0; i < faces.arraySize; i++)
            {
                SerializedProperty face = faces.GetArrayElementAtIndex(i);
                Vector3 normal = face.FindPropertyRelative("outwardNormal").vector3Value;
                Quaternion resting = Quaternion.FromToRotation(opposite ? -normal : normal, Vector3.up);
                Assert.That(definition.TryRead(resting, out int value), Is.True);
                Assert.That(value, Is.EqualTo(face.FindPropertyRelative("value").intValue));
            }
        }
    }
}
