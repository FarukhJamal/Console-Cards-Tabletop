using System;
using System.Collections.Generic;
using System.Linq;
using ConsoleCards.Presentation.Camera;
using ConsoleCards.Presentation.Input;
using ConsoleCards.Presentation.Prototype;
using ConsoleCards.Presentation.TableSurface;
using ConsoleCards.Presentation.UI;
using ConsoleCards.Presentation.Views;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ConsoleCards.Tests.EditMode.Presentation
{
    public sealed class TabletopPrototypeSceneStructureTests
    {
        private const string ScenePath = "Assets/ConsoleCards/Presentation/Scenes/TabletopPrototype.unity";
        private const string CameraControlsPath = "Assets/ConsoleCards/Presentation/Input/TabletopCameraControls.inputactions";
        private const string ObjectControlsPath = "Assets/ConsoleCards/Presentation/Input/TabletopObjectControls.inputactions";
        private const string RootInputActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const string CardPrefabPath = "Assets/ConsoleCards/Content/Prefabs/Prototype/PrototypeCard.prefab";
        private const string PawnPrefabPath = "Assets/ConsoleCards/Content/Prefabs/Prototype/PrototypePawn.prefab";
        private const string TokenPrefabPath = "Assets/ConsoleCards/Content/Prefabs/Prototype/PrototypeToken.prefab";
        private const string TableSurfaceMaterialPath = "Assets/ConsoleCards/Content/Materials/TableSurfacePrototype.mat";
        private const string TabletopObjectLayerName = "TabletopObject";
        private const float Tolerance = 0.0001f;

        [Test]
        public void TabletopPrototypeScene_ExistsAndIsEnabledInBuildSettings()
        {
            SceneAsset scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);

            Assert.That(scene, Is.Not.Null);
            Assert.That(EditorBuildSettings.scenes.Any(buildScene =>
                buildScene.enabled && buildScene.path == ScenePath), Is.True);
        }

        [Test]
        public void Hierarchy_HasApprovedRootsAndChildrenOnly()
        {
            WithScene(scene =>
            {
                GameObject[] roots = scene.GetRootGameObjects();

                Assert.That(
                    roots.Select(root => root.name),
                    Is.EquivalentTo(new[] { "CameraRig", "Environment", "Interaction", "TabletopObjects" }));
                AssertDirectChildren(FindRoot(scene, "CameraRig"), "Main Camera");
                AssertDirectChildren(FindRoot(scene, "Environment"), "Directional Light", "TableSurfaceProxy");
                AssertDirectChildren(
                    FindRoot(scene, "Interaction"),
                    "PrototypeComposition",
                    "TabletopInput",
                    "PrototypeInteractionGuide");
                AssertDirectChildren(FindRoot(scene, "TabletopObjects"), "PrototypeCard", "PrototypePawn", "PrototypeToken");

                Assert.That(AllObjects(scene).Count(go => go.name == "PrototypeCard"), Is.EqualTo(1));
                Assert.That(AllObjects(scene).Count(go => go.name == "PrototypePawn"), Is.EqualTo(1));
                Assert.That(AllObjects(scene).Count(go => go.name == "PrototypeToken"), Is.EqualTo(1));
                AssertNoMissingScripts(scene);
            });
        }

        [Test]
        public void ComponentCounts_MatchSceneDependencyInvariants()
        {
            WithScene(scene =>
            {
                List<GameObject> objects = AllObjects(scene);

                Assert.That(GetComponents<Camera>(scene), Has.Length.EqualTo(1));
                Assert.That(GetComponents<TabletopCameraController>(scene), Has.Length.EqualTo(1));
                Assert.That(GetComponents<TabletopCameraInputAdapter>(scene), Has.Length.EqualTo(1));
                Assert.That(GetComponents<TabletopObjectInputAdapter>(scene), Has.Length.EqualTo(1));
                Assert.That(GetComponents<TabletopInputFrameCoordinator>(scene), Has.Length.EqualTo(1));
                Assert.That(GetComponents<TabletopPrototypeComposition>(scene), Has.Length.EqualTo(1));
                Assert.That(GetComponents<PrototypeInteractionGuide>(scene), Has.Length.EqualTo(1));
                Assert.That(GetComponents<CardView>(scene), Has.Length.EqualTo(1));
                Assert.That(GetComponents<PawnView>(scene), Has.Length.EqualTo(1));
                Assert.That(GetComponents<TokenView>(scene), Has.Length.EqualTo(1));
                Assert.That(GetComponents<TabletopSelectionVisual>(scene), Has.Length.EqualTo(3));
                Assert.That(GetComponents<TabletopSurfaceProxy>(scene), Has.Length.EqualTo(1));

                Assert.That(CountComponents(objects, "UnityEngine.InputSystem.PlayerInput"), Is.EqualTo(0));
                Assert.That(CountComponents(objects, "UnityEngine.EventSystems.EventSystem"), Is.EqualTo(0));
                Assert.That(GetComponents<Rigidbody>(scene), Is.Empty);
                Assert.That(objects.SelectMany(go => go.GetComponents<Component>())
                    .Where(component => component != null)
                    .Any(component => component.GetType().FullName.Contains("Networking")
                        || component.GetType().Name.Contains("Network")), Is.False);
            });
        }

        [Test]
        public void CameraAndObjectInput_AreWiredToDedicatedActionAssets()
        {
            WithScene(scene =>
            {
                GameObject cameraRig = FindRoot(scene, "CameraRig");
                GameObject mainCameraObject = FindChild(cameraRig, "Main Camera");
                Camera mainCamera = mainCameraObject.GetComponent<Camera>();
                TabletopCameraController cameraController = cameraRig.GetComponent<TabletopCameraController>();
                TabletopCameraInputAdapter cameraAdapter = cameraRig.GetComponent<TabletopCameraInputAdapter>();
                TabletopObjectInputAdapter objectAdapter =
                    FindPath(scene, "Interaction/TabletopInput").GetComponent<TabletopObjectInputAdapter>();
                SerializedObject cameraControllerSerialized = new SerializedObject(cameraController);

                Assert.That(mainCamera.orthographic, Is.True);
                Assert.That(Vector3.Angle(mainCameraObject.transform.forward, Vector3.down), Is.EqualTo(0f).Within(Tolerance));
                AssertObjectReference(cameraControllerSerialized, "targetCamera", mainCamera);
                AssertObjectReference(cameraControllerSerialized, "cameraRig", cameraRig.transform);
                AssertActionReference(cameraAdapter, "keyboardPanAction", CameraControlsPath, "KeyboardPan");
                AssertActionReference(cameraAdapter, "dragPanAction", CameraControlsPath, "DragPan");
                AssertActionReference(cameraAdapter, "pointerDeltaAction", CameraControlsPath, "PointerDelta");
                AssertActionReference(cameraAdapter, "zoomAction", CameraControlsPath, "Zoom");
                AssertActionReference(objectAdapter, "pointAction", ObjectControlsPath, "Point");
                AssertActionReference(objectAdapter, "selectAction", ObjectControlsPath, "Select");
                AssertActionReference(objectAdapter, "cancelAction", ObjectControlsPath, "Cancel");
                AssertActionReference(objectAdapter, "rotateAction", ObjectControlsPath, "Rotate");
                AssertActionReference(objectAdapter, "flipAction", ObjectControlsPath, "Flip");
                Assert.That(new SerializedObject(objectAdapter).FindProperty("rotationStepDegrees").floatValue,
                    Is.EqualTo(15f).Within(Tolerance));
                Assert.That(objectAdapter.IsInitialized, Is.False);
            });
        }

        [Test]
        public void FrameCoordinatorAndComposition_AreExplicitlyWired()
        {
            WithScene(scene =>
            {
                TabletopCameraInputAdapter cameraAdapter = GetSingle<TabletopCameraInputAdapter>(scene);
                TabletopObjectInputAdapter objectAdapter = GetSingle<TabletopObjectInputAdapter>(scene);
                TabletopInputFrameCoordinator frameCoordinator = GetSingle<TabletopInputFrameCoordinator>(scene);
                TabletopPrototypeComposition composition = GetSingle<TabletopPrototypeComposition>(scene);
                CardView cardView = GetSingle<CardView>(scene);
                PawnView pawnView = GetSingle<PawnView>(scene);
                TokenView tokenView = GetSingle<TokenView>(scene);

                SerializedObject frameSerialized = new SerializedObject(frameCoordinator);
                Assert.That(frameCoordinator.enabled, Is.False);
                AssertObjectReference(frameSerialized, "cameraInputAdapter", cameraAdapter);
                AssertObjectReference(frameSerialized, "objectInputAdapter", objectAdapter);
                Assert.That(frameSerialized.FindProperty("selectionPresenter"), Is.Null);

                SerializedObject compositionSerialized = new SerializedObject(composition);
                AssertObjectReference(compositionSerialized, "targetCamera", GetSingle<Camera>(scene));
                AssertObjectReference(compositionSerialized, "cameraInputAdapter", cameraAdapter);
                AssertObjectReference(compositionSerialized, "objectInputAdapter", objectAdapter);
                AssertObjectReference(compositionSerialized, "inputFrameCoordinator", frameCoordinator);
                AssertObjectReference(compositionSerialized, "cardView", cardView);
                AssertObjectReference(compositionSerialized, "pawnView", pawnView);
                AssertObjectReference(compositionSerialized, "tokenView", tokenView);
                AssertObjectReference(
                    compositionSerialized,
                    "cardSelectionVisual",
                    cardView.GetComponent<TabletopSelectionVisual>());
                AssertObjectReference(
                    compositionSerialized,
                    "pawnSelectionVisual",
                    pawnView.GetComponent<TabletopSelectionVisual>());
                AssertObjectReference(
                    compositionSerialized,
                    "tokenSelectionVisual",
                    tokenView.GetComponent<TabletopSelectionVisual>());
                AssertObjectReference(
                    compositionSerialized,
                    "cardHighlightRoot",
                    FindChild(cardView.gameObject, "SelectionHighlightRoot"));
                AssertObjectReference(
                    compositionSerialized,
                    "pawnHighlightRoot",
                    FindChild(pawnView.gameObject, "SelectionHighlightRoot"));
                AssertObjectReference(
                    compositionSerialized,
                    "tokenHighlightRoot",
                    FindChild(tokenView.gameObject, "SelectionHighlightRoot"));
                Assert.That(compositionSerialized.FindProperty("interactionLayerMask").intValue,
                    Is.EqualTo(1 << LayerMask.NameToLayer(TabletopObjectLayerName)));
                AssertFloat(compositionSerialized, "maximumHitDistance", 100f);
                AssertFloat(compositionSerialized, "dragThresholdPixels", 8f);
                AssertFloat(compositionSerialized, "worldUnitsPerTableUnit", 1f);
                AssertFloat(compositionSerialized, "tabletopHeight", 0f);
                Assert.That(compositionSerialized.FindProperty("matchState"), Is.Null);
                Assert.That(compositionSerialized.FindProperty("selectionState"), Is.Null);
                Assert.That(compositionSerialized.FindProperty("moveCoordinator"), Is.Null);
            });
        }

        [Test]
        public void PrefabInstances_RemainConnectedAndUnmodifiedForRuntimeState()
        {
            WithScene(scene =>
            {
                AssertPrefabInstance(FindPath(scene, "TabletopObjects/PrototypeCard"), CardPrefabPath, typeof(CardView));
                AssertPrefabInstance(FindPath(scene, "TabletopObjects/PrototypePawn"), PawnPrefabPath, typeof(PawnView));
                AssertPrefabInstance(FindPath(scene, "TabletopObjects/PrototypeToken"), TokenPrefabPath, typeof(TokenView));
            });
        }

        [Test]
        public void Environment_PreservesSurfaceProxyAndLight()
        {
            WithScene(scene =>
            {
                GameObject cameraRig = FindRoot(scene, "CameraRig");
                GameObject surfaceObject = FindPath(scene, "Environment/TableSurfaceProxy");
                TabletopSurfaceProxy proxy = surfaceObject.GetComponent<TabletopSurfaceProxy>();
                MeshRenderer surfaceRenderer = surfaceObject.GetComponent<MeshRenderer>();
                SerializedObject proxySerialized = new SerializedObject(proxy);

                Assert.That(FindPath(scene, "Environment/Directional Light").GetComponent<Light>(), Is.Not.Null);
                Assert.That(proxy, Is.Not.Null);
                AssertObjectReference(proxySerialized, "trackedTransform", cameraRig.transform);
                AssertObjectReference(proxySerialized, "surfaceTransform", surfaceObject.transform);
                Assert.That(surfaceObject.GetComponent<Collider>(), Is.Null);
                Assert.That(surfaceRenderer.sharedMaterial, Is.SameAs(AssetDatabase.LoadAssetAtPath<Material>(TableSurfaceMaterialPath)));
            });
        }

        [Test]
        public void PrototypeInteractionGuide_IsSceneLocalAndPresentationOnly()
        {
            WithScene(scene =>
            {
                GameObject guideObject = FindPath(scene, "Interaction/PrototypeInteractionGuide");
                PrototypeInteractionGuide guide = guideObject.GetComponent<PrototypeInteractionGuide>();
                SerializedObject serializedGuide = new SerializedObject(guide);

                Assert.That(guide, Is.Not.Null);
                Assert.That(guide.enabled, Is.True);
                AssertIdentity(guideObject.transform);
                Assert.That(serializedGuide.FindProperty("showGuide").boolValue, Is.True);
                Assert.That(serializedGuide.FindProperty("title").stringValue, Is.EqualTo("Console Cards Prototype"));
                Assert.That(serializedGuide.FindProperty("guideLines").arraySize, Is.EqualTo(12));
                Assert.That(GuideLines(serializedGuide), Has.Member("F + selected Card: flip face"));
                Assert.That(GuideLines(serializedGuide), Has.Member("Mouse wheel + selection: rotate 15 degrees"));
                Assert.That(GuideLines(serializedGuide), Has.Member("Mouse wheel + no selection: camera zoom"));
                Assert.That(guideObject.GetComponents<Component>(), Has.Length.EqualTo(2));
            });
        }

        [Test]
        public void Boundaries_NoRootInputActionsOrOutOfScopeContentIsSerialized()
        {
            WithScene(scene =>
            {
                foreach (GameObject gameObject in AllObjects(scene))
                {
                    foreach (Component component in gameObject.GetComponents<Component>())
                    {
                        if (component == null)
                        {
                            continue;
                        }

                        SerializedObject serializedObject = new SerializedObject(component);
                        SerializedProperty property = serializedObject.GetIterator();
                        bool enterChildren = true;
                        while (property.NextVisible(enterChildren))
                        {
                            enterChildren = false;
                            if (property.propertyType != SerializedPropertyType.ObjectReference
                                || property.objectReferenceValue == null)
                            {
                                continue;
                            }

                            Assert.That(AssetDatabase.GetAssetPath(property.objectReferenceValue),
                                Is.Not.EqualTo(RootInputActionsPath));
                        }

                        string fullName = component.GetType().FullName;
                        Assert.That(fullName.Contains("GameTemplate"), Is.False);
                        Assert.That(fullName.Contains("PlayArea"), Is.False);
                        Assert.That(fullName.Contains("Networking"), Is.False);
                    }
                }
            });
        }

        private static void WithScene(Action<Scene> assertion)
        {
            SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
            bool previousSetupHadLoadedScene = previousSetup.Any(setup => setup.isLoaded);
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            try
            {
                assertion(scene);
            }
            finally
            {
                if (previousSetupHadLoadedScene)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
                }
                else
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
            }
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            GameObject root = scene.GetRootGameObjects().SingleOrDefault(go => go.name == name);
            Assert.That(root, Is.Not.Null, $"Missing scene root '{name}'.");
            return root;
        }

        private static GameObject FindPath(Scene scene, string path)
        {
            string[] parts = path.Split('/');
            GameObject current = FindRoot(scene, parts[0]);
            for (int i = 1; i < parts.Length; i++)
            {
                current = FindChild(current, parts[i]);
            }

            return current;
        }

        private static GameObject FindChild(GameObject parent, string name)
        {
            for (int i = 0; i < parent.transform.childCount; i++)
            {
                Transform child = parent.transform.GetChild(i);
                if (child.name == name)
                {
                    return child.gameObject;
                }
            }

            Assert.Fail($"Missing child '{name}' under '{parent.name}'.");
            return null;
        }

        private static void AssertDirectChildren(GameObject parent, params string[] expectedNames)
        {
            Assert.That(
                Enumerable.Range(0, parent.transform.childCount)
                    .Select(index => parent.transform.GetChild(index).name),
                Is.EquivalentTo(expectedNames));
        }

        private static List<GameObject> AllObjects(Scene scene)
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(transform => transform.gameObject)
                .ToList();
        }

        private static string[] GuideLines(SerializedObject serializedGuide)
        {
            SerializedProperty guideLines = serializedGuide.FindProperty("guideLines");
            string[] lines = new string[guideLines.arraySize];
            for (int i = 0; i < guideLines.arraySize; i++)
            {
                lines[i] = guideLines.GetArrayElementAtIndex(i).stringValue;
            }

            return lines;
        }

        private static T[] GetComponents<T>(Scene scene)
            where T : Component
        {
            return AllObjects(scene)
                .SelectMany(go => go.GetComponents<T>())
                .ToArray();
        }

        private static T GetSingle<T>(Scene scene)
            where T : Component
        {
            T[] components = GetComponents<T>(scene);
            Assert.That(components, Has.Length.EqualTo(1));
            return components[0];
        }

        private static int CountComponents(List<GameObject> objects, string fullTypeName)
        {
            return objects.Sum(go => go.GetComponents<Component>()
                .Count(component => component != null && component.GetType().FullName == fullTypeName));
        }

        private static void AssertActionReference(
            Component component,
            string fieldName,
            string expectedAssetPath,
            string expectedActionName)
        {
            UnityEngine.Object reference = new SerializedObject(component)
                .FindProperty(fieldName)
                .objectReferenceValue;

            Assert.That(reference, Is.Not.Null, $"{fieldName} is not assigned.");
            Assert.That(AssetDatabase.GetAssetPath(reference), Is.EqualTo(expectedAssetPath));
            Assert.That(GetReferencedActionName(reference), Is.EqualTo(expectedActionName));
        }

        private static string GetReferencedActionName(UnityEngine.Object actionReference)
        {
            object action = actionReference.GetType()
                .GetProperty("action")
                .GetValue(actionReference);
            Assert.That(action, Is.Not.Null);
            return (string)action.GetType().GetProperty("name").GetValue(action);
        }

        private static void AssertFloat(SerializedObject serializedObject, string propertyName, float expected)
        {
            Assert.That(serializedObject.FindProperty(propertyName).floatValue,
                Is.EqualTo(expected).Within(Tolerance));
        }

        private static void AssertObjectReference(
            SerializedObject serializedObject,
            string propertyName,
            UnityEngine.Object expected)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);

            Assert.That(property, Is.Not.Null, $"{propertyName} is not serialized.");
            Assert.That(property.propertyType, Is.EqualTo(SerializedPropertyType.ObjectReference));
            Assert.That(property.objectReferenceValue, Is.SameAs(expected), $"{propertyName} references the wrong object.");
        }

        private static void AssertPrefabInstance(GameObject root, string expectedPrefabPath, Type expectedViewType)
        {
            Assert.That(PrefabUtility.GetNearestPrefabInstanceRoot(root), Is.SameAs(root));
            Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root), Is.EqualTo(expectedPrefabPath));
            AssertIdentity(root.transform);
            Assert.That(root.layer, Is.EqualTo(LayerMask.NameToLayer(TabletopObjectLayerName)));
            Assert.That(root.GetComponent(expectedViewType), Is.Not.Null);
            Assert.That(root.GetComponent<Rigidbody>(), Is.Null);
            Assert.That(root.GetComponents<Collider>(), Has.Length.EqualTo(1));
            Assert.That(root.GetComponentsInChildren<Collider>(true), Has.Length.EqualTo(1));
            Assert.That(FindChild(root, "SelectionHighlightRoot").activeSelf, Is.False);
            Assert.That(root.GetComponent<TabletopSelectionVisual>().IsConfigured, Is.False);
            Assert.That(root.GetComponent<TabletopObjectView>().BoundState, Is.Null);

            foreach (TabletopObjectView view in root.GetComponentsInChildren<TabletopObjectView>(true))
            {
                Assert.That(view.gameObject, Is.SameAs(root));
            }

            if (expectedViewType == typeof(CardView))
            {
                CardView cardView = root.GetComponent<CardView>();
                SerializedObject cardViewSerialized = new SerializedObject(cardView);
                AssertObjectReference(cardViewSerialized, "faceUpVisualRoot", FindChild(root, "FaceUpVisualRoot"));
                AssertObjectReference(cardViewSerialized, "faceDownVisualRoot", FindChild(root, "FaceDownVisualRoot"));
            }
        }

        private static void AssertIdentity(Transform transform)
        {
            Assert.That(transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(Quaternion.Angle(transform.localRotation, Quaternion.identity), Is.EqualTo(0f).Within(Tolerance));
            Assert.That(transform.localScale, Is.EqualTo(Vector3.one));
        }

        private static void AssertNoMissingScripts(Scene scene)
        {
            foreach (GameObject gameObject in AllObjects(scene))
            {
                Assert.That(
                    GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject),
                    Is.EqualTo(0),
                    $"{gameObject.name} contains a missing script.");
            }
        }
    }
}
