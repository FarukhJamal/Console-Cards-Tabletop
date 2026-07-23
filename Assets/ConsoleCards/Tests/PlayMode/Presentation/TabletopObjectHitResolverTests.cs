using System;
using System.Collections.Generic;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Coordinates;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.TableSurface;
using ConsoleCards.Presentation.Views;
using NUnit.Framework;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace ConsoleCards.Tests.PlayMode.Presentation
{
    public sealed class TabletopObjectHitResolverTests
    {
        private const int InteractionLayer = 8;
        private const int OtherLayer = 0;
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
        public void Constructor_StoresValidConfiguration()
        {
            Camera camera = CreateCamera();
            LayerMask layerMask = LayerMaskFor(InteractionLayer);

            TabletopObjectHitResolver resolver = new TabletopObjectHitResolver(camera, layerMask, 25f);

            Assert.That(resolver.TargetCamera, Is.SameAs(camera));
            Assert.That(resolver.InteractionLayerMask.value, Is.EqualTo(layerMask.value));
            Assert.That(resolver.MaximumDistance, Is.EqualTo(25f).Within(Tolerance));
        }

        [Test]
        public void Constructor_WhenCameraIsNull_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(
                () => new TabletopObjectHitResolver(null, LayerMaskFor(InteractionLayer), 25f));
        }

        [Test]
        public void Constructor_WhenCameraIsPerspective_ThrowsArgumentException()
        {
            Camera camera = CreateCamera();
            camera.orthographic = false;

            Assert.Throws<ArgumentException>(
                () => new TabletopObjectHitResolver(camera, LayerMaskFor(InteractionLayer), 25f));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void Constructor_WhenDistanceIsNotPositive_ThrowsArgumentOutOfRangeException(float maximumDistance)
        {
            Camera camera = CreateCamera();

            Assert.Throws<ArgumentOutOfRangeException>(
                () => new TabletopObjectHitResolver(camera, LayerMaskFor(InteractionLayer), maximumDistance));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void Constructor_WhenDistanceIsNonFinite_ThrowsArgumentOutOfRangeException(float maximumDistance)
        {
            Camera camera = CreateCamera();

            Assert.Throws<ArgumentOutOfRangeException>(
                () => new TabletopObjectHitResolver(camera, LayerMaskFor(InteractionLayer), maximumDistance));
        }

        [Test]
        public void TryResolve_ResolvesBoundCardView()
        {
            Camera camera = CreateCamera();
            TabletopObjectHitResolver resolver = CreateResolver(camera);
            CardView cardView = CreateBoundCardView(1, new TableCoordinate(0.0, 0.0), out _);
            AddBoxCollider(cardView.gameObject, InteractionLayer);

            bool resolved = resolver.TryResolve(ScreenPointFor(camera, cardView.transform.position), out TabletopObjectView view);

            Assert.That(resolved, Is.True);
            Assert.That(view, Is.SameAs(cardView));
        }

        [Test]
        public void TryResolve_ResolvesBoundPawnView()
        {
            Camera camera = CreateCamera();
            TabletopObjectHitResolver resolver = CreateResolver(camera);
            PawnView pawnView = CreateBoundPawnView(2, new TableCoordinate(1.0, 0.0), out _);
            AddBoxCollider(pawnView.gameObject, InteractionLayer);

            bool resolved = resolver.TryResolve(ScreenPointFor(camera, pawnView.transform.position), out TabletopObjectView view);

            Assert.That(resolved, Is.True);
            Assert.That(view, Is.SameAs(pawnView));
        }

        [Test]
        public void TryResolve_ResolvesBoundTokenView()
        {
            Camera camera = CreateCamera();
            TabletopObjectHitResolver resolver = CreateResolver(camera);
            TokenView tokenView = CreateBoundTokenView(3, new TableCoordinate(-1.0, 0.0), out _);
            AddBoxCollider(tokenView.gameObject, InteractionLayer);

            bool resolved = resolver.TryResolve(ScreenPointFor(camera, tokenView.transform.position), out TabletopObjectView view);

            Assert.That(resolved, Is.True);
            Assert.That(view, Is.SameAs(tokenView));
        }

        [Test]
        public void TryResolve_ResolvesViewFromChildCollider()
        {
            Camera camera = CreateCamera();
            TabletopObjectHitResolver resolver = CreateResolver(camera);
            CardView cardView = CreateBoundCardView(4, new TableCoordinate(0.0, 0.0), out _);
            GameObject child = CreateChild("Child Collider", cardView.transform);
            AddBoxCollider(child, InteractionLayer);

            bool resolved = resolver.TryResolve(ScreenPointFor(camera, child.transform.position), out TabletopObjectView view);

            Assert.That(resolved, Is.True);
            Assert.That(view, Is.SameAs(cardView));
        }

        [Test]
        public void TryResolve_WhenNothingIsHit_ReturnsFalse()
        {
            Camera camera = CreateCamera();
            TabletopObjectHitResolver resolver = CreateResolver(camera);

            bool resolved = resolver.TryResolve(ScreenPointFor(camera, new Vector3(6f, 0f, 6f)), out TabletopObjectView view);

            Assert.That(resolved, Is.False);
            Assert.That(view, Is.Null);
        }

        [Test]
        public void TryResolve_WhenColliderHasNoTabletopObjectViewParent_ReturnsFalse()
        {
            Camera camera = CreateCamera();
            TabletopObjectHitResolver resolver = CreateResolver(camera);
            GameObject colliderObject = CreateGameObject("Loose Collider");
            colliderObject.transform.position = Vector3.zero;
            AddBoxCollider(colliderObject, InteractionLayer);

            bool resolved = resolver.TryResolve(ScreenPointFor(camera, colliderObject.transform.position), out TabletopObjectView view);

            Assert.That(resolved, Is.False);
            Assert.That(view, Is.Null);
        }

        [Test]
        public void TryResolve_WhenViewIsUnbound_ReturnsFalse()
        {
            Camera camera = CreateCamera();
            TabletopObjectHitResolver resolver = CreateResolver(camera);
            CardView view = CreateView<CardView>();
            AddBoxCollider(view.gameObject, InteractionLayer);

            bool resolved = resolver.TryResolve(ScreenPointFor(camera, view.transform.position), out TabletopObjectView resolvedView);

            Assert.That(resolved, Is.False);
            Assert.That(resolvedView, Is.Null);
        }

        [Test]
        public void TryResolve_WhenViewIsDisabled_ReturnsFalse()
        {
            Camera camera = CreateCamera();
            TabletopObjectHitResolver resolver = CreateResolver(camera);
            CardView cardView = CreateBoundCardView(5, new TableCoordinate(0.0, 0.0), out _);
            AddBoxCollider(cardView.gameObject, InteractionLayer);
            cardView.enabled = false;

            bool resolved = resolver.TryResolve(ScreenPointFor(camera, cardView.transform.position), out TabletopObjectView view);

            Assert.That(resolved, Is.False);
            Assert.That(view, Is.Null);
        }

        [Test]
        public void TryResolve_WhenViewIsInactive_ReturnsFalse()
        {
            Camera camera = CreateCamera();
            TabletopObjectHitResolver resolver = CreateResolver(camera);
            CardView cardView = CreateBoundCardView(6, new TableCoordinate(0.0, 0.0), out _);
            AddBoxCollider(cardView.gameObject, InteractionLayer);
            Vector3 hitPosition = cardView.transform.position;
            cardView.gameObject.SetActive(false);

            bool resolved = resolver.TryResolve(ScreenPointFor(camera, hitPosition), out TabletopObjectView view);

            Assert.That(resolved, Is.False);
            Assert.That(view, Is.Null);
        }

        [Test]
        public void TryResolve_IgnoresCollidersOutsideInteractionLayerMask()
        {
            Camera camera = CreateCamera();
            TabletopObjectHitResolver resolver = CreateResolver(camera);
            CardView cardView = CreateBoundCardView(7, new TableCoordinate(0.0, 0.0), out _);
            AddBoxCollider(cardView.gameObject, OtherLayer);

            bool resolved = resolver.TryResolve(ScreenPointFor(camera, cardView.transform.position), out TabletopObjectView view);

            Assert.That(resolved, Is.False);
            Assert.That(view, Is.Null);
        }

        [Test]
        public void TryResolve_RespectsMaximumDistance()
        {
            Camera camera = CreateCamera();
            TabletopObjectHitResolver resolver = new TabletopObjectHitResolver(camera, LayerMaskFor(InteractionLayer), 5f);
            CardView cardView = CreateBoundCardView(8, new TableCoordinate(0.0, 0.0), out _);
            AddBoxCollider(cardView.gameObject, InteractionLayer);

            bool resolved = resolver.TryResolve(ScreenPointFor(camera, cardView.transform.position), out TabletopObjectView view);

            Assert.That(resolved, Is.False);
            Assert.That(view, Is.Null);
        }

        [Test]
        public void TryResolve_SupportsTriggerColliders()
        {
            Camera camera = CreateCamera();
            TabletopObjectHitResolver resolver = CreateResolver(camera);
            CardView cardView = CreateBoundCardView(9, new TableCoordinate(0.0, 0.0), out _);
            BoxCollider collider = AddBoxCollider(cardView.gameObject, InteractionLayer);
            collider.isTrigger = true;

            bool resolved = resolver.TryResolve(ScreenPointFor(camera, cardView.transform.position), out TabletopObjectView view);

            Assert.That(resolved, Is.True);
            Assert.That(view, Is.SameAs(cardView));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void TryResolve_WhenScreenXIsNonFinite_ThrowsArgumentOutOfRangeException(float screenX)
        {
            Camera camera = CreateCamera();
            TabletopObjectHitResolver resolver = CreateResolver(camera);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => resolver.TryResolve(new Vector2(screenX, ScreenPointFor(camera, Vector3.zero).y), out _));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void TryResolve_WhenScreenYIsNonFinite_ThrowsArgumentOutOfRangeException(float screenY)
        {
            Camera camera = CreateCamera();
            TabletopObjectHitResolver resolver = CreateResolver(camera);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => resolver.TryResolve(new Vector2(ScreenPointFor(camera, Vector3.zero).x, screenY), out _));
        }

        [Test]
        public void TryResolve_DoesNotChangeViewTransform()
        {
            Camera camera = CreateCamera();
            TabletopObjectHitResolver resolver = CreateResolver(camera);
            CardView cardView = CreateBoundCardView(10, new TableCoordinate(2.0, -3.0), out _);
            cardView.transform.localScale = new Vector3(2f, 3f, 4f);
            AddBoxCollider(cardView.gameObject, InteractionLayer);
            Vector3 position = cardView.transform.position;
            Quaternion rotation = cardView.transform.rotation;
            Vector3 scale = cardView.transform.localScale;

            resolver.TryResolve(ScreenPointFor(camera, cardView.transform.position), out _);

            AssertVector3(cardView.transform.position, position);
            Assert.That(Quaternion.Angle(rotation, cardView.transform.rotation), Is.EqualTo(0f).Within(Tolerance));
            AssertVector3(cardView.transform.localScale, scale);
        }

        [Test]
        public void TryResolve_DoesNotMutateRuntimeState()
        {
            Camera camera = CreateCamera();
            TabletopObjectHitResolver resolver = CreateResolver(camera);
            CardView cardView = CreateBoundCardView(11, new TableCoordinate(2.0, -3.0), out CardInstanceState cardState);
            AddBoxCollider(cardView.gameObject, InteractionLayer);
            TabletopPose originalPose = cardState.BaseState.Pose;
            CardFace originalFace = cardState.Face;

            resolver.TryResolve(ScreenPointFor(camera, cardView.transform.position), out _);

            Assert.That(cardState.BaseState.Pose, Is.EqualTo(originalPose));
            Assert.That(cardState.Face, Is.EqualTo(originalFace));
        }

        [Test]
        public void TryResolve_DoesNotRequireTabletopSurfaceProxy()
        {
            Camera camera = CreateCamera();
            TabletopObjectHitResolver resolver = CreateResolver(camera);
            CardView cardView = CreateBoundCardView(12, new TableCoordinate(0.0, 0.0), out _);
            AddBoxCollider(cardView.gameObject, InteractionLayer);

            bool resolved = resolver.TryResolve(ScreenPointFor(camera, cardView.transform.position), out TabletopObjectView view);

            Assert.That(resolved, Is.True);
            Assert.That(view.GetComponent<TabletopSurfaceProxy>(), Is.Null);
            Assert.That(camera.GetComponent<TabletopSurfaceProxy>(), Is.Null);
        }

        [Test]
        public void TryResolve_DoesNotRequireTabletopPointerProjector()
        {
            Camera camera = CreateCamera();
            TabletopObjectHitResolver resolver = CreateResolver(camera);
            CardView cardView = CreateBoundCardView(13, new TableCoordinate(0.0, 0.0), out _);
            AddBoxCollider(cardView.gameObject, InteractionLayer);

            bool resolved = resolver.TryResolve(ScreenPointFor(camera, cardView.transform.position), out TabletopObjectView view);

            Assert.That(resolved, Is.True);
            Assert.That(view, Is.SameAs(cardView));
        }

        private Camera CreateCamera()
        {
            GameObject cameraObject = CreateGameObject("Object Hit Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.targetTexture = null;
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = 8f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.transform.SetPositionAndRotation(new Vector3(0f, 10f, 0f), Quaternion.Euler(90f, 0f, 0f));
            return camera;
        }

        private TabletopObjectHitResolver CreateResolver(Camera camera)
        {
            return new TabletopObjectHitResolver(camera, LayerMaskFor(InteractionLayer), 25f);
        }

        private CardView CreateBoundCardView(
            int seed,
            TableCoordinate coordinate,
            out CardInstanceState state)
        {
            CardView view = CreateView<CardView>();
            state = new CardInstanceState(CreateBaseState(seed, TabletopObjectKind.Card, coordinate), CardFace.FaceUp);
            view.Bind(state, CreateConverter());
            return view;
        }

        private PawnView CreateBoundPawnView(
            int seed,
            TableCoordinate coordinate,
            out PawnState state)
        {
            PawnView view = CreateView<PawnView>();
            state = new PawnState(CreateBaseState(seed, TabletopObjectKind.Pawn, coordinate));
            view.Bind(state, CreateConverter());
            return view;
        }

        private TokenView CreateBoundTokenView(
            int seed,
            TableCoordinate coordinate,
            out TokenState state)
        {
            TokenView view = CreateView<TokenView>();
            state = new TokenState(CreateBaseState(seed, TabletopObjectKind.Token, coordinate));
            view.Bind(state, CreateConverter());
            return view;
        }

        private T CreateView<T>()
            where T : TabletopObjectView
        {
            GameObject gameObject = CreateGameObject(typeof(T).Name);
            return gameObject.AddComponent<T>();
        }

        private GameObject CreateChild(string name, Transform parent)
        {
            GameObject child = CreateGameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = Vector3.zero;
            return child;
        }

        private GameObject CreateGameObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            createdGameObjects.Add(gameObject);
            return gameObject;
        }

        private static BoxCollider AddBoxCollider(GameObject gameObject, int layer)
        {
            gameObject.layer = layer;
            BoxCollider collider = gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(1f, 0.2f, 1f);
            return collider;
        }

        private static Vector2 ScreenPointFor(Camera camera, Vector3 worldPosition)
        {
            Physics.SyncTransforms();
            Vector3 screenPoint = camera.WorldToScreenPoint(worldPosition);
            Assert.That(float.IsFinite(screenPoint.x), Is.True);
            Assert.That(float.IsFinite(screenPoint.y), Is.True);
            return new Vector2(screenPoint.x, screenPoint.y);
        }

        private static LayerMask LayerMaskFor(int layer)
        {
            return 1 << layer;
        }

        private static TabletopObjectState CreateBaseState(
            int seed,
            TabletopObjectKind kind,
            TableCoordinate coordinate)
        {
            return new TabletopObjectState(
                new TabletopObjectId(GuidFromSeed(seed)),
                new ObjectDefinitionId(GuidFromSeed(seed + 1000)),
                kind,
                new TabletopPose(coordinate, 30f, 1, 2),
                ContainerId.Empty,
                PlayerId.Empty,
                ObjectVisibility.Public,
                false);
        }

        private static TabletopCoordinateConverter CreateConverter()
        {
            return new TabletopCoordinateConverter(1f, 0f, 0f, 0f);
        }

        private static Guid GuidFromSeed(int seed)
        {
            return new Guid(0, 0, 0, 0, 0, 0, 0, 0, 0, (byte)(seed / 256), (byte)(seed % 256));
        }

        private static void AssertVector3(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(Tolerance));
        }
    }
}
