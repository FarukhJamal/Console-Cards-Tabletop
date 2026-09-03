using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Dice;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Coordinates;
using ConsoleCards.Presentation.Input;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.Prototype;
using ConsoleCards.Presentation.UI;
using ConsoleCards.Presentation.Views;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ConsoleCards.Tests.EditMode.Presentation
{
    public sealed class PhysicalDieContextMenuTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<GameObject> objects = new List<GameObject>();
        private TabletopPrototypeComposition composition;
        private PrototypeRuntimeUiRoot ui;
        private TabletopInputFrameCoordinator input;
        private EventSystem eventSystem;
        private EventSystem previousEventSystem;
        private LocalPhysicalObjectAuthority authority;
        private MatchState match;
        private DieView die;
        private PlayerId actor;

        [SetUp]
        public void SetUp()
        {
            previousEventSystem = EventSystem.current;
            eventSystem = Create("UI Events").AddComponent<EventSystem>();
            EventSystem.current = eventSystem;
            GameObject canvasObject = Create("UI", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            ui = canvasObject.AddComponent<PrototypeRuntimeUiRoot>();
            Set(ui, "tabletopPopupMount", canvasObject.transform);
            Set(ui, "popupLayer", canvasObject);
            Set(ui, "tabletopPopupPrefab", AssetDatabase.LoadAssetAtPath<PrototypeTabletopPopupView>(
                "Assets/ConsoleCards/Content/Prefabs/Prototype/PrototypeTabletopPopup.prefab"));
            GameObject hiddenHud = Create("Inactive HUD");
            hiddenHud.SetActive(false);
            Set(ui, "activeSessionHudLayer", hiddenHud);

            // Isolate popup orchestration without loading a scene or starting a Match session.
            GameObject compositionObject = Create("Composition");
            compositionObject.SetActive(false);
            composition = compositionObject.AddComponent<TabletopPrototypeComposition>();
            GameObject inputObject = Create("Input");
            inputObject.SetActive(false);
            input = inputObject.AddComponent<TabletopInputFrameCoordinator>();
            actor = PlayerId.New();
            match = new MatchState(MatchId.New(), GameTemplateId.Empty, 0, Array.Empty<CardInstanceState>(),
                Array.Empty<PawnState>(), Array.Empty<TokenState>(), Array.Empty<ContainerState>(), Array.Empty<SeatState>());
            var state = new TabletopObjectState(TabletopObjectId.New(), ObjectDefinitionId.New(), TabletopObjectKind.Die,
                TabletopPose.Default, ContainerId.Empty, actor, ObjectVisibility.Public, false, DynamicState());
            var dieState = new DieState(state, 6, 1);
            match.AddUncontainedDie(dieState);
            die = Create("Die", typeof(BoxCollider)).AddComponent<DieView>();
            TextMesh label = Create("Die label").AddComponent<TextMesh>();
            label.transform.SetParent(die.transform, false);
            Set(die, "resultLabel", label);
            die.Bind(dieState, new TabletopCoordinateConverter(1f, 0f, 0f, 0f));
            authority = new LocalPhysicalObjectAuthority(match, new[] { actor }, () => actor, null, null, _ => { });
            // This test exercises launch acceptance, not authored shape generation/physics settlement.
            var physical = die.gameObject.AddComponent<PhysicalLooseObject>();
            die.PhysicalObject = physical;
            physical.Initialize(die, authority);
            Set(composition, "matchState", match);
            Set(composition, "runtimeUi", ui);
            Set(composition, "physicalAuthority", authority);
            ((List<DieView>)Get(composition, "dieViews")).Add(die);
            typeof(TabletopPrototypeComposition).GetProperty("IsInitialized").SetValue(composition, true);
        }

        [TearDown]
        public void TearDown()
        {
            if (composition != null) composition.Shutdown();
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            objects.Clear();
            EventSystem.current = previousEventSystem;
        }

        [Test]
        public void PhysicsRevisionBetweenPressAndRelease_PreservesUiOwnershipAndRollsOnce()
        {
            Button roll = OpenMenu();
            var pointer = Press(Center(roll.transform));
            Assert.That(pointer.pointerPress, Is.EqualTo(roll.gameObject));
            AssertUiOwnsPointer(pointer.position);
            Assert.That(authority.Commit(die, DynamicState(), null), Is.True);
            Invoke(composition, "RefreshOpenTabletopPopup");
            Assert.That(roll != null && roll.gameObject.activeInHierarchy, Is.True,
                "Physics revision must not destroy/deactivate the pressed Button before uGUI release.");
            AssertUiOwnsPointer(pointer.position);
            long beforeRoll = match.Revision;
            Release(pointer);
            Assert.That(match.Revision, Is.EqualTo(beforeRoll + 1));
            Assert.That(die.BoundState.PhysicalState.Mode, Is.EqualTo(PhysicalObjectMode.Dynamic));
            Assert.That(die.PhysicalObject.IsHeld, Is.False);
            Assert.That(die.PhysicalObject.Body.useGravity, Is.True);
            Assert.That(die.BoundState.PhysicalState.Velocity.Y, Is.EqualTo(4f), "Roll impulse is unchanged.");
            Assert.That(Popup.gameObject.activeInHierarchy, Is.False);
            Invoke(composition, "RollContextDie", die.ObjectId); // Stale callback from the closed menu.
            Assert.That(match.Revision, Is.EqualTo(beforeRoll + 1));
        }

        [Test]
        public void RejectedRoll_LeavesMenuOpenAndDoesNotPickUpDie()
        {
            Button roll = OpenMenu();
            die.BoundState.SetUserLocked(true);
            long revision = match.Revision;
            Release(Press(Center(roll.transform)));
            Assert.That(match.Revision, Is.EqualTo(revision));
            Assert.That(Popup.gameObject.activeInHierarchy, Is.True);
            Assert.That(die.PhysicalObject.IsHeld, Is.False);
        }

        [Test]
        public void OutsideClick_DismissesWithoutRolling_AndConsumesPointerOnPress()
        {
            OpenMenu();
            Vector2 outside = new Vector2(2f, 2f);
            AssertUiOwnsPointer(outside);
            var pointer = Press(outside);
            Assert.That(pointer.pointerPress, Is.EqualTo(Popup.gameObject));
            long revision = match.Revision;
            Release(pointer);
            Assert.That(Popup.gameObject.activeInHierarchy, Is.False);
            Assert.That(match.Revision, Is.EqualTo(revision));
            Assert.That(die.PhysicalObject.IsHeld, Is.False);
        }

        [Test]
        public void MissingTarget_StillClosesMenu()
        {
            OpenMenu();
            die.Unbind();
            Invoke(composition, "RefreshOpenTabletopPopup");
            Assert.That(Popup.gameObject.activeInHierarchy, Is.False);
        }

        private PrototypeTabletopPopupView Popup => (PrototypeTabletopPopupView)Get(ui, "tabletopPopupView");
        private Button OpenMenu()
        {
            Type menuMode = typeof(TabletopPrototypeComposition).GetNestedType("PrototypeContextMenuMode", BindingFlags.NonPublic);
            Invoke(composition, "OpenContextMenu", Enum.Parse(menuMode, "Die"),
                new Vector2(Screen.width * 0.5f, Screen.height * 0.5f),
                TabletopObjectId.Empty, ContainerId.Empty, die.ObjectId);
            Canvas.ForceUpdateCanvases();
            return Popup.GetComponentsInChildren<PrototypePopupActionRowView>()
                .Single(row => row.GetComponentInChildren<Text>().text == "Roll").GetComponent<Button>();
        }

        private PointerEventData Press(Vector2 position)
        {
            var pointer = new PointerEventData(eventSystem) { position = position, button = PointerEventData.InputButton.Left };
            GameObject hit = Hit(pointer);
            pointer.pointerPress = ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerDownHandler)
                ?? ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit);
            pointer.pointerClick = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit);
            return pointer;
        }

        private void Release(PointerEventData pointer)
        {
            GameObject click = ExecuteEvents.GetEventHandler<IPointerClickHandler>(Hit(pointer));
            ExecuteEvents.Execute(pointer.pointerPress, pointer, ExecuteEvents.pointerUpHandler);
            if (click != null && click == pointer.pointerClick)
                ExecuteEvents.Execute(click, pointer, ExecuteEvents.pointerClickHandler);
        }

        private GameObject Hit(PointerEventData pointer)
        {
            var results = new List<RaycastResult>();
            eventSystem.RaycastAll(pointer, results);
            Assert.That(results, Is.Not.Empty);
            pointer.pointerCurrentRaycast = results[0];
            return results[0].gameObject;
        }

        private void AssertUiOwnsPointer(Vector2 position) =>
            Assert.That((bool)Invoke(input, "IsInsideObjectInputBlockingGuiRect", position), Is.True);
        private PhysicalObjectState DynamicState() => PhysicalLooseObject.State(Vector3.up, Quaternion.identity,
            Vector3.zero, Vector3.zero, PhysicalObjectMode.Dynamic, actor);
        private static Vector2 Center(Transform target) => RectTransformUtility.WorldToScreenPoint(null, target.position);
        private GameObject Create(string name, params Type[] components)
        {
            var go = new GameObject(name, components);
            objects.Add(go);
            return go;
        }
        private static object Get(object target, string field) => target.GetType().GetField(field, Private).GetValue(target);
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
        private static object Invoke(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, Private).Invoke(target, args);
    }
}
