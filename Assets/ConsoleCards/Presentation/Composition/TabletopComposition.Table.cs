using System;
using System.IO;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.Saving;
using ConsoleCards.Presentation.TableSurface;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// The table's shape (doc 30, B2; T1). The Table menu's "Table: shape" row cycles Classic, Rectangle, Square,
    /// Round and Oval. Classic is the scene's own table; the others are built from a <see cref="TableSpec"/> where the
    /// classic playing area is, big enough to hold all of it, so every game layout still fits. The choice is saved
    /// in the profile (table.json) and comes back at the next start. Sizes, the shape cards and Undo come in T3.
    /// </summary>
    public sealed partial class TabletopComposition
    {
        private const string TableSpecFileName = "table.json";
        private const string TableArea = "Table";

        private bool classicTableLooked;
        private PhysicalTabletopSurface classicSurface;
        private Renderer classicBodyRenderer;
        private bool classicBodyWasEnabled;
        private TableFrame classicFrame;
        private Material classicPlayingArea;
        private Material classicBody;
        private GeneratedTable generatedTable;
        private TableSpec tableSpec = new TableSpec();
        private Func<string> cycleTableShapeAction;

        private string TableSpecPath => Path.Combine(EnsureProfile().ActiveFolder, TableSpecFileName);

        private string TableShapeText() => $"Table: {TableSpec.Label(tableSpec.shape)}";

        // At start, before the first table: puts back the shape saved in the profile. Never stops the game starting;
        // anything wrong keeps the Classic table with a warning.
        private void ApplySavedTable()
        {
            try
            {
                if (!FindClassicTable()) return;
                TableSpec saved = SaveFiles.ReadJson<TableSpec>(TableSpecPath, TableArea);
                if (saved == null || saved.shape == TableShape.Classic) return;
                if (saved.width <= 0f || saved.depth <= 0f)
                {
                    TableSpec sized = TableSpec.DefaultFor(saved.shape, classicFrame.ClassicSize.x, classicFrame.ClassicSize.y);
                    sized.edge = saved.edge;
                    saved = sized;
                }

                saved.Clamp();
                if (ShowTable(saved, out string error))
                {
                    tableSpec = saved;
                }
                else
                {
                    Debug.LogWarning($"[{TableArea}] The saved {TableSpec.Label(saved.shape)} table could not be built ({error}); using the Classic table.", this);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[{TableArea}] The saved table could not be used ({exception.Message}); using the Classic table.", this);
                ShowClassicTable();
            }
        }

        // The Table menu row, on every table. Hidden when the scene has no classic table to stand in for.
        private void RefreshTableShapeEntry()
        {
            if (runtimeUi == null) return;
            if (cycleTableShapeAction == null) cycleTableShapeAction = CycleTableShape;
            runtimeUi.ShowTableShapeEntry(TableShapeText(), FindClassicTable() ? cycleTableShapeAction : null);
        }

        // The next shape at its default size, keeping the edge. Returns the row's new text; the menu stays open.
        private string CycleTableShape()
        {
            if (!FindClassicTable()) return TableShapeText();
            TableShape next = TableSpec.Next(tableSpec.shape);
            TableSpec candidate = TableSpec.DefaultFor(next, classicFrame.ClassicSize.x, classicFrame.ClassicSize.y);
            candidate.edge = tableSpec.edge;
            if (!ShowTable(candidate, out string error))
            {
                ShowMessage($"The table was not changed: {error}.");
                return TableShapeText();
            }

            tableSpec = candidate;
            SaveFiles.WriteJson(TableSpecPath, tableSpec, TableArea);
            ShowMessage(next == TableShape.Classic
                ? "Classic table."
                : $"{TableSpec.Label(next)} table. Sizes come with the Table tools.");
            return TableShapeText();
        }

        // Shows the table for the spec. Only one table is ever the placement surface: the other is off first.
        private bool ShowTable(TableSpec spec, out string error)
        {
            error = null;
            if (spec.shape == TableShape.Classic)
            {
                ShowClassicTable();
                return true;
            }

            if (generatedTable == null)
            {
                generatedTable = GeneratedTable.Create(
                    null,
                    classicSurface.gameObject.layer,
                    classicFrame.Frame.gameObject.layer);
                SceneManager.MoveGameObjectToScene(generatedTable.gameObject, gameObject.scene);
            }

            SetClassicTableVisible(false);
            if (generatedTable.TryBuild(spec, classicFrame, classicPlayingArea, classicBody, out error))
            {
                Physics.SyncTransforms();
                return true;
            }

            // The last good table stays; with none built yet, that is the Classic table.
            if (!generatedTable.gameObject.activeSelf) SetClassicTableVisible(true);
            return false;
        }

        private void ShowClassicTable()
        {
            if (generatedTable != null) generatedTable.Hide();
            SetClassicTableVisible(true);
            Physics.SyncTransforms();
        }

        private void SetClassicTableVisible(bool visible)
        {
            if (classicSurface == null) return;
            if (classicSurface.gameObject.activeSelf != visible) classicSurface.gameObject.SetActive(visible);
            if (classicBodyRenderer != null) classicBodyRenderer.enabled = visible && classicBodyWasEnabled;
        }

        // Once per scene: the active surface marked as the Template Layout Origin is the classic table. Its frame,
        // the centre of its top, its size and its materials are what the generated tables use.
        private bool FindClassicTable()
        {
            if (classicTableLooked) return classicSurface != null;
            classicTableLooked = true;
            Physics.SyncTransforms();
            PhysicsScene physicsScene = targetCamera.gameObject.scene.GetPhysicsScene();
            Collider surfaceCollider = null;
            foreach (PhysicalTabletopSurface surface in PhysicalTabletopSurface.Registered)
            {
                if (surface != null && surface.ParticipatesIn(physicsScene) && surface.IsTemplateLayoutOrigin
                    && surface.TryGetCollider(out Collider found, out _))
                {
                    classicSurface = surface;
                    surfaceCollider = found;
                    break;
                }
            }

            if (classicSurface == null)
            {
                Debug.LogWarning($"[{TableArea}] No table surface is marked as the Template Layout Origin, so the table shape cannot change.", this);
                return false;
            }

            Transform frame = classicSurface.TemplateLayoutFrame;
            Vector3 up = frame.up.normalized;
            Bounds bounds = surfaceCollider.bounds;
            float reach = bounds.extents.magnitude + 1f;
            if (!surfaceCollider.Raycast(new Ray(bounds.center + (up * reach), -up), out RaycastHit top, reach * 2f))
            {
                Debug.LogWarning($"[{TableArea}] The classic table's top could not be found from above, so the table shape cannot change.", this);
                classicSurface = null;
                return false;
            }

            Vector3 right = Vector3.ProjectOnPlane(frame.right, up).normalized;
            Vector3 forward = Vector3.ProjectOnPlane(frame.forward, up).normalized;
            float localUnits = classicSurface.LocalUnitsPerTableUnit;
            float worldPerUnit = frame.TransformVector(Vector3.right * localUnits).magnitude * worldUnitsPerTableUnit;
            if (worldPerUnit <= 0f || float.IsNaN(worldPerUnit) || forward.sqrMagnitude < 0.5f)
            {
                Debug.LogWarning($"[{TableArea}] The classic table's frame has no usable size, so the table shape cannot change.", this);
                classicSurface = null;
                return false;
            }

            Vector3 extents = bounds.extents;
            float width = 2f * Vector3.Dot(TableAbs(right), extents);
            float depth = 2f * Vector3.Dot(TableAbs(forward), extents);
            classicFrame = new TableFrame(
                top.point,
                Quaternion.LookRotation(forward, up),
                frame,
                localUnits,
                worldPerUnit,
                new Vector2(width / worldPerUnit, depth / worldPerUnit));

            Renderer playingAreaRenderer = classicSurface.GetComponent<Renderer>();
            classicBodyRenderer = frame.GetComponent<Renderer>();
            classicBodyWasEnabled = classicBodyRenderer != null && classicBodyRenderer.enabled;
            classicPlayingArea = playingAreaRenderer != null ? playingAreaRenderer.sharedMaterial : null;
            classicBody = classicBodyRenderer != null ? classicBodyRenderer.sharedMaterial : null;
            if (classicPlayingArea == null) classicPlayingArea = classicBody;
            if (classicPlayingArea == null)
            {
                Debug.LogWarning($"[{TableArea}] The classic table has no material to copy, so the table shape cannot change.", this);
                classicSurface = null;
                return false;
            }

            return true;
        }

        private static Vector3 TableAbs(Vector3 value) => new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }
}
