using System;
using System.Collections.Generic;
using ConsoleCards.Presentation.TableSurface;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityObject = UnityEngine.Object;

namespace ConsoleCards.Tests.PlayMode.Presentation
{
    public sealed class TabletopSurfaceProxyTests
    {
        private const float Tolerance = 0.0001f;

        private readonly List<GameObject> createdGameObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < createdGameObjects.Count; i++)
            {
                if (createdGameObjects[i] != null)
                {
                    UnityObject.DestroyImmediate(createdGameObjects[i]);
                }
            }

            createdGameObjects.Clear();
        }

        [Test]
        public void ValidReferences_InitializesSuccessfully()
        {
            SurfaceProxyTestContext context = CreateInitializedProxy();

            Assert.That(context.Proxy.enabled, Is.True);
            Assert.That(context.Proxy.IsInitialized, Is.True);
            Assert.That(context.Proxy.TrackedTransform, Is.SameAs(context.TrackedTransform));
            Assert.That(context.Proxy.SurfaceTransform, Is.SameAs(context.SurfaceTransform));
            Assert.That(context.Proxy.SurfaceHeight, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void Awake_WhenValidReferences_AppliesInitialAlignment()
        {
            SurfaceProxyTestContext context = CreateInitializedProxy(
                trackedPosition: new Vector3(4f, 9f, -6f),
                surfaceHeight: 1.5f);

            AssertVector3(context.SurfaceTransform.position, 4f, 1.5f, -6f);
        }

        [Test]
        public void Awake_WhenTrackedTransformIsMissing_DisablesComponent()
        {
            LogAssert.Expect(LogType.Error, "TabletopSurfaceProxy requires a tracked Transform reference.");

            SurfaceProxyTestContext context = CreateInitializedProxy(assignTrackedTransform: false);

            Assert.That(context.Proxy.enabled, Is.False);
            Assert.That(context.Proxy.IsInitialized, Is.False);
        }

        [Test]
        public void Awake_WhenSurfaceTransformIsMissing_DisablesComponent()
        {
            LogAssert.Expect(LogType.Error, "TabletopSurfaceProxy requires a surface Transform reference.");

            SurfaceProxyTestContext context = CreateInitializedProxy(assignSurfaceTransform: false);

            Assert.That(context.Proxy.enabled, Is.False);
            Assert.That(context.Proxy.IsInitialized, Is.False);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void Awake_WhenSurfaceHeightIsNonFinite_DisablesComponent(float surfaceHeight)
        {
            LogAssert.Expect(LogType.Error, "TabletopSurfaceProxy requires finite surfaceHeight.");

            SurfaceProxyTestContext context = CreateInitializedProxy(surfaceHeight: surfaceHeight);

            Assert.That(context.Proxy.enabled, Is.False);
            Assert.That(context.Proxy.IsInitialized, Is.False);
        }

        [Test]
        public void ApplyFollow_SurfaceXFollowsTrackedX()
        {
            SurfaceProxyTestContext context = CreateInitializedProxy();

            context.Proxy.ApplyFollowPosition(new Vector3(7f, 0f, 0f));

            Assert.That(context.SurfaceTransform.position.x, Is.EqualTo(7f).Within(Tolerance));
        }

        [Test]
        public void ApplyFollow_SurfaceZFollowsTrackedZ()
        {
            SurfaceProxyTestContext context = CreateInitializedProxy();

            context.Proxy.ApplyFollowPosition(new Vector3(0f, 0f, -8f));

            Assert.That(context.SurfaceTransform.position.z, Is.EqualTo(-8f).Within(Tolerance));
        }

        [Test]
        public void ApplyFollow_SurfaceYUsesConfiguredSurfaceHeight()
        {
            SurfaceProxyTestContext context = CreateInitializedProxy(surfaceHeight: -2f);

            context.Proxy.ApplyFollowPosition(new Vector3(3f, 99f, 4f));

            Assert.That(context.SurfaceTransform.position.y, Is.EqualTo(-2f).Within(Tolerance));
        }

        [Test]
        public void ApplyFollow_TrackedYDoesNotAffectSurfaceY()
        {
            SurfaceProxyTestContext context = CreateInitializedProxy(surfaceHeight: 2.5f);

            context.Proxy.ApplyFollowPosition(new Vector3(0f, -100f, 0f));

            AssertVector3(context.SurfaceTransform.position, 0f, 2.5f, 0f);
        }

        [Test]
        public void ApplyFollow_PreservesNegativeXZPositions()
        {
            SurfaceProxyTestContext context = CreateInitializedProxy(surfaceHeight: 0.5f);

            context.Proxy.ApplyFollowPosition(new Vector3(-12f, 1f, -14f));

            AssertVector3(context.SurfaceTransform.position, -12f, 0.5f, -14f);
        }

        [Test]
        public void ApplyFollow_WhenTrackedTransformMoves_SurfaceFollows()
        {
            SurfaceProxyTestContext context = CreateInitializedProxy();

            context.TrackedTransform.position = new Vector3(9f, 3f, 11f);
            context.Proxy.ApplyFollow();

            AssertVector3(context.SurfaceTransform.position, 9f, 0f, 11f);
        }

        [Test]
        public void ApplyFollow_WhenRepeated_RemainsDeterministic()
        {
            SurfaceProxyTestContext context = CreateInitializedProxy(surfaceHeight: 1f);
            Vector3 trackedPosition = new Vector3(6f, 5f, -3f);

            context.Proxy.ApplyFollowPosition(trackedPosition);
            Vector3 firstPosition = context.SurfaceTransform.position;
            context.Proxy.ApplyFollowPosition(trackedPosition);

            AssertVector3(context.SurfaceTransform.position, firstPosition.x, firstPosition.y, firstPosition.z);
        }

        [Test]
        public void ApplyFollow_PreservesSurfaceRotation()
        {
            SurfaceProxyTestContext context = CreateInitializedProxy();
            Quaternion originalRotation = Quaternion.Euler(10f, 25f, 40f);
            context.SurfaceTransform.rotation = originalRotation;

            context.Proxy.ApplyFollowPosition(new Vector3(5f, 6f, 7f));

            Assert.That(Quaternion.Angle(originalRotation, context.SurfaceTransform.rotation), Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void ApplyFollow_PreservesSurfaceScale()
        {
            SurfaceProxyTestContext context = CreateInitializedProxy();
            Vector3 originalScale = new Vector3(3f, 1f, 4f);
            context.SurfaceTransform.localScale = originalScale;

            context.Proxy.ApplyFollowPosition(new Vector3(5f, 6f, 7f));

            AssertVector3(context.SurfaceTransform.localScale, originalScale.x, originalScale.y, originalScale.z);
        }

        [Test]
        public void ApplyFollow_DoesNotMoveTrackedTransform()
        {
            SurfaceProxyTestContext context = CreateInitializedProxy();
            Vector3 originalTrackedPosition = new Vector3(2f, 3f, 4f);
            context.TrackedTransform.position = originalTrackedPosition;

            context.Proxy.ApplyFollow();

            AssertVector3(context.TrackedTransform.position, originalTrackedPosition.x, originalTrackedPosition.y, originalTrackedPosition.z);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void ApplyFollow_WhenTrackedXIsNonFinite_ThrowsWithoutMutation(float trackedX)
        {
            SurfaceProxyTestContext context = CreateInitializedProxy(
                trackedPosition: new Vector3(1f, 2f, 3f),
                surfaceHeight: 0.75f);
            Vector3 acceptedPosition = context.SurfaceTransform.position;

            Assert.Throws<ArgumentOutOfRangeException>(
                () => context.Proxy.ApplyFollowPosition(new Vector3(trackedX, 4f, 5f)));

            AssertVector3(context.SurfaceTransform.position, acceptedPosition.x, acceptedPosition.y, acceptedPosition.z);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void ApplyFollow_WhenTrackedYIsNonFinite_ThrowsWithoutMutation(float trackedY)
        {
            SurfaceProxyTestContext context = CreateInitializedProxy(
                trackedPosition: new Vector3(1f, 2f, 3f),
                surfaceHeight: 0.75f);
            Vector3 acceptedPosition = context.SurfaceTransform.position;

            Assert.Throws<ArgumentOutOfRangeException>(
                () => context.Proxy.ApplyFollowPosition(new Vector3(4f, trackedY, 5f)));

            AssertVector3(context.SurfaceTransform.position, acceptedPosition.x, acceptedPosition.y, acceptedPosition.z);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void ApplyFollow_WhenTrackedZIsNonFinite_ThrowsWithoutMutation(float trackedZ)
        {
            SurfaceProxyTestContext context = CreateInitializedProxy(
                trackedPosition: new Vector3(1f, 2f, 3f),
                surfaceHeight: 0.75f);
            Vector3 acceptedPosition = context.SurfaceTransform.position;

            Assert.Throws<ArgumentOutOfRangeException>(
                () => context.Proxy.ApplyFollowPosition(new Vector3(4f, 5f, trackedZ)));

            AssertVector3(context.SurfaceTransform.position, acceptedPosition.x, acceptedPosition.y, acceptedPosition.z);
        }

        [Test]
        public void ApplyFollow_WhenInitializationFailed_ThrowsInvalidOperationException()
        {
            LogAssert.Expect(LogType.Error, "TabletopSurfaceProxy requires a tracked Transform reference.");
            SurfaceProxyTestContext context = CreateInitializedProxy(assignTrackedTransform: false);

            Assert.Throws<InvalidOperationException>(() => context.Proxy.ApplyFollow());
        }

        [Test]
        public void Lifecycle_WhenInvalidProxy_DoesNotThrowDuringDisableOrActivation()
        {
            LogAssert.Expect(LogType.Error, "TabletopSurfaceProxy requires a tracked Transform reference.");
            SurfaceProxyTestContext context = CreateInitializedProxy(assignTrackedTransform: false);

            Assert.DoesNotThrow(() =>
            {
                context.Proxy.enabled = false;
                context.Proxy.gameObject.SetActive(false);
                context.Proxy.gameObject.SetActive(true);
                context.Proxy.enabled = false;
            });
        }

        private SurfaceProxyTestContext CreateInitializedProxy(
            bool assignTrackedTransform = true,
            bool assignSurfaceTransform = true,
            Vector3? trackedPosition = null,
            float surfaceHeight = 0f)
        {
            Transform trackedTransform = CreateGameObject("Tracked Transform").transform;
            Transform surfaceTransform = CreateGameObject("Table Surface").transform;
            trackedTransform.position = trackedPosition ?? Vector3.zero;
            surfaceTransform.position = new Vector3(-3f, -3f, -3f);

            GameObject proxyObject = CreateGameObject("Tabletop Surface Proxy");
            proxyObject.SetActive(false);
            TabletopSurfaceProxy proxy = proxyObject.AddComponent<TabletopSurfaceProxy>();
            proxy.trackedTransform = assignTrackedTransform ? trackedTransform : null;
            proxy.surfaceTransform = assignSurfaceTransform ? surfaceTransform : null;
            proxy.surfaceHeight = surfaceHeight;

            proxyObject.SetActive(true);

            return new SurfaceProxyTestContext(proxy, trackedTransform, surfaceTransform);
        }

        private GameObject CreateGameObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            createdGameObjects.Add(gameObject);
            return gameObject;
        }

        private static void AssertVector3(Vector3 actual, float expectedX, float expectedY, float expectedZ)
        {
            Assert.That(actual.x, Is.EqualTo(expectedX).Within(Tolerance));
            Assert.That(actual.y, Is.EqualTo(expectedY).Within(Tolerance));
            Assert.That(actual.z, Is.EqualTo(expectedZ).Within(Tolerance));
        }

        private sealed class SurfaceProxyTestContext
        {
            public SurfaceProxyTestContext(
                TabletopSurfaceProxy proxy,
                Transform trackedTransform,
                Transform surfaceTransform)
            {
                Proxy = proxy;
                TrackedTransform = trackedTransform;
                SurfaceTransform = surfaceTransform;
            }

            public TabletopSurfaceProxy Proxy { get; }

            public Transform TrackedTransform { get; }

            public Transform SurfaceTransform { get; }
        }
    }
}
