using System;
using System.Collections.Generic;
using ConsoleCards.Presentation.TableSurface;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityObject = UnityEngine.Object;

namespace ConsoleCards.Tests.PlayMode.Presentation
{
    public sealed class TabletopSurfaceFollowerTests
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
            SurfaceFollowerTestContext context = CreateInitializedFollower();

            Assert.That(context.Follower.enabled, Is.True);
            Assert.That(context.Follower.IsInitialized, Is.True);
            Assert.That(context.Follower.TrackedTransform, Is.SameAs(context.TrackedTransform));
            Assert.That(context.Follower.SurfaceTransform, Is.SameAs(context.SurfaceTransform));
            Assert.That(context.Follower.SurfaceHeight, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void Awake_WhenValidReferences_AppliesInitialAlignment()
        {
            SurfaceFollowerTestContext context = CreateInitializedFollower(
                trackedPosition: new Vector3(4f, 9f, -6f),
                surfaceHeight: 1.5f);

            AssertVector3(context.SurfaceTransform.position, 4f, 1.5f, -6f);
        }

        [Test]
        public void Awake_WhenTrackedTransformIsMissing_DisablesComponent()
        {
            LogAssert.Expect(LogType.Error, "TabletopSurfaceFollower requires a tracked Transform reference.");

            SurfaceFollowerTestContext context = CreateInitializedFollower(assignTrackedTransform: false);

            Assert.That(context.Follower.enabled, Is.False);
            Assert.That(context.Follower.IsInitialized, Is.False);
        }

        [Test]
        public void Awake_WhenSurfaceTransformIsMissing_DisablesComponent()
        {
            LogAssert.Expect(LogType.Error, "TabletopSurfaceFollower requires a surface Transform reference.");

            SurfaceFollowerTestContext context = CreateInitializedFollower(assignSurfaceTransform: false);

            Assert.That(context.Follower.enabled, Is.False);
            Assert.That(context.Follower.IsInitialized, Is.False);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void Awake_WhenSurfaceHeightIsNonFinite_DisablesComponent(float surfaceHeight)
        {
            LogAssert.Expect(LogType.Error, "TabletopSurfaceFollower requires finite surfaceHeight.");

            SurfaceFollowerTestContext context = CreateInitializedFollower(surfaceHeight: surfaceHeight);

            Assert.That(context.Follower.enabled, Is.False);
            Assert.That(context.Follower.IsInitialized, Is.False);
        }

        [Test]
        public void ApplyFollow_SurfaceXFollowsTrackedX()
        {
            SurfaceFollowerTestContext context = CreateInitializedFollower();

            context.Follower.ApplyFollowPosition(new Vector3(7f, 0f, 0f));

            Assert.That(context.SurfaceTransform.position.x, Is.EqualTo(7f).Within(Tolerance));
        }

        [Test]
        public void ApplyFollow_SurfaceZFollowsTrackedZ()
        {
            SurfaceFollowerTestContext context = CreateInitializedFollower();

            context.Follower.ApplyFollowPosition(new Vector3(0f, 0f, -8f));

            Assert.That(context.SurfaceTransform.position.z, Is.EqualTo(-8f).Within(Tolerance));
        }

        [Test]
        public void ApplyFollow_SurfaceYUsesConfiguredSurfaceHeight()
        {
            SurfaceFollowerTestContext context = CreateInitializedFollower(surfaceHeight: -2f);

            context.Follower.ApplyFollowPosition(new Vector3(3f, 99f, 4f));

            Assert.That(context.SurfaceTransform.position.y, Is.EqualTo(-2f).Within(Tolerance));
        }

        [Test]
        public void ApplyFollow_TrackedYDoesNotAffectSurfaceY()
        {
            SurfaceFollowerTestContext context = CreateInitializedFollower(surfaceHeight: 2.5f);

            context.Follower.ApplyFollowPosition(new Vector3(0f, -100f, 0f));

            AssertVector3(context.SurfaceTransform.position, 0f, 2.5f, 0f);
        }

        [Test]
        public void ApplyFollow_PreservesNegativeXZPositions()
        {
            SurfaceFollowerTestContext context = CreateInitializedFollower(surfaceHeight: 0.5f);

            context.Follower.ApplyFollowPosition(new Vector3(-12f, 1f, -14f));

            AssertVector3(context.SurfaceTransform.position, -12f, 0.5f, -14f);
        }

        [Test]
        public void ApplyFollow_WhenTrackedTransformMoves_SurfaceFollows()
        {
            SurfaceFollowerTestContext context = CreateInitializedFollower();

            context.TrackedTransform.position = new Vector3(9f, 3f, 11f);
            context.Follower.ApplyFollow();

            AssertVector3(context.SurfaceTransform.position, 9f, 0f, 11f);
        }

        [Test]
        public void ApplyFollow_WhenRepeated_RemainsDeterministic()
        {
            SurfaceFollowerTestContext context = CreateInitializedFollower(surfaceHeight: 1f);
            Vector3 trackedPosition = new Vector3(6f, 5f, -3f);

            context.Follower.ApplyFollowPosition(trackedPosition);
            Vector3 firstPosition = context.SurfaceTransform.position;
            context.Follower.ApplyFollowPosition(trackedPosition);

            AssertVector3(context.SurfaceTransform.position, firstPosition.x, firstPosition.y, firstPosition.z);
        }

        [Test]
        public void ApplyFollow_PreservesSurfaceRotation()
        {
            SurfaceFollowerTestContext context = CreateInitializedFollower();
            Quaternion originalRotation = Quaternion.Euler(10f, 25f, 40f);
            context.SurfaceTransform.rotation = originalRotation;

            context.Follower.ApplyFollowPosition(new Vector3(5f, 6f, 7f));

            Assert.That(Quaternion.Angle(originalRotation, context.SurfaceTransform.rotation), Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void ApplyFollow_PreservesSurfaceScale()
        {
            SurfaceFollowerTestContext context = CreateInitializedFollower();
            Vector3 originalScale = new Vector3(3f, 1f, 4f);
            context.SurfaceTransform.localScale = originalScale;

            context.Follower.ApplyFollowPosition(new Vector3(5f, 6f, 7f));

            AssertVector3(context.SurfaceTransform.localScale, originalScale.x, originalScale.y, originalScale.z);
        }

        [Test]
        public void ApplyFollow_DoesNotMoveTrackedTransform()
        {
            SurfaceFollowerTestContext context = CreateInitializedFollower();
            Vector3 originalTrackedPosition = new Vector3(2f, 3f, 4f);
            context.TrackedTransform.position = originalTrackedPosition;

            context.Follower.ApplyFollow();

            AssertVector3(context.TrackedTransform.position, originalTrackedPosition.x, originalTrackedPosition.y, originalTrackedPosition.z);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void ApplyFollow_WhenTrackedXIsNonFinite_ThrowsWithoutMutation(float trackedX)
        {
            SurfaceFollowerTestContext context = CreateInitializedFollower(
                trackedPosition: new Vector3(1f, 2f, 3f),
                surfaceHeight: 0.75f);
            Vector3 acceptedPosition = context.SurfaceTransform.position;

            Assert.Throws<ArgumentOutOfRangeException>(
                () => context.Follower.ApplyFollowPosition(new Vector3(trackedX, 4f, 5f)));

            AssertVector3(context.SurfaceTransform.position, acceptedPosition.x, acceptedPosition.y, acceptedPosition.z);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void ApplyFollow_WhenTrackedYIsNonFinite_ThrowsWithoutMutation(float trackedY)
        {
            SurfaceFollowerTestContext context = CreateInitializedFollower(
                trackedPosition: new Vector3(1f, 2f, 3f),
                surfaceHeight: 0.75f);
            Vector3 acceptedPosition = context.SurfaceTransform.position;

            Assert.Throws<ArgumentOutOfRangeException>(
                () => context.Follower.ApplyFollowPosition(new Vector3(4f, trackedY, 5f)));

            AssertVector3(context.SurfaceTransform.position, acceptedPosition.x, acceptedPosition.y, acceptedPosition.z);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void ApplyFollow_WhenTrackedZIsNonFinite_ThrowsWithoutMutation(float trackedZ)
        {
            SurfaceFollowerTestContext context = CreateInitializedFollower(
                trackedPosition: new Vector3(1f, 2f, 3f),
                surfaceHeight: 0.75f);
            Vector3 acceptedPosition = context.SurfaceTransform.position;

            Assert.Throws<ArgumentOutOfRangeException>(
                () => context.Follower.ApplyFollowPosition(new Vector3(4f, 5f, trackedZ)));

            AssertVector3(context.SurfaceTransform.position, acceptedPosition.x, acceptedPosition.y, acceptedPosition.z);
        }

        [Test]
        public void ApplyFollow_WhenInitializationFailed_ThrowsInvalidOperationException()
        {
            LogAssert.Expect(LogType.Error, "TabletopSurfaceFollower requires a tracked Transform reference.");
            SurfaceFollowerTestContext context = CreateInitializedFollower(assignTrackedTransform: false);

            Assert.Throws<InvalidOperationException>(() => context.Follower.ApplyFollow());
        }

        [Test]
        public void Lifecycle_WhenInvalidFollower_DoesNotThrowDuringDisableOrActivation()
        {
            LogAssert.Expect(LogType.Error, "TabletopSurfaceFollower requires a tracked Transform reference.");
            SurfaceFollowerTestContext context = CreateInitializedFollower(assignTrackedTransform: false);

            Assert.DoesNotThrow(() =>
            {
                context.Follower.enabled = false;
                context.Follower.gameObject.SetActive(false);
                context.Follower.gameObject.SetActive(true);
                context.Follower.enabled = false;
            });
        }

        private SurfaceFollowerTestContext CreateInitializedFollower(
            bool assignTrackedTransform = true,
            bool assignSurfaceTransform = true,
            Vector3? trackedPosition = null,
            float surfaceHeight = 0f)
        {
            Transform trackedTransform = CreateGameObject("Tracked Transform").transform;
            Transform surfaceTransform = CreateGameObject("Table Surface").transform;
            trackedTransform.position = trackedPosition ?? Vector3.zero;
            surfaceTransform.position = new Vector3(-3f, -3f, -3f);

            GameObject followerObject = CreateGameObject("Tabletop Surface Follower");
            followerObject.SetActive(false);
            TabletopSurfaceFollower follower = followerObject.AddComponent<TabletopSurfaceFollower>();
            follower.trackedTransform = assignTrackedTransform ? trackedTransform : null;
            follower.surfaceTransform = assignSurfaceTransform ? surfaceTransform : null;
            follower.surfaceHeight = surfaceHeight;

            followerObject.SetActive(true);

            return new SurfaceFollowerTestContext(follower, trackedTransform, surfaceTransform);
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

        private sealed class SurfaceFollowerTestContext
        {
            public SurfaceFollowerTestContext(
                TabletopSurfaceFollower follower,
                Transform trackedTransform,
                Transform surfaceTransform)
            {
                Follower = follower;
                TrackedTransform = trackedTransform;
                SurfaceTransform = surfaceTransform;
            }

            public TabletopSurfaceFollower Follower { get; }

            public Transform TrackedTransform { get; }

            public Transform SurfaceTransform { get; }
        }
    }
}
