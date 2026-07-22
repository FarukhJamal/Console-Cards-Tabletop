using System;
using System.Collections.Generic;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Coordinates;
using ConsoleCards.Presentation.Views;
using NUnit.Framework;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace ConsoleCards.Tests.PlayMode.Presentation
{
    public sealed class TabletopObjectViewBindingTests
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
        public void NewView_BeginsUnbound()
        {
            TestTabletopObjectView view = CreateView<TestTabletopObjectView>();

            Assert.That(view.IsBound, Is.False);
            Assert.That(view.BoundState, Is.Null);
        }

        [Test]
        public void NewView_ObjectIdIsEmpty()
        {
            TestTabletopObjectView view = CreateView<TestTabletopObjectView>();

            Assert.That(view.ObjectId, Is.EqualTo(TabletopObjectId.Empty));
        }

        [Test]
        public void ApplyAcceptedState_WhenUnbound_ThrowsInvalidOperationException()
        {
            TestTabletopObjectView view = CreateView<TestTabletopObjectView>();

            Assert.Throws<InvalidOperationException>(() => view.ApplyAcceptedState());
        }

        [Test]
        public void Bind_AppliesAcceptedWorldPosition()
        {
            CardView view = CreateView<CardView>();
            CardInstanceState state = CreateCardState(
                1,
                new TabletopPose(new TableCoordinate(2.5, -3.0), 0f, 0, 0));

            view.Bind(state, CreateConverter(worldUnitsPerTableUnit: 2f));

            AssertVector3(view.transform.position, 5f, 0f, -6f);
        }

        [Test]
        public void Bind_AppliesAcceptedWorldYFromLayerAndLocalOrderConfiguration()
        {
            CardView view = CreateView<CardView>();
            CardInstanceState state = CreateCardState(
                2,
                new TabletopPose(new TableCoordinate(1.0, 2.0), 0f, 3, 4));

            view.Bind(state, CreateConverter(baseHeight: 0.5f, layerHeight: 0.25f, localOrderHeight: 0.02f));

            Assert.That(view.transform.position.y, Is.EqualTo(1.33f).Within(Tolerance));
        }

        [Test]
        public void Bind_AppliesAcceptedYAxisRotation()
        {
            CardView view = CreateView<CardView>();
            CardInstanceState state = CreateCardState(
                3,
                new TabletopPose(TableCoordinate.Zero, 45f, 0, 0));

            view.Bind(state, CreateConverter());

            Assert.That(Quaternion.Angle(Quaternion.Euler(0f, 45f, 0f), view.transform.rotation), Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void Bind_DoesNotModifyScale()
        {
            CardView view = CreateView<CardView>();
            Vector3 originalScale = new Vector3(2f, 3f, 4f);
            view.transform.localScale = originalScale;

            view.Bind(CreateCardState(4), CreateConverter());

            AssertVector3(view.transform.localScale, originalScale.x, originalScale.y, originalScale.z);
        }

        [Test]
        public void Bind_DoesNotMutateRuntimeState()
        {
            CardView view = CreateView<CardView>();
            CardInstanceState state = CreateCardState(
                5,
                new TabletopPose(new TableCoordinate(2.0, 3.0), 15f, 1, 2));
            TabletopPose originalPose = state.BaseState.Pose;

            view.Bind(state, CreateConverter());

            Assert.That(state.BaseState.Pose, Is.EqualTo(originalPose));
        }

        [Test]
        public void ApplyAcceptedState_AfterRuntimePoseMutation_UpdatesView()
        {
            CardView view = CreateView<CardView>();
            CardInstanceState state = CreateCardState(6);
            view.Bind(state, CreateConverter(worldUnitsPerTableUnit: 2f));

            state.BaseState.SetPose(new TabletopPose(new TableCoordinate(-4.0, 5.0), 90f, 0, 0));
            view.ApplyAcceptedState();

            AssertVector3(view.transform.position, -8f, 0f, 10f);
            Assert.That(Quaternion.Angle(Quaternion.Euler(0f, 90f, 0f), view.transform.rotation), Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void MovingViewTransformManually_DoesNotMutateRuntimeState()
        {
            CardView view = CreateView<CardView>();
            CardInstanceState state = CreateCardState(
                7,
                new TabletopPose(new TableCoordinate(1.0, 2.0), 30f, 0, 0));
            TabletopPose acceptedPose = state.BaseState.Pose;
            view.Bind(state, CreateConverter());

            view.transform.SetPositionAndRotation(new Vector3(100f, 100f, 100f), Quaternion.Euler(0f, 120f, 0f));

            Assert.That(state.BaseState.Pose, Is.EqualTo(acceptedPose));
        }

        [Test]
        public void ApplyAcceptedState_RestoresManuallyChangedViewTransform()
        {
            CardView view = CreateView<CardView>();
            CardInstanceState state = CreateCardState(
                8,
                new TabletopPose(new TableCoordinate(3.0, -2.0), 60f, 0, 0));
            view.Bind(state, CreateConverter());

            view.transform.SetPositionAndRotation(new Vector3(99f, 99f, 99f), Quaternion.identity);
            view.ApplyAcceptedState();

            AssertVector3(view.transform.position, 3f, 0f, -2f);
            Assert.That(Quaternion.Angle(Quaternion.Euler(0f, 60f, 0f), view.transform.rotation), Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void Rebind_ReplacesPreviousState()
        {
            CardView view = CreateView<CardView>();
            CardInstanceState first = CreateCardState(9);
            CardInstanceState second = CreateCardState(
                10,
                new TabletopPose(new TableCoordinate(4.0, 5.0), 0f, 0, 0));

            view.Bind(first, CreateConverter());
            view.Bind(second, CreateConverter());

            Assert.That(view.CardState, Is.SameAs(second));
            Assert.That(view.BoundState, Is.SameAs(second.BaseState));
            Assert.That(view.ObjectId, Is.EqualTo(second.BaseState.Id));
            AssertVector3(view.transform.position, 4f, 0f, 5f);
        }

        [Test]
        public void FailedRebind_PreservesPreviousBindingAndTransform()
        {
            TestTabletopObjectView view = CreateView<TestTabletopObjectView>();
            TabletopObjectState first = CreateBaseState(
                11,
                TabletopObjectKind.Card,
                new TabletopPose(new TableCoordinate(2.0, 3.0), 15f, 0, 0));
            TabletopObjectState wrongKind = CreateBaseState(12, TabletopObjectKind.Pawn);
            view.BindForTest(first, CreateConverter(), TabletopObjectKind.Card);
            Vector3 acceptedPosition = view.transform.position;
            Quaternion acceptedRotation = view.transform.rotation;

            Assert.Throws<ArgumentException>(() => view.BindForTest(wrongKind, CreateConverter(), TabletopObjectKind.Card));

            Assert.That(view.BoundState, Is.SameAs(first));
            Assert.That(view.ObjectId, Is.EqualTo(first.Id));
            AssertVector3(view.transform.position, acceptedPosition.x, acceptedPosition.y, acceptedPosition.z);
            Assert.That(Quaternion.Angle(acceptedRotation, view.transform.rotation), Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void Unbind_ClearsStateAndObjectId()
        {
            CardView view = CreateView<CardView>();
            view.Bind(CreateCardState(13), CreateConverter());

            view.Unbind();

            Assert.That(view.IsBound, Is.False);
            Assert.That(view.BoundState, Is.Null);
            Assert.That(view.ObjectId, Is.EqualTo(TabletopObjectId.Empty));
        }

        [Test]
        public void Unbind_PreservesTransform()
        {
            CardView view = CreateView<CardView>();
            view.Bind(
                CreateCardState(14, new TabletopPose(new TableCoordinate(-2.0, 6.0), 35f, 0, 0)),
                CreateConverter());
            Vector3 acceptedPosition = view.transform.position;
            Quaternion acceptedRotation = view.transform.rotation;
            Vector3 acceptedScale = view.transform.localScale;

            view.Unbind();

            AssertVector3(view.transform.position, acceptedPosition.x, acceptedPosition.y, acceptedPosition.z);
            Assert.That(Quaternion.Angle(acceptedRotation, view.transform.rotation), Is.EqualTo(0f).Within(Tolerance));
            AssertVector3(view.transform.localScale, acceptedScale.x, acceptedScale.y, acceptedScale.z);
        }

        [Test]
        public void ApplyAcceptedState_AfterUnbind_ThrowsInvalidOperationException()
        {
            CardView view = CreateView<CardView>();
            view.Bind(CreateCardState(15), CreateConverter());
            view.Unbind();

            Assert.Throws<InvalidOperationException>(() => view.ApplyAcceptedState());
        }

        [Test]
        public void CardView_BindsCardInstanceState()
        {
            CardView view = CreateView<CardView>();
            CardInstanceState state = CreateCardState(16);

            view.Bind(state, CreateConverter());

            Assert.That(view.IsBound, Is.True);
        }

        [Test]
        public void CardView_ExposesCardState()
        {
            CardView view = CreateView<CardView>();
            CardInstanceState state = CreateCardState(17);

            view.Bind(state, CreateConverter());

            Assert.That(view.CardState, Is.SameAs(state));
        }

        [Test]
        public void CardView_ExposesBaseStateThroughBoundState()
        {
            CardView view = CreateView<CardView>();
            CardInstanceState state = CreateCardState(18);

            view.Bind(state, CreateConverter());

            Assert.That(view.BoundState, Is.SameAs(state.BaseState));
        }

        [Test]
        public void CardView_WhenStateIsNull_ThrowsArgumentNullException()
        {
            CardView view = CreateView<CardView>();

            Assert.Throws<ArgumentNullException>(() => view.Bind(null, CreateConverter()));
        }

        [Test]
        public void CardView_RebindingReplacesCardState()
        {
            CardView view = CreateView<CardView>();
            CardInstanceState first = CreateCardState(19);
            CardInstanceState second = CreateCardState(20);

            view.Bind(first, CreateConverter());
            view.Bind(second, CreateConverter());

            Assert.That(view.CardState, Is.SameAs(second));
        }

        [Test]
        public void CardView_UnbindClearsCardState()
        {
            CardView view = CreateView<CardView>();
            view.Bind(CreateCardState(21), CreateConverter());

            view.Unbind();

            Assert.That(view.CardState, Is.Null);
        }

        [Test]
        public void CardView_BindDoesNotChangeCardFace()
        {
            CardView view = CreateView<CardView>();
            CardInstanceState state = CreateCardState(22, face: CardFace.FaceDown);

            view.Bind(state, CreateConverter());

            Assert.That(state.Face, Is.EqualTo(CardFace.FaceDown));
        }

        [Test]
        public void PawnView_BindsPawnState()
        {
            PawnView view = CreateView<PawnView>();
            PawnState state = CreatePawnState(23);

            view.Bind(state, CreateConverter());

            Assert.That(view.IsBound, Is.True);
            Assert.That(view.PawnState, Is.SameAs(state));
        }

        [Test]
        public void PawnView_ExposesPawnStateAndBoundState()
        {
            PawnView view = CreateView<PawnView>();
            PawnState state = CreatePawnState(24);

            view.Bind(state, CreateConverter());

            Assert.That(view.PawnState, Is.SameAs(state));
            Assert.That(view.BoundState, Is.SameAs(state.BaseState));
        }

        [Test]
        public void PawnView_WhenStateIsNull_ThrowsArgumentNullException()
        {
            PawnView view = CreateView<PawnView>();

            Assert.Throws<ArgumentNullException>(() => view.Bind(null, CreateConverter()));
        }

        [Test]
        public void PawnView_RebindsCorrectly()
        {
            PawnView view = CreateView<PawnView>();
            PawnState first = CreatePawnState(25);
            PawnState second = CreatePawnState(
                26,
                new TabletopPose(new TableCoordinate(-3.0, -4.0), 0f, 0, 0));

            view.Bind(first, CreateConverter());
            view.Bind(second, CreateConverter());

            Assert.That(view.PawnState, Is.SameAs(second));
            AssertVector3(view.transform.position, -3f, 0f, -4f);
        }

        [Test]
        public void PawnView_UnbindClearsPawnState()
        {
            PawnView view = CreateView<PawnView>();
            view.Bind(CreatePawnState(27), CreateConverter());

            view.Unbind();

            Assert.That(view.PawnState, Is.Null);
        }

        [Test]
        public void TokenView_BindsTokenState()
        {
            TokenView view = CreateView<TokenView>();
            TokenState state = CreateTokenState(28);

            view.Bind(state, CreateConverter());

            Assert.That(view.IsBound, Is.True);
            Assert.That(view.TokenState, Is.SameAs(state));
        }

        [Test]
        public void TokenView_ExposesTokenStateAndBoundState()
        {
            TokenView view = CreateView<TokenView>();
            TokenState state = CreateTokenState(29);

            view.Bind(state, CreateConverter());

            Assert.That(view.TokenState, Is.SameAs(state));
            Assert.That(view.BoundState, Is.SameAs(state.BaseState));
        }

        [Test]
        public void TokenView_WhenStateIsNull_ThrowsArgumentNullException()
        {
            TokenView view = CreateView<TokenView>();

            Assert.Throws<ArgumentNullException>(() => view.Bind(null, CreateConverter()));
        }

        [Test]
        public void TokenView_RebindsCorrectly()
        {
            TokenView view = CreateView<TokenView>();
            TokenState first = CreateTokenState(30);
            TokenState second = CreateTokenState(
                31,
                new TabletopPose(new TableCoordinate(6.0, -7.0), 0f, 0, 0));

            view.Bind(first, CreateConverter());
            view.Bind(second, CreateConverter());

            Assert.That(view.TokenState, Is.SameAs(second));
            AssertVector3(view.transform.position, 6f, 0f, -7f);
        }

        [Test]
        public void TokenView_UnbindClearsTokenState()
        {
            TokenView view = CreateView<TokenView>();
            view.Bind(CreateTokenState(32), CreateConverter());

            view.Unbind();

            Assert.That(view.TokenState, Is.Null);
        }

        [Test]
        public void Bind_WhenConverterIsNull_ThrowsArgumentNullException()
        {
            CardView view = CreateView<CardView>();

            Assert.Throws<ArgumentNullException>(() => view.Bind(CreateCardState(33), null));
        }

        [Test]
        public void BindBase_WhenExpectedKindDoesNotMatch_ThrowsArgumentException()
        {
            TestTabletopObjectView view = CreateView<TestTabletopObjectView>();
            TabletopObjectState state = CreateBaseState(34, TabletopObjectKind.Token);

            Assert.Throws<ArgumentException>(() => view.BindForTest(state, CreateConverter(), TabletopObjectKind.Card));
        }

        [Test]
        public void FailedBindingDueToInvalidPose_PreservesPreviousBindingAndTransform()
        {
            TestTabletopObjectView view = CreateView<TestTabletopObjectView>();
            TabletopObjectState first = CreateBaseState(
                35,
                TabletopObjectKind.Card,
                new TabletopPose(new TableCoordinate(1.0, 2.0), 0f, 0, 0));
            TabletopObjectState invalidPoseState = CreateBaseState(
                36,
                TabletopObjectKind.Card,
                new TabletopPose(new TableCoordinate(double.NaN, 2.0), 0f, 0, 0));
            view.BindForTest(first, CreateConverter(), TabletopObjectKind.Card);
            Vector3 acceptedPosition = view.transform.position;

            Assert.Throws<ArgumentOutOfRangeException>(() => view.BindForTest(invalidPoseState, CreateConverter(), TabletopObjectKind.Card));

            Assert.That(view.BoundState, Is.SameAs(first));
            Assert.That(view.ObjectId, Is.EqualTo(first.Id));
            AssertVector3(view.transform.position, acceptedPosition.x, acceptedPosition.y, acceptedPosition.z);
        }

        [Test]
        public void EmptyObjectIdState_CannotBeConstructedThroughPublicCoreContract()
        {
            Assert.Throws<ArgumentException>(
                () => CreateBaseState(37, TabletopObjectKind.Card, objectId: TabletopObjectId.Empty));
        }

        [Test]
        public void EmptyDefinitionIdState_CannotBeConstructedThroughPublicCoreContract()
        {
            Assert.Throws<ArgumentException>(
                () => CreateBaseState(38, TabletopObjectKind.Card, definitionId: ObjectDefinitionId.Empty));
        }

        private T CreateView<T>()
            where T : TabletopObjectView
        {
            GameObject gameObject = new GameObject(typeof(T).Name);
            createdGameObjects.Add(gameObject);
            return gameObject.AddComponent<T>();
        }

        private static CardInstanceState CreateCardState(
            int seed,
            TabletopPose? pose = null,
            CardFace face = CardFace.FaceUp)
        {
            return new CardInstanceState(CreateBaseState(seed, TabletopObjectKind.Card, pose), face);
        }

        private static PawnState CreatePawnState(
            int seed,
            TabletopPose? pose = null)
        {
            return new PawnState(CreateBaseState(seed, TabletopObjectKind.Pawn, pose));
        }

        private static TokenState CreateTokenState(
            int seed,
            TabletopPose? pose = null)
        {
            return new TokenState(CreateBaseState(seed, TabletopObjectKind.Token, pose));
        }

        private static TabletopObjectState CreateBaseState(
            int seed,
            TabletopObjectKind kind,
            TabletopPose? pose = null,
            TabletopObjectId? objectId = null,
            ObjectDefinitionId? definitionId = null)
        {
            return new TabletopObjectState(
                objectId ?? new TabletopObjectId(GuidFromSeed(seed)),
                definitionId ?? new ObjectDefinitionId(GuidFromSeed(seed + 1000)),
                kind,
                pose ?? TabletopPose.Default,
                ContainerId.Empty,
                PlayerId.Empty,
                ObjectVisibility.Public,
                false);
        }

        private static TabletopCoordinateConverter CreateConverter(
            float worldUnitsPerTableUnit = 1f,
            float baseHeight = 0f,
            float layerHeight = 0f,
            float localOrderHeight = 0f)
        {
            return new TabletopCoordinateConverter(
                worldUnitsPerTableUnit,
                baseHeight,
                layerHeight,
                localOrderHeight);
        }

        private static Guid GuidFromSeed(int seed)
        {
            return new Guid(0, 0, 0, 0, 0, 0, 0, 0, 0, (byte)(seed / 256), (byte)(seed % 256));
        }

        private static void AssertVector3(Vector3 actual, float expectedX, float expectedY, float expectedZ)
        {
            Assert.That(actual.x, Is.EqualTo(expectedX).Within(Tolerance));
            Assert.That(actual.y, Is.EqualTo(expectedY).Within(Tolerance));
            Assert.That(actual.z, Is.EqualTo(expectedZ).Within(Tolerance));
        }

        private sealed class TestTabletopObjectView : TabletopObjectView
        {
            public void BindForTest(
                TabletopObjectState state,
                TabletopCoordinateConverter converter,
                TabletopObjectKind expectedKind)
            {
                BindBase(state, converter, expectedKind);
            }
        }
    }
}
