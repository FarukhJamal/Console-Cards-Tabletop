using System.Collections;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates;
using ConsoleCards.Presentation.Input;
using ConsoleCards.Presentation.Prototype;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ConsoleCards.Tests.PlayMode.Presentation
{
    public sealed class TabletopPrototypeSceneInitializationTests
    {
        private const string ScenePath = "Assets/ConsoleCards/Presentation/Scenes/TabletopPrototype.unity";

        [UnityTest]
        public IEnumerator TabletopPrototypeScene_LoadsAndInitializesLocalPrototypeComposition()
        {
            int buildIndex = SceneUtility.GetBuildIndexByScenePath(ScenePath);
            Assert.That(buildIndex, Is.GreaterThanOrEqualTo(0), "TabletopPrototype scene is not in build settings.");

            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Additive);
            while (!loadOperation.isDone)
            {
                yield return null;
            }

            yield return null;

            Scene scene = SceneManager.GetSceneByBuildIndex(buildIndex);
            Assert.That(scene.IsValid(), Is.True);
            TabletopPrototypeComposition composition =
                FindPath(scene, "Interaction/PrototypeComposition").GetComponent<TabletopPrototypeComposition>();
            TabletopInputFrameCoordinator frameCoordinator =
                FindPath(scene, "Interaction/TabletopInput").GetComponent<TabletopInputFrameCoordinator>();
            TabletopObjectInputAdapter objectInputAdapter =
                FindPath(scene, "Interaction/TabletopInput").GetComponent<TabletopObjectInputAdapter>();
            TabletopCameraInputAdapter cameraInputAdapter =
                FindPath(scene, "CameraRig").GetComponent<TabletopCameraInputAdapter>();
            GameObject cardObject = FindPath(scene, "TabletopObjects/PrototypeCard");
            GameObject pawnObject = FindPath(scene, "TabletopObjects/PrototypePawn");
            GameObject tokenObject = FindPath(scene, "TabletopObjects/PrototypeToken");

            Assert.That(composition.IsInitialized, Is.True);
            Assert.That(frameCoordinator.enabled, Is.True);
            Assert.That(cameraInputAdapter.IsExternallyDrivenBy(frameCoordinator), Is.True);
            Assert.That(objectInputAdapter.IsExternallyDrivenBy(frameCoordinator), Is.True);
            Assert.That(objectInputAdapter.IsInitialized, Is.True);
            Assert.That(cameraInputAdapter.HasScrollRoutingPolicy, Is.True);
            Assert.That(composition.SelectionPresenter, Is.Not.Null);
            Assert.That(frameCoordinator.SelectionPresenter, Is.SameAs(composition.SelectionPresenter));

            Assert.That(composition.ActiveSession.Selection.Kind, Is.EqualTo(TabletopSessionKind.EmptyCustom));
            Assert.That(composition.MatchState.GameTemplateId, Is.EqualTo(GameTemplateId.Empty));
            Assert.That(composition.MatchState.ObjectCount, Is.EqualTo(0));
            Assert.That(composition.MatchState.Containers.Count, Is.EqualTo(0));
            Assert.That(composition.MatchState.Seats.Count, Is.EqualTo(0));
            Assert.That(composition.MatchState.PlayAreas.Count, Is.EqualTo(0));
            Assert.That(cardObject.activeSelf, Is.False);
            Assert.That(pawnObject.activeSelf, Is.False);
            Assert.That(tokenObject.activeSelf, Is.False);
            Assert.That(composition.MatchState.Revision, Is.EqualTo(0));
            Assert.That(composition.LockService.Count, Is.EqualTo(0));
            Assert.That(composition.PreviewSession.IsActive, Is.False);

            composition.Shutdown();
            AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(scene);
            while (unloadOperation != null && !unloadOperation.isDone)
            {
                yield return null;
            }

            LogAssert.NoUnexpectedReceived();
        }

        private static GameObject FindPath(Scene scene, string path)
        {
            string[] parts = path.Split('/');
            GameObject current = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == parts[0])
                {
                    current = root;
                    break;
                }
            }

            Assert.That(current, Is.Not.Null, $"Missing scene root '{parts[0]}'.");

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

    }
}
