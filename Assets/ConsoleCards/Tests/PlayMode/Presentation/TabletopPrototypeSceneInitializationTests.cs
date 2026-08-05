using System.Collections;
using ConsoleCards.Core.Domain;
using ConsoleCards.Presentation.Input;
using ConsoleCards.Presentation.Prototype;
using ConsoleCards.Presentation.Views;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ConsoleCards.Tests.PlayMode.Presentation
{
    public sealed class TabletopPrototypeSceneInitializationTests
    {
        private const string ScenePath = "Assets/ConsoleCards/Presentation/Scenes/TabletopPrototype.unity";
        private const float PositionTolerance = 0.0001f;

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
            CardView cardView = FindPath(scene, "TabletopObjects/PrototypeCard").GetComponent<CardView>();
            PawnView pawnView = FindPath(scene, "TabletopObjects/PrototypePawn").GetComponent<PawnView>();
            TokenView tokenView = FindPath(scene, "TabletopObjects/PrototypeToken").GetComponent<TokenView>();

            Assert.That(composition.IsInitialized, Is.True);
            Assert.That(frameCoordinator.enabled, Is.True);
            Assert.That(cameraInputAdapter.IsExternallyDrivenBy(frameCoordinator), Is.True);
            Assert.That(objectInputAdapter.IsExternallyDrivenBy(frameCoordinator), Is.True);
            Assert.That(objectInputAdapter.IsInitialized, Is.True);
            Assert.That(cameraInputAdapter.HasScrollRoutingPolicy, Is.True);
            Assert.That(composition.SelectionPresenter, Is.Not.Null);
            Assert.That(frameCoordinator.SelectionPresenter, Is.SameAs(composition.SelectionPresenter));

            Assert.That(cardView.IsBound, Is.True);
            Assert.That(pawnView.IsBound, Is.True);
            Assert.That(tokenView.IsBound, Is.True);
            Assert.That(cardView.CardState, Is.SameAs(composition.CardState));
            Assert.That(pawnView.PawnState, Is.SameAs(composition.PawnState));
            Assert.That(tokenView.TokenState, Is.SameAs(composition.TokenState));
            Assert.That(cardView.BoundState, Is.SameAs(composition.MatchState.GetObject(composition.CardState.BaseState.Id)));
            Assert.That(pawnView.BoundState, Is.SameAs(composition.MatchState.GetObject(composition.PawnState.BaseState.Id)));
            Assert.That(tokenView.BoundState, Is.SameAs(composition.MatchState.GetObject(composition.TokenState.BaseState.Id)));

            AssertPosition(cardView.transform.position, -2f, 0f, 0f);
            AssertPosition(pawnView.transform.position, -3.5f, 0f, -0.5f);
            AssertPosition(tokenView.transform.position, 3.5f, 0f, -0.5f);
            Assert.That(composition.CardState.Face, Is.EqualTo(CardFace.FaceUp));
            Assert.That(cardView.DisplayedFace, Is.EqualTo(CardFace.FaceUp));
            Assert.That(FindPath(scene, "TabletopObjects/PrototypeCard/SelectionHighlightRoot").activeSelf, Is.False);
            Assert.That(FindPath(scene, "TabletopObjects/PrototypePawn/SelectionHighlightRoot").activeSelf, Is.False);
            Assert.That(FindPath(scene, "TabletopObjects/PrototypeToken/SelectionHighlightRoot").activeSelf, Is.False);
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

        private static void AssertPosition(Vector3 actual, float expectedX, float expectedY, float expectedZ)
        {
            Assert.That(actual.x, Is.EqualTo(expectedX).Within(PositionTolerance));
            Assert.That(actual.y, Is.EqualTo(expectedY).Within(PositionTolerance));
            Assert.That(actual.z, Is.EqualTo(expectedZ).Within(PositionTolerance));
        }
    }
}
