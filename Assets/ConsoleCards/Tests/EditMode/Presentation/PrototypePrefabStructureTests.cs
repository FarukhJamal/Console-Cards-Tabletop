using System;
using System.Collections.Generic;
using System.Linq;
using ConsoleCards.Presentation.Camera;
using ConsoleCards.Presentation.Input;
using ConsoleCards.Presentation.Prototype;
using ConsoleCards.Presentation.TableSurface;
using ConsoleCards.Presentation.Views;
using NUnit.Framework;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace ConsoleCards.Tests.EditMode.Presentation
{
    public sealed class PrototypePrefabStructureTests
    {
        private const string TabletopObjectLayerName = "TabletopObject";
        private const int MinimumUserLayerIndex = 8;
        private const int MaximumLayerIndex = 31;
        private const float Tolerance = 0.0001f;

        private const string CardFaceUpMaterialPath =
            "Assets/ConsoleCards/Content/Materials/Prototype/CardFaceUpPrototype.mat";
        private const string CardFaceDownMaterialPath =
            "Assets/ConsoleCards/Content/Materials/Prototype/CardFaceDownPrototype.mat";
        private const string PawnMaterialPath =
            "Assets/ConsoleCards/Content/Materials/Prototype/PawnPrototype.mat";
        private const string TokenMaterialPath =
            "Assets/ConsoleCards/Content/Materials/Prototype/TokenPrototype.mat";
        private const string SelectionHighlightMaterialPath =
            "Assets/ConsoleCards/Content/Materials/Prototype/SelectionHighlightPrototype.mat";
        private const string TableSurfaceMaterialPath =
            "Assets/ConsoleCards/Content/Materials/TableSurfacePrototype.mat";
        private const string TableSurfaceMaterialGuid = "1f2035c98af2ed24c9616f459aa814ec";

        private const string CardPrefabPath =
            "Assets/ConsoleCards/Content/Prefabs/Prototype/PrototypeCard.prefab";
        private const string PawnPrefabPath =
            "Assets/ConsoleCards/Content/Prefabs/Prototype/PrototypePawn.prefab";
        private const string TokenPrefabPath =
            "Assets/ConsoleCards/Content/Prefabs/Prototype/PrototypeToken.prefab";

        [Test]
        public void TabletopObjectLayer_ExistsOnceInUserLayerSlots()
        {
            int layerIndex = LayerMask.NameToLayer(TabletopObjectLayerName);

            Assert.That(layerIndex, Is.InRange(MinimumUserLayerIndex, MaximumLayerIndex));
            Assert.That(
                InternalEditorUtility.layers.Count(layer => layer == TabletopObjectLayerName),
                Is.EqualTo(1));
            Assert.That(LayerMask.NameToLayer("Default"), Is.EqualTo(0));
            Assert.That(LayerMask.NameToLayer("TransparentFX"), Is.EqualTo(1));
            Assert.That(LayerMask.NameToLayer("Ignore Raycast"), Is.EqualTo(2));
            Assert.That(LayerMask.NameToLayer("Water"), Is.EqualTo(4));
            Assert.That(LayerMask.NameToLayer("UI"), Is.EqualTo(5));
        }

        [Test]
        public void PrototypeMaterials_HaveApprovedStructure()
        {
            Material[] materials =
            {
                LoadMaterial(CardFaceUpMaterialPath),
                LoadMaterial(CardFaceDownMaterialPath),
                LoadMaterial(PawnMaterialPath),
                LoadMaterial(TokenMaterialPath),
                LoadMaterial(SelectionHighlightMaterialPath),
            };

            Assert.That(materials.Distinct().Count(), Is.EqualTo(materials.Length));
            Assert.That(
                materials.Select(material => material.GetColor("_BaseColor")).Distinct().Count(),
                Is.EqualTo(materials.Length));
            Assert.That(AssetDatabase.AssetPathToGUID(TableSurfaceMaterialPath), Is.EqualTo(TableSurfaceMaterialGuid));
            Assert.That(materials.Contains(LoadMaterial(TableSurfaceMaterialPath)), Is.False);

            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                Assert.That(material.shader, Is.Not.Null);
                Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
                Assert.That(material.HasProperty("_Metallic"), Is.True);
                Assert.That(material.GetFloat("_Metallic"), Is.EqualTo(0f).Within(Tolerance));
                Assert.That(material.HasProperty("_Smoothness"), Is.True);
                Assert.That(material.GetFloat("_Smoothness"), Is.InRange(0f, 0.25f));
                AssertNoTexturesAssigned(material);
            }
        }

        [Test]
        public void CardPrefab_HasApprovedPlaceholderStructure()
        {
            WithPrefabContents(CardPrefabPath, root =>
            {
                AssertRootBasics(root, "PrototypeCard");
                Assert.That(root.GetComponents<CardView>(), Has.Length.EqualTo(1));
                Assert.That(root.GetComponents<TabletopSelectionVisual>(), Has.Length.EqualTo(1));
                Assert.That(root.GetComponent<TabletopSelectionVisual>().IsConfigured, Is.False);
                Assert.That(root.GetComponent<Rigidbody>(), Is.Null);
                Assert.That(root.GetComponent<Renderer>(), Is.Null);
                Assert.That(root.GetComponent<MeshFilter>(), Is.Null);

                BoxCollider collider = AssertSingleRootPickingCollider(root);
                AssertVector3(collider.center, 0f, 0f, 0f);
                AssertVector3(collider.size, 1f, 0.02f, 1.4f);

                GameObject faceUp = FindDirectChild(root, "FaceUpVisualRoot");
                GameObject faceDown = FindDirectChild(root, "FaceDownVisualRoot");
                GameObject highlight = FindDirectChild(root, "SelectionHighlightRoot");
                Assert.That(faceUp.activeSelf, Is.True);
                Assert.That(faceDown.activeSelf, Is.False);
                Assert.That(highlight.activeSelf, Is.False);

                CardView cardView = root.GetComponent<CardView>();
                Assert.That(cardView.FaceUpVisualRoot, Is.SameAs(faceUp));
                Assert.That(cardView.FaceDownVisualRoot, Is.SameAs(faceDown));
                Assert.That(cardView.IsFacePresentationConfigured, Is.True);
                Assert.That(cardView.IsBound, Is.False);
                Assert.That(cardView.CardState, Is.Null);
                Assert.That(cardView.BoundState, Is.Null);

                AssertMaterialUsed(faceUp.transform, LoadMaterial(CardFaceUpMaterialPath));
                AssertMaterialUsed(faceDown.transform, LoadMaterial(CardFaceDownMaterialPath));
                AssertMaterialUsed(highlight.transform, LoadMaterial(SelectionHighlightMaterialPath));
                AssertNoChildCollidersOrNestedViews(root);
                AssertAllObjectsUseTabletopObjectLayer(root);
                AssertNoMissingScripts(root);
            });
        }

        [Test]
        public void PawnPrefab_HasApprovedPlaceholderStructure()
        {
            WithPrefabContents(PawnPrefabPath, root =>
            {
                AssertRootBasics(root, "PrototypePawn");
                Assert.That(root.GetComponents<PawnView>(), Has.Length.EqualTo(1));
                Assert.That(root.GetComponents<TabletopSelectionVisual>(), Has.Length.EqualTo(1));
                Assert.That(root.GetComponent<TabletopSelectionVisual>().IsConfigured, Is.False);
                Assert.That(root.GetComponent<Rigidbody>(), Is.Null);
                Assert.That(root.GetComponent<Renderer>(), Is.Null);
                Assert.That(root.GetComponent<MeshFilter>(), Is.Null);

                BoxCollider collider = AssertSingleRootPickingCollider(root);
                Assert.That(collider.size.y, Is.GreaterThan(0f));
                Assert.That(collider.size.y, Is.LessThanOrEqualTo(1f));
                Assert.That(collider.size.x, Is.GreaterThanOrEqualTo(0.5f));
                Assert.That(collider.size.z, Is.GreaterThanOrEqualTo(0.5f));

                GameObject visual = FindDirectChild(root, "VisualRoot");
                GameObject highlight = FindDirectChild(root, "SelectionHighlightRoot");
                Assert.That(highlight.activeSelf, Is.False);
                Assert.That(visual.GetComponentsInChildren<MeshRenderer>(true).Length, Is.GreaterThanOrEqualTo(2));
                Assert.That(root.GetComponent<PawnView>().IsBound, Is.False);
                Assert.That(root.GetComponent<PawnView>().PawnState, Is.Null);
                Assert.That(root.GetComponent<PawnView>().BoundState, Is.Null);

                AssertMaterialUsed(visual.transform, LoadMaterial(PawnMaterialPath));
                AssertMaterialUsed(highlight.transform, LoadMaterial(SelectionHighlightMaterialPath));
                AssertNoChildCollidersOrNestedViews(root);
                AssertAllObjectsUseTabletopObjectLayer(root);
                AssertNoMissingScripts(root);
            });
        }

        [Test]
        public void TokenPrefab_HasApprovedPlaceholderStructure()
        {
            WithPrefabContents(TokenPrefabPath, root =>
            {
                AssertRootBasics(root, "PrototypeToken");
                Assert.That(root.GetComponents<TokenView>(), Has.Length.EqualTo(1));
                Assert.That(root.GetComponents<TabletopSelectionVisual>(), Has.Length.EqualTo(1));
                Assert.That(root.GetComponent<TabletopSelectionVisual>().IsConfigured, Is.False);
                Assert.That(root.GetComponent<Rigidbody>(), Is.Null);
                Assert.That(root.GetComponent<Renderer>(), Is.Null);
                Assert.That(root.GetComponent<MeshFilter>(), Is.Null);

                BoxCollider collider = AssertSingleRootPickingCollider(root);
                Assert.That(collider.size.x, Is.GreaterThan(0f));
                Assert.That(collider.size.y, Is.GreaterThan(0f));
                Assert.That(collider.size.z, Is.GreaterThan(0f));
                Assert.That(collider.size.y, Is.LessThanOrEqualTo(0.15f));

                GameObject visual = FindDirectChild(root, "VisualRoot");
                GameObject highlight = FindDirectChild(root, "SelectionHighlightRoot");
                GameObject disc = FindDirectChild(visual, "TokenDisc");
                Assert.That(highlight.activeSelf, Is.False);
                Assert.That(disc.transform.localScale.x, Is.GreaterThan(0.5f));
                Assert.That(disc.transform.localScale.y, Is.InRange(0.01f, 0.1f));
                Assert.That(disc.transform.localScale.z, Is.GreaterThan(0.5f));
                Assert.That(root.GetComponent<TokenView>().IsBound, Is.False);
                Assert.That(root.GetComponent<TokenView>().TokenState, Is.Null);
                Assert.That(root.GetComponent<TokenView>().BoundState, Is.Null);

                AssertMaterialUsed(visual.transform, LoadMaterial(TokenMaterialPath));
                AssertMaterialUsed(highlight.transform, LoadMaterial(SelectionHighlightMaterialPath));
                AssertNoChildCollidersOrNestedViews(root);
                AssertAllObjectsUseTabletopObjectLayer(root);
                AssertNoMissingScripts(root);
            });
        }

        [Test]
        public void PrototypePrefabs_AreSeparateAssetsWithMatchingViewTypes()
        {
            string[] paths = { CardPrefabPath, PawnPrefabPath, TokenPrefabPath };

            Assert.That(paths.Select(AssetDatabase.AssetPathToGUID).Distinct().Count(), Is.EqualTo(paths.Length));

            WithPrefabContents(CardPrefabPath, root =>
            {
                Assert.That(root.GetComponent<CardView>(), Is.Not.Null);
                Assert.That(root.GetComponent<PawnView>(), Is.Null);
                Assert.That(root.GetComponent<TokenView>(), Is.Null);
            });
            WithPrefabContents(PawnPrefabPath, root =>
            {
                Assert.That(root.GetComponent<CardView>(), Is.Null);
                Assert.That(root.GetComponent<PawnView>(), Is.Not.Null);
                Assert.That(root.GetComponent<TokenView>(), Is.Null);
            });
            WithPrefabContents(TokenPrefabPath, root =>
            {
                Assert.That(root.GetComponent<CardView>(), Is.Null);
                Assert.That(root.GetComponent<PawnView>(), Is.Null);
                Assert.That(root.GetComponent<TokenView>(), Is.Not.Null);
            });
        }

        [Test]
        public void PrototypePrefabs_DoNotContainOutOfScopeComponentsOrRuntimeState()
        {
            Type[] prohibitedTypes =
            {
                typeof(TabletopCameraController),
                typeof(TabletopCameraInputAdapter),
                typeof(TabletopObjectInputAdapter),
                typeof(TabletopInputFrameCoordinator),
                typeof(TabletopPrototypeComposition),
                typeof(TabletopSurfaceProxy),
            };

            foreach (string path in new[] { CardPrefabPath, PawnPrefabPath, TokenPrefabPath })
            {
                WithPrefabContents(path, root =>
                {
                    foreach (Type prohibitedType in prohibitedTypes)
                    {
                        Assert.That(root.GetComponentInChildren(prohibitedType, true), Is.Null);
                    }

                    Assert.That(root.GetComponentsInChildren<TabletopObjectView>(true), Has.Length.EqualTo(1));
                    Assert.That(root.GetComponentsInChildren<Renderer>(true), Is.Not.Empty);
                    Assert.That(root.GetComponentsInChildren<Animator>(true), Is.Empty);
                    AssertNoMissingScripts(root);
                });
            }
        }

        [Test]
        public void PrototypePrefabs_HaveExactlyOnePickingColliderOnRoot()
        {
            foreach (string path in new[] { CardPrefabPath, PawnPrefabPath, TokenPrefabPath })
            {
                WithPrefabContents(path, root =>
                {
                    Collider[] colliders = root.GetComponentsInChildren<Collider>(true);

                    Assert.That(colliders, Has.Length.EqualTo(1));
                    Assert.That(colliders[0], Is.SameAs(root.GetComponent<BoxCollider>()));
                    Assert.That(root.GetComponent<Rigidbody>(), Is.Null);
                });
            }
        }

        private static Material LoadMaterial(string path)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Assert.That(material, Is.Not.Null, $"Missing material at {path}.");
            return material;
        }

        private static void WithPrefabContents(string path, Action<GameObject> assertion)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Assert.That(root, Is.Not.Null, $"Missing prefab at {path}.");
                assertion(root);
            }
            finally
            {
                if (root != null)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static void AssertRootBasics(GameObject root, string expectedName)
        {
            Assert.That(root.name, Is.EqualTo(expectedName));
            AssertVector3(root.transform.localPosition, 0f, 0f, 0f);
            Assert.That(Quaternion.Angle(root.transform.localRotation, Quaternion.identity), Is.EqualTo(0f).Within(Tolerance));
            AssertVector3(root.transform.localScale, 1f, 1f, 1f);
            Assert.That(root.layer, Is.EqualTo(LayerMask.NameToLayer(TabletopObjectLayerName)));
        }

        private static BoxCollider AssertSingleRootPickingCollider(GameObject root)
        {
            BoxCollider collider = root.GetComponent<BoxCollider>();

            Assert.That(collider, Is.Not.Null);
            Assert.That(root.GetComponents<BoxCollider>(), Has.Length.EqualTo(1));
            Assert.That(collider.isTrigger, Is.False);
            return collider;
        }

        private static GameObject FindDirectChild(GameObject root, string name)
        {
            for (int i = 0; i < root.transform.childCount; i++)
            {
                Transform child = root.transform.GetChild(i);
                if (child.name == name)
                {
                    return child.gameObject;
                }
            }

            Assert.Fail($"Missing direct child '{name}' under '{root.name}'.");
            return null;
        }

        private static void AssertMaterialUsed(Transform root, Material expectedMaterial)
        {
            bool used = root
                .GetComponentsInChildren<MeshRenderer>(true)
                .SelectMany(renderer => renderer.sharedMaterials)
                .Where(material => material != null)
                .Any(material => AssetDatabase.GetAssetPath(material) == AssetDatabase.GetAssetPath(expectedMaterial));

            Assert.That(used, Is.True, $"{root.name} does not use {expectedMaterial.name}.");
        }

        private static void AssertNoChildCollidersOrNestedViews(GameObject root)
        {
            Collider rootCollider = root.GetComponent<Collider>();
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
            {
                Assert.That(collider, Is.SameAs(rootCollider));
            }

            foreach (TabletopObjectView view in root.GetComponentsInChildren<TabletopObjectView>(true))
            {
                Assert.That(view.gameObject, Is.SameAs(root));
            }
        }

        private static void AssertAllObjectsUseTabletopObjectLayer(GameObject root)
        {
            int layer = LayerMask.NameToLayer(TabletopObjectLayerName);
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                Assert.That(transform.gameObject.layer, Is.EqualTo(layer));
            }
        }

        private static void AssertNoMissingScripts(GameObject root)
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                Assert.That(
                    GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject),
                    Is.EqualTo(0),
                    $"{transform.name} contains a missing script.");
            }
        }

        private static void AssertNoTexturesAssigned(Material material)
        {
            foreach (string propertyName in material.GetTexturePropertyNames())
            {
                Assert.That(material.GetTexture(propertyName), Is.Null);
            }
        }

        private static void AssertVector3(Vector3 actual, float expectedX, float expectedY, float expectedZ)
        {
            Assert.That(actual.x, Is.EqualTo(expectedX).Within(Tolerance));
            Assert.That(actual.y, Is.EqualTo(expectedY).Within(Tolerance));
            Assert.That(actual.z, Is.EqualTo(expectedZ).Within(Tolerance));
        }
    }
}
