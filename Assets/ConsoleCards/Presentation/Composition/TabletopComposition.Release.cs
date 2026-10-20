using System;
using System.Collections.Generic;
using System.Text;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Random;
using ConsoleCards.Application.Results;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Boards;
using ConsoleCards.Core.Domain.Cards;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Consoles;
using ConsoleCards.Core.Domain.Dice;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.PlayAreas;
using ConsoleCards.Core.Domain.PlayerLayouts;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Events;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Definitions;
using ConsoleCards.GameTemplates;
using ConsoleCards.GameTemplates.ControllerInputs;
using ConsoleCards.GameTemplates.Definitions;
using ConsoleCards.Presentation.Art;
using ConsoleCards.Presentation.Camera;
using ConsoleCards.Presentation.Content;
using ConsoleCards.Presentation.Catalog;
using ConsoleCards.Presentation.Coordinates;
using ConsoleCards.Presentation.Feedback;
using ConsoleCards.Presentation.Hints;
using ConsoleCards.Presentation.Input;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.Saving;
using ConsoleCards.Presentation.Settings;
using ConsoleCards.Presentation.UI;
using ConsoleCards.Presentation.UI.Toolbox;
using ConsoleCards.Presentation.Views;
using ConsoleCards.Presentation.Views.Containers;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityCamera = UnityEngine.Camera;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>Releasing views and runtime instances, and small validation helpers. (R1a: moved here unchanged from TabletopComposition.cs.)</summary>
    public sealed partial class TabletopComposition
    {
        internal void EnsureInitialized()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("TabletopComposition is not initialized.");
            }
        }

        private void ReleaseAllStackViews()
        {
            List<KeyValuePair<ContainerId, StackRuntimeView>> activeStackViews =
                new List<KeyValuePair<ContainerId, StackRuntimeView>>(Registry.stackViewsByContainerId);
            for (int i = activeStackViews.Count - 1; i >= 0; i--)
            {
                ReleaseStackView(activeStackViews[i].Key, activeStackViews[i].Value);
            }

            while (Registry.runtimeOwnedStackRoots.Count > 0)
            {
                int lastIndex = Registry.runtimeOwnedStackRoots.Count - 1;
                GameObject root = Registry.runtimeOwnedStackRoots[lastIndex];
                Registry.runtimeOwnedStackRoots.RemoveAt(lastIndex);
                DisableRuntimeInteraction(root);
                DestroyRuntimeOwnedGameObject(root);
            }

            Registry.stackViewsByContainerId.Clear();
        }

        private void ReleaseStackView(ContainerId containerId, StackRuntimeView stackRuntimeView)
        {
            if (stackRuntimeView == null)
            {
                return;
            }

            StackViewOwnership ownership = stackRuntimeView.Ownership;
            GameObject root = stackRuntimeView.Root;
            FixedContainerVisual visual = stackRuntimeView.Visual;
            StackView view = stackRuntimeView.View;
            TabletopContainerDropTarget dropTarget = stackRuntimeView.DropTarget;

            DisableRuntimeInteraction(root);
            if (dropTarget != null)
            {
                dropTarget.ClearConfiguration();
                dropTarget.enabled = false;
            }

            visual?.ClearFeedback();
            if (view != null && view.IsBound)
            {
                view.Unbind();
            }

            feedbackTargetsByContainerId.Remove(containerId);
            Registry.stackViewsByContainerId.Remove(containerId);
            if (view != null)
            {
                Registry.layoutViews.Remove(view);
            }

            Registry.runtimeOwnedStackRoots.Remove(root);
            stackRuntimeView.ClearReferences();

            if (root == null)
            {
                return;
            }

            presentationTransitions?.Forget(root.transform);
            root.SetActive(false);
            if (ownership == StackViewOwnership.RuntimeOwned)
            {
                DestroyRuntimeOwnedGameObject(root);
            }
        }

        internal void ConfigureFixedContainer(
            FixedContainerVisual visual,
            IContainerView view)
        {
            visual.TargetCollider.enabled = true;
            visual.DropTarget.enabled = true;
            visual.DropTarget.Configure(view, visual.TargetCollider);
            visual.ClearFeedback();
            feedbackTargetsByContainerId[view.ContainerId] = new ContainerFeedbackTarget(visual);
        }

        private static void ValidateStackLayoutAnchor(FixedContainerVisual visual)
        {
            visual.GetView<StackView>();
            if (ReferenceEquals(visual.LayoutAnchor, visual.transform))
            {
                throw new InvalidOperationException(
                    $"Stack {visual.name} requires a distinct authored Card layout anchor.");
            }
        }

        private void ReleaseSceneOwnedFixedContainerViews()
        {
            ReleaseSceneOwnedFixedContainer(localHandVisual, handView, handContainerId);
            if (localHandVisual != null)
            {
                DestroyRuntimeOwnedGameObject(localHandVisual.gameObject);
                localHandVisual = null;
            }

            if (handTrayRig != null)
            {
                handTrayRig.Deactivate();
            }

            ReleaseHiddenHandViews();

            handView = null;
        }

        private void ReleaseSceneOwnedFixedContainer(
            FixedContainerVisual visual,
            IContainerLayoutView view,
            ContainerId containerId)
        {
            if (visual == null)
            {
                return;
            }

            visual.DropTarget?.ClearConfiguration();
            if (visual.DropTarget != null)
            {
                visual.DropTarget.enabled = false;
            }

            if (visual.TargetCollider != null)
            {
                visual.TargetCollider.enabled = false;
            }

            visual.ClearFeedback();
            if (view != null && view.IsBound)
            {
                UnbindFixedContainerView(view);
            }

            feedbackTargetsByContainerId.Remove(containerId);
            if (view != null)
            {
                Registry.layoutViews.Remove(view);
            }

            visual.gameObject.SetActive(false);
        }

        private static void UnbindFixedContainerView(IContainerLayoutView view)
        {
            if (view is DeckView deck)
            {
                deck.Unbind();
            }
            else if (view is HandView hand)
            {
                hand.Unbind();
            }
            else if (view is DiscardPileView discard)
            {
                discard.Unbind();
            }
            else
            {
                throw new InvalidOperationException(
                    $"Unsupported fixed container View type {view.GetType().Name}.");
            }
        }

        private void ReleaseRuntimeDeckInstances()
        {
            while (Registry.runtimeDeckInstances.Count > 0)
            {
                int lastIndex = Registry.runtimeDeckInstances.Count - 1;
                RuntimeDeckInstance instance = Registry.runtimeDeckInstances[lastIndex];
                GameObject root = instance.Root;
                FixedContainerVisual visual = instance.Visual;
                DeckView view = instance.View;
                DisableRuntimeInteraction(root);
                if (visual != null)
                {
                    visual.DropTarget.ClearConfiguration();
                    visual.DropTarget.enabled = false;
                    visual.TargetCollider.enabled = false;
                    visual.ClearFeedback();
                }

                if (view != null && view.IsBound)
                {
                    view.Unbind();
                }

                Registry.controllerDeckViews.Remove(view);
                Registry.layoutViews.Remove(view);
                feedbackTargetsByContainerId.Remove(instance.ContainerId);
                Registry.runtimeDeckInstances.RemoveAt(lastIndex);
                instance.ClearReferences();
                DestroyRuntimeOwnedGameObject(root);
            }
        }

        private void ReleaseRuntimeDeckInstance(ContainerId containerId)
        {
            for (int i = Registry.runtimeDeckInstances.Count - 1; i >= 0; i--)
            {
                RuntimeDeckInstance instance = Registry.runtimeDeckInstances[i];
                if (instance.ContainerId != containerId)
                {
                    continue;
                }

                GameObject root = instance.Root;
                FixedContainerVisual visual = instance.Visual;
                DeckView view = instance.View;
                DisableRuntimeInteraction(root);
                if (visual != null)
                {
                    visual.DropTarget.ClearConfiguration();
                    visual.DropTarget.enabled = false;
                    visual.TargetCollider.enabled = false;
                    visual.ClearFeedback();
                }

                if (view != null && view.IsBound)
                {
                    view.Unbind();
                }

                Registry.controllerDeckViews.Remove(view);
                Registry.layoutViews.Remove(view);
                feedbackTargetsByContainerId.Remove(containerId);
                Registry.runtimeDeckInstances.RemoveAt(i);
                instance.ClearReferences();
                DestroyRuntimeOwnedGameObject(root);
                return;
            }

            throw new InvalidOperationException("Deleted runtime Deck has no Presentation instance.");
        }

        private void ReleaseRuntimeConsoleInstances()
        {
            while (Registry.runtimeConsoleInstances.Count > 0)
            {
                int lastIndex = Registry.runtimeConsoleInstances.Count - 1;
                RuntimeConsoleInstance instance = Registry.runtimeConsoleInstances[lastIndex];
                ForgetConsoleSide(instance);
                GameObject root = instance.Root;
                DisableRuntimeInteraction(root);
                if (instance.View != null && instance.View.IsBound)
                {
                    instance.View.Unbind();
                }

                for (int i = 0; i < instance.SlotViews.Length; i++)
                {
                    ConsoleSlotView slotView = instance.SlotViews[i];
                    ConsoleSlotVisual slotVisual = instance.SlotVisuals[i];
                    if (slotVisual != null)
                    {
                        slotVisual.DropTarget.ClearConfiguration();
                        slotVisual.DropTarget.enabled = false;
                        slotVisual.TargetCollider.enabled = false;
                        slotVisual.ClearFeedback();
                    }

                    if (slotView != null && slotView.IsBound)
                    {
                        slotView.Unbind();
                    }

                    if (slotView != null)
                    {
                        Registry.consoleSlotViews.Remove(slotView);
                        Registry.layoutViews.Remove(slotView);
                    }
                }

                Registry.playerConsoleViews.Remove(instance.View);
                Registry.runtimeConsoleInstances.RemoveAt(lastIndex);
                instance.ClearReferences();
                DestroyRuntimeOwnedGameObject(root);
            }
        }

        private void ReleaseRuntimeConsoleInstance(ConsoleId consoleId)
        {
            for (int instanceIndex = Registry.runtimeConsoleInstances.Count - 1; instanceIndex >= 0; instanceIndex--)
            {
                RuntimeConsoleInstance instance = Registry.runtimeConsoleInstances[instanceIndex];
                if (instance.ConsoleId != consoleId)
                {
                    continue;
                }

                ForgetConsoleSide(instance);
                GameObject root = instance.Root;
                DisableRuntimeInteraction(root);
                if (instance.View != null && instance.View.IsBound)
                {
                    instance.View.Unbind();
                }

                for (int i = 0; i < instance.SlotViews.Length; i++)
                {
                    ConsoleSlotView slotView = instance.SlotViews[i];
                    ConsoleSlotVisual slotVisual = instance.SlotVisuals[i];
                    ContainerId slotContainerId = slotView != null
                        ? slotView.ContainerId
                        : ContainerId.Empty;
                    if (slotVisual != null)
                    {
                        slotVisual.DropTarget.ClearConfiguration();
                        slotVisual.DropTarget.enabled = false;
                        slotVisual.TargetCollider.enabled = false;
                        slotVisual.ClearFeedback();
                    }

                    if (slotView != null && slotView.IsBound)
                    {
                        slotView.Unbind();
                    }

                    if (slotView != null)
                    {
                        Registry.consoleSlotViews.Remove(slotView);
                        Registry.layoutViews.Remove(slotView);
                    }

                    if (!slotContainerId.IsEmpty)
                    {
                        Registry.consoleSlotVisualsByContainerId.Remove(slotContainerId);
                        feedbackTargetsByContainerId.Remove(slotContainerId);
                    }
                }

                Registry.playerConsoleViews.Remove(instance.View);
                Registry.runtimeConsoleInstances.RemoveAt(instanceIndex);
                instance.ClearReferences();
                DestroyRuntimeOwnedGameObject(root);
                return;
            }

            throw new InvalidOperationException("Deleted runtime Console has no Presentation instance.");
        }

        private void ReleaseRuntimeTokenContainerInstances()
        {
            while (Registry.runtimeTokenContainerInstances.Count > 0)
            {
                int lastIndex = Registry.runtimeTokenContainerInstances.Count - 1;
                RuntimeTokenContainerInstance instance = Registry.runtimeTokenContainerInstances[lastIndex];
                GameObject root = instance.Root;
                if (instance.DropTarget != null)
                {
                    instance.DropTarget.ClearConfiguration();
                    instance.DropTarget.enabled = false;
                }

                if (instance.TargetCollider != null)
                {
                    instance.TargetCollider.enabled = false;
                }

                if (instance.View != null && instance.View.IsBound)
                {
                    instance.View.Unbind();
                }

                Registry.tokenContainerViews.Remove(instance.View);
                Registry.runtimeTokenContainerInstances.RemoveAt(lastIndex);
                instance.ClearReferences();
                DisableRuntimeInteraction(root);
                DestroyRuntimeOwnedGameObject(root);
            }
        }

        private void ReleaseRuntimeObjectInstances<TView>(
            List<RuntimeObjectInstance> instances,
            List<TView> views,
            List<TabletopSelectionVisual> selectionVisuals)
            where TView : TabletopObjectView
        {
            while (instances.Count > 0)
            {
                int lastIndex = instances.Count - 1;
                RuntimeObjectInstance instance = instances[lastIndex];
                GameObject root = instance.Root;
                TabletopObjectView view = instance.View;
                TabletopSelectionVisual selectionVisual = instance.SelectionVisual;
                DisableRuntimeInteraction(root);
                selectionVisual?.SetSelected(false);
                if (view != null && view.IsBound)
                {
                    view.Unbind();
                }

                if (view is TView typedView)
                {
                    views.Remove(typedView);
                }

                selectionVisuals.Remove(selectionVisual);
                if (view != null)
                {
                    presentationTransitions?.Forget(view.transform);
                }

                instances.RemoveAt(lastIndex);
                instance.ClearReferences();
                DestroyRuntimeOwnedGameObject(root);
            }
        }

        private void ReleaseRuntimeObjectInstance<TView>(
            TabletopObjectId objectId,
            List<RuntimeObjectInstance> instances,
            List<TView> views,
            List<TabletopSelectionVisual> selectionVisuals)
            where TView : TabletopObjectView
        {
            for (int i = instances.Count - 1; i >= 0; i--)
            {
                RuntimeObjectInstance instance = instances[i];
                TabletopObjectView view = instance.View;
                if (view == null || view.ObjectId != objectId)
                {
                    continue;
                }

                GameObject root = instance.Root;
                TabletopSelectionVisual selectionVisual = instance.SelectionVisual;
                DisableRuntimeInteraction(root);
                selectionVisual?.SetSelected(false);
                if (view.IsBound)
                {
                    view.Unbind();
                }

                if (view is TView typedView)
                {
                    views.Remove(typedView);
                }

                selectionVisuals.Remove(selectionVisual);
                presentationTransitions?.Forget(view.transform);
                instances.RemoveAt(i);
                instance.ClearReferences();
                DestroyRuntimeOwnedGameObject(root);
                return;
            }

            throw new InvalidOperationException("Deleted runtime Object has no Presentation instance.");
        }

        private void ReleaseRuntimeCardInstances()
        {
            while (Registry.runtimeCardInstances.Count > 0)
            {
                int lastIndex = Registry.runtimeCardInstances.Count - 1;
                RuntimeCardInstance runtimeCardInstance = Registry.runtimeCardInstances[lastIndex];
                GameObject root = runtimeCardInstance.Root;
                CardView view = runtimeCardInstance.View;
                TabletopSelectionVisual selectionVisual = runtimeCardInstance.SelectionVisual;
                CardVisualReferences visualReferences = runtimeCardInstance.VisualReferences;

                DisableRuntimeInteraction(root);
                selectionVisual?.Clear();
                if (view != null && view.IsBound)
                {
                    view.Unbind();
                }

                if (view != null)
                {
                    Registry.cardViews.Remove(view);
                }

                if (selectionVisual != null)
                {
                    Registry.cardSelectionVisuals.Remove(selectionVisual);
                }

                if (visualReferences != null)
                {
                    Registry.cardVisualReferences.Remove(visualReferences);
                }

                if (view != null)
                {
                    presentationTransitions?.Forget(view.transform);
                }

                Registry.runtimeCardInstances.RemoveAt(lastIndex);
                runtimeCardInstance.ClearReferences();
                DestroyRuntimeOwnedGameObject(root);
            }
        }

        private static void DisableRuntimeInteraction(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            TabletopContainerDropTarget[] dropTargets =
                root.GetComponentsInChildren<TabletopContainerDropTarget>(true);
            for (int i = 0; i < dropTargets.Length; i++)
            {
                if (dropTargets[i] == null)
                {
                    continue;
                }

                dropTargets[i].ClearConfiguration();
                dropTargets[i].enabled = false;
            }

            TabletopTokenContainerDropTarget[] tokenDropTargets =
                root.GetComponentsInChildren<TabletopTokenContainerDropTarget>(true);
            for (int i = 0; i < tokenDropTargets.Length; i++)
            {
                if (tokenDropTargets[i] == null)
                {
                    continue;
                }

                tokenDropTargets[i].ClearConfiguration();
                tokenDropTargets[i].enabled = false;
            }

            Rigidbody[] rigidbodies = root.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rigidbodies.Length; i++)
            {
                if (rigidbodies[i] == null)
                {
                    continue;
                }

                if (!rigidbodies[i].isKinematic)
                {
                    rigidbodies[i].linearVelocity = Vector3.zero;
                    rigidbodies[i].angularVelocity = Vector3.zero;
                }

                rigidbodies[i].isKinematic = true;
                rigidbodies[i].useGravity = false;
                rigidbodies[i].detectCollisions = false;
            }

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = false;
                }
            }

            root.SetActive(false);
        }

        internal void DestroyRuntimeOwnedGameObject(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            root.SetActive(false);
            root.name = $"{root.name} (Pending Runtime Destruction)";
            Destroy(root);
        }

        internal static void RequireReference(UnityEngine.Object reference, string name)
        {
            if (reference == null)
            {
                throw new InvalidOperationException($"TabletopComposition requires {name}.");
            }
        }

        private static void ValidateFiniteGreaterThanZero(float value, string name)
        {
            ValidateFinite(value, name);
            if (value <= 0f)
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }

        private static void ValidateFiniteGreaterThanOrEqualToZero(float value, string name)
        {
            ValidateFinite(value, name);
            if (value < 0f)
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }

        private static void ValidateFinite(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }

        private void ReleaseRuntimeCardInstance(TabletopObjectId objectId)
        {
            for (int i = Registry.runtimeCardInstances.Count - 1; i >= 0; i--)
            {
                RuntimeCardInstance instance = Registry.runtimeCardInstances[i];
                CardView view = instance.View;
                if (view == null || view.ObjectId != objectId)
                {
                    continue;
                }

                GameObject root = instance.Root;
                TabletopSelectionVisual selectionVisual = instance.SelectionVisual;
                CardVisualReferences visualReferences = instance.VisualReferences;
                DisableRuntimeInteraction(root);
                selectionVisual?.Clear();
                if (view.IsBound)
                {
                    view.Unbind();
                }

                Registry.cardViews.Remove(view);
                Registry.cardSelectionVisuals.Remove(selectionVisual);
                Registry.cardVisualReferences.Remove(visualReferences);
                labelsByCardId.Remove(objectId);
                presentationTransitions?.Forget(view.transform);
                Registry.runtimeCardInstances.RemoveAt(i);
                instance.ClearReferences();
                DestroyRuntimeOwnedGameObject(root);
                return;
            }

            throw new InvalidOperationException("Deleted runtime Card has no Presentation instance.");
        }
    }
}
