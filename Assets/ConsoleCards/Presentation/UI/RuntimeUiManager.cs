using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ConsoleCards.Presentation.UI
{
    public interface IRuntimeUiService
    {
        T AcquireCached<T>(string id)
            where T : ReusableUiView;

        T AcquirePooled<T>(string id, Transform parent)
            where T : ReusableUiView;

        void Release(ReusableUiView view);
    }

    /// <summary>
    /// Generic prefab lifetime service. It knows layers and retention policy, not Game rules.
    /// </summary>
    public sealed class RuntimeUiManager : MonoBehaviour, IRuntimeUiService
    {
        [Header("Scene-authored UI shell")]
        [SerializeField] private PrototypeRuntimeUiRoot root;
        [SerializeField] private UiPrefabCatalog prefabCatalog;
        [SerializeField] private Transform screenLayerMount;
        [SerializeField] private Transform hudLayerMount;
        [SerializeField] private Transform popupLayerMount;
        [SerializeField] private Transform modalLayerMount;
        [SerializeField] private Transform notificationLayerMount;
        [SerializeField] private Transform poolStorageMount;

        private readonly Dictionary<string, ReusableUiView> cachedViews =
            new Dictionary<string, ReusableUiView>(StringComparer.Ordinal);
        private readonly Dictionary<string, Stack<ReusableUiView>> pools =
            new Dictionary<string, Stack<ReusableUiView>>(StringComparer.Ordinal);
        private readonly Dictionary<ReusableUiView, InstanceRegistration> registrations =
            new Dictionary<ReusableUiView, InstanceRegistration>(ReferenceIdentityComparer.Instance);

        public void ValidateReferences()
        {
            if (root == null
                || prefabCatalog == null
                || screenLayerMount == null
                || hudLayerMount == null
                || popupLayerMount == null
                || modalLayerMount == null
                || notificationLayerMount == null
                || poolStorageMount == null)
            {
                throw new InvalidOperationException(
                    "RuntimeUiManager requires its scene-authored root, catalog, layer mounts, and pool storage mount.");
            }

            prefabCatalog.ValidateEntries();
        }

        public T AcquireCached<T>(string id)
            where T : ReusableUiView
        {
            ValidateReferences();
            UiPrefabCatalogEntry entry = RequireEntry<T>(id, UiRetention.CachedSingleInstance);
            if (!cachedViews.TryGetValue(id, out ReusableUiView view) || view == null)
            {
                view = Create(entry, GetLayerMount(entry.Layer));
                cachedViews[id] = view;
            }

            if (!view.IsAcquired)
            {
                view.Acquire();
            }

            return (T)view;
        }

        public T AcquirePooled<T>(string id, Transform parent)
            where T : ReusableUiView
        {
            ValidateReferences();
            UiPrefabCatalogEntry entry = RequireEntry<T>(id, UiRetention.Pooled);
            if (!pools.TryGetValue(id, out Stack<ReusableUiView> pool))
            {
                pool = new Stack<ReusableUiView>();
                pools.Add(id, pool);
            }

            ReusableUiView view = null;
            while (pool.Count > 0 && view == null)
            {
                view = pool.Pop();
            }

            Transform targetParent = parent != null ? parent : GetLayerMount(entry.Layer);
            if (view == null)
            {
                view = Create(entry, targetParent);
            }
            else
            {
                view.transform.SetParent(targetParent, false);
            }

            view.Acquire();
            return (T)view;
        }

        public void Release(ReusableUiView view)
        {
            if (view == null)
            {
                return;
            }

            if (!registrations.TryGetValue(view, out InstanceRegistration registration))
            {
                throw new InvalidOperationException(
                    $"{view.GetType().Name} was not acquired by this UI manager.");
            }

            view.Release();
            if (registration.Retention != UiRetention.Pooled)
            {
                return;
            }

            view.transform.SetParent(poolStorageMount, false);
            pools[registration.Id].Push(view);
        }

        public void ReleaseAll()
        {
            foreach (ReusableUiView view in cachedViews.Values)
            {
                if (view != null)
                {
                    view.Release();
                }
            }

            foreach (Stack<ReusableUiView> pool in pools.Values)
            {
                foreach (ReusableUiView view in pool)
                {
                    if (view != null)
                    {
                        view.Release();
                    }
                }
            }

            cachedViews.Clear();
            pools.Clear();
            registrations.Clear();
        }

        private Transform GetLayerMount(UiLayer layer)
        {
            switch (layer)
            {
                case UiLayer.Screen:
                    return screenLayerMount;
                case UiLayer.Hud:
                    return hudLayerMount;
                case UiLayer.Popup:
                    return popupLayerMount;
                case UiLayer.Modal:
                    return modalLayerMount;
                case UiLayer.Notification:
                    return notificationLayerMount;
                default:
                    throw new ArgumentOutOfRangeException(nameof(layer), layer, null);
            }
        }

        private UiPrefabCatalogEntry RequireEntry<T>(string id, UiRetention expectedRetention)
            where T : ReusableUiView
        {
            UiPrefabCatalogEntry entry = prefabCatalog.GetRequired(id);
            if (entry.Retention != expectedRetention)
            {
                throw new InvalidOperationException(
                    $"UI prefab '{id}' is configured as {entry.Retention}, not {expectedRetention}.");
            }

            if (!(entry.Prefab is T))
            {
                throw new InvalidOperationException(
                    $"UI prefab '{id}' is {entry.Prefab.GetType().Name}, not {typeof(T).Name}.");
            }

            return entry;
        }

        private ReusableUiView Create(UiPrefabCatalogEntry entry, Transform parent)
        {
            ReusableUiView view = UnityEngine.Object.Instantiate(entry.Prefab, parent, false);
            view.name = entry.Prefab.name;
            registrations.Add(
                view,
                new InstanceRegistration(entry.Id, entry.Retention));
            return view;
        }

        private sealed class ReferenceIdentityComparer : IEqualityComparer<ReusableUiView>
        {
            public static readonly ReferenceIdentityComparer Instance = new ReferenceIdentityComparer();

            public bool Equals(ReusableUiView x, ReusableUiView y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(ReusableUiView view)
            {
                return RuntimeHelpers.GetHashCode(view);
            }
        }

        private readonly struct InstanceRegistration
        {
            public InstanceRegistration(string id, UiRetention retention)
            {
                Id = id;
                Retention = retention;
            }

            public string Id { get; }

            public UiRetention Retention { get; }
        }

        private void OnDestroy()
        {
            ReleaseAll();
        }
    }
}
