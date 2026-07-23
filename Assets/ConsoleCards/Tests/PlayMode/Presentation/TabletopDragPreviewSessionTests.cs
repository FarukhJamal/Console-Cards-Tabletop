using System;
using System.Collections.Generic;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Coordinates;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.Views;
using NUnit.Framework;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace ConsoleCards.Tests.PlayMode.Presentation
{
    public sealed class TabletopDragPreviewSessionTests
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
        public void NewBoundView_IsNotPreviewing()
        {
            CardView view = CreateBoundCardView(1, out _);

            Assert.That(view.IsPreviewing, Is.False);
        }

        [Test]
        public void PreviewPose_WhenNotPreviewing_ReturnsDefault()
        {
            CardView view = CreateBoundCardView(2, out _);

            Assert.That(view.PreviewPose, Is.EqualTo(TabletopPose.Default));
        }

        [Test]
        public void ApplyPreviewPose_MovesTransform()
        {
            CardView view = CreateBoundCardView(3, out _);

            view.ApplyPreviewPose(new TabletopPose(new TableCoordinate(4.0, -5.0), 0f, 0, 0));

            AssertVector3(view.transform.position, 4f, 0f, -5f);
            Assert.That(view.IsPreviewing, Is.True);
        }

        [Test]
        public void ApplyPreviewPose_AppliesRotation()
        {
            CardView view = CreateBoundCardView(4, out _);

            view.ApplyPreviewPose(new TabletopPose(TableCoordinate.Zero, 75f, 0, 0));

            Assert.That(Quaternion.Angle(Quaternion.Euler(0f, 75f, 0f), view.transform.rotation), Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void ApplyPreviewPose_AppliesLayerAndLocalOrderHeight()
        {
            CardView view = CreateBoundCardView(
                5,
                out _,
                converter: CreateConverter(baseHeight: 0.5f, layerHeight: 0.25f, localOrderHeight: 0.02f));

            view.ApplyPreviewPose(new TabletopPose(TableCoordinate.Zero, 0f, 3, 4));

            Assert.That(view.transform.position.y, Is.EqualTo(1.33f).Within(Tolerance));
        }

        [Test]
        public void ApplyPreviewPose_PreservesScale()
        {
            CardView view = CreateBoundCardView(6, out _);
            Vector3 originalScale = new Vector3(2f, 3f, 4f);
            view.transform.localScale = originalScale;

            view.ApplyPreviewPose(new TabletopPose(new TableCoordinate(1.0, 2.0), 30f, 0, 0));

            AssertVector3(view.transform.localScale, originalScale);
        }

        [Test]
        public void ApplyPreviewPose_DoesNotMutateBoundStatePose()
        {
            CardView view = CreateBoundCardView(7, out CardInstanceState state);
            TabletopPose acceptedPose = state.BaseState.Pose;

            view.ApplyPreviewPose(new TabletopPose(new TableCoordinate(8.0, 9.0), 45f, 2, 3));

            Assert.That(state.BaseState.Pose, Is.EqualTo(acceptedPose));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void ApplyPreviewPose_WhenPositionXIsInvalid_PreservesTransformAndPreviewState(double x)
        {
            CardView view = CreateBoundCardView(8, out _);
            TabletopPose validPreview = new TabletopPose(new TableCoordinate(1.0, 2.0), 15f, 0, 0);
            view.ApplyPreviewPose(validPreview);
            Vector3 previousPosition = view.transform.position;
            Quaternion previousRotation = view.transform.rotation;

            Assert.Throws<ArgumentOutOfRangeException>(
                () => view.ApplyPreviewPose(new TabletopPose(new TableCoordinate(x, 3.0), 30f, 0, 0)));

            Assert.That(view.IsPreviewing, Is.True);
            Assert.That(view.PreviewPose, Is.EqualTo(validPreview));
            AssertVector3(view.transform.position, previousPosition);
            Assert.That(Quaternion.Angle(previousRotation, view.transform.rotation), Is.EqualTo(0f).Within(Tolerance));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void ApplyPreviewPose_WhenPositionYIsInvalid_PreservesTransformAndPreviewState(double y)
        {
            CardView view = CreateBoundCardView(9, out _);
            TabletopPose validPreview = new TabletopPose(new TableCoordinate(1.0, 2.0), 15f, 0, 0);
            view.ApplyPreviewPose(validPreview);
            Vector3 previousPosition = view.transform.position;

            Assert.Throws<ArgumentOutOfRangeException>(
                () => view.ApplyPreviewPose(new TabletopPose(new TableCoordinate(3.0, y), 30f, 0, 0)));

            Assert.That(view.IsPreviewing, Is.True);
            Assert.That(view.PreviewPose, Is.EqualTo(validPreview));
            AssertVector3(view.transform.position, previousPosition);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void ApplyPreviewPose_WhenRotationIsInvalid_PreservesTransformAndPreviewState(float rotation)
        {
            CardView view = CreateBoundCardView(10, out _);
            TabletopPose validPreview = new TabletopPose(new TableCoordinate(1.0, 2.0), 15f, 0, 0);
            view.ApplyPreviewPose(validPreview);
            Quaternion previousRotation = view.transform.rotation;

            Assert.Throws<ArgumentOutOfRangeException>(
                () => view.ApplyPreviewPose(new TabletopPose(new TableCoordinate(3.0, 4.0), rotation, 0, 0)));

            Assert.That(view.IsPreviewing, Is.True);
            Assert.That(view.PreviewPose, Is.EqualTo(validPreview));
            Assert.That(Quaternion.Angle(previousRotation, view.transform.rotation), Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void ApplyAcceptedState_ClearsActivePreview()
        {
            CardView view = CreateBoundCardView(11, out _);
            view.ApplyPreviewPose(new TabletopPose(new TableCoordinate(4.0, 5.0), 90f, 0, 0));

            view.ApplyAcceptedState();

            Assert.That(view.IsPreviewing, Is.False);
            Assert.That(view.PreviewPose, Is.EqualTo(TabletopPose.Default));
        }

        [Test]
        public void ReconcileAcceptedState_RestoresAcceptedPose()
        {
            TabletopPose acceptedPose = new TabletopPose(new TableCoordinate(2.0, -3.0), 35f, 0, 0);
            CardView view = CreateBoundCardView(12, out _, acceptedPose);
            view.ApplyPreviewPose(new TabletopPose(new TableCoordinate(8.0, 9.0), 120f, 0, 0));

            view.ReconcileAcceptedState();

            AssertWorldPose(view, acceptedPose);
        }

        [Test]
        public void ReconcileAcceptedState_ClearsPreviewState()
        {
            CardView view = CreateBoundCardView(13, out _);
            view.ApplyPreviewPose(new TabletopPose(new TableCoordinate(8.0, 9.0), 120f, 0, 0));

            view.ReconcileAcceptedState();

            Assert.That(view.IsPreviewing, Is.False);
            Assert.That(view.PreviewPose, Is.EqualTo(TabletopPose.Default));
        }

        [Test]
        public void Unbind_ClearsPreviewState()
        {
            CardView view = CreateBoundCardView(14, out _);
            view.ApplyPreviewPose(new TabletopPose(new TableCoordinate(8.0, 9.0), 120f, 0, 0));

            view.Unbind();

            Assert.That(view.IsPreviewing, Is.False);
            Assert.That(view.PreviewPose, Is.EqualTo(TabletopPose.Default));
        }

        [Test]
        public void ApplyPreviewPose_WhenUnbound_ThrowsInvalidOperationException()
        {
            CardView view = CreateView<CardView>();

            Assert.Throws<InvalidOperationException>(
                () => view.ApplyPreviewPose(new TabletopPose(new TableCoordinate(1.0, 2.0), 0f, 0, 0)));
        }

        [Test]
        public void NewSession_IsInactive()
        {
            TabletopDragPreviewSession session = new TabletopDragPreviewSession();

            Assert.That(session.IsActive, Is.False);
            Assert.That(session.ActiveView, Is.Null);
        }

        [Test]
        public void NewSession_ActiveObjectIdIsEmpty()
        {
            TabletopDragPreviewSession session = new TabletopDragPreviewSession();

            Assert.That(session.ActiveObjectId, Is.EqualTo(TabletopObjectId.Empty));
        }

        [Test]
        public void NewSession_CurrentPreviewPoseIsDefault()
        {
            TabletopDragPreviewSession session = new TabletopDragPreviewSession();

            Assert.That(session.CurrentPreviewPose, Is.EqualTo(TabletopPose.Default));
        }

        [Test]
        public void Begin_AcceptsBoundCardView()
        {
            TabletopDragPreviewSession session = new TabletopDragPreviewSession();
            CardView view = CreateBoundCardView(15, out _);

            session.Begin(view);

            Assert.That(session.IsActive, Is.True);
            Assert.That(session.ActiveView, Is.SameAs(view));
            Assert.That(session.ActiveObjectId, Is.EqualTo(view.ObjectId));
        }

        [Test]
        public void Begin_AcceptsBoundPawnView()
        {
            TabletopDragPreviewSession session = new TabletopDragPreviewSession();
            PawnView view = CreateBoundPawnView(16, out _);

            session.Begin(view);

            Assert.That(session.ActiveView, Is.SameAs(view));
        }

        [Test]
        public void Begin_AcceptsBoundTokenView()
        {
            TabletopDragPreviewSession session = new TabletopDragPreviewSession();
            TokenView view = CreateBoundTokenView(17, out _);

            session.Begin(view);

            Assert.That(session.ActiveView, Is.SameAs(view));
        }

        [Test]
        public void Begin_DoesNotMoveView()
        {
            TabletopDragPreviewSession session = new TabletopDragPreviewSession();
            CardView view = CreateBoundCardView(18, out _);
            Vector3 position = view.transform.position;
            Quaternion rotation = view.transform.rotation;

            session.Begin(view);

            AssertVector3(view.transform.position, position);
            Assert.That(Quaternion.Angle(rotation, view.transform.rotation), Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void Begin_DoesNotMutateRuntimeState()
        {
            TabletopDragPreviewSession session = new TabletopDragPreviewSession();
            CardView view = CreateBoundCardView(19, out CardInstanceState state);
            TabletopPose acceptedPose = state.BaseState.Pose;

            session.Begin(view);

            Assert.That(state.BaseState.Pose, Is.EqualTo(acceptedPose));
        }

        [Test]
        public void Begin_WhenViewIsNull_ThrowsArgumentNullException()
        {
            TabletopDragPreviewSession session = new TabletopDragPreviewSession();

            Assert.Throws<ArgumentNullException>(() => session.Begin(null));
        }

        [Test]
        public void Begin_WhenViewIsUnbound_ThrowsArgumentException()
        {
            TabletopDragPreviewSession session = new TabletopDragPreviewSession();
            CardView view = CreateView<CardView>();

            Assert.Throws<ArgumentException>(() => session.Begin(view));
        }

        [Test]
        public void Begin_WhenAnotherViewIsActive_ThrowsInvalidOperationException()
        {
            TabletopDragPreviewSession session = new TabletopDragPreviewSession();
            CardView first = CreateBoundCardView(20, out _);
            CardView second = CreateBoundCardView(21, out _);
            session.Begin(first);

            Assert.Throws<InvalidOperationException>(() => session.Begin(second));
            Assert.That(session.ActiveView, Is.SameAs(first));
        }

        [Test]
        public void Begin_WhenViewAlreadyPreviewing_ThrowsInvalidOperationException()
        {
            TabletopDragPreviewSession session = new TabletopDragPreviewSession();
            CardView view = CreateBoundCardView(22, out _);
            view.ApplyPreviewPose(new TabletopPose(new TableCoordinate(1.0, 2.0), 0f, 0, 0));

            Assert.Throws<InvalidOperationException>(() => session.Begin(view));
        }

        [Test]
        public void UpdatePosition_MovesActiveView()
        {
            TabletopDragPreviewSession session = BeginCardSession(23, out CardView view, out _);

            session.UpdatePosition(new TableCoordinate(6.0, -7.0));

            AssertVector3(view.transform.position, 6f, 0f, -7f);
        }

        [Test]
        public void UpdatePosition_PreservesAcceptedRotation()
        {
            TabletopPose acceptedPose = new TabletopPose(new TableCoordinate(0.0, 0.0), 45f, 1, 2);
            TabletopDragPreviewSession session = BeginCardSession(24, out CardView view, out _, acceptedPose);

            session.UpdatePosition(new TableCoordinate(3.0, 4.0));

            Assert.That(session.CurrentPreviewPose.RotationDegrees, Is.EqualTo(45f).Within(Tolerance));
            Assert.That(Quaternion.Angle(Quaternion.Euler(0f, 45f, 0f), view.transform.rotation), Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void UpdatePosition_PreservesAcceptedLayer()
        {
            TabletopPose acceptedPose = new TabletopPose(new TableCoordinate(0.0, 0.0), 45f, 3, 2);
            TabletopDragPreviewSession session = BeginCardSession(
                25,
                out CardView view,
                out _,
                acceptedPose,
                CreateConverter(baseHeight: 0.5f, layerHeight: 0.25f, localOrderHeight: 0.02f));

            session.UpdatePosition(new TableCoordinate(3.0, 4.0));

            Assert.That(session.CurrentPreviewPose.Layer, Is.EqualTo(3));
            Assert.That(view.transform.position.y, Is.EqualTo(1.29f).Within(Tolerance));
        }

        [Test]
        public void UpdatePosition_PreservesAcceptedLocalOrder()
        {
            TabletopPose acceptedPose = new TabletopPose(new TableCoordinate(0.0, 0.0), 45f, 3, 5);
            TabletopDragPreviewSession session = BeginCardSession(
                26,
                out _,
                out _,
                acceptedPose,
                CreateConverter(baseHeight: 0.5f, layerHeight: 0.25f, localOrderHeight: 0.02f));

            session.UpdatePosition(new TableCoordinate(3.0, 4.0));

            Assert.That(session.CurrentPreviewPose.LocalOrder, Is.EqualTo(5));
        }

        [Test]
        public void UpdatePosition_DoesNotMutateAcceptedPose()
        {
            TabletopDragPreviewSession session = BeginCardSession(27, out _, out CardInstanceState state);
            TabletopPose acceptedPose = state.BaseState.Pose;

            session.UpdatePosition(new TableCoordinate(3.0, 4.0));

            Assert.That(state.BaseState.Pose, Is.EqualTo(acceptedPose));
        }

        [Test]
        public void UpdatePosition_RepeatedUpdatesReplaceTemporaryPreview()
        {
            TabletopDragPreviewSession session = BeginCardSession(28, out CardView view, out _);

            session.UpdatePosition(new TableCoordinate(3.0, 4.0));
            session.UpdatePosition(new TableCoordinate(-5.0, 6.0));

            Assert.That(session.CurrentPreviewPose.Position, Is.EqualTo(new TableCoordinate(-5.0, 6.0)));
            AssertVector3(view.transform.position, -5f, 0f, 6f);
        }

        [Test]
        public void UpdatePosition_UsesAcceptedPoseRatherThanPreviousPreview()
        {
            TabletopPose acceptedPose = new TabletopPose(new TableCoordinate(0.0, 0.0), 30f, 1, 2);
            TabletopDragPreviewSession session = BeginCardSession(29, out _, out _, acceptedPose);

            session.UpdatePose(new TabletopPose(new TableCoordinate(5.0, 6.0), 120f, 9, 10));
            session.UpdatePosition(new TableCoordinate(-2.0, -3.0));

            Assert.That(session.CurrentPreviewPose.RotationDegrees, Is.EqualTo(30f).Within(Tolerance));
            Assert.That(session.CurrentPreviewPose.Layer, Is.EqualTo(1));
            Assert.That(session.CurrentPreviewPose.LocalOrder, Is.EqualTo(2));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void UpdatePosition_WhenCoordinateXIsNonFinite_ThrowsWithoutMutation(double x)
        {
            TabletopDragPreviewSession session = BeginCardSession(30, out CardView view, out _);
            session.UpdatePosition(new TableCoordinate(1.0, 2.0));
            TabletopPose previousPreview = session.CurrentPreviewPose;
            Vector3 previousPosition = view.transform.position;

            Assert.Throws<ArgumentOutOfRangeException>(() => session.UpdatePosition(new TableCoordinate(x, 3.0)));

            Assert.That(session.CurrentPreviewPose, Is.EqualTo(previousPreview));
            AssertVector3(view.transform.position, previousPosition);
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void UpdatePosition_WhenCoordinateYIsNonFinite_ThrowsWithoutMutation(double y)
        {
            TabletopDragPreviewSession session = BeginCardSession(31, out CardView view, out _);
            session.UpdatePosition(new TableCoordinate(1.0, 2.0));
            Vector3 previousPosition = view.transform.position;

            Assert.Throws<ArgumentOutOfRangeException>(() => session.UpdatePosition(new TableCoordinate(3.0, y)));

            AssertVector3(view.transform.position, previousPosition);
        }

        [Test]
        public void UpdatePosition_WithoutBegin_ThrowsInvalidOperationException()
        {
            TabletopDragPreviewSession session = new TabletopDragPreviewSession();

            Assert.Throws<InvalidOperationException>(() => session.UpdatePosition(new TableCoordinate(1.0, 2.0)));
        }

        [Test]
        public void UpdatePose_AppliesCoordinate()
        {
            TabletopDragPreviewSession session = BeginCardSession(32, out CardView view, out _);

            session.UpdatePose(new TabletopPose(new TableCoordinate(7.0, 8.0), 0f, 0, 0));

            AssertVector3(view.transform.position, 7f, 0f, 8f);
        }

        [Test]
        public void UpdatePose_AppliesRotation()
        {
            TabletopDragPreviewSession session = BeginCardSession(33, out CardView view, out _);

            session.UpdatePose(new TabletopPose(TableCoordinate.Zero, -45f, 0, 0));

            Assert.That(Quaternion.Angle(Quaternion.Euler(0f, -45f, 0f), view.transform.rotation), Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void UpdatePose_AppliesLayerAndLocalOrder()
        {
            TabletopDragPreviewSession session = BeginCardSession(
                34,
                out CardView view,
                out _,
                converter: CreateConverter(baseHeight: 0.5f, layerHeight: 0.25f, localOrderHeight: 0.02f));

            session.UpdatePose(new TabletopPose(TableCoordinate.Zero, 0f, 2, 3));

            Assert.That(view.transform.position.y, Is.EqualTo(1.06f).Within(Tolerance));
        }

        [Test]
        public void UpdatePose_DoesNotMutateRuntimeState()
        {
            TabletopDragPreviewSession session = BeginCardSession(35, out _, out CardInstanceState state);
            TabletopPose acceptedPose = state.BaseState.Pose;

            session.UpdatePose(new TabletopPose(new TableCoordinate(9.0, 10.0), 90f, 2, 3));

            Assert.That(state.BaseState.Pose, Is.EqualTo(acceptedPose));
        }

        [Test]
        public void UpdatePose_WhenInvalid_PreservesPreviousPreviewAndTransform()
        {
            TabletopDragPreviewSession session = BeginCardSession(36, out CardView view, out _);
            TabletopPose validPreview = new TabletopPose(new TableCoordinate(1.0, 2.0), 10f, 0, 0);
            session.UpdatePose(validPreview);
            Vector3 previousPosition = view.transform.position;

            Assert.Throws<ArgumentOutOfRangeException>(
                () => session.UpdatePose(new TabletopPose(new TableCoordinate(double.NaN, 2.0), 20f, 0, 0)));

            Assert.That(session.CurrentPreviewPose, Is.EqualTo(validPreview));
            AssertVector3(view.transform.position, previousPosition);
        }

        [Test]
        public void UpdatePose_WithoutBegin_ThrowsInvalidOperationException()
        {
            TabletopDragPreviewSession session = new TabletopDragPreviewSession();

            Assert.Throws<InvalidOperationException>(
                () => session.UpdatePose(new TabletopPose(new TableCoordinate(1.0, 2.0), 0f, 0, 0)));
        }

        [Test]
        public void ReconcileAndEnd_RestoresPreviousAcceptedPoseWhenRuntimeStateDidNotChange()
        {
            TabletopPose acceptedPose = new TabletopPose(new TableCoordinate(2.0, 3.0), 25f, 0, 0);
            TabletopDragPreviewSession session = BeginCardSession(37, out CardView view, out _, acceptedPose);
            session.UpdatePose(new TabletopPose(new TableCoordinate(9.0, 10.0), 90f, 0, 0));

            session.ReconcileAndEnd();

            AssertWorldPose(view, acceptedPose);
        }

        [Test]
        public void ReconcileAndEnd_AppliesNewlyAcceptedPoseWhenRuntimeStateChanged()
        {
            TabletopDragPreviewSession session = BeginCardSession(38, out CardView view, out CardInstanceState state);
            TabletopPose acceptedPose = new TabletopPose(new TableCoordinate(-4.0, 5.0), 80f, 1, 2);
            session.UpdatePose(new TabletopPose(new TableCoordinate(9.0, 10.0), 90f, 0, 0));
            state.BaseState.SetPose(acceptedPose);

            session.ReconcileAndEnd();

            AssertWorldPose(view, acceptedPose);
        }

        [Test]
        public void ReconcileAndEnd_ClearsSession()
        {
            TabletopDragPreviewSession session = BeginCardSession(39, out _, out _);
            session.UpdatePosition(new TableCoordinate(3.0, 4.0));

            session.ReconcileAndEnd();

            Assert.That(session.IsActive, Is.False);
            Assert.That(session.ActiveView, Is.Null);
            Assert.That(session.ActiveObjectId, Is.EqualTo(TabletopObjectId.Empty));
            Assert.That(session.CurrentPreviewPose, Is.EqualTo(TabletopPose.Default));
        }

        [Test]
        public void ReconcileAndEnd_ClearsViewPreviewState()
        {
            TabletopDragPreviewSession session = BeginCardSession(40, out CardView view, out _);
            session.UpdatePosition(new TableCoordinate(3.0, 4.0));

            session.ReconcileAndEnd();

            Assert.That(view.IsPreviewing, Is.False);
            Assert.That(view.PreviewPose, Is.EqualTo(TabletopPose.Default));
        }

        [Test]
        public void ReconcileAndEnd_WithoutActiveSession_ThrowsInvalidOperationException()
        {
            TabletopDragPreviewSession session = new TabletopDragPreviewSession();

            Assert.Throws<InvalidOperationException>(() => session.ReconcileAndEnd());
        }

        [Test]
        public void CancelAndEnd_RestoresAcceptedPose()
        {
            TabletopPose acceptedPose = new TabletopPose(new TableCoordinate(2.0, 3.0), 25f, 0, 0);
            TabletopDragPreviewSession session = BeginCardSession(41, out CardView view, out _, acceptedPose);
            session.UpdatePose(new TabletopPose(new TableCoordinate(9.0, 10.0), 90f, 0, 0));

            session.CancelAndEnd();

            AssertWorldPose(view, acceptedPose);
        }

        [Test]
        public void CancelAndEnd_ClearsSession()
        {
            TabletopDragPreviewSession session = BeginCardSession(42, out _, out _);
            session.UpdatePosition(new TableCoordinate(3.0, 4.0));

            session.CancelAndEnd();

            Assert.That(session.IsActive, Is.False);
        }

        [Test]
        public void CancelAndEnd_DoesNotRequireStoredRollbackPose()
        {
            TabletopDragPreviewSession session = BeginCardSession(43, out CardView view, out CardInstanceState state);
            TabletopPose acceptedPose = new TabletopPose(new TableCoordinate(-8.0, -9.0), 10f, 0, 0);
            session.UpdatePosition(new TableCoordinate(3.0, 4.0));
            state.BaseState.SetPose(acceptedPose);

            session.CancelAndEnd();

            AssertWorldPose(view, acceptedPose);
        }

        [Test]
        public void CancelAndEnd_WithoutActiveSession_ThrowsInvalidOperationException()
        {
            TabletopDragPreviewSession session = new TabletopDragPreviewSession();

            Assert.Throws<InvalidOperationException>(() => session.CancelAndEnd());
        }

        [Test]
        public void Reset_WhenInactive_IsSafe()
        {
            TabletopDragPreviewSession session = new TabletopDragPreviewSession();

            Assert.DoesNotThrow(() => session.Reset());
            Assert.That(session.IsActive, Is.False);
        }

        [Test]
        public void Reset_WhenActive_RestoresAcceptedState()
        {
            TabletopPose acceptedPose = new TabletopPose(new TableCoordinate(2.0, 3.0), 25f, 0, 0);
            TabletopDragPreviewSession session = BeginCardSession(44, out CardView view, out _, acceptedPose);
            session.UpdatePose(new TabletopPose(new TableCoordinate(9.0, 10.0), 90f, 0, 0));

            session.Reset();

            AssertWorldPose(view, acceptedPose);
        }

        [Test]
        public void Reset_ClearsActiveState()
        {
            TabletopDragPreviewSession session = BeginCardSession(45, out _, out _);

            session.Reset();

            Assert.That(session.IsActive, Is.False);
            Assert.That(session.ActiveObjectId, Is.EqualTo(TabletopObjectId.Empty));
        }

        [Test]
        public void Reset_WhenActiveViewWasUnbound_ClearsSessionSafely()
        {
            TabletopDragPreviewSession session = BeginCardSession(46, out CardView view, out _);
            view.Unbind();

            Assert.DoesNotThrow(() => session.Reset());
            Assert.That(session.IsActive, Is.False);
        }

        [Test]
        public void Reset_WhenActiveViewWasDestroyed_ClearsSessionSafely()
        {
            TabletopDragPreviewSession session = BeginCardSession(47, out CardView view, out _);
            UnityObject.DestroyImmediate(view.gameObject);

            Assert.DoesNotThrow(() => session.Reset());
            Assert.That(session.IsActive, Is.False);
        }

        [Test]
        public void Session_DoesNotRequireMatchState()
        {
            AssertPreviewSessionCanRunWithoutExternalCoordinator(48);
        }

        [Test]
        public void Session_DoesNotRequireMoveObjectUseCase()
        {
            AssertPreviewSessionCanRunWithoutExternalCoordinator(49);
        }

        [Test]
        public void Session_DoesNotRequireLocalInteractionLockService()
        {
            AssertPreviewSessionCanRunWithoutExternalCoordinator(50);
        }

        [Test]
        public void Session_DoesNotRequireSelectionState()
        {
            AssertPreviewSessionCanRunWithoutExternalCoordinator(51);
        }

        [Test]
        public void Session_DoesNotRequireInputActions()
        {
            AssertPreviewSessionCanRunWithoutExternalCoordinator(52);
        }

        [Test]
        public void Preview_NeverChangesNonPoseRuntimeState()
        {
            ContainerId containerId = new ContainerId(GuidFromSeed(4000));
            PlayerId ownerId = new PlayerId(GuidFromSeed(5000));
            TabletopObjectState baseState = CreateBaseState(
                53,
                TabletopObjectKind.Card,
                TabletopPose.Default,
                containerId,
                ownerId,
                ObjectVisibility.OwnerOnly,
                true);
            CardView view = CreateView<CardView>();
            CardInstanceState state = new CardInstanceState(baseState, CardFace.FaceDown);
            view.Bind(state, CreateConverter());
            TabletopDragPreviewSession session = new TabletopDragPreviewSession();
            session.Begin(view);

            session.UpdatePose(new TabletopPose(new TableCoordinate(3.0, 4.0), 90f, 2, 3));

            Assert.That(baseState.ContainerId, Is.EqualTo(containerId));
            Assert.That(baseState.OwnerPlayerId, Is.EqualTo(ownerId));
            Assert.That(baseState.Visibility, Is.EqualTo(ObjectVisibility.OwnerOnly));
            Assert.That(baseState.IsUserLocked, Is.True);
            Assert.That(baseState.Id, Is.EqualTo(view.ObjectId));
            Assert.That(baseState.DefinitionId, Is.EqualTo(new ObjectDefinitionId(GuidFromSeed(1053))));
            Assert.That(state.Face, Is.EqualTo(CardFace.FaceDown));
        }

        private TabletopDragPreviewSession BeginCardSession(
            int seed,
            out CardView view,
            out CardInstanceState state,
            TabletopPose? acceptedPose = null,
            TabletopCoordinateConverter converter = null)
        {
            TabletopDragPreviewSession session = new TabletopDragPreviewSession();
            view = CreateBoundCardView(seed, out state, acceptedPose, converter);
            session.Begin(view);
            return session;
        }

        private void AssertPreviewSessionCanRunWithoutExternalCoordinator(int seed)
        {
            TabletopDragPreviewSession session = BeginCardSession(seed, out CardView view, out _);

            session.UpdatePosition(new TableCoordinate(1.0, 2.0));
            session.CancelAndEnd();

            Assert.That(session.IsActive, Is.False);
            Assert.That(view.IsPreviewing, Is.False);
        }

        private CardView CreateBoundCardView(
            int seed,
            out CardInstanceState state,
            TabletopPose? acceptedPose = null,
            TabletopCoordinateConverter converter = null)
        {
            CardView view = CreateView<CardView>();
            state = new CardInstanceState(
                CreateBaseState(seed, TabletopObjectKind.Card, acceptedPose ?? TabletopPose.Default),
                CardFace.FaceUp);
            view.Bind(state, converter ?? CreateConverter());
            return view;
        }

        private PawnView CreateBoundPawnView(int seed, out PawnState state)
        {
            PawnView view = CreateView<PawnView>();
            state = new PawnState(CreateBaseState(seed, TabletopObjectKind.Pawn, TabletopPose.Default));
            view.Bind(state, CreateConverter());
            return view;
        }

        private TokenView CreateBoundTokenView(int seed, out TokenState state)
        {
            TokenView view = CreateView<TokenView>();
            state = new TokenState(CreateBaseState(seed, TabletopObjectKind.Token, TabletopPose.Default));
            view.Bind(state, CreateConverter());
            return view;
        }

        private T CreateView<T>()
            where T : TabletopObjectView
        {
            GameObject gameObject = new GameObject(typeof(T).Name);
            createdGameObjects.Add(gameObject);
            return gameObject.AddComponent<T>();
        }

        private static TabletopObjectState CreateBaseState(
            int seed,
            TabletopObjectKind kind,
            TabletopPose pose,
            ContainerId? containerId = null,
            PlayerId? ownerPlayerId = null,
            ObjectVisibility visibility = ObjectVisibility.Public,
            bool isUserLocked = false)
        {
            return new TabletopObjectState(
                new TabletopObjectId(GuidFromSeed(seed)),
                new ObjectDefinitionId(GuidFromSeed(seed + 1000)),
                kind,
                pose,
                containerId ?? ContainerId.Empty,
                ownerPlayerId ?? PlayerId.Empty,
                visibility,
                isUserLocked);
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

        private static void AssertWorldPose(TabletopObjectView view, TabletopPose pose)
        {
            AssertVector3(view.transform.position, (float)pose.Position.X, 0f, (float)pose.Position.Y);
            Assert.That(Quaternion.Angle(Quaternion.Euler(0f, pose.RotationDegrees, 0f), view.transform.rotation), Is.EqualTo(0f).Within(Tolerance));
        }

        private static void AssertVector3(Vector3 actual, Vector3 expected)
        {
            AssertVector3(actual, expected.x, expected.y, expected.z);
        }

        private static void AssertVector3(Vector3 actual, float expectedX, float expectedY, float expectedZ)
        {
            Assert.That(actual.x, Is.EqualTo(expectedX).Within(Tolerance));
            Assert.That(actual.y, Is.EqualTo(expectedY).Within(Tolerance));
            Assert.That(actual.z, Is.EqualTo(expectedZ).Within(Tolerance));
        }
    }
}
