using System;
using System.Collections;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Coordinates;
using ConsoleCards.Presentation.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityObject = UnityEngine.Object;

namespace ConsoleCards.Tests.PlayMode.Presentation
{
    public sealed class PhysicalTabletopSurfacesTests
    {
        private Scene scene;
        private Camera camera;
        private PhysicalTabletopSurfaces query;
        private readonly TabletopCoordinateConverter converter = new TabletopCoordinateConverter(1f, 0f, 0.02f, 0.0005f);

        [SetUp]
        public void SetUp()
        {
            // Isolate registry queries from any existing loaded tabletop/test surfaces.
            scene = SceneManager.CreateScene("PhysicalSurfaceTests-" + Guid.NewGuid(),
                new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            camera = Create("Camera").AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 10f;
            camera.transform.SetPositionAndRotation(new Vector3(0f, 20f, 0f), Quaternion.Euler(90f, 0f, 0f));
            query = new PhysicalTabletopSurfaces(camera, converter);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (scene.IsValid()) yield return SceneManager.UnloadSceneAsync(scene);
        }

        [Test]
        public void MarkedCollider_ResolvesPointerAndAuthoritativePlacementWithoutModelReferences()
        {
            BoxCollider collider = Surface(new Vector3(0f, 2f, 0f));
            Assert.That(query.TryPointer(camera.WorldToScreenPoint(Vector3.zero), out RaycastHit hit), Is.True);
            Assert.That(hit.collider, Is.SameAs(collider));
            Assert.That(hit.point.y, Is.EqualTo(2.1f).Within(0.001f));
            PlayerId actor = new PlayerId(Guid.NewGuid());
            Assert.That(query.TryResolve(Pose(0f, 0f), actor, out PhysicalObjectState state), Is.True);
            Assert.That(state.Position.Y, Is.EqualTo(hit.point.y + PhysicalTabletopSurfaces.PlacementClearance).Within(0.001f));
            Assert.That(state.Mode, Is.EqualTo(PhysicalObjectMode.Dynamic));
            Assert.That(state.ControllingPlayerId, Is.EqualTo(actor));
        }

        [Test]
        public void NearestMarkedBoardWins_DecorativeCollidersAreIgnored()
        {
            Surface(Vector3.zero);
            BoxCollider board = Surface(new Vector3(0f, 2f, 0f));
            GameObject decoration = Create("Not a surface");
            decoration.transform.position = new Vector3(0f, 4f, 0f);
            decoration.AddComponent<BoxCollider>().size = new Vector3(8f, 0.2f, 8f);

            Assert.That(query.TryAtLayout(Pose(0f, 0f), out RaycastHit hit), Is.True);
            Assert.That(hit.collider, Is.SameAs(board));
            Assert.That(query.TryAtLayout(Pose(8f, 8f), out _), Is.False);
        }

        [Test]
        public void AdditionalColliderOnMarkedGameObject_IsNotAutomaticallyAPlacementTarget()
        {
            BoxCollider surface = Surface(Vector3.zero);
            BoxCollider decoration = surface.gameObject.AddComponent<BoxCollider>();
            decoration.center = new Vector3(0f, 4f, 0f);
            decoration.size = new Vector3(8f, 0.2f, 8f);
            AssertHit(surface);
        }

        [Test]
        public void ReplacingModel_ReparentingAndRenamingNeedsNoRewiring()
        {
            BoxCollider oldSurface = Surface(Vector3.zero);
            Assert.That(query.TryAtLayout(Pose(0f, 0f), out _), Is.True);
            UnityObject.DestroyImmediate(oldSurface.gameObject);
            BoxCollider replacement = Surface(new Vector3(6f, 3f, -5f));
            replacement.name = "An arbitrary imported asset";
            replacement.transform.SetParent(Create("Any hierarchy").transform, true);

            Assert.That(query.TryAtLayout(Pose(0f, 0f), out _), Is.False);
            Assert.That(query.TryAtLayout(Pose(6f, -5f), out RaycastHit hit), Is.True);
            Assert.That(hit.collider, Is.SameAs(replacement));
        }

        [Test]
        public void ParentTranslationRotationScaleAndColliderCenter_DefineTheCurrentSurface()
        {
            GameObject model = Create("Model");
            BoxCollider collider = Surface(Vector3.zero);
            collider.transform.SetParent(model.transform, false);
            collider.center = new Vector3(0.5f, 0f, 0f);
            collider.size = new Vector3(4f, 0.2f, 2f);
            model.transform.SetPositionAndRotation(new Vector3(6f, 30f, -5f), Quaternion.Euler(12f, 35f, 0f));
            model.transform.localScale = new Vector3(2f, 1.5f, 3f);
            Vector3 inside = collider.transform.TransformPoint(collider.center);

            // This high translated surface is also above the Camera and the layout plane: no stale bounds are used.
            Assert.That(query.TryAtLayout(Pose(inside.x, inside.z), out RaycastHit hit), Is.True);
            Assert.That(hit.collider, Is.SameAs(collider));
            Assert.That(query.TryAtLayout(Pose(0f, 0f), out _), Is.False);
            Assert.That(query.TryAtLayout(Pose(30f, 30f), out _), Is.False);
        }

        [Test]
        public void DisabledMarkerColliderOrGameObject_IsExcludedAndCanBeReenabled()
        {
            BoxCollider table = Surface(Vector3.zero);
            BoxCollider board = Surface(new Vector3(0f, 2f, 0f));
            PhysicalTabletopSurface marker = board.GetComponent<PhysicalTabletopSurface>();
            marker.enabled = false;
            AssertHit(table);
            marker.enabled = true;
            AssertHit(board);
            board.enabled = false;
            AssertHit(table);
            board.enabled = true;
            board.gameObject.SetActive(false);
            AssertHit(table);
            board.gameObject.SetActive(true);
            AssertHit(board);
        }

        [Test]
        public void MissingSurface_LogsOnceAndRecoversWhenOneIsRegistered()
        {
            LogAssert.Expect(LogType.Error, PhysicalTabletopSurfaces.MissingSurfaceMessage);
            Assert.That(query.TryAtLayout(Pose(0f, 0f), out _), Is.False);
            Assert.That(query.TryAtLayout(Pose(0f, 0f), out _), Is.False);
            BoxCollider surface = Surface(Vector3.zero);
            AssertHit(surface);
            surface.gameObject.SetActive(false);
            LogAssert.Expect(LogType.Error, PhysicalTabletopSurfaces.MissingSurfaceMessage);
            Assert.That(query.ValidateSetup(), Is.False);
        }

        [Test]
        public void TriggerOrMissingCollider_IsDiagnosedAndDoesNotAuthorizePlacement()
        {
            BoxCollider table = Surface(Vector3.zero);
            GameObject invalid = Create("Invalid authored surface");
            LogAssert.Expect(LogType.Error, "PhysicalTabletopSurface: Add a Collider to this GameObject. If it has multiple colliders, assign the authored top collider explicitly.");
            PhysicalTabletopSurface marker = invalid.AddComponent<PhysicalTabletopSurface>();
            Assert.That(marker.TryGetCollider(out _, out string issue), Is.False);
            Assert.That(issue, Does.Contain("Add a Collider"));
            BoxCollider collider = invalid.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            LogAssert.Expect(LogType.Error, "PhysicalTabletopSurface: Turn off Is Trigger on the surface Collider so it can catch physical pieces.");
            AssertHit(table);
            collider.isTrigger = false;
            Assert.That(marker.TryGetCollider(out _, out _), Is.True);
        }

        [Test]
        public void ParentMarker_DoesNotOptInChildDecorativeCollider()
        {
            Surface(Vector3.zero);
            GameObject parent = Create("Parent without collider");
            GameObject child = Create("Decorative child");
            child.transform.SetParent(parent.transform, false);
            child.transform.localPosition = new Vector3(0f, 4f, 0f);
            child.AddComponent<BoxCollider>();
            LogAssert.Expect(LogType.Error, "PhysicalTabletopSurface: Add a Collider to this GameObject. If it has multiple colliders, assign the authored top collider explicitly.");
            PhysicalTabletopSurface marker = parent.AddComponent<PhysicalTabletopSurface>();
            Assert.That(marker.TryGetCollider(out _, out _), Is.False);
            Assert.That(query.TryAtLayout(Pose(0f, 0f), out RaycastHit hit), Is.True);
            Assert.That(hit.collider.gameObject, Is.Not.SameAs(child));
        }

        private void AssertHit(Collider expected)
        {
            Assert.That(query.TryAtLayout(Pose(0f, 0f), out RaycastHit hit), Is.True);
            Assert.That(hit.collider, Is.SameAs(expected));
        }

        private BoxCollider Surface(Vector3 position)
        {
            GameObject target = Create("Arbitrary surface");
            target.transform.position = position;
            BoxCollider collider = target.AddComponent<BoxCollider>();
            collider.size = new Vector3(8f, 0.2f, 8f);
            target.AddComponent<PhysicalTabletopSurface>();
            return collider;
        }

        private GameObject Create(string name)
        {
            GameObject target = new GameObject(name);
            SceneManager.MoveGameObjectToScene(target, scene);
            return target;
        }

        private static TabletopPose Pose(float x, float z) => new TabletopPose(new TableCoordinate(x, z), 0f, 0, 0);
    }
}
