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

namespace ConsoleCards.Tests.EditMode.Presentation
{
    public sealed class TabletopSelectionStateTests
    {
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
        public void NewState_HasNoHoverOrSelection()
        {
            TabletopSelectionState state = new TabletopSelectionState();

            Assert.That(state.HasHoveredObject, Is.False);
            Assert.That(state.HasSelection, Is.False);
            Assert.That(state.HoveredView, Is.Null);
            Assert.That(state.SelectedView, Is.Null);
        }

        [Test]
        public void NewState_EmptyIdGettersReturnEmpty()
        {
            TabletopSelectionState state = new TabletopSelectionState();

            Assert.That(state.HoveredObjectId, Is.EqualTo(TabletopObjectId.Empty));
            Assert.That(state.SelectedObjectId, Is.EqualTo(TabletopObjectId.Empty));
        }

        [Test]
        public void SetHovered_StoresBoundView()
        {
            TabletopSelectionState state = new TabletopSelectionState();
            CardView view = CreateBoundCardView(1, out CardInstanceState cardState);

            state.SetHovered(view);

            Assert.That(state.HoveredView, Is.SameAs(view));
            Assert.That(state.HasHoveredObject, Is.True);
            Assert.That(state.HoveredObjectId, Is.EqualTo(cardState.BaseState.Id));
        }

        [Test]
        public void SetHovered_WhenViewIsNull_ClearsHover()
        {
            TabletopSelectionState state = new TabletopSelectionState();
            state.SetHovered(CreateBoundCardView(2, out _));

            state.SetHovered(null);

            Assert.That(state.HoveredView, Is.Null);
            Assert.That(state.HasHoveredObject, Is.False);
            Assert.That(state.HoveredObjectId, Is.EqualTo(TabletopObjectId.Empty));
        }

        [Test]
        public void SetHovered_WhenViewIsUnbound_ThrowsArgumentException()
        {
            TabletopSelectionState state = new TabletopSelectionState();
            CardView view = CreateView<CardView>();

            Assert.Throws<ArgumentException>(() => state.SetHovered(view));
        }

        [Test]
        public void SetHovered_DoesNotChangeSelection()
        {
            TabletopSelectionState state = new TabletopSelectionState();
            CardView selected = CreateBoundCardView(3, out _);
            PawnView hovered = CreateBoundPawnView(4, out _);
            state.Select(selected);

            state.SetHovered(hovered);

            Assert.That(state.SelectedView, Is.SameAs(selected));
            Assert.That(state.HoveredView, Is.SameAs(hovered));
        }

        [Test]
        public void Select_StoresBoundView()
        {
            TabletopSelectionState state = new TabletopSelectionState();
            CardView view = CreateBoundCardView(5, out CardInstanceState cardState);

            state.Select(view);

            Assert.That(state.SelectedView, Is.SameAs(view));
            Assert.That(state.HasSelection, Is.True);
            Assert.That(state.SelectedObjectId, Is.EqualTo(cardState.BaseState.Id));
        }

        [Test]
        public void Select_WhenViewIsNull_ThrowsArgumentNullException()
        {
            TabletopSelectionState state = new TabletopSelectionState();

            Assert.Throws<ArgumentNullException>(() => state.Select(null));
        }

        [Test]
        public void Select_WhenViewIsUnbound_ThrowsArgumentException()
        {
            TabletopSelectionState state = new TabletopSelectionState();
            CardView view = CreateView<CardView>();

            Assert.Throws<ArgumentException>(() => state.Select(view));
        }

        [Test]
        public void Select_ReplacesPreviousSelection()
        {
            TabletopSelectionState state = new TabletopSelectionState();
            CardView first = CreateBoundCardView(6, out _);
            PawnView second = CreateBoundPawnView(7, out _);

            state.Select(first);
            state.Select(second);

            Assert.That(state.SelectedView, Is.SameAs(second));
        }

        [Test]
        public void Select_DoesNotChangeHover()
        {
            TabletopSelectionState state = new TabletopSelectionState();
            CardView hovered = CreateBoundCardView(8, out _);
            PawnView selected = CreateBoundPawnView(9, out _);
            state.SetHovered(hovered);

            state.Select(selected);

            Assert.That(state.HoveredView, Is.SameAs(hovered));
            Assert.That(state.SelectedView, Is.SameAs(selected));
        }

        [Test]
        public void ClearHovered_ClearsOnlyHover()
        {
            TabletopSelectionState state = new TabletopSelectionState();
            CardView hovered = CreateBoundCardView(10, out _);
            PawnView selected = CreateBoundPawnView(11, out _);
            state.SetHovered(hovered);
            state.Select(selected);

            state.ClearHovered();

            Assert.That(state.HoveredView, Is.Null);
            Assert.That(state.SelectedView, Is.SameAs(selected));
        }

        [Test]
        public void ClearSelection_ClearsOnlySelection()
        {
            TabletopSelectionState state = new TabletopSelectionState();
            CardView hovered = CreateBoundCardView(12, out _);
            PawnView selected = CreateBoundPawnView(13, out _);
            state.SetHovered(hovered);
            state.Select(selected);

            state.ClearSelection();

            Assert.That(state.HoveredView, Is.SameAs(hovered));
            Assert.That(state.SelectedView, Is.Null);
        }

