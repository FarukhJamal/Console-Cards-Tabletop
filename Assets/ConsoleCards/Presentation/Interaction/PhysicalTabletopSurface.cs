using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ConsoleCards.Presentation.Interaction
{
    /// <summary>Opt-in placement surface. Owns only collider registration, never Match state or model transforms.</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Console Cards/Physical Tabletop Surface")]
    public sealed class PhysicalTabletopSurface : MonoBehaviour
    {
        // Technical membership only: no services or authoritative object state are stored globally.
        // ExecuteAlways keeps registration valid with domain/scene reload disabled, and supports Inspector diagnostics.
        private static readonly HashSet<PhysicalTabletopSurface> registered = new HashSet<PhysicalTabletopSurface>();
        internal static IEnumerable<PhysicalTabletopSurface> Registered => registered;

        [SerializeField, Tooltip("The authored top collider on this same GameObject. Automatically resolved when there is exactly one collider. Other colliders are not placement surfaces.")]
        private Collider surfaceCollider;
        private string lastReportedIssue;

        private void Reset() => ResolveLocalCollider();

        private void OnEnable()
        {
            ResolveLocalCollider();
            // Scene.isLoaded can still be false during load-time OnEnable; query eligibility is checked later.
            if (gameObject.scene.IsValid())
                registered.Add(this);
            ReportConfigurationIssue();
        }

        private void OnDisable() => registered.Remove(this);
        private void OnDestroy() => registered.Remove(this);

        internal bool ParticipatesIn(PhysicsScene physicsScene) =>
            this != null && isActiveAndEnabled && gameObject.scene.IsValid() && gameObject.scene.isLoaded
            && (!UnityEngine.Application.isPlaying || UnityEngine.Application.IsPlaying(gameObject))
            && gameObject.scene.GetPhysicsScene() == physicsScene;

        /// <summary>Shared runtime/Inspector validation; only this GameObject's explicitly opted-in collider is eligible.</summary>
        public bool TryGetCollider(out Collider collider, out string issue)
        {
            ResolveLocalCollider();
            collider = surfaceCollider;
            if (collider == null)
                issue = "Add a Collider to this GameObject. If it has multiple colliders, assign the authored top collider explicitly.";
            else if (collider.gameObject != gameObject)
                issue = "The surface Collider must be on the same GameObject as PhysicalTabletopSurface.";
            else if (collider.isTrigger)
                issue = "Turn off Is Trigger on the surface Collider so it can catch physical pieces.";
            else if (collider.attachedRigidbody != null)
                issue = "Use a fixed surface Collider without an attached Rigidbody (including on a parent).";
            else if (collider is MeshCollider mesh && mesh.sharedMesh == null)
                issue = "The surface MeshCollider has no mesh. Assign an authored collision mesh or use a BoxCollider.";
            else if (!isActiveAndEnabled || !collider.enabled)
                issue = "The surface or its Collider is disabled; it is not available for placement.";
            else if (collider.bounds.size.sqrMagnitude <= Mathf.Epsilon)
                issue = "The surface Collider has no usable size. Check its dimensions and Transform scale.";
            else
            {
                issue = null;
                return true;
            }

            return false;
        }

        internal void ReportConfigurationIssue()
        {
            if (!UnityEngine.Application.IsPlaying(gameObject) || !isActiveAndEnabled) return;
            // Disabling a collider is a legitimate session/Board visibility operation.
            if (TryGetCollider(out Collider collider, out string issue) || (collider != null && !collider.enabled))
            {
                lastReportedIssue = null;
                return;
            }
            if (lastReportedIssue == issue) return;
            lastReportedIssue = issue;
            Debug.LogError($"PhysicalTabletopSurface: {issue}", this);
        }

        private void ResolveLocalCollider()
        {
            if (surfaceCollider != null) return;
            Collider[] colliders = GetComponents<Collider>();
            if (colliders.Length == 1) surfaceCollider = colliders[0];
        }
    }
}