        [Test]
        public void ClearAll_ClearsHoverAndSelection()
        {
            TabletopSelectionState state = new TabletopSelectionState();
            state.SetHovered(CreateBoundCardView(14, out _));
            state.Select(CreateBoundPawnView(15, out _));

            state.ClearAll();

            Assert.That(state.HoveredView, Is.Null);
            Assert.That(state.SelectedView, Is.Null);
        }

        [Test]
        public void ClearUnavailable_ClearsUnboundHoveredView()
        {
            TabletopSelectionState state = new TabletopSelectionState();
            CardView view = CreateBoundCardView(16, out _);
            state.SetHovered(view);
            view.Unbind();

            bool changed = state.ClearUnavailable();

            Assert.That(changed, Is.True);
            Assert.That(state.HoveredView, Is.Null);
        }

        [Test]
        public void ClearUnavailable_ClearsUnboundSelectedView()
        {
            TabletopSelectionState state = new TabletopSelectionState();
            CardView view = CreateBoundCardView(17, out _);
            state.Select(view);
            view.Unbind();

            bool changed = state.ClearUnavailable();

            Assert.That(changed, Is.True);
            Assert.That(state.SelectedView, Is.Null);
        }

        [Test]
        public void ClearUnavailable_ClearsInactiveViews()
        {
            TabletopSelectionState state = new TabletopSelectionState();
            CardView view = CreateBoundCardView(18, out _);
            state.SetHovered(view);
            view.gameObject.SetActive(false);

            bool changed = state.ClearUnavailable();

            Assert.That(changed, Is.True);
            Assert.That(state.HoveredView, Is.Null);
        }

        [Test]
        public void ClearUnavailable_ClearsDisabledViews()
        {
            TabletopSelectionState state = new TabletopSelectionState();
            CardView view = CreateBoundCardView(19, out _);
            state.Select(view);
            view.enabled = false;

            bool changed = state.ClearUnavailable();

            Assert.That(changed, Is.True);
            Assert.That(state.SelectedView, Is.Null);
        }

        [Test]
        public void ClearUnavailable_ClearsDestroyedViews()
        {
            TabletopSelectionState state = new TabletopSelectionState();
            CardView view = CreateBoundCardView(20, out _);
            state.SetHovered(view);
            createdGameObjects.Remove(view.gameObject);
            UnityObject.DestroyImmediate(view.gameObject);

            bool changed = state.ClearUnavailable();

            Assert.That(changed, Is.True);
            Assert.That(state.HoveredView, Is.Null);
        }

        [Test]
        public void ClearUnavailable_PreservesValidViews()
        {
            TabletopSelectionState state = new TabletopSelectionState();
            CardView hovered = CreateBoundCardView(21, out _);
            PawnView selected = CreateBoundPawnView(22, out _);
            state.SetHovered(hovered);
            state.Select(selected);

            bool changed = state.ClearUnavailable();

            Assert.That(changed, Is.False);
            Assert.That(state.HoveredView, Is.SameAs(hovered));
            Assert.That(state.SelectedView, Is.SameAs(selected));
        }

        [Test]
        public void ClearUnavailable_ReturnsFalseWhenNoStateChanged()
        {
            TabletopSelectionState state = new TabletopSelectionState();

            bool changed = state.ClearUnavailable();

            Assert.That(changed, Is.False);
        }

        [Test]
        public void Selection_DoesNotMutateRuntimeState()
        {
            TabletopSelectionState selection = new TabletopSelectionState();
            CardView view = CreateBoundCardView(23, out CardInstanceState cardState);
            TabletopPose originalPose = cardState.BaseState.Pose;
            CardFace originalFace = cardState.Face;

            selection.SetHovered(view);
            selection.Select(view);
            selection.ClearAll();

            Assert.That(cardState.BaseState.Pose, Is.EqualTo(originalPose));
            Assert.That(cardState.Face, Is.EqualTo(originalFace));
        }

        private CardView CreateBoundCardView(int seed, out CardInstanceState state)
        {
            CardView view = CreateView<CardView>();
            state = CreateCardState(seed);
            view.Bind(state, CreateConverter());
            return view;
        }

        private PawnView CreateBoundPawnView(int seed, out PawnState state)
        {
            PawnView view = CreateView<PawnView>();
            state = CreatePawnState(seed);
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

        private static CardInstanceState CreateCardState(int seed)
        {
            return new CardInstanceState(CreateBaseState(seed, TabletopObjectKind.Card), CardFace.FaceUp);
        }

        private static PawnState CreatePawnState(int seed)
        {
            return new PawnState(CreateBaseState(seed, TabletopObjectKind.Pawn));
        }

        private static TabletopObjectState CreateBaseState(int seed, TabletopObjectKind kind)
        {
            return new TabletopObjectState(
                new TabletopObjectId(GuidFromSeed(seed)),
                new ObjectDefinitionId(GuidFromSeed(seed + 1000)),
                kind,
                new TabletopPose(new TableCoordinate(seed, -seed), 15f, 1, 2),
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
    }
}
